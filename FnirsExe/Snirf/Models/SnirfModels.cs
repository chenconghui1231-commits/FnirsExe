using System;
using System.Collections.Generic;

namespace FnirsExe.Snirf.Models
{
    /// <summary>
    /// Top-level SNIRF object (similar to Homer3 SnirfClass).
    /// Renamed to SnirfFile to avoid namespace/type collision with FnirsExe.Snirf.
    /// </summary>
    public sealed class SnirfFile
    {
        public List<NirsDataBlock> Data { get; private set; } = new List<NirsDataBlock>();

        // optional
        public Probe Probe { get; set; } = null;

        public List<Stim> Stim { get; private set; } = new List<Stim>();
        public List<Aux> Aux { get; private set; } = new List<Aux>();

        public MetaDataTags MetaDataTags { get; private set; } = new MetaDataTags();
    }

    public sealed class NirsDataBlock
    {
        public string Name { get; set; } = "";
        public double[] Time { get; set; } = Array.Empty<double>();

        /// <summary>
        /// SNIRF convention: dataTimeSeries is [timePoints x measurements]
        /// (rows = time, cols = measurementList entries).
        /// </summary>
        public double[,] DataTimeSeries { get; set; } = new double[0, 0];

        public List<MeasList> MeasurementList { get; private set; } = new List<MeasList>();
    }

    public sealed class MeasList
    {
        public int SourceIndex { get; set; }
        public int DetectorIndex { get; set; }
        public int WavelengthIndex { get; set; }
        public int DataType { get; set; }
        public string DataTypeLabel { get; set; } = "";
    }

    public sealed class Probe
    {
        // Common SNIRF fields (3D or 2D)
        public double[,] SourcePos3D { get; set; } = new double[0, 0];
        public double[,] DetectorPos3D { get; set; } = new double[0, 0];

        public double[,] SourcePos2D { get; set; } = new double[0, 0];
        public double[,] DetectorPos2D { get; set; } = new double[0, 0];

        public string[] SourceLabels { get; set; } = null;
        public string[] DetectorLabels { get; set; } = null;

        public double[] Wavelengths { get; set; } = Array.Empty<double>();

        // Optional fields seen in some vendors (e.g., NIRSport2/Brite)
        public double[] Frequencies { get; set; } = null;
        public double[] TimeDelays { get; set; } = null;
        public double[] TimeDelayWidths { get; set; } = null;
        public double[] CorrelationTimeDelays { get; set; } = null;
        public double[] CorrelationTimeDelayWidths { get; set; } = null;

        // optional landmarks
        public double[,] LandmarkPos3D { get; set; } = null;
        public string[] LandmarkLabels { get; set; } = null;
    }

    public sealed class Stim
    {
        public string Name { get; set; } = "";
        /// <summary>
        /// Usually Nx3: [onset, duration, amplitude]. Some files store 1x3 as a 1D vector.
        /// </summary>
        public double[,] Data { get; set; } = new double[0, 0];
    }

    public sealed class Aux
    {
        public string Name { get; set; } = "";
        public double[] Time { get; set; } = Array.Empty<double>();

        /// <summary>
        /// Some writers use aux/data, others use aux/dataTimeSeries.
        /// This is stored as 1D (flattened) for simplicity.
        /// </summary>
        public double[] Data { get; set; } = Array.Empty<double>();
    }

    public sealed class MetaDataTags
    {
        public Dictionary<string, string> Tags { get; private set; } = new Dictionary<string, string>();
    }
}
