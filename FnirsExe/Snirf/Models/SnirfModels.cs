using System.Collections.Generic;

namespace FnirsExe.Snirf.Models
{
    public class SnirfFile
    {
        public string FilePath { get; set; }
        public string FormatVersion { get; set; }
        public List<MetaDataTag> MetaDataTags { get; set; } = new List<MetaDataTag>();
        public List<NirsDataBlock> Data { get; set; } = new List<NirsDataBlock>();
        public Probe Probe { get; set; } = new Probe();
    }

    public class MetaDataTag
    {
        public string Key { get; set; }
        public string Value { get; set; }
    }

    public class NirsDataBlock
    {
        public double[,] DataTimeSeries { get; set; } // [Time, Channels]
        public double[] Time { get; set; }
        public List<MeasList> MeasurementList { get; set; } = new List<MeasList>();
        public List<Stim> Stim { get; set; } = new List<Stim>();
    }

    public class MeasList
    {
        public int SourceIndex { get; set; }     // 1-based
        public int DetectorIndex { get; set; }   // 1-based
        public int WavelengthIndex { get; set; } // 1-based usually
        public string DataTypeLabel { get; set; }
        public int DataType { get; set; }
    }

    public class Stim
    {
        public string Name { get; set; }
        public double[,] Data { get; set; } // [N, 3] -> [Onset, Duration, Amp]
    }

    public class Probe
    {
        public double[] Wavelengths { get; set; }
        public double[,] SourcePos3D { get; set; }
        public double[,] DetectorPos3D { get; set; }
        public double[,] SourcePos2D { get; set; }
        public double[,] DetectorPos2D { get; set; }
    }
}