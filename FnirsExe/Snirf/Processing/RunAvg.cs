using System;
using System.Collections.Generic;

namespace FnirsExe.Snirf.Processing
{
    public static class RunAvg
    {
        public class RunInput
        {
            public double[,,] HbO; public int[] NTrials;
        }
        public class RunResult
        {
            public double[,,] HbO_Avg; public int[] NTrialsTotal;
        }

        public static RunResult ComputeWeightedAverage(List<RunInput> runs)
        {
            if (runs == null || runs.Count == 0) return null;
            var tpl = runs[0];
            int T = tpl.HbO.GetLength(0);
            int Ch = tpl.HbO.GetLength(1);
            int C = tpl.HbO.GetLength(2);

            var res = new RunResult
            {
                HbO_Avg = new double[T, Ch, C],
                NTrialsTotal = new int[C]
            };

            for (int c = 0; c < C; c++)
            {
                int sumN = 0;
                foreach (var r in runs) if (c < r.NTrials.Length) sumN += r.NTrials[c];
                res.NTrialsTotal[c] = sumN;

                if (sumN == 0) continue;
                for (int ch = 0; ch < Ch; ch++)
                {
                    for (int t = 0; t < T; t++)
                    {
                        double sum = 0;
                        foreach (var r in runs)
                        {
                            int n = (c < r.NTrials.Length) ? r.NTrials[c] : 0;
                            sum += r.HbO[t, ch, c] * n;
                        }
                        res.HbO_Avg[t, ch, c] = sum / sumN;
                    }
                }
            }
            return res;
        }
    }
}