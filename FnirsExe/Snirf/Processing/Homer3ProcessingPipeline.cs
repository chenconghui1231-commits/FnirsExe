using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using FnirsExe.Snirf.Models;

namespace FnirsExe.Snirf.Processing
{
    /// <summary>
    /// Run SNIRF data through the same sequence Homer3 uses:
    /// Intensity -> OD -> Prune -> Motion detection -> Wavelet correction -> Bandpass -> OD2Conc.
    /// </summary>
    public static class Homer3ProcessingPipeline
    {
        public sealed class Options
        {
            public int DataBlockIndex { get; set; } = 0;
            public double[] PartialPathLengthFactors { get; set; } = new double[] { 6.0, 6.0 };
            public double HighpassHz { get; set; } = 0.01;
            public double LowpassHz { get; set; } = 0.5;
            public double WaveletIqr { get; set; } = 1.5;
            public bool EnableWavelet { get; set; } = true;
            public double TMotion { get; set; } = 0.5;
            public double TMask { get; set; } = 1.0;
            public double StdThresh { get; set; } = 50.0;
            public double AmpThresh { get; set; } = 5.0;
            public int MaxChannelsToExport { get; set; } = 8;
            public PruneChannels_Homer3_StrictOrder.Options PruneOptions { get; set; } = new PruneChannels_Homer3_StrictOrder.Options();
        }

        public sealed class Result
        {
            public SnirfFile ProcessedSnirf { get; set; }
            public bool[] MlActAuto { get; set; }
            public MotionArtifactByChannel_Homer3Strict.Result MotionArtifacts { get; set; }
            public Od2Conc.ConcResult Concentration { get; set; }
            public double SamplingRateHz { get; set; }
            public List<string> Warnings { get; } = new List<string>();
        }

        public static Result Run(SnirfFile snirf, Options options = null)
        {
            if (snirf == null) throw new ArgumentNullException(nameof(snirf));
            options ??= new Options();

            if (snirf.Data == null || snirf.Data.Count == 0)
                throw new InvalidOperationException("SNIRF 文件中缺少 data 节点。");

            if (options.DataBlockIndex < 0 || options.DataBlockIndex >= snirf.Data.Count)
                throw new ArgumentOutOfRangeException(nameof(options.DataBlockIndex), "指定的数据块索引不存在。");

            var result = new Result
            {
                ProcessedSnirf = snirf
            };

            var block = snirf.Data[options.DataBlockIndex];

            // 1) intensity -> OD
            IntensityToOd_Homer3_Strict.ApplyInPlace(snirf);

            // 2) prune channels (Homer3 strict order)
            try
            {
                result.MlActAuto = PruneChannels_Homer3_StrictOrder.ComputeMlActAuto(
                    snirf,
                    options.DataBlockIndex,
                    null,
                    null,
                    options.PruneOptions);
            }
            catch (Exception ex)
            {
                result.Warnings.Add($"跳过 PruneChannels：{ex.Message}");
                result.MlActAuto = BuildAllTrue(block.DataTimeSeries.GetLength(1));
            }

            // 3) motion artifact detection
            bool[] mlActAuto = result.MlActAuto ?? BuildAllTrue(block.DataTimeSeries.GetLength(1));
            result.MotionArtifacts = MotionArtifactByChannel_Homer3Strict.Compute(
                snirf,
                options.DataBlockIndex,
                null,
                mlActAuto,
                null,
                options.TMotion,
                options.TMask,
                options.StdThresh,
                options.AmpThresh);

            // 4) motion correction (wavelet)
            int[] mlActAutoInt = ToIntMask(mlActAuto);
            MotionCorrectWaveletHomer3.ApplyInPlace(
                block.DataTimeSeries,
                null,
                mlActAutoInt,
                options.WaveletIqr,
                options.EnableWavelet ? 1 : 0);

            // 5) bandpass filter
            result.SamplingRateHz = ComputeSamplingRate(block.Time);
            if (options.HighpassHz > 0 || options.LowpassHz > 0)
            {
                if (result.SamplingRateHz <= 0 || double.IsNaN(result.SamplingRateHz) || double.IsInfinity(result.SamplingRateHz))
                {
                    result.Warnings.Add("无法计算采样率，跳过带通滤波。");
                }
                else if (HasNaNOrInf(block.DataTimeSeries))
                {
                    result.Warnings.Add("数据存在 NaN/Inf，跳过带通滤波以保持 Homer3 行为。");
                }
                else
                {
                    BandpassFilt.ApplyInPlace(block.DataTimeSeries, result.SamplingRateHz, options.HighpassHz, options.LowpassHz);
                }
            }

            // 6) OD -> HbO/HbR/HbT
            result.Concentration = Od2Conc.ComputeStrictHomer3(
                snirf,
                options.DataBlockIndex,
                options.PartialPathLengthFactors);

            return result;
        }

        public static string BuildHemoglobinCsv(Result result, int maxChannels = int.MaxValue)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.Concentration == null) throw new InvalidOperationException("尚未计算血红蛋白浓度。");

            var conc = result.Concentration;
            var motion = result.MotionArtifacts;
            int P = conc.Pairs.Count;
            int exportCount = Math.Min(P, maxChannels);
            int T = conc.Time.Length;

            var sb = new StringBuilder();
            sb.AppendLine("Time(sec),Channel,HbO,HbR,HbT");

            for (int t = 0; t < T; t++)
            {
                bool timeIncluded = motion?.TInc != null ? motion.TInc[t] : true;

                for (int p = 0; p < exportCount; p++)
                {
                    bool include = timeIncluded;
                    if (motion?.TIncCh != null)
                    {
                        foreach (int midx in conc.Pairs[p].MeasIdxByWl)
                        {
                            if (!motion.TIncCh[t, midx])
                            {
                                include = false;
                                break;
                            }
                        }
                    }

                    double hbo = conc.HbO[t, p];
                    double hbr = conc.HbR[t, p];
                    double hbt = conc.HbT[t, p];

                    if (!include)
                    {
                        hbo = double.NaN;
                        hbr = double.NaN;
                        hbt = double.NaN;
                    }

                    sb.Append(conc.Time[t].ToString("G17", CultureInfo.InvariantCulture));
                    sb.Append(',');
                    sb.Append(p + 1);
                    sb.Append(',');
                    sb.Append(hbo.ToString("G17", CultureInfo.InvariantCulture));
                    sb.Append(',');
                    sb.Append(hbr.ToString("G17", CultureInfo.InvariantCulture));
                    sb.Append(',');
                    sb.Append(hbt.ToString("G17", CultureInfo.InvariantCulture));
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        private static bool[] BuildAllTrue(int length)
        {
            var mask = new bool[length];
            for (int i = 0; i < length; i++) mask[i] = true;
            return mask;
        }

        private static int[] ToIntMask(bool[] mask)
        {
            if (mask == null) return null;
            int[] res = new int[mask.Length];
            for (int i = 0; i < mask.Length; i++)
                res[i] = mask[i] ? 1 : 0;
            return res;
        }

        private static double ComputeSamplingRate(double[] time)
        {
            if (time == null || time.Length < 2) return double.NaN;
            double dt = time[1] - time[0];
            if (dt == 0) return double.NaN;
            return 1.0 / dt;
        }

        private static bool HasNaNOrInf(double[,] data)
        {
            int r = data.GetLength(0);
            int c = data.GetLength(1);
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                {
                    double v = data[i, j];
                    if (double.IsNaN(v) || double.IsInfinity(v))
                        return true;
                }
            return false;
        }
    }
}
