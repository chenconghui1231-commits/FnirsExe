using System;
using System.Collections.Generic;
using FnirsExe.Snirf.Models;

namespace FnirsExe.Snirf.Processing
{
    public static class BlockAvgHomer3
    {
        public sealed class BlockAvgResult
        {
            public double[] TimeRel;
            public double[,,] HbO_Avg; // [W, P, C]
            public double[,,] HbR_Avg;
            public double[,,] HbT_Avg;
            public int[] NTrials;
        }

        public static BlockAvgResult Compute(Od2Conc.ConcResult conc, SnirfFile snirf, double tPreSec, double tPostSec)
        {
            if (conc == null || snirf == null) return null;
            double dt = conc.Time[1] - conc.Time[0];
            int nPre = (int)Math.Round(tPreSec / dt);
            int nPost = (int)Math.Round(tPostSec / dt);
            int W = nPost - nPre + 1;
            double[] tHRF = new double[W];
            for (int i = 0; i < W; i++) tHRF[i] = (nPre + i) * dt;

            List<Stim> stims = snirf.Data[0].Stim;
            int C = stims.Count;
            int P = conc.HbO.GetLength(1);
            int T = conc.HbO.GetLength(0);

            var res = new BlockAvgResult
            {
                TimeRel = tHRF,
                HbO_Avg = new double[W, P, C],
                HbR_Avg = new double[W, P, C],
                HbT_Avg = new double[W, P, C],
                NTrials = new int[C]
            };

            for (int c = 0; c < C; c++)
            {
                var s = stims[c];
                if (s.Data == null) continue;
                int nEvents = s.Data.GetLength(0);
                int validCnt = 0;

                for (int e = 0; e < nEvents; e++)
                {
                    double onset = s.Data[e, 0];
                    int idx0 = (int)Math.Round((onset - conc.Time[0]) / dt);
                    int start = idx0 + nPre;
                    int end = idx0 + nPost;

                    if (start < 0 || end >= T) continue;

                    // Baseline Correction (t < 0)
                    // In the window [0..W-1], index -nPre corresponds to t=0.
                    // Baseline is 0 to -nPre
                    int zeroIdx = -nPre;

                    for (int p = 0; p < P; p++)
                    {
                        double baseO = 0, baseR = 0;
                        for (int k = 0; k < zeroIdx; k++)
                        {
                            baseO += conc.HbO[start + k, p];
                            baseR += conc.HbR[start + k, p];
                        }
                        baseO /= zeroIdx; baseR /= zeroIdx;

                        for (int w = 0; w < W; w++)
                        {
                            res.HbO_Avg[w, p, c] += (conc.HbO[start + w, p] - baseO);
                            res.HbR_Avg[w, p, c] += (conc.HbR[start + w, p] - baseR);
                            res.HbT_Avg[w, p, c] += (conc.HbT[start + w, p] - (baseO + baseR));
                        }
                    }
                    validCnt++;
                }
                res.NTrials[c] = validCnt;
                if (validCnt > 0)
                {
                    for (int w = 0; w < W; w++) for (int p = 0; p < P; p++)
                        {
                            res.HbO_Avg[w, p, c] /= validCnt;
                            res.HbR_Avg[w, p, c] /= validCnt;
                            res.HbT_Avg[w, p, c] /= validCnt;
                        }
                }
            }
            return res;
        }
    }
}