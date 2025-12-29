using System;
using FnirsExe.Snirf.Models;

namespace FnirsExe.Snirf.Processing
{
    /// <summary>
    /// Strict Homer3/MATLAB-like hmrR_Intensity2OD:
    /// d  = intensity.GetDataTimeSeries()
    /// dm = mean(abs(d), 1)           // MATLAB mean behavior: NaN propagates
    /// dod = -log( abs(d) ./ dm )     // allow log(0) -> -Inf => dod -> +Inf
    /// 
    /// Notes:
    /// - No clamping for 0.
    /// - No "skip NaN/Inf"; NaN/Inf propagate naturally as in MATLAB.
    /// - In-place overwrite of block.DataTimeSeries.
    /// </summary>
    public static class IntensityToOd_Homer3_Strict
    {
        public sealed class Options
        {
            /// <summary>
            /// If true, will also set MeasList.DataType / DataTypeLabel if present.
            /// (Homer3 SetDataTypeDod intent)
            /// </summary>
            public bool UpdateMeasListDataType { get; set; } = true;
        }

        public static void ApplyInPlace(SnirfFile snirf, Options opt = null)
        {
            if (snirf == null) throw new ArgumentNullException(nameof(snirf));
            if (opt == null) opt = new Options(); // C# 7.3

            if (snirf.Data == null || snirf.Data.Count == 0)
                throw new InvalidOperationException("No data blocks found in SNIRF.");

            for (int b = 0; b < snirf.Data.Count; b++)
                ApplyInPlace(snirf.Data[b], opt);
        }

        public static void ApplyInPlace(NirsDataBlock block, Options opt = null)
        {
            if (block == null) throw new ArgumentNullException(nameof(block));
            if (opt == null) opt = new Options(); // C# 7.3

            double[,] d = block.DataTimeSeries;
            if (d == null) throw new InvalidOperationException("DataTimeSeries is null.");

            int nTpts = d.GetLength(0);
            int nCh = d.GetLength(1);
            if (nTpts == 0 || nCh == 0) throw new InvalidOperationException("Empty DataTimeSeries.");

            // dm = mean(abs(d), 1) using MATLAB-like propagation:
            // If any NaN is present in a channel column -> dm becomes NaN.
            // Inf participates: mean can become Inf if any Inf present and no NaN.
            double[] dm = new double[nCh];

            for (int ch = 0; ch < nCh; ch++)
            {
                double sum = 0.0;

                for (int t = 0; t < nTpts; t++)
                {
                    double v = Math.Abs(d[t, ch]); // abs() exactly like MATLAB

                    // MATLAB mean: if any element is NaN, sum becomes NaN and stays NaN.
                    // If element is +Inf, sum becomes +Inf unless later NaN occurs.
                    sum += v;
                }

                // MATLAB mean divides by nTpts (not "count of finite")
                dm[ch] = sum / nTpts;
            }

            // dod = -log( abs(d) / dm ) in-place
            for (int ch = 0; ch < nCh; ch++)
            {
                double baseVal = dm[ch];

                for (int t = 0; t < nTpts; t++)
                {
                    double a = Math.Abs(d[t, ch]);   // abs(d)
                    double ratio = a / baseVal;      // abs(d) ./ dm (broadcast)
                    d[t, ch] = -Math.Log(ratio);     // allow NaN/Inf naturally; allow log(0) -> -Inf => +Inf
                }
            }

            // Update measList data type (align with Homer3 SetDataTypeDod intent)
            if (opt.UpdateMeasListDataType && block.MeasurementList != null && block.MeasurementList.Count > 0)
            {
                foreach (var ml in block.MeasurementList)
                {
                    ml.DataType = 1;
                    ml.DataTypeLabel = "OD";
                }
            }
        }
    }
}
