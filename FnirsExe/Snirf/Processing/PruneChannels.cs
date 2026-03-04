using System;
using System.Collections.Generic;
using FnirsExe.Snirf.Models;

namespace FnirsExe.Snirf.Processing
{
    public static class PruneChannels
    {
        public class Options
        {
            /// <summary>光强范围 [min, max] </summary>
            public double[] DRange { get; set; } = { 0, 100 };
            /// <summary>信噪比阈值</summary>
            public double SnrThresh { get; set; } = 0.8;
            /// <summary>源-探测器距离范围 [min, max] (mm)</summary>
            public double[] SdRange { get; set; } = { 0.0, 45.0 };
        }

        /// <summary>
        /// 计算活跃通道掩码 (true=保留, false=剔除)
        /// </summary>
        public static bool[] ComputeActiveChannels(SnirfFile snirf, int blkIdx, bool[] tInc, Options opt)
        {
            if (snirf?.Data == null || blkIdx >= snirf.Data.Count) return null;
            if (opt == null) opt = new Options();

            var block = snirf.Data[blkIdx];
            double[,] d = block.DataTimeSeries;
            if (d == null) return null;

            int T = d.GetLength(0);
            int Ch = d.GetLength(1);
            bool[] act = new bool[Ch];

            // 获取 Probe 信息用于 SD Range 检查
            var probe = snirf.Probe;
            bool hasProbe = (probe != null && probe.SourcePos3D != null && probe.DetectorPos3D != null);

            for (int i = 0; i < Ch; i++)
            {
                double sum = 0, sumSq = 0;
                int n = 0;

                // 1. 计算均值和标准差 (仅使用 tInc 包含的时间点)
                for (int t = 0; t < T; t++)
                {
                    if (tInc == null || (t < tInc.Length && tInc[t]))
                    {
                        double val = d[t, i];
                        // 注意：这里允许 NaN 进入累加，最后结果也会是 NaN
                        sum += val;
                        sumSq += val * val;
                        n++;
                    }
                }

                double mean = 0, std = 0;
                if (n > 1)
                {
                    mean = sum / n;
                    // 样本标准差
                    double var = (sumSq - n * mean * mean) / (n - 1);
                    std = Math.Sqrt(var);
                }

                // 2. 判定逻辑 (Homer3 风格)
                bool good = true;

                // (A) 检查数值异常 (NaN / Infinity)
                // 如果数据发散，mean/std 会变成 NaN/Inf，直接剔除
                if (double.IsNaN(mean) || double.IsInfinity(mean) ||
                    double.IsNaN(std) || double.IsInfinity(std))
                {
                    good = false;
                }

                // (B) 检查 dRange (光强范围)
                if (good)
                {
                    if (mean < opt.DRange[0] || mean > opt.DRange[1]) good = false;
                }

                // (C) 检查 SNR (均值/标准差)
                if (good)
                {
                    // 如果 std 为 0 (死线)，SNR 为 Inf，Homer3 通常保留，但这里防止除0异常
                    if (std == 0)
                    {
                        // 如果均值正常且 std=0，通常认为是人为填充数据，视为好或坏取决于策略。
                        // 这里简单处理：如果 std=0，视为 SNR 无穷大，通过。
                    }
                    else
                    {
                        double snr = mean / std;
                        if (snr < opt.SnrThresh) good = false;
                    }
                }

                // (D) 检查 SD Range (距离)
                if (good && hasProbe && block.MeasurementList != null && i < block.MeasurementList.Count)
                {
                    var ml = block.MeasurementList[i];
                    double dist = GetDistance(probe, ml.SourceIndex, ml.DetectorIndex);
                    // 如果距离无效(0)或超出范围，剔除
                    if (dist < opt.SdRange[0] || dist > opt.SdRange[1]) good = false;
                }

                act[i] = good;
            }
            return act;
        }

        private static double GetDistance(Probe p, int s, int d)
        {
            // 索引转 0-based
            int si = s - 1; int di = d - 1;
            if (si < 0 || di < 0 || si >= p.SourcePos3D.GetLength(0) || di >= p.DetectorPos3D.GetLength(0)) return 0;

            double dx = p.SourcePos3D[si, 0] - p.DetectorPos3D[di, 0];
            double dy = p.SourcePos3D[si, 1] - p.DetectorPos3D[di, 1];
            double dz = p.SourcePos3D[si, 2] - p.DetectorPos3D[di, 2];
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}