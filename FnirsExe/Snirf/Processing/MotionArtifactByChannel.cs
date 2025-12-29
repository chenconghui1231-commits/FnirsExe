using System;
using System.Collections.Generic;
using FnirsExe.Snirf.Models;

namespace FnirsExe.Snirf.Processing
{
    /// <summary>
    /// 1:1 port of Homer3:
    /// [tInc,tIncCh] = hmrR_MotionArtifactByChannel(data, probe, mlActMan, mlActAuto, tIncMan, tMotion, tMask, std_thresh, amp_thresh)
    ///
    /// Conventions:
    /// - tInc / tIncCh are "include masks": true = include (1), false = exclude (0).
    /// - bad_inds are computed on diff domain (length T-1) and applied with +1 offset like Homer3.
    /// </summary>
    public static class MotionArtifactByChannel_Homer3Strict
    {
        public sealed class Result
        {
            /// <summary>Global include mask: [T], true=include, false=artifact</summary>
            public bool[] TInc;

            /// <summary>Channel-wise include mask: [T, M], true=include, false=artifact</summary>
            public bool[,] TIncCh;

            /// <summary>Sampling rate used (Hz)</summary>
            public double Fs;
        }

        /// <summary>
        /// Compute motion artifacts for one data block (OD/dOD data).
        /// </summary>
        /// <param name="snirf">SnirfFile (for probe if needed)</param>
        /// <param name="blockIndex">data block index</param>
        /// <param name="mlActManActive">manual active channels mask [M] (true=active). null => all true</param>
        /// <param name="mlActAutoActive">auto active channels mask [M] (true=active). null => all true</param>
        /// <param name="tIncMan">manual time include mask [T] (true=include). null => all true</param>
        public static Result Compute(
            SnirfFile snirf,
            int blockIndex,
            bool[] mlActManActive,
            bool[] mlActAutoActive,
            bool[] tIncMan,
            double tMotion = 0.5,
            double tMask = 1.0,
            double stdThresh = 50.0,
            double ampThresh = 5.0)
        {
            if (snirf == null) throw new ArgumentNullException(nameof(snirf));
            if (snirf.Data == null || blockIndex < 0 || blockIndex >= snirf.Data.Count)
                throw new ArgumentOutOfRangeException(nameof(blockIndex));

            var block = snirf.Data[blockIndex];
            return ComputeBlock(block, mlActManActive, mlActAutoActive, tIncMan, tMotion, tMask, stdThresh, ampThresh);
        }

        /// <summary>
        /// Compute motion artifacts for a block directly.
        /// </summary>
        public static Result ComputeBlock(
            NirsDataBlock block,
            bool[] mlActManActive,
            bool[] mlActAutoActive,
            bool[] tIncMan,
            double tMotion = 0.5,
            double tMask = 1.0,
            double stdThresh = 50.0,
            double ampThresh = 5.0)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));

            double[,] d = block.DataTimeSeries;
            if (d == null) throw new InvalidOperationException("DataTimeSeries is null.");

            int T = d.GetLength(0);
            int M = d.GetLength(1);
            if (T < 2) throw new InvalidOperationException("Need at least 2 time points for diff-based motion detection.");
            if (block.MeasurementList == null || block.MeasurementList.Count != M)
                throw new InvalidOperationException("MeasurementList must exist and match DataTimeSeries column count.");

            // fs = data(iBlk).GetTime(); if length(fs)~=1 fs = 1/(fs(2)-fs(1));
            double fs = ComputeFs(block.Time);
            if (fs <= 0 || double.IsNaN(fs) || double.IsInfinity(fs))
                throw new InvalidOperationException("Invalid sampling rate fs computed from Time.");

            // tIncMan default all ones
            if (tIncMan == null || tIncMan.Length == 0)
            {
                tIncMan = new bool[T];
                for (int i = 0; i < T; i++) tIncMan[i] = true;
            }
            if (tIncMan.Length != T)
                throw new ArgumentException("tIncMan length must equal number of time points.");

            // tInc init ones
            bool[] tInc = new bool[T];
            for (int i = 0; i < T; i++) tInc[i] = true;

            // tIncCh init ones
            bool[,] tIncCh = new bool[T, M];
            for (int t = 0; t < T; t++)
                for (int m = 0; m < M; m++)
                    tIncCh[t, m] = true;

            // mlActMan/mlActAuto default all ones
            if (mlActManActive == null || mlActManActive.Length == 0)
            {
                mlActManActive = new bool[M];
                for (int i = 0; i < M; i++) mlActManActive[i] = true;
            }
            if (mlActAutoActive == null || mlActAutoActive.Length == 0)
            {
                mlActAutoActive = new bool[M];
                for (int i = 0; i < M; i++) mlActAutoActive[i] = true;
            }
            if (mlActManActive.Length != M) throw new ArgumentException("mlActManActive length must equal number of channels.");
            if (mlActAutoActive.Length != M) throw new ArgumentException("mlActAutoActive length must equal number of channels.");

            // lstAct = mlAct_CombineIndexLists(mlActMan, mlActAuto, MeasList)
            // simplest strict equivalent: channels where both are active
            List<int> lstAct = new List<int>();
            for (int i = 0; i < M; i++)
            {
                if (mlActManActive[i] && mlActAutoActive[i])
                    lstAct.Add(i);
            }

            // art_buffer = round(tMask*fs)
            int artBuffer = (int)Math.Round(tMask * fs);

            // maxDelay = round(tMotion*fs)
            int maxDelay = (int)Math.Round(tMotion * fs);
            if (maxDelay < 1) maxDelay = 1;

            // LOOP OVER CHANNELS (strictly like Homer3)
            for (int idxAct = 0; idxAct < lstAct.Count; idxAct++)
            {
                int iCh = lstAct[idxAct];

                int src = block.MeasurementList[iCh].SourceIndex;
                int det = block.MeasurementList[iCh].DetectorIndex;

                // lstActTmp = find(MeasList(:,1)==src & MeasList(:,2)==det)
                List<int> lstActTmp = FindAllIndicesSameSd(block.MeasurementList, src, det);
                if (lstActTmp.Count == 0) continue;

                // std_diff = std(d(2:end,lstActTmp) - d(1:end-1,lstActTmp), 0, 1)
                double[] stdDiff = new double[lstActTmp.Count];
                for (int j = 0; j < lstActTmp.Count; j++)
                {
                    int col = lstActTmp[j];
                    stdDiff[j] = StdOfFirstDifference(d, col); // sample std over T-1
                }

                // max_diff = zeros(T-1, nCols)
                int diffLen = T - 1;
                double[,] maxDiff = new double[diffLen, lstActTmp.Count];

                // for ii = 1:round(tMotion*fs)
                for (int delay = 1; delay <= maxDelay; delay++)
                {
                    for (int j = 0; j < lstActTmp.Count; j++)
                    {
                        int col = lstActTmp[j];

                        // temp is like [abs(d((ii+1):end)-d(1:end-ii)); zeros(ii-1,1)]
                        // in 0-based:
                        // for t=0..T-1-delay-1 => abs(d[t+delay]-d[t]) goes to temp[t]
                        // for last delay-1 entries => 0
                        int valid = T - delay; // number of samples in d((ii+1):end)
                        int tempLen = valid - 1; // because max_diff length is T-1
                        // Actually abs(d((ii+1):end)-d(1:end-ii)) has length T-delay
                        // max_diff is length T-1; MATLAB appends zeros(ii-1) making length (T-delay)+(delay-1) = T-1
                        // So:
                        // t=0..(T-delay-1) => value
                        // t=(T-delay)..(T-2) => 0

                        int lastValueIndex = T - delay - 1; // inclusive
                        for (int t = 0; t <= lastValueIndex; t++)
                        {
                            double v = Math.Abs(d[t + delay, col] - d[t, col]);
                            if (v > maxDiff[t, j])
                                maxDiff[t, j] = v;
                        }
                        // trailing positions implicitly 0, and maxDiff already has previous max, so nothing to do
                    }
                }

                // bad_inds(:,ii) = max( [ max_diff(:,ii)>mc_thresh(ii), max_diff(:,ii)>amp_thresh ], [], 2);
                // bad_inds = find(max(bad_inds,[],2)==1);
                bool[] badRow = new bool[diffLen]; // on diff domain [0..T-2]
                for (int t = 0; t < diffLen; t++)
                {
                    bool anyBad = false;
                    for (int j = 0; j < lstActTmp.Count; j++)
                    {
                        double mcThresh = stdDiff[j] * stdThresh;
                        double md = maxDiff[t, j];

                        if (md > mcThresh || md > ampThresh)
                        {
                            anyBad = true;
                            break;
                        }
                    }
                    badRow[t] = anyBad;
                }

                // bad_inds = find(badRow==true)
                List<int> badInds = new List<int>();
                for (int t = 0; t < diffLen; t++)
                    if (badRow[t]) badInds.Add(t);

                // Eliminate time points before or after motion artifacts (expand +/- art_buffer)
                if (badInds.Count > 0)
                {
                    // bad_inds = repmat(bad_inds,1,2*art_buffer+1)+repmat(-art_buffer:art_buffer,...)
                    // then clip to (0, T-1] in MATLAB 1-based, i.e. [1..T-1]
                    // in 0-based diff domain: [0..T-2]
                    var expanded = new HashSet<int>();
                    for (int k = 0; k < badInds.Count; k++)
                    {
                        int center = badInds[k];
                        for (int off = -artBuffer; off <= artBuffer; off++)
                        {
                            int v = center + off;
                            if (v >= 0 && v <= diffLen - 1)
                                expanded.Add(v);
                        }
                    }

                    // exclude points that were manually excluded: bad_inds(find(tIncMan(bad_inds)==0))=[]
                    // Note Homer3 checks tIncMan at bad_inds (diff-domain), not at (1+bad_inds)
                    // We keep exactly that.
                    var finalBad = new List<int>();
                    foreach (int bi in expanded)
                    {
                        if (tIncMan[bi]) // true=include => keep as auto-detected artifact
                            finalBad.Add(bi);
                    }

                    // Set tInc(1+bad_inds)=0; because bad inds are on diff so add 1
                    // Set tIncCh(1+bad_inds, lstAct(iCh))=0;
                    for (int k = 0; k < finalBad.Count; k++)
                    {
                        int bi = finalBad[k];
                        int tIdx = bi + 1; // +1 offset
                        if (tIdx >= 0 && tIdx < T)
                        {
                            tInc[tIdx] = false;
                            tIncCh[tIdx, iCh] = false;
                        }
                    }
                }
            }

            return new Result { TInc = tInc, TIncCh = tIncCh, Fs = fs };
        }

        // ---------------- helpers ----------------

        private static double ComputeFs(double[] time)
        {
            if (time == null || time.Length == 0) return double.NaN;
            if (time.Length == 1) return time[0]; // Homer3: if length(fs)~=1 then fs=1/(t(2)-t(1))
            double dt = time[1] - time[0];
            if (dt == 0) return double.NaN;
            return 1.0 / dt;
        }

        private static List<int> FindAllIndicesSameSd(List<MeasList> ml, int src, int det)
        {
            var list = new List<int>();
            for (int i = 0; i < ml.Count; i++)
            {
                if (ml[i].SourceIndex == src && ml[i].DetectorIndex == det)
                    list.Add(i);
            }
            return list;
        }

        /// <summary>
        /// std(d(2:end)-d(1:end-1), 0, 1) for one column (sample std, N-1).
        /// NaN will propagate similarly to MATLAB default behavior (if present).
        /// </summary>
        private static double StdOfFirstDifference(double[,] d, int col)
        {
            int T = d.GetLength(0);
            int n = T - 1;
            if (n <= 1) return 0.0;

            // compute diff mean
            double sum = 0.0;
            for (int t = 1; t < T; t++)
            {
                double v = d[t, col] - d[t - 1, col];
                if (double.IsNaN(v)) return double.NaN; // MATLAB-like propagation
                sum += v;
            }
            double mean = sum / n;

            // sample variance
            double var = 0.0;
            for (int t = 1; t < T; t++)
            {
                double v = d[t, col] - d[t - 1, col];
                double dv = v - mean;
                var += dv * dv;
            }
            var /= (n - 1);
            return Math.Sqrt(var);
        }
    }
}
