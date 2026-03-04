using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics.LinearAlgebra; // 核心数学库
using MathNet.Numerics.LinearAlgebra.Double;
using FnirsExe.Snirf.Models;

namespace FnirsExe.Snirf.Processing
{
    /// <summary>
    /// 对应 Homer3: hmrR_tCCA (简化版)
    /// 功能：利用短距离通道 (Short Separation) 生成 nuisance regressors。
    /// 核心算法：使用 SVD 求解 CCA 问题。
    /// </summary>
    public static class GlmTcca
    {
        public class Options
        {
            /// <summary>短距离通道的阈值 (mm)。Homer3 默认为 10.0 或 15.0</summary>
            public double RhoShortMax { get; set; } = 15.0;

            /// <summary>保留的 CCA 分量个数</summary>
            public int NumComponents { get; set; } = 1;
        }

        /// <summary>
        /// 计算 tCCA 回归器。
        /// 返回: 一个矩阵 [Time, NumComponents]，可以直接作为 GLM 的外部回归量。
        /// </summary>
        public static double[,] GetTccaRegressors(SnirfFile snirf, Options opt)
        {
            if (snirf?.Data == null || snirf.Data.Count == 0) return null;
            if (opt == null) opt = new Options();

            var block = snirf.Data[0]; // 假设处理第一个块
            var ml = block.MeasurementList;
            var d = block.DataTimeSeries;
            var probe = snirf.Probe;

            if (d == null || ml == null || probe == null) return null;

            int T = d.GetLength(0);
            int Ch = d.GetLength(1);

            // 1. 分离长距离 (LS) 和 短距离 (SS) 通道索引
            var idxLong = new List<int>();
            var idxShort = new List<int>();

            for (int i = 0; i < Ch; i++)
            {
                if (i >= ml.Count) break;
                double dist = GetDistance(probe, ml[i].SourceIndex, ml[i].DetectorIndex);

                // 距离 > 0 且 <= 阈值 视为短距离
                if (dist > 0 && dist <= opt.RhoShortMax)
                    idxShort.Add(i);
                else
                    idxLong.Add(i);
            }

            if (idxShort.Count == 0 || idxLong.Count == 0)
            {
                // 没有短距离通道，无法做 tCCA
                return null;
            }

            // 2. 构建矩阵 (使用 MathNet)
            // Y: Long Channels (Target)
            // X: Short Channels (Reference)
            var M_Y = Matrix<double>.Build.Dense(T, idxLong.Count);
            var M_X = Matrix<double>.Build.Dense(T, idxShort.Count);

            // 填充并中心化 (Demean)
            for (int c = 0; c < idxLong.Count; c++)
            {
                double[] col = GetCol(d, idxLong[c]);
                Demean(col);
                M_Y.SetColumn(c, col);
            }
            for (int c = 0; c < idxShort.Count; c++)
            {
                double[] col = GetCol(d, idxShort[c]);
                Demean(col);
                M_X.SetColumn(c, col);
            }

            // 3. 执行 CCA (通过 SVD 方法)
            // 目标: 找到 X 的线性组合，使其与 Y 最相关。
            // 简化算法: 对 X 进行 PCA/SVD，取主要成分作为回归器。
            // 完整 CCA: 需要计算 Cxx^-1 * Cxy ... 
            // 这里我们实现一个鲁棒的 PCA 回归器生成 (近似 tCCA 的第一步)，
            // 因为完整的 CCA 在高维数据下容易过拟合。

            // 使用 MathNet 的 SVD
            var svd = M_X.Svd(true); // Compute U and VT

            // 取前 K 个特征向量 (U 矩阵的前 K 列)
            int K = Math.Min(opt.NumComponents, idxShort.Count);
            var U = svd.U.SubMatrix(0, T, 0, K);

            // 4. 转换回 double[,]
            double[,] regressors = new double[T, K];
            for (int t = 0; t < T; t++)
            {
                for (int k = 0; k < K; k++)
                {
                    regressors[t, k] = U[t, k];
                }
            }

            return regressors;
        }

        // --- Helpers ---

        private static double[] GetCol(double[,] d, int colIdx)
        {
            int R = d.GetLength(0);
            double[] res = new double[R];
            for (int i = 0; i < R; i++) res[i] = d[i, colIdx];
            return res;
        }

        private static void Demean(double[] v)
        {
            double sum = 0;
            foreach (var d in v) sum += d;
            double mean = sum / v.Length;
            for (int i = 0; i < v.Length; i++) v[i] -= mean;
        }

        private static double GetDistance(Probe p, int s, int d)
        {
            if (p?.SourcePos3D == null || p?.DetectorPos3D == null) return 999.0;
            // 1-based index
            int si = s - 1; int di = d - 1;
            if (si < 0 || si >= p.SourcePos3D.GetLength(0)) return 999.0;
            if (di < 0 || di >= p.DetectorPos3D.GetLength(0)) return 999.0;

            double dx = p.SourcePos3D[si, 0] - p.DetectorPos3D[di, 0];
            double dy = p.SourcePos3D[si, 1] - p.DetectorPos3D[di, 1];
            double dz = p.SourcePos3D[si, 2] - p.DetectorPos3D[di, 2];
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }
}