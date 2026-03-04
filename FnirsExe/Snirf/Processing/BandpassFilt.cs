using System;
using System.Collections.Generic;

namespace FnirsExe.Snirf.Processing
{

    public static class BandpassFilt
    {
        private struct SosSection { public double b0, b1, b2, a1, a2; }

        public static void ApplyInPlace(double[,] x, double fs, double hpf, double lpf)
        {
            if (x == null) return;
            int T = x.GetLength(0);
            int M = x.GetLength(1);

            // 设计滤波器
            var sosLow = DesignButterworth(3, lpf, fs, true);
            var sosHigh = DesignButterworth(2, hpf, fs, false);

            double[] col = new double[T];

            for (int ch = 0; ch < M; ch++)
            {
                for (int i = 0; i < T; i++) col[i] = x[i, ch];

                // 仅保留去直流 (通常 Homer3 不显式做，但为了波形可见度保留这一步物理操作)
                // 如果不需要可注释掉
                /*
                double firstVal = col[0];
                for (int i = 0; i < T; i++) col[i] -= firstVal;
                */

                // 直接滤波，不检查 isBad，不回退
                if (lpf > 0) col = FiltFiltHomer3(col, sosLow);
                if (hpf > 0) col = FiltFiltHomer3(col, sosHigh);

                // 写入结果
                for (int i = 0; i < T; i++) x[i, ch] = col[i];
            }
        }

        // ... (保持 FiltFiltHomer3, FilterSos, DesignButterworth 方法不变) ...
        // ... (请复制之前的底层私有方法代码，此处省略以节省篇幅) ...

        private static double[] FiltFiltHomer3(double[] x, List<SosSection> sos)
        {
            int n = x.Length;
            int nFact = 3 * (sos.Count * 2);
            if (nFact > n - 1) nFact = n - 1;
            int nExt = n + 2 * nFact;
            double[] y = new double[nExt];

            for (int i = 0; i < nFact; i++) y[i] = 2 * x[0] - x[nFact - i];
            for (int i = 0; i < n; i++) y[nFact + i] = x[i];
            for (int i = 0; i < nFact; i++) y[nFact + n + i] = 2 * x[n - 1] - x[n - 2 - i];

            FilterSos(y, sos); Array.Reverse(y);
            FilterSos(y, sos); Array.Reverse(y);

            double[] result = new double[n];
            for (int i = 0; i < n; i++) result[i] = y[nFact + i];
            return result;
        }

        private static void FilterSos(double[] data, List<SosSection> sos)
        {
            int len = data.Length;
            double[,] w = new double[sos.Count, 2];
            for (int i = 0; i < len; i++)
            {
                double val = data[i];
                for (int s = 0; s < sos.Count; s++)
                {
                    var sec = sos[s];
                    double yn = sec.b0 * val + w[s, 0];
                    w[s, 0] = sec.b1 * val - sec.a1 * yn + w[s, 1];
                    w[s, 1] = sec.b2 * val - sec.a2 * yn;
                    val = yn;
                }
                data[i] = val;
            }
        }

        private static List<SosSection> DesignButterworth(int order, double fc, double fs, bool isLowPass)
        {
            var sos = new List<SosSection>();
            if (fc <= 0 || fc >= fs / 2) return sos;
            double w0 = 2.0 * Math.PI * fc / fs;
            double u = Math.Tan(w0 / 2.0);
            for (int k = 0; k < (order + 1) / 2; k++)
            {
                double theta = Math.PI * (2.0 * k + order + 1.0) / (2.0 * order);
                double re = Math.Cos(theta);
                double im = Math.Sin(theta);
                if (Math.Abs(im) < 1e-9)
                {
                    double b0, b1, a1; double D = 1 + u;
                    if (isLowPass) { b0 = u / D; b1 = u / D; a1 = (u - 1) / D; }
                    else { b0 = 1 / D; b1 = -1 / D; a1 = (u - 1) / D; }
                    sos.Add(new SosSection { b0 = b0, b1 = b1, b2 = 0, a1 = a1, a2 = 0 });
                }
                else
                {
                    double D, b0, b1, b2, a1, a2;
                    double u2 = u * u; double neu = -2 * re * u;
                    D = 1 + neu + u2;
                    if (isLowPass)
                    {
                        b0 = u2 / D; b1 = (2 * u2) / D; b2 = u2 / D; a1 = (2 * u2 - 2) / D; a2 = (1 - neu + u2) / D;
                    }
                    else
                    {
                        b0 = 1 / D; b1 = -2 / D; b2 = 1 / D; a1 = (2 * u2 - 2) / D; a2 = (1 - neu + u2) / D;
                    }
                    sos.Add(new SosSection { b0 = b0, b1 = b1, b2 = b2, a1 = a1, a2 = a2 });
                }
            }
            return sos;
        }
    }
}