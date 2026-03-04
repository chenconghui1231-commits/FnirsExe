using System;
using FnirsExe.Snirf.Models;

namespace FnirsExe.Snirf.Processing
{
    public static class MotionCorrectWavelet
    {
        public class Options
        {
            public double IQR { get; set; } = 1.5;
            public bool TurnOn { get; set; } = true;

            // 只对 active 通道做 wavelet（更贴近 Homer3）
            public bool[] ActiveMask { get; set; } = null;

            // Homer3: L = 4
            public int L { get; set; } = 4;
        }

        // db2 scaling filter h (Daubechies 2)
        private static readonly double[] h = new double[]
        {
            0.4829629131445341,
            0.8365163037378079,
            0.2241438680420134,
           -0.1294095225512603
        };

        // db2 wavelet filter g (QMF): g[k] = (-1)^k * h[L-1-k]
        private static readonly double[] g = BuildQmfFromScaling(h);

        public static void Apply(SnirfFile snirf, Options opt)
        {
            if (snirf?.Data == null) return;
            if (opt == null || !opt.TurnOn) return;
            if (opt.IQR < 0) return;

            int L = opt.L <= 0 ? 4 : opt.L;

            foreach (var block in snirf.Data)
            {
                double[,] dod = block.DataTimeSeries;
                if (dod == null) continue;

                int signalLen = dod.GetLength(0);
                int chCount = dod.GetLength(1);
                if (signalLen < 2) continue;

                int N = (int)Math.Ceiling(Math.Log(signalLen, 2.0));
                if (N < 1) N = 1;
                int nPow2 = 1 << N;

                // zero pad to 2^N like Homer3
                double[] padded = new double[nPow2];

                for (int ch = 0; ch < chCount; ch++)
                {
                    if (opt.ActiveMask != null && ch < opt.ActiveMask.Length && !opt.ActiveMask[ch])
                        continue;

                    Array.Clear(padded, 0, padded.Length);
                    for (int t = 0; t < signalLen; t++)
                        padded[t] = dod[t, ch];

                    // DC remove
                    double dc = MeanFinite(padded, signalLen); // ✅只用有效段算均值更贴近 Homer3
                    if (double.IsNaN(dc) || double.IsInfinity(dc)) dc = 0.0;

                    for (int i = 0; i < nPow2; i++)
                    {
                        double v = padded[i];
                        if (double.IsNaN(v) || double.IsInfinity(v)) v = 0.0;
                        padded[i] = v - dc;
                    }

                    // NormalizationNoise (近似实现)：用 level-1 detail 的 MAD 估计 sigma
                    double normCoef;
                    double[] yn = NormalizeNoise(padded, signalLen, out normCoef); // ✅统计也只用有效段

                    // SWT + per-level IQR threshold + inverse SWT
                    // ✅关键：IQR 统计/阈值只基于 signalLen，不把 padding 的 0 算进去
                    double[] ar = WaveletIqrDenoise_Swt_Strided(yn, N, L, opt.IQR, signalLen);

                    // undo normalization + add DC
                    double invNorm = (normCoef == 0.0 || double.IsNaN(normCoef) || double.IsInfinity(normCoef)) ? 1.0 : (1.0 / normCoef);
                    for (int i = 0; i < ar.Length; i++)
                        ar[i] = ar[i] * invNorm + dc;

                    // write back (original length)
                    for (int t = 0; t < signalLen; t++)
                        dod[t, ch] = ar[t];
                }
            }
        }

        // ---------------------------
        // SWT decomposition/reconstruction using strided periodic convolution
        // ---------------------------
        private static double[] WaveletIqrDenoise_Swt_Strided(double[] x, int N, int L, double iqrMul, int signalLen)
        {
            int n = x.Length;

            double[][] details = new double[N + 1][];
            for (int j = 1; j <= N; j++) details[j] = new double[n];

            double[] approx = (double[])x.Clone();

            // Decompose
            for (int j = 1; j <= N; j++)
            {
                int step = 1 << (j - 1);
                double[] aNew = ConvolvePeriodicStrided(approx, h, step);
                double[] dNew = ConvolvePeriodicStrided(approx, g, step);
                approx = aNew;
                details[j] = dNew;
            }

            // Threshold details by IQR from level L..N (Homer3: L=4)
            int startLevel = Math.Max(1, L);
            int validLen = Math.Min(signalLen, n); // ✅只在有效段做阈值
            for (int j = startLevel; j <= N; j++)
                ApplyIqrThresholdInPlace(details[j], iqrMul, validLen);

            // Reconstruct (inverse SWT)
            for (int j = N; j >= 1; j--)
            {
                int step = 1 << (j - 1);

                double[] aPart = ConvolvePeriodicStrided(approx, Reverse(h), step);
                double[] dPart = ConvolvePeriodicStrided(details[j], Reverse(g), step);

                for (int i = 0; i < n; i++)
                    approx[i] = 0.5 * (aPart[i] + dPart[i]);
            }

            return approx;
        }

        // y[i] = sum_{k} x[(i - k*step) mod n] * f[k]
        private static double[] ConvolvePeriodicStrided(double[] x, double[] f, int step)
        {
            int n = x.Length;
            int m = f.Length;
            double[] y = new double[n];

            for (int i = 0; i < n; i++)
            {
                double sum = 0.0;
                for (int k = 0; k < m; k++)
                {
                    int idx = i - k * step;
                    idx %= n;
                    if (idx < 0) idx += n;
                    sum += x[idx] * f[k];
                }
                y[i] = sum;
            }

            return y;
        }

        // ---------------------------
        // NormalizationNoise (近似)：MAD(detail1)/0.6745
        // ✅统计只基于有效段，避免 padding 0 影响 sigma
        // ---------------------------
        private static double[] NormalizeNoise(double[] x, int signalLen, out double normCoef)
        {
            double[] d1 = ConvolvePeriodicStrided(x, g, 1);
            double sigma = MadSigma(d1, signalLen);

            if (sigma <= 0 || double.IsNaN(sigma) || double.IsInfinity(sigma))
                sigma = 1.0;

            normCoef = sigma;

            double inv = 1.0 / sigma;
            double[] y = new double[x.Length];
            for (int i = 0; i < x.Length; i++)
                y[i] = x[i] * inv;

            return y;
        }

        private static double MadSigma(double[] v, int validLen)
        {
            validLen = Math.Min(validLen, v.Length);
            if (validLen <= 0) return 1.0;

            double[] abs = new double[validLen];
            for (int i = 0; i < validLen; i++) abs[i] = Math.Abs(v[i]);

            Array.Sort(abs);
            double med = QuantileSorted(abs, 0.5);
            return med / 0.6745;
        }

        // ✅IQR outlier -> 0（只用有效段 validLen 计算分位数&阈值，并且只阈值有效段）
        private static void ApplyIqrThresholdInPlace(double[] coeff, double mul, int validLen)
        {
            validLen = Math.Min(validLen, coeff.Length);
            if (validLen <= 4) return;

            double[] tmp = new double[validLen];
            Array.Copy(coeff, 0, tmp, 0, validLen);
            Array.Sort(tmp);

            double q1 = QuantileSorted(tmp, 0.25);
            double q3 = QuantileSorted(tmp, 0.75);
            double iqr = q3 - q1;

            if (iqr <= 0 || double.IsNaN(iqr) || double.IsInfinity(iqr))
                return;

            double lower = q1 - mul * iqr;
            double upper = q3 + mul * iqr;

            for (int i = 0; i < validLen; i++)
            {
                double v = coeff[i];
                if (v < lower || v > upper)
                    coeff[i] = 0.0;
            }
            // padding 段不动（一般本来就接近0）
        }

        private static double QuantileSorted(double[] sorted, double q)
        {
            if (sorted.Length == 0) return 0.0;
            double pos = (sorted.Length - 1) * q;
            int i = (int)pos;
            double f = pos - i;
            if (i >= sorted.Length - 1) return sorted[sorted.Length - 1];
            return sorted[i] * (1.0 - f) + sorted[i + 1] * f;
        }

        // ✅均值也只算有效段（SignalLength）
        private static double MeanFinite(double[] v, int validLen)
        {
            validLen = Math.Min(validLen, v.Length);
            double sum = 0.0;
            int n = 0;
            for (int i = 0; i < validLen; i++)
            {
                double x = v[i];
                if (double.IsNaN(x) || double.IsInfinity(x)) continue;
                sum += x;
                n++;
            }
            return n > 0 ? sum / n : double.NaN;
        }

        private static double[] Reverse(double[] a)
        {
            double[] r = new double[a.Length];
            for (int i = 0; i < a.Length; i++)
                r[i] = a[a.Length - 1 - i];
            return r;
        }

        private static double[] BuildQmfFromScaling(double[] scaling)
        {
            int L = scaling.Length;
            double[] qmf = new double[L];
            for (int k = 0; k < L; k++)
            {
                double sign = (k % 2 == 0) ? 1.0 : -1.0;
                qmf[k] = sign * scaling[L - 1 - k];
            }
            return qmf;
        }
    }
}
