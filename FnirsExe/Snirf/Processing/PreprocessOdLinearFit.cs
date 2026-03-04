using System;
using MathNet.Numerics; // 必须引用
using FnirsExe.Snirf.Models;

namespace FnirsExe.Snirf.Processing
{
    /// <summary>
    /// 对应 Homer3: hmrR_PreprocessOD_LinearFit
    /// 功能：去除 OD 数据的线性趋势 (Detrend)。
    /// 使用 MathNet.Numerics.Fit.Line 进行最小二乘拟合。
    /// </summary>
    public static class PreprocessOdLinearFit
    {
        public static void Apply(SnirfFile snirf)
        {
            if (snirf?.Data == null) return;

            foreach (var block in snirf.Data)
            {
                double[,] d = block.DataTimeSeries;
                double[] t = block.Time;
                if (d == null || t == null) continue;

                int T = d.GetLength(0);
                int Ch = d.GetLength(1);
                if (T < 2) continue;

                // 对每个通道分别处理
                for (int c = 0; c < Ch; c++)
                {
                    // 1. 提取单列数据
                    double[] y = new double[T];
                    bool hasNaN = false;
                    for (int i = 0; i < T; i++)
                    {
                        y[i] = d[i, c];
                        if (double.IsNaN(y[i])) hasNaN = true;
                    }

                    if (hasNaN) continue; // MathNet Fit 不支持 NaN，跳过坏通道

                    // 2. 线性拟合: y = a + b*x
                    // MathNet 返回 Tuple (intercept, slope) -> (截距 a, 斜率 b)
                    var p = Fit.Line(t, y);
                    double intercept = p.Item1;
                    double slope = p.Item2;

                    // 3. 减去趋势
                    // 注意：Homer3 的 polyfit(n=1) 也会减去截距（即去除了直流分量 DC）
                    for (int i = 0; i < T; i++)
                    {
                        double trend = intercept + slope * t[i];
                        d[i, c] -= trend;
                    }
                }
            }
        }
    }
}