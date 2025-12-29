using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using HDF.PInvoke;

namespace FnirsExe.Snirf.Hdf5
{
    public static class SnirfInspector
    {
        public static string InspectMeasurementList(string snirfPath)
        {
            long fileId = H5F.open(snirfPath, H5F.ACC_RDONLY);
            if (fileId < 0) throw new Exception("Failed to open SNIRF file.");

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== SNIRF measurementList Inspect ===");
                sb.AppendLine("File: " + snirfPath);

                // Try common paths
                var pathsToTry = new[]
                {
                    "/nirs/data1/measurementList",
                    "/nirs/data1/measurementList1",
                    "/nirs/data/measurementList",
                    "/nirs/measurementList"
                };

                string found = null;
                foreach (var p in pathsToTry)
                {
                    if (LinkExists(fileId, p))
                    {
                        found = p;
                        break;
                    }
                }

                if (found == null)
                {
                    sb.AppendLine("❌ measurementList not found in common paths.");
                    sb.AppendLine("Tried:");
                    foreach (var p in pathsToTry) sb.AppendLine("  - " + p);
                    return sb.ToString();
                }

                sb.AppendLine("✅ Found: " + found);

                long g = H5G.open(fileId, found);
                if (g < 0)
                {
                    sb.AppendLine("❌ Found path exists but cannot open as group: " + found);

                    // 有些文件 measurementList 可能是 dataset，不是 group
                    if (IsDatasetByPath(fileId, found))
                    {
                        sb.AppendLine("It is a DATASET (not group).");
                        sb.AppendLine(InspectDatasetByPath(fileId, found));
                    }
                    return sb.ToString();
                }

                try
                {
                    // list children
                    var children = ListChildrenNames(g);
                    sb.AppendLine($"Children count: {children.Count}");
                    foreach (var name in children)
                    {
                        string childPath = found.TrimEnd('/') + "/" + name;
                        var type = GetObjectType(g, name);
                        sb.AppendLine();
                        sb.AppendLine($"- {name}  ({type})  path={childPath}");

                        if (type == "GROUP")
                        {
                            long cg = H5G.open(g, name);
                            if (cg >= 0)
                            {
                                try
                                {
                                    var sub = ListChildrenNames(cg);
                                    sb.AppendLine($"  Sub-children count: {sub.Count}");
                                    foreach (var s in sub)
                                    {
                                        string sp = childPath + "/" + s;
                                        string st = GetObjectType(cg, s);
                                        sb.AppendLine($"  * {s} ({st}) path={sp}");
                                        if (st == "DATASET")
                                            sb.AppendLine(Indent(InspectDatasetInGroup(cg, s), "    "));
                                    }
                                }
                                finally { H5G.close(cg); }
                            }
                        }
                        else if (type == "DATASET")
                        {
                            sb.AppendLine(Indent(InspectDatasetInGroup(g, name), "  "));
                        }
                    }

                    // 额外：优先打印 sourceIndex/detectorIndex/wavelengthIndex
                    sb.AppendLine();
                    sb.AppendLine("=== Quick check for common fields ===");
                    foreach (var key in new[] { "sourceIndex", "detectorIndex", "wavelengthIndex", "dataType", "dataTypeLabel" })
                    {
                        if (H5L.exists(g, key) > 0 && IsDatasetInGroup(g, key))
                        {
                            sb.AppendLine($"[{key}]");
                            sb.AppendLine(InspectDatasetInGroup(g, key));
                            sb.AppendLine();
                        }
                    }
                }
                finally
                {
                    H5G.close(g);
                }

                return sb.ToString();
            }
            finally
            {
                H5F.close(fileId);
            }
        }

        // ---------- helpers ----------

        private static string Indent(string s, string prefix)
        {
            var lines = s.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            return string.Join(Environment.NewLine, lines.Select(l => prefix + l));
        }

        private static bool LinkExists(long fileOrGroupId, string path)
        {
            long obj = H5O.open(fileOrGroupId, path, H5P.DEFAULT);
            if (obj >= 0)
            {
                H5O.close(obj);
                return true;
            }
            return false;
        }

        private static string GetObjectType(long groupId, string name)
        {
            long obj = H5O.open(groupId, name, H5P.DEFAULT);
            if (obj < 0) return "UNKNOWN";
            try
            {
                H5O.info_t info = new H5O.info_t();
                if (H5O.get_info(obj, ref info) < 0) return "UNKNOWN";
                if (info.type == H5O.type_t.GROUP) return "GROUP";
                if (info.type == H5O.type_t.DATASET) return "DATASET";
                return info.type.ToString();
            }
            finally { H5O.close(obj); }
        }

        private static bool IsDatasetInGroup(long parentId, string name)
        {
            long obj = H5O.open(parentId, name, H5P.DEFAULT);
            if (obj < 0) return false;
            try
            {
                H5O.info_t info = new H5O.info_t();
                if (H5O.get_info(obj, ref info) < 0) return false;
                return info.type == H5O.type_t.DATASET;
            }
            finally { H5O.close(obj); }
        }

        private static bool IsDatasetByPath(long fileId, string fullPath)
        {
            long obj = H5O.open(fileId, fullPath, H5P.DEFAULT);
            if (obj < 0) return false;
            try
            {
                H5O.info_t info = new H5O.info_t();
                if (H5O.get_info(obj, ref info) < 0) return false;
                return info.type == H5O.type_t.DATASET;
            }
            finally { H5O.close(obj); }
        }

        private static List<string> ListChildrenNames(long groupId)
        {
            var names = new List<string>();

            ulong idx = 0;
            H5L.iterate(groupId, H5.index_t.NAME, H5.iter_order_t.NATIVE, ref idx,
                (long g, IntPtr namePtr, ref H5L.info_t info, IntPtr op_data) =>
                {
                    string n = Marshal.PtrToStringAnsi(namePtr) ?? "";
                    if (!string.IsNullOrEmpty(n))
                        names.Add(n);
                    return 0;
                },
                IntPtr.Zero);

            return names;
        }

        /// <summary>
        /// ✅ 读 groupId 下的 datasetName
        /// </summary>
        private static string InspectDatasetInGroup(long groupId, string datasetName)
        {
            long ds = H5D.open(groupId, datasetName);
            if (ds < 0) return "❌ cannot open dataset";

            try
            {
                long space = H5D.get_space(ds);
                if (space < 0) return "❌ cannot get dataspace";

                try
                {
                    int rank = H5S.get_simple_extent_ndims(space);
                    ulong[] dims = new ulong[Math.Max(rank, 0)];
                    ulong[] max = new ulong[Math.Max(rank, 0)];
                    if (rank > 0)
                        H5S.get_simple_extent_dims(space, dims, max);

                    long type = H5D.get_type(ds);
                    string typeStr = TypeToString(type);

                    var sb = new StringBuilder();
                    sb.AppendLine($"dtype={typeStr}, rank={rank}, dims=[{string.Join(",", dims.Select(d => d.ToString()))}]");

                    if (IsStringType(type))
                    {
                        sb.AppendLine("preview(string): " + ReadStringPreview(ds, type, dims, maxItems: 8));
                    }
                    else if (IsIntegerType(type))
                    {
                        sb.AppendLine("preview(int): " + ReadIntPreview(ds, dims, maxItems: 12));
                    }
                    else if (IsFloatType(type))
                    {
                        sb.AppendLine("preview(double): " + ReadDoublePreview(ds, dims, maxItems: 12));
                    }
                    else
                    {
                        sb.AppendLine("preview: (unhandled type)");
                    }

                    H5T.close(type);
                    return sb.ToString();
                }
                finally { H5S.close(space); }
            }
            finally { H5D.close(ds); }
        }

        /// <summary>
        /// ✅ 读 fileId 下的 fullPath（路径式 dataset），用于 measurementList 本身是 dataset 的情况
        /// </summary>
        private static string InspectDatasetByPath(long fileId, string fullPath)
        {
            long ds = H5D.open(fileId, fullPath);
            if (ds < 0) return "❌ cannot open dataset";

            try
            {
                long type = H5D.get_type(ds);
                try
                {
                    long space = H5D.get_space(ds);
                    try
                    {
                        int rank = H5S.get_simple_extent_ndims(space);
                        ulong[] dims = new ulong[Math.Max(rank, 0)];
                        ulong[] max = new ulong[Math.Max(rank, 0)];
                        if (rank > 0) H5S.get_simple_extent_dims(space, dims, max);

                        var sb = new StringBuilder();
                        sb.AppendLine($"dtype={TypeToString(type)}, rank={rank}, dims=[{string.Join(",", dims.Select(d => d.ToString()))}]");

                        if (IsStringType(type))
                            sb.AppendLine("preview(string): " + ReadStringPreview(ds, type, dims, maxItems: 8));
                        else if (IsIntegerType(type))
                            sb.AppendLine("preview(int): " + ReadIntPreview(ds, dims, maxItems: 12));
                        else if (IsFloatType(type))
                            sb.AppendLine("preview(double): " + ReadDoublePreview(ds, dims, maxItems: 12));

                        return sb.ToString();
                    }
                    finally { H5S.close(space); }
                }
                finally { H5T.close(type); }
            }
            finally { H5D.close(ds); }
        }

        private static bool IsStringType(long type)
        {
            if (type < 0) return false;
            return H5T.get_class(type) == H5T.class_t.STRING;
        }

        private static bool IsIntegerType(long type)
        {
            if (type < 0) return false;
            var cls = H5T.get_class(type);
            return cls == H5T.class_t.INTEGER;
        }

        private static bool IsFloatType(long type)
        {
            if (type < 0) return false;
            var cls = H5T.get_class(type);
            return cls == H5T.class_t.FLOAT;
        }

        private static string TypeToString(long type)
        {
            if (type < 0) return "UNKNOWN";
            var cls = H5T.get_class(type);
            if (cls == H5T.class_t.STRING)
            {
                bool vlen = H5T.is_variable_str(type) > 0;
                return vlen ? "string(vlen)" : "string(fixed)";
            }
            if (cls == H5T.class_t.INTEGER)
            {
                var sz = H5T.get_size(type);
                return "int(" + sz + "B)";
            }
            if (cls == H5T.class_t.FLOAT)
            {
                var sz = H5T.get_size(type);
                return "float(" + sz + "B)";
            }
            return cls.ToString();
        }

        private static string ReadIntPreview(long ds, ulong[] dims, int maxItems)
        {
            int n = (int)Math.Min((ulong)maxItems, Product(dims));
            if (n <= 0) return "(empty)";

            var buf = new long[Product(dims)];
            GCHandle h = GCHandle.Alloc(buf, GCHandleType.Pinned);
            try
            {
                var status = H5D.read(ds, H5T.NATIVE_LLONG, H5S.ALL, H5S.ALL, H5P.DEFAULT, h.AddrOfPinnedObject());
                if (status < 0) return "❌ read failed";
            }
            finally { h.Free(); }

            return string.Join(", ", buf.Take(n));
        }

        private static string ReadDoublePreview(long ds, ulong[] dims, int maxItems)
        {
            int n = (int)Math.Min((ulong)maxItems, Product(dims));
            if (n <= 0) return "(empty)";

            var buf = new double[Product(dims)];
            GCHandle h = GCHandle.Alloc(buf, GCHandleType.Pinned);
            try
            {
                var status = H5D.read(ds, H5T.NATIVE_DOUBLE, H5S.ALL, H5S.ALL, H5P.DEFAULT, h.AddrOfPinnedObject());
                if (status < 0) return "❌ read failed";
            }
            finally { h.Free(); }

            return string.Join(", ", buf.Take(n).Select(v => v.ToString("G6")));
        }

        private static string ReadStringPreview(long ds, long type, ulong[] dims, int maxItems)
        {
            ulong total = Product(dims);
            if (total == 0) total = 1;

            int n = (int)Math.Min((ulong)maxItems, total);

            if (H5T.is_variable_str(type) > 0)
            {
                var ptrs = new IntPtr[total];
                GCHandle h = GCHandle.Alloc(ptrs, GCHandleType.Pinned);
                try
                {
                    var status = H5D.read(ds, type, H5S.ALL, H5S.ALL, H5P.DEFAULT, h.AddrOfPinnedObject());
                    if (status < 0) return "❌ read failed";

                    var arr = new string[n];
                    for (int i = 0; i < n; i++)
                    {
                        arr[i] = Marshal.PtrToStringAnsi(ptrs[i]) ?? "";
                        H5.free_memory(ptrs[i]);
                    }
                    return string.Join(" | ", arr);
                }
                finally { h.Free(); }
            }
            else
            {
                int size = (int)H5T.get_size(type);
                var bytes = new byte[size * n];
                GCHandle h = GCHandle.Alloc(bytes, GCHandleType.Pinned);
                try
                {
                    var status = H5D.read(ds, type, H5S.ALL, H5S.ALL, H5P.DEFAULT, h.AddrOfPinnedObject());
                    if (status < 0) return "❌ read failed";

                    var arr = new string[n];
                    for (int i = 0; i < n; i++)
                        arr[i] = Encoding.ASCII.GetString(bytes, i * size, size).TrimEnd('\0');

                    return string.Join(" | ", arr);
                }
                finally { h.Free(); }
            }
        }

        private static ulong Product(ulong[] dims)
        {
            if (dims == null || dims.Length == 0) return 1;
            ulong p = 1;
            foreach (var d in dims) p *= (d == 0 ? 1UL : d);
            return p;
        }
    }
}
