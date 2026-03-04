using System;

namespace FnirsExe.Snirf.Processing
{
    public static class Statistics
    {
        public static double TTest(double mean, double std, int n)
        {
            if (std == 0) return 0;
            return mean / (std / Math.Sqrt(n));
        }
    }
}