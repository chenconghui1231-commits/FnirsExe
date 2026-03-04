using System;
using System.Collections.Generic;
using FnirsExe.Snirf.Hdf5; // 请确保您的 Hdf5Util 在此命名空间
using FnirsExe.Snirf.Models;

namespace FnirsExe.Snirf.IO
{
    public class SnirfLoader
    {
        public SnirfFile Load(string filePath)
        {
            var snirf = new SnirfFile
            {
                FilePath = filePath,
                FormatVersion = "1.0",
                MetaDataTags = new List<MetaDataTag>(),
                Data = new List<NirsDataBlock>(),
                Probe = new Probe()
            };

            long fileId = -1;
            try
            {
                fileId = Hdf5Util.OpenFile(filePath);
                if (fileId < 0) throw new Exception("Failed to open HDF5 file.");

                var ver = Hdf5Util.ReadStringAttribute(fileId, "/", "formatVersion");
                if (!string.IsNullOrEmpty(ver)) snirf.FormatVersion = ver;

                int groupIdx = 1;
                while (true)
                {
                    string groupName = $"/nirs/data{groupIdx}";
                    if (!Hdf5Util.GroupExists(fileId, groupName)) break;
                    var block = ReadDataBlock(fileId, groupName);
                    if (block != null) snirf.Data.Add(block);
                    groupIdx++;
                }

                ReadProbe(fileId, snirf);
            }
            finally { if (fileId >= 0) Hdf5Util.CloseFile(fileId); }
            return snirf;
        }

        private NirsDataBlock ReadDataBlock(long fileId, string groupPath)
        {
            var block = new NirsDataBlock();
            var d = Hdf5Util.ReadDataset<double>(fileId, $"{groupPath}/dataTimeSeries");
            if (d == null) return null;

            // 修复转置: 时间维度通常较长
            if (d.GetLength(0) < d.GetLength(1) && d.GetLength(1) > 100) d = Transpose(d);
            block.DataTimeSeries = d;

            var t = Hdf5Util.ReadDataset1D<double>(fileId, $"{groupPath}/time");
            if (t == null || t.Length != block.DataTimeSeries.GetLength(0))
            {
                t = new double[block.DataTimeSeries.GetLength(0)];
                for (int i = 0; i < t.Length; i++) t[i] = i * 0.1;
            }
            block.Time = t;

            block.MeasurementList = new List<MeasList>();
            int mlIdx = 1;
            while (true)
            {
                string mlGroup = $"{groupPath}/measurementList{mlIdx}";
                if (!Hdf5Util.GroupExists(fileId, mlGroup)) break;
                var ml = new MeasList();
                ml.SourceIndex = Hdf5Util.ReadScalar<int>(fileId, $"{mlGroup}/sourceIndex");
                ml.DetectorIndex = Hdf5Util.ReadScalar<int>(fileId, $"{mlGroup}/detectorIndex");
                ml.WavelengthIndex = Hdf5Util.ReadScalar<int>(fileId, $"{mlGroup}/wavelengthIndex");
                block.MeasurementList.Add(ml);
                mlIdx++;
            }

            block.Stim = new List<Stim>();
            int stimIdx = 1;
            while (true)
            {
                string stimGroup = $"{groupPath}/stim{stimIdx}";
                if (!Hdf5Util.GroupExists(fileId, stimGroup)) break;
                var s = new Stim();
                s.Name = Hdf5Util.ReadStringDataset(fileId, $"{stimGroup}/name");
                s.Data = Hdf5Util.ReadDataset<double>(fileId, $"{stimGroup}/data");
                if (s.Data != null && s.Data.GetLength(0) == 3 && s.Data.GetLength(1) > 3) s.Data = Transpose(s.Data);
                block.Stim.Add(s);
                stimIdx++;
            }
            return block;
        }

        private void ReadProbe(long fileId, SnirfFile snirf)
        {
            string group = "/nirs/probe";
            if (!Hdf5Util.GroupExists(fileId, group)) return;
            snirf.Probe.Wavelengths = Hdf5Util.ReadDataset1D<double>(fileId, $"{group}/wavelengths");

            var srcPos2D = Hdf5Util.ReadDataset<double>(fileId, $"{group}/sourcePos2D");
            var srcPos3D = Hdf5Util.ReadDataset<double>(fileId, $"{group}/sourcePos3D");
            var detPos2D = Hdf5Util.ReadDataset<double>(fileId, $"{group}/detectorPos2D");
            var detPos3D = Hdf5Util.ReadDataset<double>(fileId, $"{group}/detectorPos3D");

            srcPos2D = FixTranspose(srcPos2D, 2); srcPos3D = FixTranspose(srcPos3D, 3);
            detPos2D = FixTranspose(detPos2D, 2); detPos3D = FixTranspose(detPos3D, 3);

            if (srcPos3D == null && srcPos2D != null) srcPos3D = Convert2Dto3D(srcPos2D);
            if (detPos3D == null && detPos2D != null) detPos3D = Convert2Dto3D(detPos2D);

            snirf.Probe.SourcePos3D = srcPos3D; snirf.Probe.DetectorPos3D = detPos3D;
            snirf.Probe.SourcePos2D = srcPos2D; snirf.Probe.DetectorPos2D = detPos2D;
        }

        private double[,] FixTranspose(double[,] mat, int expectedCols)
        {
            if (mat == null) return null;
            if (mat.GetLength(1) != expectedCols && mat.GetLength(0) == expectedCols) return Transpose(mat);
            return mat;
        }
        private double[,] Convert2Dto3D(double[,] mat2D)
        {
            if (mat2D == null) return null;
            int r = mat2D.GetLength(0);
            var res = new double[r, 3];
            for (int i = 0; i < r; i++) { res[i, 0] = mat2D[i, 0]; res[i, 1] = mat2D[i, 1]; res[i, 2] = 0; }
            return res;
        }
        private double[,] Transpose(double[,] m)
        {
            if (m == null) return null;
            int r = m.GetLength(0), c = m.GetLength(1);
            var res = new double[c, r];
            for (int i = 0; i < r; i++) for (int j = 0; j < c; j++) res[j, i] = m[i, j];
            return res;
        }
    }
}