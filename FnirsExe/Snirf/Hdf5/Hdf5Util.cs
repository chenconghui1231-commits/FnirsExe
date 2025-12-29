using HDF.PInvoke;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace FnirsExe.Snirf.Hdf5
{
    /// <summary>
    /// HDF5 helper utilities compatible with:
    /// - .NET Framework 4.8
    /// - HDF.PInvoke 1.10.612
    ///
    /// Conventions:
    /// - HDF5 identifiers (hid_t) are long in this PInvoke version.
    /// - H5L.iterate uses ref ulong index in this PInvoke version.
    /// </summary>
    internal static class Hdf5Util
    {
        // ---------------- Existence helpers ----------------

        public static bool Exists(long parentId, string childName)
            => H5L.exists(parentId, childName) > 0;

        public static bool ExistsGroup(long parentId, string childName)
            => Exists(parentId, childName) && IsGroup(parentId, childName);

        public static bool ExistsDataset(long parentId, string childName)
            => Exists(parentId, childName) && IsDataset(parentId, childName);

        // ---------------- Type checks ----------------

        public static bool IsGroup(long parentId, string name)
        {
            long objId = H5O.open(parentId, name, H5P.DEFAULT);
            if (objId < 0) return false;

            try
            {
                H5O.info_t info = new H5O.info_t();
                if (H5O.get_info(objId, ref info) < 0) return false;
                return info.type == H5O.type_t.GROUP;
            }
            finally
            {
                H5O.close(objId);
            }
        }

        public static bool IsDataset(long parentId, string name)
        {
            long objId = H5O.open(parentId, name, H5P.DEFAULT);
            if (objId < 0) return false;

            try
            {
                H5O.info_t info = new H5O.info_t();
                if (H5O.get_info(objId, ref info) < 0) return false;
                return info.type == H5O.type_t.DATASET;
            }
            finally
            {
                H5O.close(objId);
            }
        }

        public static bool IsDatasetRank(long groupId, string datasetName, int expectedRank)
        {
            long ds = H5D.open(groupId, datasetName);
            if (ds < 0) return false;

            try
            {
                long space = H5D.get_space(ds);
                if (space < 0) return false;

                try
                {
                    int rank = H5S.get_simple_extent_ndims(space);
                    return rank == expectedRank;
                }
                finally { H5S.close(space); }
            }
            finally { H5D.close(ds); }
        }

        // ---------------- List children ----------------

        public static List<string> ListChildGroups(long groupId)
            => ListChildren(groupId, H5O.type_t.GROUP);

        public static List<string> ListChildDatasets(long groupId)
            => ListChildren(groupId, H5O.type_t.DATASET);

        private static List<string> ListChildren(long groupId, H5O.type_t type)
        {
            List<string> results = new List<string>();
            ulong idx = 0; // H5L.iterate expects ref ulong

            H5L.iterate(
                groupId,
                H5.index_t.NAME,
                H5.iter_order_t.NATIVE,
                ref idx,
                (long g, IntPtr namePtr, ref H5L.info_t info, IntPtr opData) =>
                {
                    string name = Marshal.PtrToStringAnsi(namePtr);
                    if (!string.IsNullOrEmpty(name))
                    {
                        if (type == H5O.type_t.GROUP)
                        {
                            if (IsGroup(g, name)) results.Add(name);
                        }
                        else if (type == H5O.type_t.DATASET)
                        {
                            if (IsDataset(g, name)) results.Add(name);
                        }
                    }
                    return 0;
                },
                IntPtr.Zero
            );

            return results;
        }

        // ---------------- Read ints ----------------

        /// <summary>
        /// Reads an integer dataset that is either scalar or a 1-element vector.
        /// Supports int32/int64 by reading as native long long.
        /// </summary>
        public static int ReadIntScalar(long groupId, string datasetName)
        {
            long ds = H5D.open(groupId, datasetName);
            if (ds < 0) throw new Exception("Missing dataset: " + datasetName);

            try
            {
                long space = H5D.get_space(ds);
                if (space < 0) throw new Exception("Failed to get dataspace: " + datasetName);

                try
                {
                    int rank = H5S.get_simple_extent_ndims(space);
                    if (rank == 0)
                    {
                        // scalar
                    }
                    else if (rank == 1)
                    {
                        ulong[] dims = new ulong[1];
                        H5S.get_simple_extent_dims(space, dims, null);
                        if (dims[0] != 1) throw new Exception(datasetName + " is not scalar/1-element.");
                    }
                    else
                    {
                        throw new Exception(datasetName + " is not scalar/1-element.");
                    }

                    long[] buf = new long[1];
                    GCHandle h = GCHandle.Alloc(buf, GCHandleType.Pinned);
                    try
                    {
                        if (H5D.read(ds, H5T.NATIVE_LLONG, H5S.ALL, H5S.ALL, H5P.DEFAULT, h.AddrOfPinnedObject()) < 0)
                            throw new Exception("Failed reading dataset: " + datasetName);
                    }
                    finally { h.Free(); }

                    return checked((int)buf[0]);
                }
                finally { H5S.close(space); }
            }
            finally { H5D.close(ds); }
        }

        // ---------------- Read doubles ----------------

        public static double[] ReadDouble1D(long parentId, string name)
        {
            long dset = H5D.open(parentId, name);
            if (dset < 0) throw new Exception("Missing dataset: " + name);

            long space = H5D.get_space(dset);
            if (space < 0)
            {
                H5D.close(dset);
                throw new Exception("Failed to get dataspace: " + name);
            }

            try
            {
                ulong[] dims = new ulong[1];
                H5S.get_simple_extent_dims(space, dims, null);

                int n = checked((int)dims[0]);
                double[] data = new double[n];

                GCHandle h = GCHandle.Alloc(data, GCHandleType.Pinned);
                try
                {
                    if (H5D.read(dset, H5T.NATIVE_DOUBLE, H5S.ALL, H5S.ALL, H5P.DEFAULT, h.AddrOfPinnedObject()) < 0)
                        throw new Exception("Failed to read dataset: " + name);
                }
                finally
                {
                    h.Free();
                }

                return data;
            }
            finally
            {
                H5S.close(space);
                H5D.close(dset);
            }
        }

        public static double[,] ReadDouble2D(long parentId, string name)
        {
            long dset = H5D.open(parentId, name);
            if (dset < 0) throw new Exception("Missing dataset: " + name);

            long space = H5D.get_space(dset);
            if (space < 0)
            {
                H5D.close(dset);
                throw new Exception("Failed to get dataspace: " + name);
            }

            try
            {
                ulong[] dims = new ulong[2];
                H5S.get_simple_extent_dims(space, dims, null);

                int rows = checked((int)dims[0]);
                int cols = checked((int)dims[1]);
                double[] flat = new double[rows * cols];

                GCHandle h = GCHandle.Alloc(flat, GCHandleType.Pinned);
                try
                {
                    if (H5D.read(dset, H5T.NATIVE_DOUBLE, H5S.ALL, H5S.ALL, H5P.DEFAULT, h.AddrOfPinnedObject()) < 0)
                        throw new Exception("Failed to read dataset: " + name);
                }
                finally
                {
                    h.Free();
                }

                double[,] result = new double[rows, cols];
                int k = 0;
                for (int i = 0; i < rows; i++)
                    for (int j = 0; j < cols; j++)
                        result[i, j] = flat[k++];

                return result;
            }
            finally
            {
                H5S.close(space);
                H5D.close(dset);
            }
        }

        // ---------------- Read strings ----------------

        /// <summary>
        /// Reads a scalar string dataset (either fixed-length or variable-length).
        /// </summary>
        public static string ReadString(long parentId, string name)
        {
            long dset = H5D.open(parentId, name);
            if (dset < 0) throw new Exception("Missing dataset: " + name);

            long type = H5D.get_type(dset);
            if (type < 0)
            {
                H5D.close(dset);
                throw new Exception("Failed to get datatype: " + name);
            }

            try
            {
                if (H5T.is_variable_str(type) > 0)
                {
                    IntPtr[] rdata = new IntPtr[1];
                    GCHandle h = GCHandle.Alloc(rdata, GCHandleType.Pinned);
                    try
                    {
                        if (H5D.read(dset, type, H5S.ALL, H5S.ALL, H5P.DEFAULT, h.AddrOfPinnedObject()) < 0)
                            throw new Exception("Failed to read vlen string: " + name);

                        string s = Marshal.PtrToStringAnsi(rdata[0]) ?? string.Empty;
                        if (rdata[0] != IntPtr.Zero) H5.free_memory(rdata[0]);
                        return s;
                    }
                    finally
                    {
                        h.Free();
                    }
                }
                else
                {
                    int size = checked((int)H5T.get_size(type));
                    byte[] buf = new byte[size];

                    GCHandle h = GCHandle.Alloc(buf, GCHandleType.Pinned);
                    try
                    {
                        if (H5D.read(dset, type, H5S.ALL, H5S.ALL, H5P.DEFAULT, h.AddrOfPinnedObject()) < 0)
                            throw new Exception("Failed to read string: " + name);
                    }
                    finally
                    {
                        h.Free();
                    }

                    return Encoding.ASCII.GetString(buf).TrimEnd('\0');
                }
            }
            finally
            {
                H5T.close(type);
                H5D.close(dset);
            }
        }

        /// <summary>
        /// Reads a 1D string array dataset. Supports variable-length strings and fixed-length strings.
        /// </summary>
        public static string[] ReadStringArray(long parentId, string name)
        {
            long dset = H5D.open(parentId, name);
            if (dset < 0) throw new Exception("Missing dataset: " + name);

            long space = H5D.get_space(dset);
            if (space < 0)
            {
                H5D.close(dset);
                throw new Exception("Failed to get dataspace: " + name);
            }

            long type = -1;

            try
            {
                int rank = H5S.get_simple_extent_ndims(space);
                if (rank != 1) throw new Exception(name + " is not a 1D string array.");

                ulong[] dims = new ulong[1];
                H5S.get_simple_extent_dims(space, dims, null);
                int n = checked((int)dims[0]);

                type = H5D.get_type(dset);
                if (type < 0) throw new Exception("Failed to get datatype: " + name);

                if (H5T.is_variable_str(type) > 0)
                {
                    IntPtr[] ptrs = new IntPtr[n];
                    GCHandle h = GCHandle.Alloc(ptrs, GCHandleType.Pinned);
                    try
                    {
                        if (H5D.read(dset, type, H5S.ALL, H5S.ALL, H5P.DEFAULT, h.AddrOfPinnedObject()) < 0)
                            throw new Exception("Failed to read string array: " + name);

                        string[] arr = new string[n];
                        for (int i = 0; i < n; i++)
                        {
                            arr[i] = Marshal.PtrToStringAnsi(ptrs[i]) ?? string.Empty;
                            if (ptrs[i] != IntPtr.Zero) H5.free_memory(ptrs[i]);
                        }
                        return arr;
                    }
                    finally { h.Free(); }
                }
                else
                {
                    // fixed-length string array: each element is 'size' bytes
                    int size = checked((int)H5T.get_size(type));
                    byte[] buf = new byte[n * size];

                    GCHandle h = GCHandle.Alloc(buf, GCHandleType.Pinned);
                    try
                    {
                        if (H5D.read(dset, type, H5S.ALL, H5S.ALL, H5P.DEFAULT, h.AddrOfPinnedObject()) < 0)
                            throw new Exception("Failed to read fixed string array: " + name);
                    }
                    finally { h.Free(); }

                    string[] arr = new string[n];
                    for (int i = 0; i < n; i++)
                        arr[i] = Encoding.ASCII.GetString(buf, i * size, size).TrimEnd('\0', ' ');

                    return arr;
                }
            }
            finally
            {
                if (type >= 0) H5T.close(type);
                H5S.close(space);
                H5D.close(dset);
            }
        }
    }
}
