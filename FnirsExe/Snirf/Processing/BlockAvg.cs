using System;
using System.Collections.Generic;
using FnirsExe.Snirf.Models;

namespace FnirsExe.Snirf.Processing
{
    /// <summary>
    /// 1:1 Homer3 hmrR_BlockAvg (for Concentration/Hb data):
    /// - per condition (snirf.Stim list)
    /// - windowing by sample index
    /// - exclude trials out of range
    /// - compute mean first (yavg_raw)
    /// - baseline = mean(yavg_raw[t<0])  (exclude t=0)
    /// - subtract same baseline from yavg and every trial
    /// - outputs: avg/std/sum2/nTrials/yTrials (per condition)
    /// </summary>
    public static class BlockAvgHomer3
    {
        public sealed class BlockAvgResult
        {
            public double[] TimeRel; // tHRF, same as Homer3: nPre*dt:dt:nPost*dt

            // [TimeRel, Pair, Cond]
            public double[,,] HbO_Avg;
            public double[,,] HbR_Avg;
            public double[,,] HbT_Avg;

            public double[,,] HbO_Std;
            public double[,,] HbR_Std;
            public double[,,] HbT_Std;

            public double[,,] HbO_Sum2;
            public double[,,] HbR_Sum2;
            public double[,,] HbT_Sum2;

            public int[] NTrials;                 // per condition
            public List<Od2Conc.PairInfo> Pairs;  // align with conc.Pairs

            // Optional: store trials (per condition, each trial is [W, Pair, 3(HbO/HbR/HbT)])
            public List<double[,,]>[] YTrials;    // can be null if not requested
            public string[] ConditionNames;       // from stim.Name
        }

        /// <summary>
        /// trange like Homer3: [-2, 20] seconds.
        /// </summary>
        public static BlockAvgResult Compute(
            Od2Conc.ConcResult conc,
            SnirfFile snirf,
            double tPreSec = -2.0,
            double tPostSec = 20.0,
            bool includeTrials = true)
        {
            if (conc == null) throw new ArgumentNullException(nameof(conc));
            if (snirf == null) throw new ArgumentNullException(nameof(snirf));
            if (conc.Time == null || conc.Time.Length < 2) return null;

            double[] t = conc.Time;
            int T = t.Length;

            int P = conc.HbO.GetLength(1);
            if (P <= 0) return null;

            double dt = t[1] - t[0];
            if (dt <= 0) return null;

            // Homer3: nPre = round(trange(1)/dt), trange(1) is negative => nPre negative
            int nPre = (int)Math.Round(tPreSec / dt);
            int nPost = (int)Math.Round(tPostSec / dt);

            if (nPost < nPre) return null;

            int W = nPost - nPre + 1;
            if (W <= 1) return null;

            // TimeRel like Homer3: nPre*dt : dt : nPost*dt
            var tHRF = new double[W];
            for (int w = 0; w < W; w++)
                tHRF[w] = (nPre + w) * dt;

            // Conditions = snirf.Stim groups (stim1, stim2, ...)
            int C = snirf.Stim?.Count ?? 0;
            if (C == 0) return null;

            var condNames = new string[C];
            var onsetIdxByCond = new List<int>[C];

            for (int c = 0; c < C; c++)
            {
                var stim = snirf.Stim[c];
                condNames[c] = stim?.Name ?? $"stim{c + 1}";
                onsetIdxByCond[c] = ExtractOnsetSampleIndices(stim, t, dt);
            }

            // Allocate outputs [W, P, C]
            var HbO_Avg = new double[W, P, C];
            var HbR_Avg = new double[W, P, C];
            var HbT_Avg = new double[W, P, C];

            var HbO_Std = new double[W, P, C];
            var HbR_Std = new double[W, P, C];
            var HbT_Std = new double[W, P, C];

            var HbO_Sum2 = new double[W, P, C];
            var HbR_Sum2 = new double[W, P, C];
            var HbT_Sum2 = new double[W, P, C];

            var nTrials = new int[C];

            // Optional trials
            List<double[,,]>[] yTrials = null;
            if (includeTrials)
            {
                yTrials = new List<double[,,]>[C];
                for (int c = 0; c < C; c++) yTrials[c] = new List<double[,,]>();
            }

            // -------- Pass 1: accumulate sums to get yavg_raw (no baseline yet) --------
            for (int c = 0; c < C; c++)
            {
                var lstS = onsetIdxByCond[c];
                if (lstS == null || lstS.Count == 0) continue;

                int nBlk = 0;

                foreach (int idx0 in lstS)
                {
                    int a = idx0 + nPre;
                    int b = idx0 + nPost;
                    if (a < 0 || b >= T) continue; // exclude out-of-range (Homer3 warning)

                    for (int w = 0; w < W; w++)
                    {
                        int ti = a + w;
                        for (int p = 0; p < P; p++)
                        {
                            double o = conc.HbO[ti, p];
                            double r = conc.HbR[ti, p];
                            HbO_Avg[w, p, c] += o;
                            HbR_Avg[w, p, c] += r;
                            HbT_Avg[w, p, c] += (o + r);
                        }
                    }

                    nBlk++;
                }

                nTrials[c] = nBlk;

                if (nBlk > 0)
                {
                    double inv = 1.0 / nBlk;
                    for (int w = 0; w < W; w++)
                        for (int p = 0; p < P; p++)
                        {
                            HbO_Avg[w, p, c] *= inv; // now yavg_raw
                            HbR_Avg[w, p, c] *= inv;
                            HbT_Avg[w, p, c] *= inv;
                        }
                }
            }

            // Baseline window in Homer3:
            // mean(yavg(1:-nPre,...)) => first (-nPre) samples of window => t < 0, excludes t=0
            int baseLen = -nPre;
            if (baseLen < 1) baseLen = 0;
            if (baseLen > W) baseLen = W; // safety

            // -------- Baseline (from yavg_raw), and subtract from yavg + trials --------
            // baseline arrays per cond+pair for HbO/HbR/HbT
            var baseO = new double[P, C];
            var baseR = new double[P, C];
            var baseT = new double[P, C];

            if (baseLen > 0)
            {
                for (int c = 0; c < C; c++)
                {
                    if (nTrials[c] == 0) continue;

                    for (int p = 0; p < P; p++)
                    {
                        double sumO = 0, sumR = 0, sumT = 0;
                        for (int w = 0; w < baseLen; w++)
                        {
                            sumO += HbO_Avg[w, p, c];
                            sumR += HbR_Avg[w, p, c];
                            sumT += HbT_Avg[w, p, c];
                        }
                        baseO[p, c] = sumO / baseLen;
                        baseR[p, c] = sumR / baseLen;
                        baseT[p, c] = sumT / baseLen;
                    }

                    // subtract baseline from yavg (same as Homer3)
                    for (int w = 0; w < W; w++)
                        for (int p = 0; p < P; p++)
                        {
                            HbO_Avg[w, p, c] -= baseO[p, c];
                            HbR_Avg[w, p, c] -= baseR[p, c];
                            HbT_Avg[w, p, c] -= baseT[p, c];
                        }
                }
            }

            // -------- Pass 2: build trials (optional), and compute sum2 + std on baseline-corrected trials --------
            for (int c = 0; c < C; c++)
            {
                int nBlk = nTrials[c];
                if (nBlk == 0) continue;

                var lstS = onsetIdxByCond[c];
                int used = 0;

                foreach (int idx0 in lstS)
                {
                    int a = idx0 + nPre;
                    int b = idx0 + nPost;
                    if (a < 0 || b >= T) continue;

                    // optional store one trial: [W, P, 3]
                    double[,,] trial = null;
                    if (includeTrials) trial = new double[W, P, 3];

                    for (int w = 0; w < W; w++)
                    {
                        int ti = a + w;
                        for (int p = 0; p < P; p++)
                        {
                            double o = conc.HbO[ti, p] - baseO[p, c];
                            double r = conc.HbR[ti, p] - baseR[p, c];
                            double tt = (o + r); // HbT forced = HbO + HbR

                            if (includeTrials)
                            {
                                trial[w, p, 0] = o;
                                trial[w, p, 1] = r;
                                trial[w, p, 2] = tt;
                            }

                            HbO_Sum2[w, p, c] += o * o;
                            HbR_Sum2[w, p, c] += r * r;
                            HbT_Sum2[w, p, c] += tt * tt;
                        }
                    }

                    if (includeTrials) yTrials[c].Add(trial);
                    used++;
                    if (used >= nBlk) break;
                }

                // std across trials (MATLAB std default uses N-1)
                if (nBlk > 1)
                {
                    for (int w = 0; w < W; w++)
                        for (int p = 0; p < P; p++)
                        {
                            // var = (sum(x^2) - n*mean^2)/(n-1)
                            double meanO = HbO_Avg[w, p, c];
                            double meanR = HbR_Avg[w, p, c];
                            double meanT = HbT_Avg[w, p, c];

                            double varO = (HbO_Sum2[w, p, c] - nBlk * meanO * meanO) / (nBlk - 1);
                            double varR = (HbR_Sum2[w, p, c] - nBlk * meanR * meanR) / (nBlk - 1);
                            double varT = (HbT_Sum2[w, p, c] - nBlk * meanT * meanT) / (nBlk - 1);

                            HbO_Std[w, p, c] = Math.Sqrt(Math.Max(0, varO));
                            HbR_Std[w, p, c] = Math.Sqrt(Math.Max(0, varR));
                            HbT_Std[w, p, c] = Math.Sqrt(Math.Max(0, varT));
                        }
                }
                else
                {
                    // nBlk == 1 => std = 0
                }
            }

            // If all conditions had 0 trials, return null (like "nothing to average")
            bool any = false;
            for (int c = 0; c < C; c++) if (nTrials[c] > 0) { any = true; break; }
            if (!any) return null;

            return new BlockAvgResult
            {
                TimeRel = tHRF,
                HbO_Avg = HbO_Avg,
                HbR_Avg = HbR_Avg,
                HbT_Avg = HbT_Avg,
                HbO_Std = HbO_Std,
                HbR_Std = HbR_Std,
                HbT_Std = HbT_Std,
                HbO_Sum2 = HbO_Sum2,
                HbR_Sum2 = HbR_Sum2,
                HbT_Sum2 = HbT_Sum2,
                NTrials = nTrials,
                Pairs = conc.Pairs,
                YTrials = yTrials,
                ConditionNames = condNames
            };
        }

        // ----------------- helpers -----------------

        /// <summary>
        /// Extract onset sample indices for one condition from stim.Data.
        /// stim.Data is usually Nx3: [onset,duration,amplitude].
        /// Also tolerates 3xN or 3x1 (caused by 1D->2D wrapping).
        /// </summary>
        private static List<int> ExtractOnsetSampleIndices(Stim stim, double[] time, double dt)
        {
            var res = new List<int>();
            if (stim == null || stim.Data == null) return res;

            double[,] d = stim.Data;
            int r = d.GetLength(0);
            int c = d.GetLength(1);

            // Normalize to Nx3
            // cases:
            // - Nx3 => ok (onset in col0)
            // - 3xN => transpose
            // - 3x1 (from wrapping a 1D [onset,dur,amp]) => treat as 1x3 after transpose
            if (c != 3 && r == 3) d = Transpose(d);

            r = d.GetLength(0);
            c = d.GetLength(1);
            if (c < 1) return res;

            double t0 = time[0];
            int T = time.Length;

            for (int i = 0; i < r; i++)
            {
                double onset = d[i, 0];

                // Homer3 uses sample index derived from stim vector aligned to time grid.
                // Closest 1:1 here is round((onset - t0)/dt).
                int idx = (int)Math.Round((onset - t0) / dt);

                if (idx < 0 || idx >= T) continue;
                res.Add(idx);
            }

            return res;
        }

        private static double[,] Transpose(double[,] a)
        {
            int r = a.GetLength(0);
            int c = a.GetLength(1);
            var b = new double[c, r];
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                    b[j, i] = a[i, j];
            return b;
        }
    }
}
