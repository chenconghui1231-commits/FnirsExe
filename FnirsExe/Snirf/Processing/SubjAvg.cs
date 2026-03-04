using System;
using System.Collections.Generic;

namespace FnirsExe.Snirf.Processing
{
    public static class SubjAvg
    {
        public static double[,,] Compute(List<RunAvg.RunResult> subjects)
        {
            if (subjects == null || subjects.Count == 0) return null;
            var tpl = subjects[0];
            int T = tpl.HbO_Avg.GetLength(0);
            int Ch = tpl.HbO_Avg.GetLength(1);
            int C = tpl.HbO_Avg.GetLength(2);
            int N = subjects.Count;

            var res = new double[T, Ch, C];
            for (int c = 0; c < C; c++)
            {
                for (int ch = 0; ch < Ch; ch++)
                {
                    for (int t = 0; t < T; t++)
                    {
                        double sum = 0;
                        foreach (var s in subjects) sum += s.HbO_Avg[t, ch, c];
                        res[t, ch, c] = sum / N;
                    }
                }
            }
            return res;
        }
    }
}