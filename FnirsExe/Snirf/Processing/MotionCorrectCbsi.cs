using System;

namespace FnirsExe.Snirf.Processing
{
    public static class MotionCorrectCbsi
    {
        public static void ApplyInPlace(Od2Conc.ConcResult conc)
        {
            if (conc == null) throw new ArgumentNullException(nameof(conc));
            if (conc.HbO == null || conc.HbR == null || conc.HbT == null) return;

            int T = conc.HbO.GetLength(0);
            int P = conc.HbO.GetLength(1);

            for (int p = 0; p < P; p++)
            {
                double stdO = Std(conc.HbO, p);
                double stdR = Std(conc.HbR, p);
                if (stdO <= 1e-12 || stdR <= 1e-12) continue;

                double k = stdO / stdR;

                for (int t = 0; t < T; t++)
                {
                    double o = conc.HbO[t, p];
                    double r = conc.HbR[t, p];
                    if (double.IsNaN(o) || double.IsNaN(r)) continue;

                    double o2 = (o - k * r) * 0.5;
                    double r2 = (-o / k + r) * 0.5;

                    conc.HbO[t, p] = o2;
                    conc.HbR[t, p] = r2;
                    conc.HbT[t, p] = o2 + r2;
                }
            }
        }

        private static double Std(double[,] x, int col)
        {
            int T = x.GetLength(0);
            double mean = 0; int n = 0;
            for (int t = 0; t < T; t++)
            {
                double v = x[t, col];
                if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                mean += v; n++;
            }
            if (n <= 1) return 0;
            mean /= n;

            double var = 0;
            for (int t = 0; t < T; t++)
            {
                double v = x[t, col];
                if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                double d = v - mean;
                var += d * d;
            }
            var /= (n - 1);
            return Math.Sqrt(var);
        }
    }
}
