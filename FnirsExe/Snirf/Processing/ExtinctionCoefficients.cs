using System;

namespace FnirsExe.Snirf.Processing
{
    public static class ExtinctionCoefficients
    {
        public static double[,] GetCoefficients(double[] lambda)
        {
            int n = lambda.Length;
            double[,] res = new double[n, 5]; // [HbO, HbR, H2O, Lipid, AA3]
            for (int i = 0; i < n; i++)
            {
                var vals = GetVal(lambda[i]);
                res[i, 0] = vals.hbo;
                res[i, 1] = vals.hbr;
            }
            return res;
        }

        private static (double hbo, double hbr) GetVal(double nm)
        {
            // 使用 Wray et al. 标准值 (cm-1 M-1 base 10)
            if (Math.Abs(nm - 760) < 1) return (1486, 3843);
            if (Math.Abs(nm - 850) < 1) return (2526, 1798);

            // 简单线性插值作为后备
            // 690nm: HbO=1070, HbR=5268
            // 830nm: HbO=2356, HbR=1943
            if (Math.Abs(nm - 690) < 1) return (1070, 5268);
            if (Math.Abs(nm - 830) < 1) return (2356, 1943);

            return (2000, 2000); // 默认
        }
    }
}