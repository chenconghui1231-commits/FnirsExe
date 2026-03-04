using System;
using FnirsExe.Snirf.Models;

namespace FnirsExe.Snirf.Processing
{
    public static class IntensityToOd
    {
        public sealed class Options
        {
            public bool RepairNaNInf { get; set; } = true;

            public enum NegativeFixMode
            {
                AddOffset = 1,
                SetToEps = 2
            }

            public NegativeFixMode FixNegative { get; set; } = NegativeFixMode.SetToEps;

            // 可选：Homer3 MedianFilter(3点)；你的 procStream 没放这一项，默认关掉
            public bool ApplyMedian3 { get; set; } = false;
        }

        public static void ApplyInPlace(SnirfFile snirf, Options opt = null)
        {
            if (snirf?.Data == null || snirf.Data.Count == 0) return;
            if (opt == null) opt = new Options();

            const double EPS = 2.2204460492503131e-16; // MATLAB eps

            foreach (var block in snirf.Data)
            {
                var d = block.DataTimeSeries;
                if (d == null) continue;

                int nTpts = d.GetLength(0);
                int nCh = d.GetLength(1);
                double[] dm = new double[nCh];

                // 0) 先在 intensity 阶段修复异常，避免后续 log / filt 扩散
                if (opt.RepairNaNInf || opt.ApplyMedian3 || opt.FixNegative != Options.NegativeFixMode.SetToEps)
                {
                    for (int ch = 0; ch < nCh; ch++)
                    {
                        double[] x = new double[nTpts];
                        for (int t = 0; t < nTpts; t++) x[t] = d[t, ch];

                        if (opt.RepairNaNInf)
                        {
                            // 把 Inf 当作 NaN 处理（Homer3 NAN 只处理 isnan，但工程上必须稳）
                            for (int t = 0; t < nTpts; t++)
                                if (double.IsInfinity(x[t])) x[t] = double.NaN;

                            FillNaNByLinearInPlace(x);
                        }

                        if (opt.FixNegative == Options.NegativeFixMode.AddOffset)
                        {
                            double min = double.PositiveInfinity;
                            for (int t = 0; t < nTpts; t++)
                            {
                                if (double.IsNaN(x[t])) continue;
                                if (x[t] < min) min = x[t];
                            }
                            if (!double.IsInfinity(min) && min <= 0)
                            {
                                double offset = Math.Abs(min) + EPS;
                                for (int t = 0; t < nTpts; t++)
                                    if (!double.IsNaN(x[t])) x[t] += offset;
                            }
                        }
                        else
                        {
                            for (int t = 0; t < nTpts; t++)
                            {
                                if (double.IsNaN(x[t])) continue;
                                if (x[t] <= 0) x[t] = EPS;
                            }
                        }

                        if (opt.ApplyMedian3)
                        {
                            double[] y = new double[nTpts];
                            if (nTpts > 0) y[0] = x[0];
                            if (nTpts > 1) y[nTpts - 1] = x[nTpts - 1];
                            for (int t = 1; t < nTpts - 1; t++)
                                y[t] = Median3(x[t - 1], x[t], x[t + 1]);
                            x = y;
                        }

                        for (int t = 0; t < nTpts; t++) d[t, ch] = x[t];
                    }
                }

                // 1) baseline mean（忽略 NaN/Inf）
                for (int ch = 0; ch < nCh; ch++)
                {
                    double sum = 0.0;
                    int n = 0;
                    for (int t = 0; t < nTpts; t++)
                    {
                        double v = d[t, ch];
                        if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                        sum += v;
                        n++;
                    }
                    dm[ch] = n > 0 ? (sum / n) : double.NaN;
                }

                // 2) OD = -log(I / mean)
                for (int ch = 0; ch < nCh; ch++)
                {
                    double baseVal = dm[ch];
                    for (int t = 0; t < nTpts; t++)
                    {
                        double val = d[t, ch];
                        if (double.IsNaN(val) || double.IsInfinity(val) || double.IsNaN(baseVal) || baseVal == 0)
                        {
                            d[t, ch] = double.NaN;
                            continue;
                        }
                        d[t, ch] = -Math.Log(val / baseVal);
                    }
                }
            }
        }

        // ---- helpers ----

        private static void FillNaNByLinearInPlace(double[] y)
        {
            int n = y.Length;
            int firstValid = -1;
            for (int i = 0; i < n; i++)
            {
                if (!double.IsNaN(y[i]) && !double.IsInfinity(y[i]))
                {
                    firstValid = i;
                    break;
                }
            }
            if (firstValid < 0) return;

            // 前向填充
            for (int i = 0; i < firstValid; i++) y[i] = y[firstValid];

            int lastValid = firstValid;
            for (int i = firstValid + 1; i < n; i++)
            {
                if (!double.IsNaN(y[i]) && !double.IsInfinity(y[i]))
                {
                    // 线性插值 lastValid..i
                    int a = lastValid;
                    int b = i;
                    double ya = y[a];
                    double yb = y[b];
                    int gap = b - a;
                    for (int k = 1; k < gap; k++)
                        y[a + k] = ya + (yb - ya) * (k / (double)gap);

                    lastValid = i;
                }
            }

            // 末尾填充
            for (int i = lastValid + 1; i < n; i++) y[i] = y[lastValid];
        }

        private static double Median3(double a, double b, double c)
        {
            // 允许 NaN：尽量用现有值
            if (double.IsNaN(a) && double.IsNaN(b) && double.IsNaN(c)) return double.NaN;
            if (double.IsNaN(a)) return (b + c) * 0.5;
            if (double.IsNaN(b)) return (a + c) * 0.5;
            if (double.IsNaN(c)) return (a + b) * 0.5;

            if (a > b) { double t = a; a = b; b = t; }
            if (b > c) { double t = b; b = c; c = t; }
            if (a > b) { double t = a; a = b; b = t; }
            return b;
        }
    }
}
