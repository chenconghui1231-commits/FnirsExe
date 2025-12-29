using System;
using System.Collections.Generic;
using System.Linq;
using HDF.PInvoke;
using FnirsExe.Snirf.Hdf5;
using FnirsExe.Snirf.Utils;

namespace FnirsExe.Snirf.IO
{
    public static class SnirfLoaderApi
    {
        public static global::FnirsExe.Snirf.Models.SnirfFile Load(string snirfPath)
        {
            if (string.IsNullOrWhiteSpace(snirfPath))
                throw new ArgumentNullException(nameof(snirfPath));

            long fileId = H5F.open(snirfPath, H5F.ACC_RDONLY);
            if (fileId < 0) throw new Exception("Failed to open SNIRF file.");

            try
            {
                var snirf = new global::FnirsExe.Snirf.Models.SnirfFile();

                long nirsGroup = H5G.open(fileId, "/nirs");
                if (nirsGroup < 0) throw new Exception("Missing group /nirs");

                try
                {
                    // ---------- data blocks ----------
                    foreach (var child in Hdf5Util.ListChildGroups(nirsGroup))
                    {
                        if (child.StartsWith("data", StringComparison.OrdinalIgnoreCase))
                            snirf.Data.Add(ReadDataBlock(nirsGroup, child));
                    }

                    snirf.Data.Sort((a, b) => NaturalSort.Compare(a.Name, b.Name));

                    // ---------- probe ----------
                    if (Hdf5Util.ExistsGroup(nirsGroup, "probe"))
                    {
                        try { snirf.Probe = ReadProbe(nirsGroup, "probe"); }
                        catch { snirf.Probe = null; } // probe 失败不影响 OD
                    }

                    // ---------- stim (optional, tolerant) ----------
                    foreach (var child in Hdf5Util.ListChildGroups(nirsGroup))
                    {
                        if (!child.StartsWith("stim", StringComparison.OrdinalIgnoreCase)) continue;
                        try
                        {
                            var s = ReadStim_Tolerant(nirsGroup, child);
                            if (s != null) snirf.Stim.Add(s);
                        }
                        catch
                        {
                            // 忽略 stim 读取失败（不影响 OD）
                        }
                    }

                    // ---------- aux (optional, tolerant) ----------
                    foreach (var child in Hdf5Util.ListChildGroups(nirsGroup))
                    {
                        if (!child.StartsWith("aux", StringComparison.OrdinalIgnoreCase)) continue;
                        try
                        {
                            var a = ReadAux_Tolerant(nirsGroup, child);
                            if (a != null) snirf.Aux.Add(a);
                        }
                        catch
                        {
                            // 忽略 aux 读取失败（不影响 OD）
                        }
                    }

                    // ---------- metaDataTags ----------
                    if (Hdf5Util.ExistsGroup(nirsGroup, "metaDataTags"))
                    {
                        try { ReadMetaDataTags(nirsGroup, "metaDataTags", snirf.MetaDataTags); }
                        catch { /* ignore */ }
                    }
                }
                finally
                {
                    H5G.close(nirsGroup);
                }

                return snirf;
            }
            finally
            {
                H5F.close(fileId);
            }
        }

        // =========================================================
        // Data block
        // =========================================================
        private static global::FnirsExe.Snirf.Models.NirsDataBlock ReadDataBlock(long nirsGroupId, string dataGroupName)
        {
            long g = H5G.open(nirsGroupId, dataGroupName);
            if (g < 0) throw new Exception($"Failed to open /nirs/{dataGroupName}");

            try
            {
                var block = new global::FnirsExe.Snirf.Models.NirsDataBlock { Name = dataGroupName };

                // 1) time
                block.Time = Hdf5Util.ReadDouble1D(g, "time");
                int timeLen = (block.Time != null) ? block.Time.Length : 0;

                // 2) dataTimeSeries (可能是 2D，也可能是 1D；统一包装成 2D)
                block.DataTimeSeries = Read2DOrMake2DFrom1D(g, "dataTimeSeries");
                int r = block.DataTimeSeries.GetLength(0);
                int c = block.DataTimeSeries.GetLength(1);

                // 3) measurementList：按文件内容自动适配（不要写死按某个维度）
                //    用 max(r,c) 做候选测量数，最大化兼容性（Homer3 风格）
                int measCountCandidate = Math.Max(r, c);

                var mlist = ReadMeasurementListAnyFormat(g, measCountCandidate);
                if (mlist == null || mlist.Count == 0)
                    throw new Exception("measurementList is empty (cannot build channel/wavelength mapping).");

                // 4) wavelengthIndex 自动归一化：若文件是 0-based(0/1)，统一转为 1-based(1/2)
                int minWl = int.MaxValue;
                for (int i = 0; i < mlist.Count; i++)
                    if (mlist[i].WavelengthIndex < minWl) minWl = mlist[i].WavelengthIndex;

                if (minWl == 0)
                {
                    for (int i = 0; i < mlist.Count; i++)
                        mlist[i].WavelengthIndex = mlist[i].WavelengthIndex + 1;
                }

                // 5) 关键：对齐 Homer3 —— 自动判断 dataTimeSeries 方向并在必要时转置
                //    内部约定：DataTimeSeries = [time, measurement]
                int measCount = mlist.Count;

                bool colsMatchMeas = (c == measCount);
                bool rowsMatchMeas = (r == measCount);
                bool rowsMatchTime = (timeLen > 0 && r == timeLen);
                bool colsMatchTime = (timeLen > 0 && c == timeLen);

                if (colsMatchMeas)
                {
                    // meas 在列上，符合内部约定（或至少不需要转置）
                }
                else if (rowsMatchMeas)
                {
                    // meas 在行上：强烈暗示是 [meas, time]，转置成 [time, meas]
                    block.DataTimeSeries = Transpose(block.DataTimeSeries);

                    r = block.DataTimeSeries.GetLength(0);
                    c = block.DataTimeSeries.GetLength(1);

                    if (c != measCount)
                        throw new Exception($"dataTimeSeries transpose mismatch: expected cols==measurementList.Count({measCount}), got {c}.");
                }
                else
                {
                    // 两边都不等于 measurementList.Count：尝试用 timeLen 做一次解释，否则报错
                    if (timeLen > 0)
                    {
                        if (rowsMatchTime)
                            throw new Exception($"dataTimeSeries cols({c}) does not match measurementList.Count({measCount}).");
                        if (colsMatchTime)
                            throw new Exception($"dataTimeSeries rows({r}) does not match measurementList.Count({measCount}).");
                    }

                    throw new Exception(
                        $"dataTimeSeries dims [{r}x{c}] cannot be aligned with measurementList.Count({measCount}) and timeLen({timeLen}).");
                }

                // 6) 写回 measurementList
                block.MeasurementList.AddRange(mlist);

                return block;
            }
            finally
            {
                H5G.close(g);
            }
        }

        // 2D 转置：A[r,c] -> B[c,r]
        private static double[,] Transpose(double[,] a)
        {
            int r = a.GetLength(0);
            int c = a.GetLength(1);
            var b = new double[c, r];
            for (int i = 0; i < r; i++)
                for (int j = 0; j < c; j++)
                    b[j, i] = a[i, j];
            return b;
        }


        private static double[,] Read2DOrMake2DFrom1D(long groupId, string datasetName)
        {
            if (!Hdf5Util.ExistsDataset(groupId, datasetName))
                throw new Exception($"Missing dataset {datasetName}");

            if (Hdf5Util.IsDatasetRank(groupId, datasetName, 2))
                return Hdf5Util.ReadDouble2D(groupId, datasetName);

            if (Hdf5Util.IsDatasetRank(groupId, datasetName, 1))
            {
                var v = Hdf5Util.ReadDouble1D(groupId, datasetName);
                var m = new double[v.Length, 1];
                for (int i = 0; i < v.Length; i++) m[i, 0] = v[i];
                return m;
            }

            throw new Exception($"{datasetName} is not 1D/2D");
        }

        // =========================================================
        // MeasurementList (兼容两种格式)
        // =========================================================
        private static List<global::FnirsExe.Snirf.Models.MeasList> ReadMeasurementListAnyFormat(long dataGroupId, int measCount)
        {
            // A) /measurementList/ml1..mlN  (group "measurementList" contains children "ml1", "ml2"...)
            var listA = ReadMeasurementList_GroupList(dataGroupId);
            if (listA.Count > 0)
                return FitToMeasCount(listA, measCount);

            // B) /measurementList1 .. /measurementListN (each is a GROUP describing one measurement)
            var listB = ReadMeasurementList_NumberedGroups(dataGroupId, measCount);
            if (listB.Count > 0)
                return listB;

            // C) /measurementList1 (GROUP) containing field arrays (sourceIndex[], detectorIndex[] ...)
            var listC = ReadMeasurementList_FieldArrays(dataGroupId, measCount, "measurementList1");
            if (listC.Count > 0) return listC;

            for (int k = 2; k <= 8; k++)
            {
                var listCk = ReadMeasurementList_FieldArrays(dataGroupId, measCount, "measurementList" + k);
                if (listCk.Count > 0) return listCk;
            }

            return new List<global::FnirsExe.Snirf.Models.MeasList>();
        }

        private static List<global::FnirsExe.Snirf.Models.MeasList> ReadMeasurementList_GroupList(long dataGroupId)
        {
            if (!Hdf5Util.ExistsGroup(dataGroupId, "measurementList"))
                return new List<global::FnirsExe.Snirf.Models.MeasList>();

            long mlGroup = H5G.open(dataGroupId, "measurementList");
            if (mlGroup < 0)
                return new List<global::FnirsExe.Snirf.Models.MeasList>();

            try
            {
                var list = new List<global::FnirsExe.Snirf.Models.MeasList>();

                foreach (var ml in Hdf5Util.ListChildGroups(mlGroup)
                             .Where(x => x.StartsWith("ml", StringComparison.OrdinalIgnoreCase))
                             .OrderBy(x => x, Comparer<string>.Create(NaturalSort.Compare)))
                {
                    list.Add(ReadSingleMeasList(mlGroup, ml));
                }

                return list;
            }
            finally
            {
                H5G.close(mlGroup);
            }
        }

        private static global::FnirsExe.Snirf.Models.MeasList ReadSingleMeasList(long mlGroupId, string mlName)
        {
            long g = H5G.open(mlGroupId, mlName);
            if (g < 0) throw new Exception($"Failed to open measurementList/{mlName}");

            try
            {
                return new global::FnirsExe.Snirf.Models.MeasList
                {
                    SourceIndex = Hdf5Util.ReadIntScalar(g, "sourceIndex"),
                    DetectorIndex = Hdf5Util.ReadIntScalar(g, "detectorIndex"),
                    WavelengthIndex = Hdf5Util.ReadIntScalar(g, "wavelengthIndex"),
                    DataType = Hdf5Util.ExistsDataset(g, "dataType") ? Hdf5Util.ReadIntScalar(g, "dataType") : 0,
                    DataTypeLabel = ""
                };
            }
            finally
            {
                H5G.close(g);
            }
        }


        private static List<global::FnirsExe.Snirf.Models.MeasList> ReadMeasurementList_NumberedGroups(long dataGroupId, int measCount)
        {
            // SNIRF v1 commonly stores measurementList1..measurementListN as GROUPS.
            // Each group contains scalar datasets: sourceIndex, detectorIndex, wavelengthIndex, dataType, dataTypeLabel...
            var list = new List<global::FnirsExe.Snirf.Models.MeasList>();

            for (int i = 1; i <= measCount; i++)
            {
                string gname = "measurementList" + i;
                if (!Hdf5Util.ExistsGroup(dataGroupId, gname))
                {
                    // stop at first missing (typical)
                    break;
                }

                long g = H5G.open(dataGroupId, gname);
                if (g < 0) break;

                try
                {
                    var ml = new global::FnirsExe.Snirf.Models.MeasList();

                    if (Hdf5Util.ExistsDataset(g, "sourceIndex")) ml.SourceIndex = Hdf5Util.ReadIntScalar(g, "sourceIndex");
                    if (Hdf5Util.ExistsDataset(g, "detectorIndex")) ml.DetectorIndex = Hdf5Util.ReadIntScalar(g, "detectorIndex");
                    if (Hdf5Util.ExistsDataset(g, "wavelengthIndex")) ml.WavelengthIndex = Hdf5Util.ReadIntScalar(g, "wavelengthIndex");

                    // optional
                    if (Hdf5Util.ExistsDataset(g, "dataType")) ml.DataType = Hdf5Util.ReadIntScalar(g, "dataType");
                    if (Hdf5Util.ExistsDataset(g, "dataTypeLabel")) ml.DataTypeLabel = Hdf5Util.ReadString(g, "dataTypeLabel");

                    list.Add(ml);
                }
                finally
                {
                    H5G.close(g);
                }
            }

            return list;
        }

        private static List<global::FnirsExe.Snirf.Models.MeasList> ReadMeasurementList_FieldArrays(long dataGroupId, int measCount, string groupName)
        {
            if (!Hdf5Util.ExistsGroup(dataGroupId, groupName))
                return new List<global::FnirsExe.Snirf.Models.MeasList>();

            long g = H5G.open(dataGroupId, groupName);
            if (g < 0)
                return new List<global::FnirsExe.Snirf.Models.MeasList>();

            try
            {
                var src = ReadIntArrayLenient(g, "sourceIndex", measCount);
                var det = ReadIntArrayLenient(g, "detectorIndex", measCount);
                var wl = ReadIntArrayLenient(g, "wavelengthIndex", measCount);

                int n = Math.Max(measCount, Math.Max(src.Length, det.Length));
                n = Math.Max(n, wl.Length);

                var list = new List<global::FnirsExe.Snirf.Models.MeasList>(n);
                for (int i = 0; i < n; i++)
                {
                    list.Add(new global::FnirsExe.Snirf.Models.MeasList
                    {
                        SourceIndex = GetArrayVal(src, i),
                        DetectorIndex = GetArrayVal(det, i),
                        WavelengthIndex = GetArrayVal(wl, i),
                        DataType = 0,
                        DataTypeLabel = ""
                    });
                }

                return FitToMeasCount(list, measCount);
            }
            finally
            {
                H5G.close(g);
            }
        }

        private static int[] ReadIntArrayLenient(long groupId, string datasetName, int expectedLen)
        {
            // 注意：你的 measurementList1 里 dtype=float(8B)，所以用 ReadDouble1D 再转 int
            var d = Hdf5Util.ReadDouble1D(groupId, datasetName);
            if (d == null || d.Length == 0) return new int[0];

            if (d.Length == 1 && expectedLen > 1)
            {
                int v = (int)Math.Round(d[0]);
                var arr = new int[expectedLen];
                for (int i = 0; i < expectedLen; i++) arr[i] = v;
                return arr;
            }

            var outArr = new int[d.Length];
            for (int i = 0; i < d.Length; i++)
                outArr[i] = (int)Math.Round(d[i]);

            return outArr;
        }

        private static int GetArrayVal(int[] arr, int i)
        {
            if (arr == null || arr.Length == 0) return 0;
            if (arr.Length == 1) return arr[0];
            if (i < 0) return arr[0];
            if (i >= arr.Length) return arr[arr.Length - 1];
            return arr[i];
        }

        private static List<global::FnirsExe.Snirf.Models.MeasList> FitToMeasCount(List<global::FnirsExe.Snirf.Models.MeasList> list, int measCount)
        {
            if (measCount <= 0) return list;
            if (list.Count == measCount) return list;

            if (list.Count > measCount)
                return list.Take(measCount).ToList();

            var res = new List<global::FnirsExe.Snirf.Models.MeasList>(measCount);
            res.AddRange(list);
            var last = list.Count > 0 ? list[list.Count - 1] : new global::FnirsExe.Snirf.Models.MeasList();
            while (res.Count < measCount) res.Add(last);
            return res;
        }

        // =========================================================
        // Probe / Stim / Aux / Meta (tolerant)
        // =========================================================
        private static global::FnirsExe.Snirf.Models.Probe ReadProbe(long nirsGroupId, string probeGroupName)
        {
            long g = H5G.open(nirsGroupId, probeGroupName);
            if (g < 0) throw new Exception("Failed to open /nirs/probe");

            try
            {
                var p = new global::FnirsExe.Snirf.Models.Probe();

                // 3D (SNIRF spec)
                if (Hdf5Util.ExistsDataset(g, "sourcePos3D"))
                    p.SourcePos3D = Hdf5Util.ReadDouble2D(g, "sourcePos3D");
                if (Hdf5Util.ExistsDataset(g, "detectorPos3D"))
                    p.DetectorPos3D = Hdf5Util.ReadDouble2D(g, "detectorPos3D");

                // 2D (common in some devices: e.g., Brite)
                if (Hdf5Util.ExistsDataset(g, "sourcePos2D"))
                    p.SourcePos2D = Hdf5Util.ReadDouble2D(g, "sourcePos2D");
                if (Hdf5Util.ExistsDataset(g, "detectorPos2D"))
                    p.DetectorPos2D = Hdf5Util.ReadDouble2D(g, "detectorPos2D");

                // wavelengths
                if (Hdf5Util.ExistsDataset(g, "wavelengths"))
                    p.Wavelengths = Hdf5Util.ReadDouble1D(g, "wavelengths");

                // labels (may be fixed-len or vlen)
                if (Hdf5Util.ExistsDataset(g, "sourceLabels"))
                    p.SourceLabels = Hdf5Util.ReadStringArray(g, "sourceLabels");
                if (Hdf5Util.ExistsDataset(g, "detectorLabels"))
                    p.DetectorLabels = Hdf5Util.ReadStringArray(g, "detectorLabels");

                // optional fields (ignore if missing)
                if (Hdf5Util.ExistsDataset(g, "frequencies"))
                    p.Frequencies = Hdf5Util.ReadDouble1D(g, "frequencies");
                if (Hdf5Util.ExistsDataset(g, "timeDelays"))
                    p.TimeDelays = Hdf5Util.ReadDouble1D(g, "timeDelays");
                if (Hdf5Util.ExistsDataset(g, "timeDelayWidths"))
                    p.TimeDelayWidths = Hdf5Util.ReadDouble1D(g, "timeDelayWidths");
                if (Hdf5Util.ExistsDataset(g, "correlationTimeDelays"))
                    p.CorrelationTimeDelays = Hdf5Util.ReadDouble1D(g, "correlationTimeDelays");
                if (Hdf5Util.ExistsDataset(g, "correlationTimeDelayWidths"))
                    p.CorrelationTimeDelayWidths = Hdf5Util.ReadDouble1D(g, "correlationTimeDelayWidths");

                // landmarks (optional)
                if (Hdf5Util.ExistsDataset(g, "landmarkPos3D"))
                    p.LandmarkPos3D = Hdf5Util.ReadDouble2D(g, "landmarkPos3D");
                if (Hdf5Util.ExistsDataset(g, "landmarkLabels"))
                    p.LandmarkLabels = Hdf5Util.ReadStringArray(g, "landmarkLabels");

                return p;
            }
            finally
            {
                H5G.close(g);
            }
        }


        private static global::FnirsExe.Snirf.Models.Stim ReadStim_Tolerant(long nirsGroupId, string stimGroupName)
        {
            long g = H5G.open(nirsGroupId, stimGroupName);
            if (g < 0) return null;

            try
            {
                var s = new global::FnirsExe.Snirf.Models.Stim();

                if (Hdf5Util.ExistsDataset(g, "name"))
                    s.Name = Hdf5Util.ReadString(g, "name");

                // ✅ 关键修复：stim/data 允许 1D/2D
                if (Hdf5Util.ExistsDataset(g, "data"))
                    s.Data = Read2DOrMake2DFrom1D(g, "data");

                return s;
            }
            finally
            {
                H5G.close(g);
            }
        }

        private static global::FnirsExe.Snirf.Models.Aux ReadAux_Tolerant(long nirsGroupId, string auxGroupName)
        {
            long g = H5G.open(nirsGroupId, auxGroupName);
            if (g < 0) return null;

            try
            {
                var a = new global::FnirsExe.Snirf.Models.Aux();

                if (Hdf5Util.ExistsDataset(g, "name"))
                    a.Name = Hdf5Util.ReadString(g, "name");

                if (Hdf5Util.ExistsDataset(g, "time"))
                    a.Time = Hdf5Util.ReadDouble1D(g, "time");

                if (Hdf5Util.ExistsDataset(g, "data"))
                {
                    // ✅ 关键修复：aux/data 允许 1D/2D
                    if (Hdf5Util.IsDatasetRank(g, "data", 1))
                        a.Data = Hdf5Util.ReadDouble1D(g, "data");
                    else if (Hdf5Util.IsDatasetRank(g, "data", 2))
                        a.Data = FlattenColumnMajor(Hdf5Util.ReadDouble2D(g, "data"));
                }

                return a;
            }
            finally
            {
                H5G.close(g);
            }
        }

        private static void ReadMetaDataTags(long nirsGroupId, string metaGroupName, global::FnirsExe.Snirf.Models.MetaDataTags tags)
        {
            long g = H5G.open(nirsGroupId, metaGroupName);
            if (g < 0) return;

            try
            {
                foreach (var ds in Hdf5Util.ListChildDatasets(g))
                {
                    try { tags.Tags[ds] = Hdf5Util.ReadString(g, ds); }
                    catch { /* ignore non-string */ }
                }
            }
            finally
            {
                H5G.close(g);
            }
        }

        private static double[] FlattenColumnMajor(double[,] m)
        {
            int r = m.GetLength(0), c = m.GetLength(1);
            var arr = new double[r * c];
            int k = 0;
            for (int j = 0; j < c; j++)
                for (int i = 0; i < r; i++)
                    arr[k++] = m[i, j];
            return arr;
        }
    }
}
