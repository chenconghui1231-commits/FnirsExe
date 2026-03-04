using System;
using System.Collections.Generic;
using FnirsExe.Snirf.Models;

namespace FnirsExe.Snirf.Processing
{
    /// <summary>
    /// 对应 Homer3: hmrR_StimRejection
    /// 功能：根据运动伪影掩膜 (tInc) 剔除无效的 Stim 事件。
    /// 如果 Stim 发生的时间窗口内存在被标记为 false 的时间点，该 Stim 将被移除。
    /// </summary>
    public static class StimRejection
    {
        public class Options
        {
            /// <summary>
            /// 检查伪影的时间窗口 [tPre, tPost]。
            /// 例如 [-5.0, 10.0] 表示检查刺激发生前 5秒 到 后 10秒。
            /// </summary>
            public double[] TRange { get; set; } = { -5.0, 10.0 };
        }

        public static void Apply(SnirfFile snirf, List<bool[]> tIncList, Options opt)
        {
            if (snirf?.Data == null || tIncList == null) return;
            if (opt == null) opt = new Options();

            double tPre = opt.TRange.Length >= 1 ? opt.TRange[0] : -5.0;
            double tPost = opt.TRange.Length >= 2 ? opt.TRange[1] : 10.0;

            // 遍历每个数据块
            for (int iBlk = 0; iBlk < snirf.Data.Count; iBlk++)
            {
                if (iBlk >= tIncList.Count) break;

                var block = snirf.Data[iBlk];
                bool[] mask = tIncList[iBlk]; // true=保留, false=伪影
                if (mask == null || mask.Length == 0) continue;

                double[] t = block.Time;
                if (t == null || t.Length < 2) continue;

                double dt = t[1] - t[0];
                int nTime = t.Length;

                // 遍历该块下的所有 Stim 条件 (Condition)
                foreach (var stim in block.Stim)
                {
                    if (stim.Data == null) continue;

                    // Stim.Data 结构: [N, 3] -> [Onset, Duration, Amp]
                    // 我们将筛选出有效的行
                    var validRows = new List<double[]>();
                    int nEvents = stim.Data.GetLength(0);

                    for (int i = 0; i < nEvents; i++)
                    {
                        double onset = stim.Data[i, 0];

                        // 计算检查窗口的索引范围
                        int idxStart = (int)Math.Round((onset + tPre - t[0]) / dt);
                        int idxEnd = (int)Math.Round((onset + tPost - t[0]) / dt);

                        // 边界保护
                        if (idxStart < 0) idxStart = 0;
                        if (idxEnd >= nTime) idxEnd = nTime - 1;

                        bool isClean = true;
                        // 只要窗口内有任意一点是 false (伪影)，就剔除该 Stim
                        for (int k = idxStart; k <= idxEnd; k++)
                        {
                            if (!mask[k])
                            {
                                isClean = false;
                                break;
                            }
                        }

                        if (isClean)
                        {
                            // 保留有效 Stim
                            validRows.Add(new double[] { stim.Data[i, 0], stim.Data[i, 1], stim.Data[i, 2] });
                        }
                    }

                    // 重建 Stim.Data
                    if (validRows.Count > 0)
                    {
                        double[,] newData = new double[validRows.Count, 3];
                        for (int i = 0; i < validRows.Count; i++)
                        {
                            newData[i, 0] = validRows[i][0];
                            newData[i, 1] = validRows[i][1];
                            newData[i, 2] = validRows[i][2];
                        }
                        stim.Data = newData;
                    }
                    else
                    {
                        // 如果全部被剔除，设为空
                        stim.Data = new double[0, 3];
                    }
                }
            }
        }
    }
}