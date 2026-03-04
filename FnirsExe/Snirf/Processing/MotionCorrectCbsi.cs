using System;

namespace FnirsExe.Snirf.Processing
{
    public static class MotionCorrectCbsi
    {
        public static void ApplyInPlace(Od2Conc.ConcResult conc)
        {
            if (conc?.HbO == null) return;
            int T = conc.HbO.GetLength(0);
            int Ch = conc.HbO.GetLength(1);

            for (int ch = 0; ch < Ch; ch++)
            {
                double stdO = Std(conc.HbO, ch);
                double stdR = Std(conc.HbR, ch);
                if (stdR <= 1e-12) continue;

                double alpha = stdO / stdR;
                for (int t = 0; t < T; t++)
                {
                    double o = conc.HbO[t, ch];
                    double r = conc.HbR[t, ch];
                    double newO = 0.5 * (o - alpha * r);
                    double newR = 0.5 * (r - (1.0 / alpha) * o);
                    conc.HbO[t, ch] = newO;
                    conc.HbR[t, ch] = newR;
                    conc.HbT[t, ch] = newO + newR;
                }
            }
        }

        private static double Std(double[,] d, int c)
        {
            int T = d.GetLength(0);
            double mean = 0; int n = 0;
            for (int i = 0; i < T; i++)
            {
                if (!double.IsNaN(d[i, c])) { mean += d[i, c]; n++; }
            }
            if (n <= 1) return 0;
            mean /= n;
            double sumSq = 0;
            for (int i = 0; i < T; i++)
            {
                if (!double.IsNaN(d[i, c])) sumSq += Math.Pow(d[i, c] - mean, 2);
            }
            return Math.Sqrt(sumSq / (n - 1));
        }
    }
}