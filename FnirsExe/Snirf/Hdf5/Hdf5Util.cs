using System;
using System.Runtime.InteropServices;
using System.Text;
using HDF.PInvoke;

namespace FnirsExe.Snirf.Hdf5
{
    public static class Hdf5Util
    {
        // 1. 打开文件
        public static long OpenFile(string filePath)
        {
            return H5F.open(filePath, H5F.ACC_RDONLY);
        }

        // 2. 关闭文件
        public static void CloseFile(long fileId)
        {
            if (fileId >= 0) H5F.close(fileId);
        }

        // 3. 检查组是否存在
        public static bool GroupExists(long locId, string name)
        {
            return H5L.exists(locId, name) > 0;
        }

        // 4. 读取标量
        public static T ReadScalar<T>(long locId, string name) where T : struct
        {
            if (H5L.exists(locId, name) <= 0) return default;

            long dsetId = H5D.open(locId, name);
            if (dsetId < 0) return default;

            T[] data = new T[1];
            GCHandle hnd = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                long typeId = GetMemType<T>();
                H5D.read(dsetId, typeId, H5S.ALL, H5S.ALL, H5P.DEFAULT, hnd.AddrOfPinnedObject());
            }
            finally
            {
                hnd.Free();
                H5D.close(dsetId);
            }
            return data[0];
        }

        // 5. 读取 1D 数组 (修复了 ulong 类型问题)
        public static T[] ReadDataset1D<T>(long locId, string name) where T : struct
        {
            if (H5L.exists(locId, name) <= 0) return null;

            long dsetId = H5D.open(locId, name);
            long spaceId = H5D.get_space(dsetId);

            // 修复: 使用 ulong[] 接收 HDF5 维度
            ulong[] dims = new ulong[2];
            H5S.get_simple_extent_dims(spaceId, dims, null);

            // 修复: 计算长度时转回 long
            long len = (long)(dims[0] * (dims[1] > 0 ? dims[1] : 1));
            T[] data = new T[len];

            GCHandle hnd = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                long typeId = GetMemType<T>();
                H5D.read(dsetId, typeId, H5S.ALL, H5S.ALL, H5P.DEFAULT, hnd.AddrOfPinnedObject());
            }
            finally
            {
                hnd.Free();
                H5S.close(spaceId);
                H5D.close(dsetId);
            }
            return data;
        }

        // 6. 读取 2D 数组 (修复了 ulong 类型问题)
        public static T[,] ReadDataset<T>(long locId, string name) where T : struct
        {
            if (H5L.exists(locId, name) <= 0) return null;

            long dsetId = H5D.open(locId, name);
            long spaceId = H5D.get_space(dsetId);

            // 修复: ulong[]
            ulong[] dims = new ulong[2];
            int rank = H5S.get_simple_extent_ndims(spaceId);
            H5S.get_simple_extent_dims(spaceId, dims, null);

            if (rank == 1) dims[1] = 1;

            // 修复: 显式强转为 long 创建数组
            T[,] data = new T[(long)dims[0], (long)dims[1]];

            GCHandle hnd = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                long typeId = GetMemType<T>();
                H5D.read(dsetId, typeId, H5S.ALL, H5S.ALL, H5P.DEFAULT, hnd.AddrOfPinnedObject());
            }
            finally
            {
                hnd.Free();
                H5S.close(spaceId);
                H5D.close(dsetId);
            }
            return data;
        }

        // 7. 读取字符串属性 (修复了 IntPtr 转换问题)
        public static string ReadStringAttribute(long locId, string objName, string attrName)
        {
            long oid = locId;
            bool needClose = false;

            if (objName != "/")
            {
                if (H5L.exists(locId, objName) <= 0) return null;
                oid = H5O.open(locId, objName);
                needClose = true;
            }

            string result = null;
            if (H5A.exists(oid, attrName) > 0)
            {
                long attrId = H5A.open(oid, attrName);
                long typeId = H5A.get_type(attrId);

                // 修复: 显式强转 IntPtr -> long
                long sz = (long)H5T.get_size(typeId);

                byte[] buf = new byte[sz];
                GCHandle hnd = GCHandle.Alloc(buf, GCHandleType.Pinned);
                H5A.read(attrId, typeId, hnd.AddrOfPinnedObject());
                hnd.Free();
                result = Encoding.ASCII.GetString(buf).TrimEnd('\0');
                H5T.close(typeId);
                H5A.close(attrId);
            }

            if (needClose) H5O.close(oid);
            return result;
        }

        // 8. 读取字符串 Dataset (修复了 IntPtr 转换问题)
        public static string ReadStringDataset(long locId, string name)
        {
            if (H5L.exists(locId, name) <= 0) return null;

            long dsetId = H5D.open(locId, name);
            long typeId = H5D.get_type(dsetId);

            if (H5T.get_class(typeId) != H5T.class_t.STRING)
            {
                H5T.close(typeId);
                H5D.close(dsetId);
                return null;
            }

            // 修复: 显式强转 IntPtr -> long
            long size = (long)H5T.get_size(typeId);
            bool isVlen = H5T.is_variable_str(typeId) > 0;

            string result = "";

            if (isVlen)
            {
                // 暂不支持变长字符串，防止报错
                H5T.close(typeId);
                H5D.close(dsetId);
                return "";
            }
            else
            {
                byte[] buf = new byte[size];
                GCHandle hnd = GCHandle.Alloc(buf, GCHandleType.Pinned);
                H5D.read(dsetId, typeId, H5S.ALL, H5S.ALL, H5P.DEFAULT, hnd.AddrOfPinnedObject());
                hnd.Free();
                result = Encoding.UTF8.GetString(buf).TrimEnd('\0');
            }

            H5T.close(typeId);
            H5D.close(dsetId);
            return result;
        }

        private static long GetMemType<T>()
        {
            if (typeof(T) == typeof(double)) return H5T.NATIVE_DOUBLE;
            if (typeof(T) == typeof(float)) return H5T.NATIVE_FLOAT;
            if (typeof(T) == typeof(int)) return H5T.NATIVE_INT;
            if (typeof(T) == typeof(long)) return H5T.NATIVE_LLONG;
            if (typeof(T) == typeof(ulong)) return H5T.NATIVE_ULLONG;
            return H5T.NATIVE_DOUBLE;
        }
    }
}