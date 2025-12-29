using System;
using System.Collections.Generic;

namespace FnirsExe.Snirf.Processing
{
    /// <summary>
    /// Homer3-style Wavelet Motion Correction (close 1:1 to hmrR_MotionCorrectWavelet idea):
    /// - db2 wavelet
    /// - shift-invariant style using MODWT (maximal overlap / undecimated)
    /// - iqr * IQR outlier hard removal (set coeffs to 0)
    /// - zero pad to 2^N, remove DC, noise normalization, reconstruct, unpad
    ///
    /// Notes:
    /// - Homer3 uses internal Matlab functions (NormalizationNoise/WT_inv/WaveletAnalysis).
    ///   This implementation matches the same processing steps and intent closely.
    /// - Boundary handling here uses circular convolution (periodic extension), which is standard for MODWT.
    /// </summary>
    public static class MotionCorrectWaveletHomer3
    {
        /// <summary>
        /// Apply wavelet motion correction in-place on dod [T x M].
        /// mlActMan/mlActAuto: vectors length M, 1=active, 0=inactive. null/empty => treated as all active.
        /// iqr: if <0 => skip (same as Homer3). Typical 1.5.
        /// turnon: 0 => skip, 1 => enable.
        /// </summary>
        public static void ApplyInPlace(double[,] dod,
                                        int[] mlActMan,
                                        int[] mlActAuto,
                                        double iqr = 1.5,
                                        int turnon = 1)
        {
            if (dod == null) throw new ArgumentNullException(nameof(dod));

            if (turnon == 0) return;
            if (iqr < 0) return;
            if (iqr == 0) return; // degenerate: remove everything beyond median; keep safe

            int T = dod.GetLength(0);
            int M = dod.GetLength(1);

            // Initialize active masks (Homer3: if empty, initialize; then combine)
            bool[] actMan = BuildActiveMask(M, mlActMan);
            bool[] actAuto = BuildActiveMask(M, mlActAuto);
            bool[] act = new bool[M];
            for (int ch = 0; ch < M; ch++)
                act[ch] = actMan[ch] && actAuto[ch];

            int SignalLength = T;
            int N = (int)Math.Ceiling(Math.Log(SignalLength, 2.0)); // #levels
            int L = 4; // Homer3 fixed in code
            if (N < 1) return;

            // In Homer3: DataPadded = zeros(2^N,1)
            int padLen = 1 << N;

            var x = new double[SignalLength];
            var padded = new double[padLen];

            for (int ch = 0; ch < M; ch++)
            {
                if (!act[ch])
                    continue; // inactive channels remain unchanged (Homer3 behavior)

                // copy channel
                for (int t = 0; t < SignalLength; t++) x[t] = dod[t, ch];

                // pad with zeros to 2^N
                Array.Clear(padded, 0, padLen);
                Array.Copy(x, 0, padded, 0, SignalLength);

                // remove DC
                double dc = Mean(padded);
                for (int i = 0; i < padLen; i++) padded[i] -= dc;

                // NormalizationNoise (Homer3): estimate noise scale and normalize
                // Here: estimate from level-1 detail (highest frequency) via MODWT db2; sigma via MAD; NormCoef = sigma (guarded)
                double normCoef = EstimateNoiseScaleMad_Db2_ModwtLevel1(padded);
                if (normCoef <= 0) normCoef = 1e-12;
                for (int i = 0; i < padLen; i++) padded[i] /= normCoef;

                // MODWT decomposition up to N levels
                // W[j] = detail at level j (1-based), V = scaling (approx)
                var W = new double[N][];
                double[] V = (double[])padded.Clone();

                for (int j = 1; j <= N; j++)
                {
                    (double[] Wj, double[] Vj) = Modwt_Db2_OneLevel(V, j);
                    W[j - 1] = Wj;
                    V = Vj;
                }

                // WaveletAnalysis(StatWT,L,'db2',iqr,SignalLength):
                // hard-remove coeffs outside median ± iqr*IQR using coefficients distribution
                // Apply from L..N (if L > N, nothing happens)
                if (L <= N)
                {
                    var pool = new List<double>(padLen * (N - L + 1));
                    for (int j = L; j <= N; j++)
                        pool.AddRange(W[j - 1]);

                    // distribution stats
                    double q1 = Quantile(pool, 0.25);
                    double q3 = Quantile(pool, 0.75);
                    double iqrVal = q3 - q1;
                    if (iqrVal < 1e-18) iqrVal = 1e-18;

                    // center around median (robust)
                    double med = Quantile(pool, 0.50);
                    double low = med - iqr * iqrVal;
                    double high = med + iqr * iqrVal;

                    for (int j = L; j <= N; j++)
                    {
                        var wj = W[j - 1];
                        for (int i = 0; i < wj.Length; i++)
                        {
                            double v = wj[i];
                            if (v < low || v > high)
                                wj[i] = 0.0; // Homer3 description: set to zero
                        }
                    }
                }

                // inverse MODWT reconstruction
                double[] recon = (double[])V.Clone(); // start from coarsest scaling
                for (int j = N; j >= 1; j--)
                    recon = Imodwt_Db2_OneLevel(recon, W[j - 1], j);

                // de-normalize and add DC back
                for (int i = 0; i < padLen; i++) recon[i] = recon[i] * normCoef + dc;

                // unpad back to original length and write back
                for (int t = 0; t < SignalLength; t++)
                    dod[t, ch] = recon[t];
            }
        }

        // ------------------------- Active masks -------------------------

        private static bool[] BuildActiveMask(int M, int[] ml)
        {
            var act = new bool[M];
            if (ml == null || ml.Length == 0)
            {
                for (int i = 0; i < M; i++) act[i] = true;
                return act;
            }

            int len = Math.Min(M, ml.Length);
            for (int i = 0; i < len; i++) act[i] = (ml[i] != 0);
            for (int i = len; i < M; i++) act[i] = true; // if shorter, treat remaining as active
            return act;
        }

        // ------------------------- Homer3-like noise normalization -------------------------

        /// <summary>
        /// Estimate noise scale using MAD of MODWT level-1 detail coefficients with db2.
        /// This approximates Homer3's NormalizationNoise behavior (robust noise scaling).
        /// </summary>
        private static double EstimateNoiseScaleMad_Db2_ModwtLevel1(double[] x)
        {
            // Level 1 detail
            var (W1, _) = Modwt_Db2_OneLevel(x, 1);

            // sigma ≈ MAD / 0.6745
            return MadSigma(W1);
        }

        // ------------------------- MODWT db2 (analysis & synthesis) -------------------------

        // db2 scaling filter (orthonormal) length 4
        // h = [ (1+sqrt3)/(4*sqrt2), (3+sqrt3)/(4*sqrt2), (3-sqrt3)/(4*sqrt2), (1-sqrt3)/(4*sqrt2) ]
        // wavelet filter g[k] = (-1)^k * h[L-1-k]
        private static readonly double[] H_db2 = BuildDb2Scaling();
        private static readonly double[] G_db2 = BuildDb2WaveletFromScaling(H_db2);

        // MODWT uses rescaled filters: h_tilde = h / sqrt(2), g_tilde = g / sqrt(2)
        private static (double[] Wj, double[] Vj) Modwt_Db2_OneLevel(double[] Vprev, int level)
        {
            int n = Vprev.Length;
            var W = new double[n];
            var V = new double[n];

            double[] ht = GetUpsampledFilter(H_db2, level, scaleBySqrt2: true);
            double[] gt = GetUpsampledFilter(G_db2, level, scaleBySqrt2: true);

            CircularConvolve(Vprev, gt, W);
            CircularConvolve(Vprev, ht, V);

            return (W, V);
        }

        private static double[] Imodwt_Db2_OneLevel(double[] Vj, double[] Wj, int level)
        {
            int n = Vj.Length;
            var Vprev = new double[n];

            // synthesis filters for orthonormal wavelets:
            // time-reversed analysis filters (MODWT-scaled)
            double[] ht = GetUpsampledFilter(H_db2, level, scaleBySqrt2: true);
            double[] gt = GetUpsampledFilter(G_db2, level, scaleBySqrt2: true);

            double[] hrev = Reverse(ht);
            double[] grev = Reverse(gt);

            var partA = new double[n];
            var partD = new double[n];
            CircularConvolve(Vj, hrev, partA);
            CircularConvolve(Wj, grev, partD);

            for (int i = 0; i < n; i++)
                Vprev[i] = partA[i] + partD[i];

            return Vprev;
        }

        private static void CircularConvolve(double[] x, double[] f, double[] y)
        {
            int n = x.Length;
            int m = f.Length;
            Array.Clear(y, 0, y.Length);

            // y[t] = sum_k f[k] * x[(t - k) mod n]
            for (int t = 0; t < n; t++)
            {
                double s = 0.0;
                for (int k = 0; k < m; k++)
                {
                    int idx = t - k;
                    idx %= n;
                    if (idx < 0) idx += n;
                    s += f[k] * x[idx];
                }
                y[t] = s;
            }
        }

        private static double[] GetUpsampledFilter(double[] baseFilter, int level, bool scaleBySqrt2)
        {
            // level j => insert (2^(j-1)-1) zeros between taps
            int step = 1 << (level - 1);
            int L = baseFilter.Length;
            int outLen = (L - 1) * step + 1;
            var up = new double[outLen];

            double scale = scaleBySqrt2 ? (1.0 / Math.Sqrt(2.0)) : 1.0;
            for (int k = 0; k < L; k++)
                up[k * step] = baseFilter[k] * scale;

            return up;
        }

        private static double[] Reverse(double[] a)
        {
            var r = new double[a.Length];
            for (int i = 0; i < a.Length; i++)
                r[i] = a[a.Length - 1 - i];
            return r;
        }

        private static double[] BuildDb2Scaling()
        {
            double s3 = Math.Sqrt(3.0);
            double s2 = Math.Sqrt(2.0);
            return new[]
            {
                (1 + s3) / (4 * s2),
                (3 + s3) / (4 * s2),
                (3 - s3) / (4 * s2),
                (1 - s3) / (4 * s2)
            };
        }

        private static double[] BuildDb2WaveletFromScaling(double[] h)
        {
            int L = h.Length;
            var g = new double[L];
            for (int k = 0; k < L; k++)
            {
                double sign = (k % 2 == 0) ? 1.0 : -1.0;
                g[k] = sign * h[L - 1 - k];
            }
            return g;
        }

        // ------------------------- Stats helpers -------------------------

        private static double Mean(double[] x)
        {
            double s = 0;
            for (int i = 0; i < x.Length; i++) s += x[i];
            return s / x.Length;
        }

        private static double MadSigma(double[] data)
        {
            if (data == null || data.Length == 0) return 0;

            var arr = (double[])data.Clone();
            Array.Sort(arr);
            double med = MedianSorted(arr);

            for (int i = 0; i < arr.Length; i++)
                arr[i] = Math.Abs(arr[i] - med);

            Array.Sort(arr);
            double mad = MedianSorted(arr);
            return mad / 0.6745;
        }

        private static double MedianSorted(double[] sorted)
        {
            int n = sorted.Length;
            if (n == 0) return 0;
            if ((n & 1) == 1) return sorted[n / 2];
            return 0.5 * (sorted[n / 2 - 1] + sorted[n / 2]);
        }

        private static double Quantile(List<double> data, double p)
        {
            if (data == null || data.Count == 0) return 0;
            if (p <= 0) return Min(data);
            if (p >= 1) return Max(data);

            var arr = data.ToArray();
            Array.Sort(arr);

            // linear interpolation between closest ranks
            double pos = (arr.Length - 1) * p;
            int i = (int)Math.Floor(pos);
            int j = (int)Math.Ceiling(pos);
            if (i == j) return arr[i];

            double w = pos - i;
            return arr[i] * (1.0 - w) + arr[j] * w;
        }

        private static double Min(List<double> a)
        {
            double m = double.PositiveInfinity;
            for (int i = 0; i < a.Count; i++) if (a[i] < m) m = a[i];
            return m;
        }

        private static double Max(List<double> a)
        {
            double m = double.NegativeInfinity;
            for (int i = 0; i < a.Count; i++) if (a[i] > m) m = a[i];
            return m;
        }
    }
}
