using System;
using System.IO;

namespace FnirsExe
{
    /// <summary>
    /// NIfTI 文件解析器 - 简化版本
    /// 避免NIfTI库依赖问题，提供基本的空间参考功能
    /// </summary>
    public static class NiftiParser
    {
        /// <summary>
        /// NIfTI 数据信息
        /// </summary>
        public class NiftiInfo
        {
            public int[] Dimensions { get; set; }    // 图像尺寸 [X, Y, Z, ...]
            public float[] VoxelSize { get; set; }   // 体素大小 [mm]
            public string DataType { get; set; }     // 数据类型
            public long VoxelCount { get; set; }     // 体素总数
            public string Description { get; set; }  // 描述信息

            public NiftiInfo()
            {
                Dimensions = new int[3];
                VoxelSize = new float[3];
                DataType = "Unknown";
                Description = "No NIfTI file loaded";
            }
        }

        /// <summary>
        /// 加载NIfTI文件并返回图像对象和信息 - 简化版本
        /// </summary>
        /// <param name="filePath">NIfTI文件路径</param>
        /// <returns>图像对象和信息</returns>
        public static (object image, NiftiInfo info) LoadNifti(string filePath)
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"NIfTI文件不存在: {filePath}");
                return (null, CreateDefaultInfo("File not found"));
            }

            try
            {
                Console.WriteLine($"检测到NIfTI文件: {Path.GetFileName(filePath)}");
                Console.WriteLine("注意: 当前使用简化的NIfTI处理，主要功能依赖.mat文件");

                // 获取文件信息
                FileInfo fileInfo = new FileInfo(filePath);
                string fileSize = FormatFileSize(fileInfo.Length);

                // 创建信息对象
                var info = CreateDefaultInfo($"NIfTI参考文件: {Path.GetFileName(filePath)}");
                info.Description = $"NIfTI参考文件: {Path.GetFileName(filePath)} ({fileSize})";

                // 返回占位对象和信息
                return (new object(), info);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"NIfTI文件处理警告: {ex.Message}");
                return (null, CreateDefaultInfo($"Error: {ex.Message}"));
            }
        }

        /// <summary>
        /// 创建默认的脑模板信息
        /// </summary>
        /// <param name="description">描述信息</param>
        /// <returns>NIfTI信息对象</returns>
        private static NiftiInfo CreateDefaultInfo(string description = "")
        {
            return new NiftiInfo
            {
                Dimensions = new int[] { 181, 217, 181 }, // 标准MNI脑模板尺寸
                VoxelSize = new float[] { 1.0f, 1.0f, 1.0f },
                DataType = "Template Reference",
                VoxelCount = 181 * 217 * 181,
                Description = string.IsNullOrEmpty(description) ? "Standard MNI brain template reference" : description
            };
        }

        /// <summary>
        /// 格式化文件大小
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB" };
            int counter = 0;
            decimal number = bytes;

            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }

            return $"{number:n1} {suffixes[counter]}";
        }

        /// <summary>
        /// 检查是否为脑掩模文件（简化版本）
        /// </summary>
        /// <param name="image">图像对象</param>
        /// <returns>总是返回true（简化实现）</returns>
        public static bool IsBrainMask(object image)
        {
            // 简化实现，假设是脑掩模文件
            return true;
        }

        /// <summary>
        /// 获取图像数据统计信息（简化版本）
        /// </summary>
        /// <param name="image">图像对象</param>
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="mean">平均值</param>
        public static void GetImageStatistics(object image, out float min, out float max, out float mean)
        {
            // 返回模拟的统计信息
            min = 0f;
            max = 1f;
            mean = 0.3f;
        }

        /// <summary>
        /// 将MNI坐标转换为体素索引（基于标准MNI空间）
        /// </summary>
        /// <param name="mniX">MNI X坐标</param>
        /// <param name="mniY">MNI Y坐标</param>
        /// <param name="mniZ">MNI Z坐标</param>
        /// <returns>体素索引 [x, y, z]</returns>
        public static int[] MniToVoxel(float mniX, float mniY, float mniZ)
        {
            // 基于标准MNI空间的坐标转换
            // MNI空间范围: X: -90 to +90, Y: -126 to +90, Z: -72 to +108
            int[] voxel = new int[3];

            // 转换为体素坐标（基于181x217x181的MNI模板）
            voxel[0] = (int)((mniX + 90) / 1.0f);   // X: -90 to +90 -> 0 to 180
            voxel[1] = (int)((mniY + 126) / 1.0f);  // Y: -126 to +90 -> 0 to 216
            voxel[2] = (int)((mniZ + 72) / 1.0f);   // Z: -72 to +108 -> 0 to 180

            // 确保索引在有效范围内
            voxel[0] = Math.Max(0, Math.Min(180, voxel[0]));
            voxel[1] = Math.Max(0, Math.Min(216, voxel[1]));
            voxel[2] = Math.Max(0, Math.Min(180, voxel[2]));

            return voxel;
        }

        /// <summary>
        /// 在指定MNI坐标处获取模拟的体素值
        /// </summary>
        /// <param name="mniX">MNI X坐标</param>
        /// <param name="mniY">MNI Y坐标</param>
        /// <param name="mniZ">MNI Z坐标</param>
        /// <returns>模拟的体素值</returns>
        public static float GetValueAtMni(float mniX, float mniY, float mniZ)
        {
            try
            {
                var voxel = MniToVoxel(mniX, mniY, mniZ);

                // 计算到脑中心的距离（模拟脑激活模式）
                float centerX = 90f;  // 脑中心X
                float centerY = 108f; // 脑中心Y  
                float centerZ = 90f;  // 脑中心Z

                float distanceFromCenter = (float)Math.Sqrt(
                    Math.Pow(voxel[0] - centerX, 2) +
                    Math.Pow(voxel[1] - centerY, 2) +
                    Math.Pow(voxel[2] - centerZ, 2)
                );

                // 模拟激活值：距离中心越近，值越大
                float activation = Math.Max(0, 1.0f - distanceFromCenter / 100.0f);

                // 添加一些随机变化模拟真实数据
                Random rand = new Random((voxel[0] * 397) ^ (voxel[1] * 571) ^ (voxel[2] * 739));
                activation += (float)(rand.NextDouble() * 0.2 - 0.1); // ±0.1的随机变化

                return Math.Max(0, Math.Min(1, activation));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"获取MNI坐标值失败: {ex.Message}");
                return 0f;
            }
        }

        /// <summary>
        /// 在指定体素坐标处获取值（简化版本）
        /// </summary>
        /// <param name="x">体素X坐标</param>
        /// <param name="y">体素Y坐标</param>
        /// <param name="z">体素Z坐标</param>
        /// <returns>模拟的体素值</returns>
        public static float GetValueAtVoxel(int x, int y, int z)
        {
            // 将体素坐标转换回MNI坐标
            float mniX = x - 90f;
            float mniY = y - 126f;
            float mniZ = z - 72f;

            return GetValueAtMni(mniX, mniY, mniZ);
        }

        /// <summary>
        /// 获取标准MNI脑模板的信息
        /// </summary>
        /// <returns>MNI模板信息</returns>
        public static NiftiInfo GetStandardMniInfo()
        {
            return new NiftiInfo
            {
                Dimensions = new int[] { 181, 217, 181 },
                VoxelSize = new float[] { 1.0f, 1.0f, 1.0f },
                DataType = "MNI152 Template",
                VoxelCount = 181 * 217 * 181,
                Description = "Standard MNI152 nonlinear asymmetric brain template"
            };
        }

        /// <summary>
        /// 验证MNI坐标是否在有效范围内
        /// </summary>
        /// <param name="mniX">MNI X坐标</param>
        /// <param name="mniY">MNI Y坐标</param>
        /// <param name="mniZ">MNI Z坐标</param>
        /// <returns>是否在有效范围内</returns>
        public static bool IsValidMniCoordinate(float mniX, float mniY, float mniZ)
        {
            return mniX >= -90 && mniX <= 90 &&
                   mniY >= -126 && mniY <= 90 &&
                   mniZ >= -72 && mniZ <= 108;
        }

        /// <summary>
        /// 获取MNI坐标空间的边界信息
        /// </summary>
        /// <returns>边界信息字符串</returns>
        public static string GetMniSpaceBounds()
        {
            return "MNI坐标空间范围:\n" +
                   $"X: -90 到 +90 mm\n" +
                   $"Y: -126 到 +90 mm\n" +
                   $"Z: -72 到 +108 mm\n" +
                   $"体素尺寸: 1.0 x 1.0 x 1.0 mm\n" +
                   $"模板尺寸: 181 x 217 x 181 体素";
        }

        /// <summary>
        /// 获取常见的fNIRS通道MNI坐标（示例数据）
        /// </summary>
        /// <returns>通道位置列表</returns>
        public static (string region, float x, float y, float z)[] GetCommonFnirsChannels()
        {
            return new (string, float, float, float)[]
            {
                ("前额叶左侧", -40f, 50f, 30f),
                ("前额叶右侧", 40f, 50f, 30f),
                ("运动皮层左侧", -50f, 0f, 60f),
                ("运动皮层右侧", 50f, 0f, 60f),
                ("顶叶左侧", -30f, -60f, 50f),
                ("顶叶右侧", 30f, -60f, 50f),
                ("枕叶左侧", -20f, -90f, 10f),
                ("枕叶右侧", 20f, -90f, 10f)
            };
        }

        /// <summary>
        /// 获取脑区对应的常见MNI坐标
        /// </summary>
        /// <param name="region">脑区名称</param>
        /// <returns>MNI坐标</returns>
        public static (float x, float y, float z) GetRegionCoordinates(string region)
        {
            var channels = GetCommonFnirsChannels();
            foreach (var channel in channels)
            {
                if (channel.region == region)
                    return (channel.x, channel.y, channel.z);
            }

            // 默认返回脑中心坐标
            return (0f, 0f, 0f);
        }

        /// <summary>
        /// 获取所有脑区名称
        /// </summary>
        /// <returns>脑区名称数组</returns>
        public static string[] GetBrainRegions()
        {
            var channels = GetCommonFnirsChannels();
            string[] regions = new string[channels.Length];
            for (int i = 0; i < channels.Length; i++)
            {
                regions[i] = channels[i].region;
            }
            return regions;
        }
    }
}