using System;
using System.Collections.Generic;
using FnirsExe.Snirf.Models;

namespace FnirsExe.Snirf.Processing
{
    /// <summary>
    /// Strict Homer3 hmrR_PruneChannels with ORDER ASSUMPTION (half split):
    /// - MeasList is ordered as [all wavelength-1 channels; all wavelength-2 channels]
    /// - Pairing is by index across halves.
    ///
    /// Also auto-aligns wavelengthIndex base:
    /// - If your C# parser stores WavelengthIndex as 0/1 (0-based), we convert to 1/2 for Homer3 logic.
    /// - If it is already 1/2 (1-based), we keep it.
    ///
    /// Output:
    /// - Returns a bool[] channel activity mask (mlActAuto style). Does NOT delete columns.
    /// </summary>
    public static class PruneChannels_Homer3_StrictOrder
    {
        public sealed class Options
        {
            /// <summary>dRange: [min, max]</summary>
            public double[] DRange { get; set; } = new double[] { 1.0e4, 1.0e7 };

            /// <summary>SNRthresh: if mean/std <= SNRthresh -> exclude</summary>
            public double SnrThresh { get; set; } = 2.0;

            /// <summary>SDrange: [min, max] source-detector distance (units must match probe coords)</summary>
            public double[] SdRange { get; set; } = new double[] { 0.0, 45.0 };

            /// <summary>
            /// If true, validates the ordering assumption:
            /// first half must be wl==1 and second half wl==2 (after base alignment).
            /// If false, will NOT check ordering (closer to Homer3 in the sense it assumes it).
            /// </summary>
            public bool ThrowIfOrderMismatch { get; set; } = true;

            /// <summary>
            /// If true, requires probe positions (Src/Det) for SDrange gating.
            /// Homer3 expects probe; keep true for strict replication.
            /// </summary>
            public bool RequireProbePositions { get; set; } = true;
        }

        /// <summary>
        /// Compute mlActAuto for a single data block (Homer3 style).
        /// </summary>
        public static bool[] ComputeMlActAuto(
            SnirfFile snirf,
            int dataBlockIndex,
            bool[] mlActManActive,
            bool[] tIncMan,
            Options opt = null)
        {
            if (snirf == null) return null;
            if (snirf.Data == null || dataBlockIndex < 0 || dataBlockIndex >= snirf.Data.Count) return null;
            if (opt == null) opt = new Options();

            var block = snirf.Data[dataBlockIndex];
            var ml = block.MeasurementList;
            if (ml == null || ml.Count == 0) return null;

            var dAll = block.DataTimeSeries;
            if (dAll == null) return null;

            int nTpts = dAll.GetLength(0);
            int nCh = dAll.GetLength(1);

            if (nCh != ml.Count)
                throw new InvalidOperationException("DataTimeSeries column count != MeasurementList count (misalignment).");

            // Probe
            double[] lambda = (snirf.Probe != null) ? snirf.Probe.Wavelengths : null;
            double[,] srcPos = (snirf.Probe != null) ? snirf.Probe.SourcePos3D : null;
            double[,] detPos = (snirf.Probe != null) ? snirf.Probe.DetectorPos3D : null;

            int nLambda = (lambda != null) ? lambda.Length : 0;

            // Strict Homer3 PruneChannels assumes 2 wavelengths and half split pairing.
            if (nLambda != 2)
                throw new InvalidOperationException("Strict Homer3 PruneChannels requires exactly 2 wavelengths (nLambda==2).");
            if (nCh % 2 != 0)
                throw new InvalidOperationException("Strict Homer3 PruneChannels requires even number of channels for half-split pairing.");

            bool hasPos = (srcPos != null && detPos != null);
            if (opt.RequireProbePositions && !hasPos)
                throw new InvalidOperationException("Probe positions missing (SourcePos/DetectorPos). Required for strict SDrange gating.");

            // Default tInc: all true
            if (tIncMan == null || tIncMan.Length == 0)
            {
                tIncMan = new bool[nTpts];
                for (int i = 0; i < nTpts; i++) tIncMan[i] = true;
            }
            if (tIncMan.Length != nTpts)
                throw new ArgumentException("tIncMan length must equal number of time points.");

            // Default mlActMan: all true
            if (mlActManActive == null || mlActManActive.Length == 0)
            {
                mlActManActive = new bool[nCh];
                for (int i = 0; i < nCh; i++) mlActManActive[i] = true;
            }
            if (mlActManActive.Length != nCh)
                throw new ArgumentException("mlActManActive length must equal number of channels.");

            // ---- WavelengthIndex base alignment (0-based vs 1-based) ----
            // We auto-detect based on min/max of stored indices.
            // - If indices are {0,1} => treat as 0-based and convert to {1,2} internally.
            // - If indices are {1,2} => already Homer3 style.
            // Otherwise throw (unknown/unsupported).
            int wlOffset = DetectWavelengthIndexOffset(ml); // 0 means already 1-based; +1 means stored 0-based.

            // Optional: validate strict ordering assumption
            if (opt.ThrowIfOrderMismatch)
            {
                if (!LooksLikeHalfSplitOrder(ml, wlOffset))
                {
                    throw new InvalidOperationException(
                        "MeasurementList does NOT match Homer3 half-split ordering assumption: " +
                        "first half wavelengthIndex==1, second half wavelengthIndex==2 (after base alignment).");
                }
            }

            // Compute dmean/dstd on included points only (Homer3 style)
            double[] dmean = new double[nCh];
            double[] dstd = new double[nCh];
            ComputeMeanStdOverIncluded_MatlabLike(dAll, tIncMan, dmean, dstd);

            // Start by including all channels
            bool[] chanList = new bool[nCh];
            for (int i = 0; i < nCh; i++) chanList[i] = true;

            // lst1 = find(MeasList(:,4)==1)
            // In C# terms: indices where (WavelengthIndex + offset) == 1
            List<int> lst1 = new List<int>();
            for (int k = 0; k < nCh; k++)
            {
                if (GetWavelengthIndexHomer3(ml[k], wlOffset) == 1)
                    lst1.Add(k);
            }

            // For ii=1..2
            for (int ii = 1; ii <= 2; ii++)
            {
                int pairCount = lst1.Count;
                int[] lst = new int[pairCount];
                double[] rhoSD = new double[pairCount];

                for (int jj = 0; jj < pairCount; jj++)
                {
                    int baseIdx = lst1[jj]; // a wl==1 channel
                    int s = GetSourceIndex(ml[baseIdx]);   // 1-based
                    int d = GetDetectorIndex(ml[baseIdx]); // 1-based

                    int idx = FindChannelIndex(ml, s, d, ii, wlOffset);
                    lst[jj] = idx;

                    if (hasPos)
                        rhoSD[jj] = NormSrcDet(srcPos, detPos, s, d);
                    else
                        rhoSD[jj] = double.NaN;
                }

                // idxsExcl are indices into the PAIR LIST (jj indices), same as Homer3
                var idxsExcl = new HashSet<int>();

                // dRange: mean <= min OR >= max
                for (int jj = 0; jj < pairCount; jj++)
                {
                    int chIdx = lst[jj];
                    if (chIdx < 0) { idxsExcl.Add(jj); continue; }

                    double m = dmean[chIdx];
                    if (m <= opt.DRange[0] || m >= opt.DRange[1])
                        idxsExcl.Add(jj);
                }

                // SNRthresh: mean/std <= thresh
                for (int jj = 0; jj < pairCount; jj++)
                {
                    int chIdx = lst[jj];
                    if (chIdx < 0) { idxsExcl.Add(jj); continue; }

                    double snr = dmean[chIdx] / dstd[chIdx]; // if std==0 => Inf (MATLAB-like)
                    if (snr <= opt.SnrThresh)
                        idxsExcl.Add(jj);
                }

                // SDrange: rho < min OR > max
                if (hasPos)
                {
                    for (int jj = 0; jj < pairCount; jj++)
                    {
                        double rho = rhoSD[jj];
                        if (rho < opt.SdRange[0] || rho > opt.SdRange[1])
                            idxsExcl.Add(jj);
                    }
                }

                // Apply: chanList(lst(idxsExcl)) = 0
                foreach (int jj in idxsExcl)
                {
                    int chIdx = lst[jj];
                    if (chIdx >= 0) chanList[chIdx] = false;
                }
            }

            // Manual AND: chanList = chanList & mlActMan(:,3)
            for (int i = 0; i < nCh; i++)
                chanList[i] = chanList[i] && mlActManActive[i];

            // ---- Strict Homer3 wavelength coupling (half split) ----
            // nFirst = length(chanList)/2
            // second(~first)=0; first(~second)=0;
            int nFirstHalf = nCh / 2;
            for (int i = 0; i < nFirstHalf; i++)
            {
                bool first = chanList[i];
                bool second = chanList[i + nFirstHalf];

                if (!first) chanList[i + nFirstHalf] = false;
                if (!second) chanList[i] = false;
            }

            return chanList;
        }

        // ----------------- Base alignment + ordering checks -----------------

        /// <summary>
        /// Returns wlOffset:
        /// - 0  => stored wavelengthIndex already 1-based (1..2)
        /// - +1 => stored wavelengthIndex is 0-based (0..1), so Homer3Index = stored + 1
        /// Throws if cannot determine reliably.
        /// </summary>
        private static int DetectWavelengthIndexOffset(IList<MeasList> ml)
        {
            int minW = int.MaxValue;
            int maxW = int.MinValue;

            for (int i = 0; i < ml.Count; i++)
            {
                int w = GetWavelengthIndexRaw(ml[i]);
                if (w < minW) minW = w;
                if (w > maxW) maxW = w;
            }

            // Common cases:
            // 0-based: {0,1}
            if (minW == 0 && maxW == 1) return 1;

            // 1-based: {1,2}
            if (minW == 1 && maxW == 2) return 0;

            // Sometimes files may contain only one wavelength in a partial dataset
            // but PruneChannels strict needs two wavelengths.
            throw new InvalidOperationException(
                "Cannot determine wavelength index base. Expected raw WavelengthIndex range [0,1] or [1,2], " +
                $"but got [{minW},{maxW}].");
        }

        private static bool LooksLikeHalfSplitOrder(IList<MeasList> ml, int wlOffset)
        {
            int nCh = ml.Count;
            if (nCh % 2 != 0) return false;

            int half = nCh / 2;

            for (int i = 0; i < half; i++)
            {
                if (GetWavelengthIndexHomer3(ml[i], wlOffset) != 1) return false;
            }
            for (int i = half; i < nCh; i++)
            {
                if (GetWavelengthIndexHomer3(ml[i], wlOffset) != 2) return false;
            }
            return true;
        }

        // ----------------- MATLAB-like mean/std over included time points -----------------

        private static void ComputeMeanStdOverIncluded_MatlabLike(double[,] dAll, bool[] tInc, double[] mean, double[] std)
        {
            int T = dAll.GetLength(0);
            int M = dAll.GetLength(1);

            for (int ch = 0; ch < M; ch++)
            {
                double sum = 0.0;
                double sum2 = 0.0;
                int n = 0;

                for (int t = 0; t < T; t++)
                {
                    if (!tInc[t]) continue;

                    double v = dAll[t, ch];

                    // MATLAB mean/std: NaN propagates (default mean/std).
                    if (double.IsNaN(v))
                    {
                        sum = double.NaN;
                        sum2 = double.NaN;
                        n = 1;
                        break;
                    }

                    sum += v;
                    sum2 += v * v;
                    n++;
                }

                if (n == 0)
                {
                    mean[ch] = double.NaN;
                    std[ch] = double.NaN;
                    continue;
                }

                mean[ch] = sum / n;

                if (n <= 1)
                {
                    std[ch] = 0.0;
                    continue;
                }

                double var = (sum2 - n * mean[ch] * mean[ch]) / (n - 1); // sample variance like MATLAB std default
                std[ch] = Math.Sqrt(var);
            }
        }

        // ----------------- MeasurementList accessors (edit here if your model differs) -----------------

        private static int GetSourceIndex(MeasList m)
        {
            return m.SourceIndex; // expected 1-based like Homer3
        }

        private static int GetDetectorIndex(MeasList m)
        {
            return m.DetectorIndex; // expected 1-based like Homer3
        }

        /// <summary>
        /// Raw stored wavelength index from your C# parser (may be 0-based or 1-based).
        /// </summary>
        private static int GetWavelengthIndexRaw(MeasList m)
        {
            return m.WavelengthIndex;
        }

        /// <summary>
        /// Homer3-aligned wavelength index (must be 1 or 2 here).
        /// </summary>
        private static int GetWavelengthIndexHomer3(MeasList m, int wlOffset)
        {
            return GetWavelengthIndexRaw(m) + wlOffset;
        }

        private static int FindChannelIndex(IList<MeasList> ml, int srcIdx, int detIdx, int wlHomer3Idx, int wlOffset)
        {
            for (int i = 0; i < ml.Count; i++)
            {
                if (GetSourceIndex(ml[i]) == srcIdx &&
                    GetDetectorIndex(ml[i]) == detIdx &&
                    GetWavelengthIndexHomer3(ml[i], wlOffset) == wlHomer3Idx)
                    return i;
            }
            return -1;
        }

        // ----------------- Distance -----------------

        private static double NormSrcDet(double[,] srcPos, double[,] detPos, int src1Based, int det1Based)
        {
            int s = src1Based - 1;
            int d = det1Based - 1;

            if (s < 0 || d < 0 ||
                s >= srcPos.GetLength(0) ||
                d >= detPos.GetLength(0))
                return double.NaN;

            double dx = srcPos[s, 0] - detPos[d, 0];
            double dy = srcPos[s, 1] - detPos[d, 1];
            double dz = 0.0;

            if (srcPos.GetLength(1) > 2 && detPos.GetLength(1) > 2)
                dz = srcPos[s, 2] - detPos[d, 2];

            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}
