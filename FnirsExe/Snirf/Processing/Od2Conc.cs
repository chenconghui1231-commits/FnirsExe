using System;
using System.Collections.Generic;
using System.Linq;
using FnirsExe.Snirf.Models;

namespace FnirsExe.Snirf.Processing
{
    public static class Od2Conc
    {
        // =============================
        // Multi-wavelength (e.g., 730/850/940) support for realtime pipeline
        // =============================

        /// <summary>
        /// Solve HbO/HbR from multi-wavelength OD using Homer3-style extinction coefficients.
        /// For 2 wavelengths this reduces to the usual 2x2 inverse; for 3+ wavelengths it uses
        /// least squares: x = (A^T A)^{-1} A^T y, where x=[HbO,HbR].
        ///
        /// Conventions match ComputeStrictHomer3():
        /// - Extinction coefficients in the embedded table are treated as cm^-1 and converted to mm^-1 by /10.
        /// - y_k = OD_k / (rho_mm * ppf_k)
        /// - HbT = HbO + HbR
        ///
        /// NOTE: The built-in table covers 600..900nm. For wavelengths outside that range (e.g. 940nm),
        /// this method linearly extrapolates using boundary segments.
        /// </summary>
        public static (double HbO, double HbR, double HbT) SolveHbFromOdMultiWavelength(
            double[] od,
            double[] lambdasNm,
            double rhoMm,
            double[] ppf)
        {
            if (od == null || lambdasNm == null || ppf == null) return (double.NaN, double.NaN, double.NaN);
            int n = Math.Min(od.Length, Math.Min(lambdasNm.Length, ppf.Length));
            if (n < 2) return (double.NaN, double.NaN, double.NaN);

            // ---- rho 单位归一到 mm ----
            if (rhoMm > 0.1 && rhoMm < 10.0) rhoMm *= 10.0; // cm->mm
            if (rhoMm < 0.1 || double.IsNaN(rhoMm)) rhoMm = 30.0;

            // Build normal equations for least squares: (A^T A) x = (A^T y)
            // A is (n x 2) with columns [eHbO, eHbR]
            double a00 = 0.0, a01 = 0.0, a11 = 0.0;
            double b0 = 0.0, b1 = 0.0;

            for (int k = 0; k < n; k++)
            {
                double lam = lambdasNm[k];
                var e = InterpExtrapolate(lam);
                // cm^-1 -> mm^-1
                double eo = e.o / 10.0;
                double er = e.r / 10.0;

                double denom = rhoMm * ppf[k];
                if (denom == 0 || double.IsNaN(denom) || double.IsInfinity(denom)) continue;
                double y = od[k] / denom;

                a00 += eo * eo;
                a01 += eo * er;
                a11 += er * er;
                b0 += eo * y;
                b1 += er * y;
            }

            double det = a00 * a11 - a01 * a01;
            if (Math.Abs(det) < 1e-12) return (double.NaN, double.NaN, double.NaN);

            double hbo = (a11 * b0 - a01 * b1) / det;
            double hbr = (-a01 * b0 + a00 * b1) / det;
            return (hbo, hbr, hbo + hbr);
        }

        /// <summary>
        /// Convenience wrapper for 3 wavelengths.
        /// </summary>
        public static (double HbO, double HbR, double HbT) SolveHbFromOd3Wavelengths(
            double od1, double od2, double od3,
            double lambda1Nm, double lambda2Nm, double lambda3Nm,
            double rhoMm,
            double ppf1 = 6.0, double ppf2 = 6.0, double ppf3 = 6.0)
        {
            return SolveHbFromOdMultiWavelength(
                new[] { od1, od2, od3 },
                new[] { lambda1Nm, lambda2Nm, lambda3Nm },
                rhoMm,
                new[] { ppf1, ppf2, ppf3 });
        }

        public sealed class ConcResult
        {
            public double[,] HbO;
            public double[,] HbR;
            public double[,] HbT;
            public double[] Time;
            public List<PairInfo> Pairs;
        }

        public class PairInfo
        {
            public int SourceIndex;
            public int DetectorIndex;
            public double Lambda1;
            public double Lambda2;

            // 注意：我们将其更新为“最终使用的 rho(mm)”
            public double Rho;

            public int Wavelength1Index;
            public int Wavelength2Index;
        }

        public static ConcResult ComputeStrictHomer3(SnirfFile snirf, int baselineFlag = 0, double[] ppf = null)
        {
            if (snirf?.Data == null || snirf.Data.Count == 0) return null;
            var block = snirf.Data[0];
            int T = block.DataTimeSeries.GetLength(0);

            var pairs = FindPairs(block.MeasurementList, snirf.Probe);
            int P = pairs.Count;
            if (P == 0) return null;

            var res = new ConcResult
            {
                HbO = new double[T, P],
                HbR = new double[T, P],
                HbT = new double[T, P],
                Time = block.Time,
                Pairs = pairs
            };

            if (ppf == null || ppf.Length < 2) ppf = new double[] { 6.0, 6.0 };

            for (int i = 0; i < P; i++)
            {
                var p = pairs[i];

                var E = GetExtinctionsHomer3(p.Lambda1, p.Lambda2);
                // cm^-1 -> mm^-1
                E[0, 0] /= 10.0; E[0, 1] /= 10.0; E[1, 0] /= 10.0; E[1, 1] /= 10.0;

                double det = E[0, 0] * E[1, 1] - E[0, 1] * E[1, 0];
                if (Math.Abs(det) < 1e-9) det = 1.0;

                double i00 = E[1, 1] / det, i01 = -E[0, 1] / det;
                double i10 = -E[1, 0] / det, i11 = E[0, 0] / det;

                // ---- rho 单位归一到 mm ----
                double rho = p.Rho;
                if (rho > 0.1 && rho < 10.0) rho *= 10.0;     // cm -> mm
                if (rho < 0.1 || double.IsNaN(rho)) rho = 30.0;

                // ✅关键：把最终用的 rho(mm) 写回 PairInfo，供绘图/调试使用
                p.Rho = rho;

                double p1 = rho * ppf[0];
                double p2 = rho * ppf[1];

                int c1 = p.Wavelength1Index;
                int c2 = p.Wavelength2Index;

                for (int t = 0; t < T; t++)
                {
                    double od1 = block.DataTimeSeries[t, c1];
                    double od2 = block.DataTimeSeries[t, c2];

                    // 允许传播（你现在工程是这个策略）
                    double y1 = od1 / p1;
                    double y2 = od2 / p2;

                    res.HbO[t, i] = i00 * y1 + i01 * y2;
                    res.HbR[t, i] = i10 * y1 + i11 * y2;
                    res.HbT[t, i] = res.HbO[t, i] + res.HbR[t, i];
                }
            }

            return res;
        }

        private static List<PairInfo> FindPairs(List<MeasList> ml, Probe probe)
        {
            var pairs = new List<PairInfo>();
            if (ml == null) return pairs;

            for (int i = 0; i < ml.Count; i++)
            {
                if (ml[i].WavelengthIndex == 1)
                {
                    int src = ml[i].SourceIndex;
                    int det = ml[i].DetectorIndex;
                    int idx2 = ml.FindIndex(m => m.SourceIndex == src && m.DetectorIndex == det && m.WavelengthIndex == 2);
                    if (idx2 >= 0)
                    {
                        double l1 = (probe.Wavelengths != null && probe.Wavelengths.Length >= 1) ? probe.Wavelengths[0] : 760;
                        double l2 = (probe.Wavelengths != null && probe.Wavelengths.Length >= 2) ? probe.Wavelengths[1] : 850;
                        double dist = GetDistance(probe, src, det);

                        pairs.Add(new PairInfo
                        {
                            SourceIndex = src,
                            DetectorIndex = det,
                            Wavelength1Index = i,
                            Wavelength2Index = idx2,
                            Lambda1 = l1,
                            Lambda2 = l2,
                            Rho = dist
                        });
                    }
                }
            }

            return pairs.OrderBy(p => p.SourceIndex).ThenBy(p => p.DetectorIndex).ToList();
        }

        private static double GetDistance(Probe p, int s, int d)
        {
            if (p?.SourcePos3D == null || p?.DetectorPos3D == null) return 0.0;
            double[] p1 = GetPos(p.SourcePos3D, s - 1), p2 = GetPos(p.DetectorPos3D, d - 1);
            if (p1 == null || p2 == null) return 0.0;
            return Math.Sqrt(Math.Pow(p1[0] - p2[0], 2) + Math.Pow(p1[1] - p2[1], 2) + Math.Pow(p1[2] - p2[2], 2));
        }

        private static double[] GetPos(double[,] pos, int i)
            => (pos != null && i >= 0 && i < pos.GetLength(0))
                ? new double[] { pos[i, 0], pos[i, 1], pos[i, 2] }
                : null;

        private static double[,] GetExtinctionsHomer3(double l1, double l2)
        {
            var e = new double[2, 2];
            var c1 = Interp(l1);
            var c2 = Interp(l2);
            e[0, 0] = c1.o; e[0, 1] = c1.r;
            e[1, 0] = c2.o; e[1, 1] = c2.r;
            return e;
        }

        private static (double o, double r) Interp(double x)
        {
            double[] wl = { 600, 700, 750, 760, 800, 850, 900 };
            double[] ho = { 7369, 672, 3236, 3422, 4168, 5817, 6912 };
            double[] hr = { 36848, 4145, 9007, 8850, 4421, 4140, 4007 };
            if (x < wl[0] || x > wl[6]) return (0, 0);
            for (int i = 0; i < 6; i++)
            {
                if (x >= wl[i] && x <= wl[i + 1])
                {
                    double r = (x - wl[i]) / (wl[i + 1] - wl[i]);
                    return (ho[i] + r * (ho[i + 1] - ho[i]),
                            hr[i] + r * (hr[i + 1] - hr[i]));
                }
            }
            return (0, 0);
        }


        /// <summary>
        /// Same table as Interp(), but linearly extrapolates outside range.
        /// This is needed for realtime devices using 940nm, etc.
        /// </summary>
        private static (double o, double r) InterpExtrapolate(double x)
        {
            double[] wl = { 600, 700, 750, 760, 800, 850, 900 };
            double[] ho = { 7369, 672, 3236, 3422, 4168, 5817, 6912 };
            double[] hr = { 36848, 4145, 9007, 8850, 4421, 4140, 4007 };

            // Below range: extrapolate using first segment
            if (x <= wl[0])
            {
                double r0 = (x - wl[0]) / (wl[1] - wl[0]);
                return (ho[0] + r0 * (ho[1] - ho[0]), hr[0] + r0 * (hr[1] - hr[0]));
            }

            // Inside range: interpolate
            for (int i = 0; i < wl.Length - 1; i++)
            {
                if (x >= wl[i] && x <= wl[i + 1])
                {
                    double r = (x - wl[i]) / (wl[i + 1] - wl[i]);
                    return (ho[i] + r * (ho[i + 1] - ho[i]), hr[i] + r * (hr[i + 1] - hr[i]));
                }
            }

            // Above range: extrapolate using last segment
            int n = wl.Length;
            double r1 = (x - wl[n - 2]) / (wl[n - 1] - wl[n - 2]);
            return (ho[n - 2] + r1 * (ho[n - 1] - ho[n - 2]), hr[n - 2] + r1 * (hr[n - 1] - hr[n - 2]));
        }

    }
}
