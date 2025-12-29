using System;
using System.Collections.Generic;
using System.Linq;
using FnirsExe.Snirf.Models;

namespace FnirsExe.Snirf.Processing
{
    /// <summary>
    /// STRICT 1:1 port of Homer3 hmrR_OD2Conc (OD -> HbO/HbR/HbT)
    ///
    /// Matches Homer3 behaviors:
    /// 1) Extinctions: GetExtinctions(lambda, WhichSpectrum=0 default),
    ///    including the "* 2.303" scaling in GetExtinctions.m AND the "/10" in hmrR_OD2Conc
    ///    (convert /cm to /mm).
    /// 2) ppf handling:
    ///    - if length(ppf) < nWav => fall back to ones(1,nWav)
    ///    - if length(ppf) > nWav => truncate to nWav
    ///    - if any ppf == 1 => set ALL ppf = ones(size(ppf))
    /// 3) rho handling:
    ///    - if ppf(1) ~= 1: divide dOD by (rho * ppf) (rho in mm; ppf per wavelength)
    ///    - else: do NOT divide by rho (and ppf is all ones anyway)
    /// 4) channel pairing:
    ///    - lst = find( ml(:,4)==1 ) (wavelengthIndex==1) in measurementList order
    ///    - for each idx1 in lst, find the other wavelengths for same (src,det)
    ///      and build HbO/HbR/HbT output columns in the same iteration order.
    ///
    /// Output units are whatever Homer3 yields (no extra mM/uM scaling is applied).
    /// </summary>
    public static class Od2Conc
    {
        public sealed class PairInfo
        {
            public int PairIndex;        // 0-based
            public int SourceIndex;      // 1-based (SNIRF/Homer style)
            public int DetectorIndex;    // 1-based
            public int[] MeasIdxByWl;    // length = nWav, 0-based column indices into y
            public double RhoMm;         // source-detector distance in mm
        }

        public sealed class ConcResult
        {
            public double[] Time;        // length T
            public double[,] HbO;        // T x P
            public double[,] HbR;        // T x P
            public double[,] HbT;        // T x P
            public List<PairInfo> Pairs; // length P
        }

        // Compatibility overload (like your old API)
        public static ConcResult Compute(SnirfFile snirf, int dataBlockIndex = 0, double[] ppf = null)
        {
            return ComputeStrictHomer3(snirf, dataBlockIndex, ppf);
        }

        /// <summary>
        /// Strict Homer3 implementation.
        /// </summary>
        public static ConcResult ComputeStrictHomer3(SnirfFile snirf, int dataBlockIndex, double[] ppf)
        {
            if (snirf == null) throw new ArgumentNullException(nameof(snirf));
            if (snirf.Data == null || snirf.Data.Count <= dataBlockIndex)
                throw new Exception("SNIRF has no data block.");
            if (snirf.Probe == null)
                throw new Exception("probe missing.");

            var block = snirf.Data[dataBlockIndex];

            double[,] y = block.DataTimeSeries; // dod in Homer3
            double[] t = block.Time;
            var mlList = block.MeasurementList;

            if (y == null) throw new Exception("dataTimeSeries missing.");
            if (t == null || t.Length < 2) throw new Exception("time missing.");
            if (mlList == null || mlList.Count == 0) throw new Exception("measurementList missing.");

            // Homer3 uses probe.GetWls()
            double[] lambda = snirf.Probe.Wavelengths ?? Array.Empty<double>();
            int nWav = lambda.Length;
            if (nWav < 2)
                throw new Exception("probe.wavelengths must have at least 2 wavelengths for OD2Conc.");

            // --- Homer3 ppf rules ---
            double[] ppfH = NormalizePpfLikeHomer3(ppf, nWav);

            // --- Build ml matrix semantics (ml(:,1)=src, ml(:,2)=det, ml(:,4)=wavelengthIndex) ---
            // We only need src/det/wlIndex. We assume MeasList.SourceIndex/DetectorIndex/WavelengthIndex follow SNIRF.
            // Homer3 ml indices are 1-based. We'll normalize from 0-based if needed.
            var ml = NormalizeMeasurementListIndices(mlList);

            // --- Channel pairing strictly like Homer3 ---
            var pairs = BuildPairsStrictHomer3(ml, snirf.Probe, nWav);

            int T = t.Length;

            // Determine whether y is [T x M] or [M x T], similar to your previous guard.
            int R = y.GetLength(0);
            int C = y.GetLength(1);
            bool timeIsRow = (R == T);
            int M = timeIsRow ? C : R;

            // Validate measurement indices
            foreach (var p in pairs)
            {
                foreach (int midx in p.MeasIdxByWl)
                {
                    if (midx < 0 || midx >= M)
                        throw new Exception("Measurement index out of range for dataTimeSeries.");
                }
            }

            int P = pairs.Count;
            var HbO = new double[T, P];
            var HbR = new double[T, P];
            var HbT = new double[T, P];

            // --- Homer3 extinction handling ---
            // e = GetExtinctions(Lambda); e = e(:,1:2)/10; einv = inv(e'*e)*e';
            // We'll build E (nWav x 2), then compute Einv (2 x nWav).
            double[,] E = Homer3Extinctions.GetExtinctions_HbOHb(lambda, whichSpectrum: 0);

            // /10 convert from /cm to /mm (exactly like Homer3 hmrR_OD2Conc)
            for (int i = 0; i < nWav; i++)
            {
                E[i, 0] /= 10.0;
                E[i, 1] /= 10.0;
            }

            double[,] Einv = ComputePseudoInverse_2Chromophores(E); // 2 x nWav

            // --- Main loop: follow Homer3’s branch on ppf(1) ---
            bool useRhoAndPpf = (ppfH[0] != 1.0);

            for (int p = 0; p < P; p++)
            {
                var pair = pairs[p];

                for (int tt = 0; tt < T; tt++)
                {
                    // Gather dOD across wavelengths in the exact wavelength order 1..nWav
                    // Homer3: y(:, [idx1 idx2 ...]) where idx1 is wl=1 and others are wl>1 for same sd
                    double[] dOD = new double[nWav];
                    bool anyNaN = false;

                    for (int w = 0; w < nWav; w++)
                    {
                        int midx = pair.MeasIdxByWl[w];
                        double v = timeIsRow ? y[tt, midx] : y[midx, tt];
                        dOD[w] = v;
                        if (double.IsNaN(v)) anyNaN = true;
                    }

                    if (anyNaN || double.IsInfinity(pair.RhoMm) || pair.RhoMm <= 0)
                    {
                        HbO[tt, p] = double.NaN;
                        HbR[tt, p] = double.NaN;
                        HbT[tt, p] = double.NaN;
                        continue;
                    }

                    // Homer3:
                    // if ppf(1) ~= 1:
                    //    (einv * (y/(rho*ppf))')'
                    // else:
                    //    (einv * (y)')'
                    double[] b = new double[nWav];
                    if (useRhoAndPpf)
                    {
                        double rho = pair.RhoMm;
                        for (int w = 0; w < nWav; w++)
                            b[w] = dOD[w] / (rho * ppfH[w]);
                    }
                    else
                    {
                        for (int w = 0; w < nWav; w++)
                            b[w] = dOD[w];
                    }

                    // conc = Einv (2 x nWav) * b (nWav)
                    double hbo = 0.0;
                    double hbr = 0.0;
                    for (int w = 0; w < nWav; w++)
                    {
                        hbo += Einv[0, w] * b[w];
                        hbr += Einv[1, w] * b[w];
                    }

                    HbO[tt, p] = hbo;
                    HbR[tt, p] = hbr;
                    HbT[tt, p] = hbo + hbr;
                }
            }

            return new ConcResult
            {
                Time = t,
                HbO = HbO,
                HbR = HbR,
                HbT = HbT,
                Pairs = pairs
            };
        }

        // ============================
        // Homer3 helpers (ppf / ml / pairing)
        // ============================

        private static double[] NormalizePpfLikeHomer3(double[] ppf, int nWav)
        {
            // Homer3:
            // if length(ppf) < nWav => warning + ppf = ones(1,nWav)
            // elseif length(ppf) > nWav => truncate
            // if ~isempty(find(ppf==1)) => ppf = ones(size(ppf))
            if (ppf == null || ppf.Length < nWav)
            {
                var ones = new double[nWav];
                for (int i = 0; i < nWav; i++) ones[i] = 1.0;
                return ones;
            }

            double[] p = ppf;
            if (p.Length > nWav)
            {
                var t = new double[nWav];
                Array.Copy(p, t, nWav);
                p = t;
            }

            // if any == 1 => all ones
            for (int i = 0; i < p.Length; i++)
            {
                if (p[i] == 1.0)
                {
                    var ones = new double[p.Length];
                    for (int k = 0; k < ones.Length; k++) ones[k] = 1.0;
                    return ones;
                }
            }

            return p;
        }

        private sealed class MlRow
        {
            public int Source;      // 1-based
            public int Detector;    // 1-based
            public int WlIndex;     // 1-based (ml(:,4))
            public int MeasCol;     // 0-based column index in y
        }

        private static List<MlRow> NormalizeMeasurementListIndices(List<MeasList> mlList)
        {
            // Robustly accept 0-based or 1-based in SNIRF exporters.
            // Homer3 treats ml as 1-based. We store as 1-based.
            int minSrc = mlList.Min(m => m.SourceIndex);
            int minDet = mlList.Min(m => m.DetectorIndex);
            int minWl = mlList.Min(m => m.WavelengthIndex);

            bool srcZero = (minSrc == 0);
            bool detZero = (minDet == 0);
            bool wlZero = (minWl == 0);

            int NormSrc(int s) => srcZero ? (s + 1) : s;
            int NormDet(int d) => detZero ? (d + 1) : d;
            int NormWl(int w) => wlZero ? (w + 1) : w;

            var ml = new List<MlRow>(mlList.Count);
            for (int i = 0; i < mlList.Count; i++)
            {
                var m = mlList[i];
                ml.Add(new MlRow
                {
                    Source = NormSrc(m.SourceIndex),
                    Detector = NormDet(m.DetectorIndex),
                    WlIndex = NormWl(m.WavelengthIndex),
                    MeasCol = i
                });
            }
            return ml;
        }

        private static List<PairInfo> BuildPairsStrictHomer3(List<MlRow> ml, Probe probe, int nWav)
        {
            // Homer3:
            // lst = find( ml(:,4)==1 );
            // for idx1 in lst:
            //   idx2 = find( ml(:,4)>1 & ml(:,1)==ml(idx1,1) & ml(:,2)==ml(idx1,2) );
            //
            // For strict multi-wavelength support, we build a full MeasIdxByWl[1..nWav]
            // using the first occurrence of each wlIndex for that (src,det), in wavelength order.
            var srcPos = probe.SourcePos3D;
            var detPos = probe.DetectorPos3D;

            // lst = all rows where wlIndex==1, in ml order
            var lst = ml.Where(r => r.WlIndex == 1).ToList();

            var pairs = new List<PairInfo>();
            int pIdx = 0;

            foreach (var r1 in lst)
            {
                int s = r1.Source;
                int d = r1.Detector;

                // For this (s,d), pick the first measurement per wavelength index 1..nWav
                int[] measIdx = new int[nWav];
                for (int w = 1; w <= nWav; w++)
                {
                    var found = ml.FirstOrDefault(r => r.Source == s && r.Detector == d && r.WlIndex == w);
                    if (found == null)
                        throw new Exception($"Missing wavelengthIndex={w} for Src={s}, Det={d} (strict Homer3 requires complete wavelengths per pair).");
                    measIdx[w - 1] = found.MeasCol;
                }

                double rhoMm = DistanceMm(srcPos, detPos, s, d);
                if (rhoMm <= 0 || double.IsNaN(rhoMm) || double.IsInfinity(rhoMm))
                    throw new Exception($"Invalid rho for Src={s}, Det={d}.");

                pairs.Add(new PairInfo
                {
                    PairIndex = pIdx++,
                    SourceIndex = s,
                    DetectorIndex = d,
                    MeasIdxByWl = measIdx,
                    RhoMm = rhoMm
                });
            }

            return pairs;
        }

        private static double DistanceMm(double[,] src, double[,] det, int s1, int d1)
        {
            if (src == null || det == null) return 0.0;

            int s = s1 - 1; // 1-based -> 0-based
            int d = d1 - 1;

            if (s < 0 || d < 0) return 0.0;
            if (src.GetLength(0) <= s || det.GetLength(0) <= d) return 0.0;

            int sDim = src.GetLength(1);
            int dDim = det.GetLength(1);
            if (sDim < 2 || dDim < 2) return 0.0;

            double dx = src[s, 0] - det[d, 0];
            double dy = src[s, 1] - det[d, 1];
            double dz = (sDim > 2 && dDim > 2) ? (src[s, 2] - det[d, 2]) : 0.0;

            // Homer3 uses norm(...) in the same units as SrcPos/DetPos (SNIRF uses mm).
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        // ============================
        // Linear algebra: einv = inv(E' E) E'
        // E is (nWav x 2) => einv is (2 x nWav)
        // ============================
        private static double[,] ComputePseudoInverse_2Chromophores(double[,] E)
        {
            int nWav = E.GetLength(0);
            int cols = E.GetLength(1);
            if (cols != 2) throw new Exception("E must be nWav x 2.");

            // Compute A = E' E (2x2)
            double a00 = 0, a01 = 0, a11 = 0;
            for (int i = 0; i < nWav; i++)
            {
                double e0 = E[i, 0];
                double e1 = E[i, 1];
                if (double.IsNaN(e0) || double.IsNaN(e1))
                    throw new Exception("Extinction coefficients contain NaN (wavelength out of supported range).");
                a00 += e0 * e0;
                a01 += e0 * e1;
                a11 += e1 * e1;
            }

            // Invert A
            double det = a00 * a11 - a01 * a01;
            if (Math.Abs(det) < 1e-18) throw new Exception("Singular (E' E) matrix.");

            double inv00 = a11 / det;
            double inv01 = -a01 / det;
            double inv10 = -a01 / det;
            double inv11 = a00 / det;

            // einv = inv(A) * E' => (2x2) * (2 x nWav) => (2 x nWav)
            var Einv = new double[2, nWav];
            for (int i = 0; i < nWav; i++)
            {
                double e0 = E[i, 0];
                double e1 = E[i, 1];

                // Column i of E' is [e0; e1]
                Einv[0, i] = inv00 * e0 + inv01 * e1;
                Einv[1, i] = inv10 * e0 + inv11 * e1;
            }

            return Einv;
        }

        // ============================
        // STRICT Homer3 GetExtinctions for HbO/Hb (WhichSpectrum=0 default)
        // ============================
        private static class Homer3Extinctions
        {
            /// <summary>
            /// Strict port of GetExtinctions.m for HbO/Hb only (case 0).
            /// Returns exs(:,1:2) = [HbO Hb] with the same "*2.303" scaling done in GetExtinctions.m.
            ///
            /// Out-of-range wavelengths return NaN (like MATLAB interp1 default).
            /// </summary>
            public static double[,] GetExtinctions_HbOHb(double[] lambda, int whichSpectrum = 0)
            {
                if (lambda == null) throw new ArgumentNullException(nameof(lambda));
                if (whichSpectrum != 0)
                    throw new NotSupportedException("Only WhichSpectrum=0 is implemented (Homer3 default).");

                int n = lambda.Length;
                var ex = new double[n, 2];

                // Default NaN (interp1 out-of-range)
                for (int i = 0; i < n; i++)
                {
                    ex[i, 0] = double.NaN;
                    ex[i, 1] = double.NaN;
                }

                // vLambdaHbOHb for case 0 (Wray et al. 1988) — copied from your pasted GetExtinctions.m (case 0)
                ReadOnlySpan<double> wl = new double[]
                {
                    650.0,652.0,654.0,656.0,658.0,660.0,662.0,664.0,666.0,668.0,
                    670.0,672.0,674.0,676.0,678.0,680.0,682.0,684.0,686.0,688.0,
                    690.0,692.0,694.0,696.0,698.0,700.0,702.0,704.0,706.0,708.0,
                    710.0,712.0,714.0,716.0,718.0,720.0,722.0,724.0,726.0,728.0,
                    730.0,732.0,734.0,736.0,738.0,740.0,742.0,744.0,746.0,748.0,
                    750.0,752.0,754.0,756.0,758.0,760.0,762.0,764.0,766.0,768.0,
                    770.0,772.0,774.0,776.0,778.0,780.0,782.0,784.0,786.0,788.0,
                    790.0,792.0,794.0,796.0,798.0,800.0,802.0,804.0,806.0,808.0,
                    810.0,812.0,814.0,816.0,818.0,820.0,822.0,824.0,826.0,828.0,
                    830.0,832.0,834.0,836.0,838.0,840.0,842.0,844.0,846.0,848.0,
                    850.0,852.0,854.0,856.0,858.0,860.0,862.0,864.0,866.0,868.0,
                    870.0,872.0,874.0,876.0,878.0,880.0,882.0,884.0,886.0,888.0,
                    890.0,892.0,894.0,896.0,898.0,900.0
                };

                ReadOnlySpan<double> hbO = new double[]
                {
                    506.0,488.0,474.0,464.0,454.3,445.0,438.3,433.8,431.3,429.0,
                    427.0,426.5,426.0,424.0,423.0,423.0,422.0,420.0,418.0,416.5,
                    415.5,415.0,415.0,415.5,416.0,419.3,422.5,425.5,429.7,435.0,
                    441.0,446.5,451.5,458.0,466.0,472.7,479.5,486.5,494.3,503.0,
                    510.0,517.0,521.0,530.7,546.0,553.5,561.0,571.0,581.3,592.0,
                    600.0,608.0,618.7,629.7,641.0,645.5,650.0,666.7,681.0,693.0,
                    701.5,710.0,722.0,733.7,745.0,754.0,763.0,775.0,787.0,799.0,
                    808.0,817.0,829.0,840.7,852.0,863.3,873.3,881.8,891.7,903.0,
                    914.3,924.7,934.0,943.0,952.0,962.7,973.0,983.0,990.5,998.0,
                    1008.0,1018.0,1028.0,1038.0,1047.7,1057.0,1063.5,1070.0,1079.3,1088.3,
                    1097.0,1105.7,1113.0,1119.0,1126.0,1134.0,1142.0,1149.7,1157.0,1163.7,
                    1170.3,1177.0,1182.0,1187.0,1193.0,1198.7,1204.0,1209.3,1214.3,1219.0,
                    1223.7,1227.5,1230.5,1234.0,1238.0,1241.3
                };

                ReadOnlySpan<double> hb = new double[]
                {
                    3743.0,3677.0,3612.0,3548.0,3491.3,3442.0,3364.7,3292.8,3226.3,3133.0,
                    3013.0,2946.0,2879.0,2821.7,2732.3,2610.8,2497.3,2392.0,2292.7,2209.3,
                    2141.8,2068.7,1990.0,1938.5,1887.0,1827.7,1778.5,1739.5,1695.7,1647.0,
                    1601.7,1562.5,1529.5,1492.0,1450.0,1411.3,1380.0,1356.0,1331.7,1307.0,
                    1296.5,1286.0,1286.0,1293.0,1307.0,1328.0,1349.0,1384.3,1431.3,1490.0,
                    1532.0,1574.0,1620.7,1655.3,1678.0,1669.0,1660.0,1613.3,1555.0,1485.0,
                    1425.0,1365.0,1288.3,1216.3,1149.0,1107.5,1066.0,1021.3,972.0,918.0,
                    913.0,908.0,887.3,868.7,852.0,838.7,828.0,820.0,812.0,804.0,
                    798.7,793.7,789.0,787.0,785.0,783.0,781.0,779.0,778.5,778.0,
                    778.0,777.7,777.0,777.0,777.0,777.0,777.5,778.0,779.3,780.3,
                    781.0,783.0,785.0,787.0,789.3,792.0,795.3,799.0,803.0,807.7,
                    812.3,817.0,820.5,824.0,830.0,835.7,841.0,847.0,852.7,858.0,
                    863.3,867.8,871.3,875.3,880.0,883.3
                };

                const double scale2303 = 2.303; // MATLAB: vLambdaHbOHb(:,2:3) *= 2.303

                for (int i = 0; i < n; i++)
                {
                    double xq = lambda[i];

                    // MATLAB case 0 only has 650..900; outside => NaN
                    if (xq < wl[0] || xq > wl[wl.Length - 1])
                        continue;

                    ex[i, 0] = LinearInterp(wl, hbO, xq) * scale2303;
                    ex[i, 1] = LinearInterp(wl, hb, xq) * scale2303;
                }

                return ex;
            }

            // Linear interpolation like MATLAB interp1(x, y, xq) for in-range xq.
            private static double LinearInterp(ReadOnlySpan<double> x, ReadOnlySpan<double> y, double xq)
            {
                int n = x.Length;

                if (xq == x[0]) return y[0];
                if (xq == x[n - 1]) return y[n - 1];

                int lo = 0, hi = n - 1;
                while (hi - lo > 1)
                {
                    int mid = lo + ((hi - lo) >> 1);
                    if (x[mid] <= xq) lo = mid;
                    else hi = mid;
                }

                double x0 = x[lo], x1 = x[hi];
                double y0 = y[lo], y1 = y[hi];
                double t = (xq - x0) / (x1 - x0);
                return y0 + t * (y1 - y0);
            }
        }
    }
}
