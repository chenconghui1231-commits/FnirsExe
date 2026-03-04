using System;
using System.Collections.Generic;
using FnirsExe.Snirf.Models;

namespace FnirsExe.Snirf.Processing
{
    public static class MotionArtifactByChannel
    {
        public class Options
        {
            public double TMotion { get; set; } = 0.5;
            public double TMask { get; set; } = 1.0;
            public double StdThresh { get; set; } = 50.0;
            public double AmpThresh { get; set; } = 5.0;
        }

        public sealed class Result
        {
            public bool[] TInc;      // [Time]
            public bool[,] TIncCh;   // [Time, Ch]
            public double Fs;
        }

        public static Result Compute(
            SnirfFile snirf,
            int blockIndex,
            bool[] mlActManActive,
            bool[] mlActAutoActive,
            bool[] tIncMan,
            Options opt)
        {
            if (opt == null) opt = new Options();
            return ComputeCore(snirf, blockIndex, mlActAutoActive, tIncMan,
                opt.TMotion, opt.TMask, opt.StdThresh, opt.AmpThresh);
        }

        private static Result ComputeCore(
            SnirfFile snirf,
            int blockIndex,
            bool[] mlActAutoActive,
            bool[] tIncMan,
            double tMotion,
            double tMask,
            double stdThresh,
            double ampThresh)
        {
            if (snirf == null || snirf.Data.Count <= blockIndex) return null;
            var block = snirf.Data[blockIndex];

            double[,] d = block.DataTimeSeries;
            int T = d.GetLength(0);
            int M = d.GetLength(1);

            double dt = block.Time[1] - block.Time[0];
            double fs = 1.0 / dt;

            if (tIncMan == null || tIncMan.Length != T)
            {
                tIncMan = new bool[T];
                for (int i = 0; i < T; i++) tIncMan[i] = true;
            }

            bool[] tInc = new bool[T];
            for (int i = 0; i < T; i++) tInc[i] = true;

            bool[,] tIncCh = new bool[T, M];
            for (int i = 0; i < T; i++)
                for (int j = 0; j < M; j++)
                    tIncCh[i, j] = true;

            int artBuffer = (int)Math.Round(tMask * fs);
            int maxDelay = (int)Math.Round(tMotion * fs);
            if (maxDelay < 1) maxDelay = 1;

            // std of diff
            double[] stdDiff = new double[M];
            for (int i = 0; i < M; i++)
            {
                if (mlActAutoActive != null && i < mlActAutoActive.Length && !mlActAutoActive[i])
                {
                    stdDiff[i] = 0;
                    continue;
                }

                double sum = 0, sumSq = 0;
                int n = 0;
                for (int t = 1; t < T; t++)
                {
                    double v = d[t, i] - d[t - 1, i];
                    sum += v; sumSq += v * v; n++;
                }
                double mean = sum / n;
                stdDiff[i] = Math.Sqrt((sumSq - n * mean * mean) / (n - 1));
            }

            // scan
            for (int i = 0; i < M; i++)
            {
                if (mlActAutoActive != null && i < mlActAutoActive.Length && !mlActAutoActive[i])
                    continue;

                double thresh = stdDiff[i] * stdThresh;
                List<int> bad = new List<int>();

                for (int t = 0; t < T - maxDelay; t++)
                {
                    double maxVal = 0;
                    for (int k = 1; k <= maxDelay; k++)
                    {
                        double diff = Math.Abs(d[t + k, i] - d[t, i]);
                        if (diff > maxVal) maxVal = diff;
                    }
                    if (maxVal > thresh || maxVal > ampThresh)
                        bad.Add(t);
                }

                foreach (int badIdx in bad)
                {
                    int start = badIdx - artBuffer;
                    int end = badIdx + artBuffer;
                    for (int k = start; k <= end; k++)
                    {
                        if (k < 0 || k >= T) continue;
                        if (!tIncMan[k]) continue;
                        tInc[k] = false;
                        tIncCh[k, i] = false;
                    }
                }
            }

            return new Result { TInc = tInc, TIncCh = tIncCh, Fs = fs };
        }
    }
}
