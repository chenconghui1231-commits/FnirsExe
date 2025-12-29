using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Data.Matlab;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace FnirsExe
{
    /// <summary>
    /// MATLAB .mat 文件解析器
    /// 用于解析脑表面网格数据
    /// </summary>
    public static class MatParser
    {
        /// <summary>
        /// 脑表面网格数据结构
        /// </summary>
        public class BrainSurface
        {
            public float[] Vertices { get; set; }  // 顶点坐标数组 [x1, y1, z1, x2, y2, z2, ...]
            public uint[] Faces { get; set; }      // 面索引数组 [f1a, f1b, f1c, f2a, f2b, f2c, ...]
            public int VertexCount { get; set; }   // 顶点数量
            public int FaceCount { get; set; }     // 面数量

            public BrainSurface()
            {
                Vertices = Array.Empty<float>();
                Faces = Array.Empty<uint>();
            }
        }

        /// <summary>
        /// 从.mat文件加载脑表面数据（支持多种数据类型）
        /// </summary>
        /// <param name="filePath">.mat文件路径</param>
        /// <returns>脑表面数据</returns>
        public static BrainSurface LoadBrainSurface(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"找不到MAT文件: {filePath}");

            try
            {
                Console.WriteLine($"开始解析MAT文件: {filePath}");

                // 尝试多种数据类型
                Dictionary<string, Matrix<double>> matDict = null;

                try
                {
                    // 首先尝试double类型
                    matDict = MatlabReader.ReadAll<double>(filePath);
                    Console.WriteLine("使用double类型解析成功");
                }
                catch (Exception ex1)
                {
                    Console.WriteLine($"double类型解析失败: {ex1.Message}");

                    try
                    {
                        // 尝试float类型（因为顶点数据是single）
                        var floatDict = MatlabReader.ReadAll<float>(filePath);
                        matDict = ConvertDictionary(floatDict);
                        Console.WriteLine("使用float类型解析成功");
                    }
                    catch (Exception ex2)
                    {
                        Console.WriteLine($"float类型解析失败: {ex2.Message}");

                        // 尝试int类型（因为面数据是int32）
                        var intDict = MatlabReader.ReadAll<int>(filePath);
                        matDict = ConvertDictionary(intDict);
                        Console.WriteLine("使用int类型解析成功");
                    }
                }

                Console.WriteLine($"找到 {matDict.Count} 个数据变量");
                foreach (var key in matDict.Keys)
                {
                    Console.WriteLine($"变量: {key}, 尺寸: {matDict[key].RowCount}x{matDict[key].ColumnCount}");
                }

                // 查找vertices和faces
                var (verticesMat, facesMat) = FindVerticesAndFaces(matDict);
                return ConvertToBrainSurface(verticesMat, facesMat);
            }
            catch (Exception ex)
            {
                throw new Exception($"解析MAT文件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 将其他类型的矩阵字典转换为double类型
        /// </summary>
        private static Dictionary<string, Matrix<double>> ConvertDictionary<T>(Dictionary<string, Matrix<T>> originalDict)
            where T : struct, IEquatable<T>, IFormattable
        {
            var result = new Dictionary<string, Matrix<double>>();
            foreach (var kvp in originalDict)
            {
                var originalMatrix = kvp.Value;
                var doubleMatrix = Matrix<double>.Build.Dense(originalMatrix.RowCount, originalMatrix.ColumnCount);

                // 手动复制数据并转换类型
                for (int i = 0; i < originalMatrix.RowCount; i++)
                {
                    for (int j = 0; j < originalMatrix.ColumnCount; j++)
                    {
                        doubleMatrix[i, j] = Convert.ToDouble(originalMatrix[i, j]);
                    }
                }

                result[kvp.Key] = doubleMatrix;
            }
            return result;
        }

        /// <summary>
        /// 在数据字典中查找顶点和面数据
        /// </summary>
        private static (Matrix<double> vertices, Matrix<double> faces) FindVerticesAndFaces(
            Dictionary<string, Matrix<double>> matDict)
        {
            Matrix<double> verticesMat = null;
            Matrix<double> facesMat = null;

            // 尝试常见的变量名组合
            var vertexNames = new[] { "vertices", "Vertices", "vertex", "Vertex", "surf_vertices", "brain_vertices" };
            var faceNames = new[] { "faces", "Faces", "face", "Face", "surf_faces", "brain_faces", "tri" };

            foreach (var vName in vertexNames)
            {
                if (matDict.ContainsKey(vName))
                {
                    verticesMat = matDict[vName];
                    Console.WriteLine($"使用顶点变量: {vName}");
                    break;
                }
            }

            foreach (var fName in faceNames)
            {
                if (matDict.ContainsKey(fName))
                {
                    facesMat = matDict[fName];
                    Console.WriteLine($"使用面变量: {fName}");
                    break;
                }
            }

            // 如果没找到标准名称，尝试使用第一个合适的矩阵
            if (verticesMat == null || facesMat == null)
            {
                foreach (var kvp in matDict)
                {
                    var matrix = kvp.Value;
                    if (verticesMat == null && matrix.ColumnCount == 3)
                    {
                        verticesMat = matrix;
                        Console.WriteLine($"自动选择顶点变量: {kvp.Key}");
                    }
                    else if (facesMat == null && matrix.ColumnCount == 3)
                    {
                        facesMat = matrix;
                        Console.WriteLine($"自动选择面变量: {kvp.Key}");
                    }
                }
            }

            if (verticesMat == null)
                throw new Exception("在MAT文件中找不到顶点数据");
            if (facesMat == null)
                throw new Exception("在MAT文件中找不到面数据");

            return (verticesMat, facesMat);
        }

        /// <summary>
        /// 将MathNet矩阵转换为脑表面数据结构
        /// </summary>
        private static BrainSurface ConvertToBrainSurface(Matrix<double> verticesMat, Matrix<double> facesMat)
        {
            var surface = new BrainSurface();

            // 转换顶点数据
            surface.VertexCount = verticesMat.RowCount;
            surface.Vertices = new float[verticesMat.RowCount * 3];

            for (int i = 0; i < verticesMat.RowCount; i++)
            {
                surface.Vertices[i * 3] = (float)verticesMat[i, 0];     // X
                surface.Vertices[i * 3 + 1] = (float)verticesMat[i, 1]; // Y
                surface.Vertices[i * 3 + 2] = (float)verticesMat[i, 2]; // Z
            }

            // 转换面数据 - 重要修改：索引从0开始，不需要减1！
            surface.FaceCount = facesMat.RowCount;
            surface.Faces = new uint[facesMat.RowCount * 3];

            for (int i = 0; i < facesMat.RowCount; i++)
            {
                // 注意：现在索引是从0开始的，不需要减1！
                surface.Faces[i * 3] = (uint)facesMat[i, 0];
                surface.Faces[i * 3 + 1] = (uint)facesMat[i, 1];
                surface.Faces[i * 3 + 2] = (uint)facesMat[i, 2];
            }

            Console.WriteLine($"成功转换脑表面数据: {surface.VertexCount}个顶点, {surface.FaceCount}个面");

            // 验证数据
            ValidateSurfaceData(surface);

            return surface;
        }

        /// <summary>
        /// 验证表面数据有效性
        /// </summary>
        private static void ValidateSurfaceData(BrainSurface surface)
        {
            // 检查顶点索引范围
            uint maxVertexIndex = 0;
            foreach (var index in surface.Faces)
            {
                if (index > maxVertexIndex)
                    maxVertexIndex = index;
            }

            if (maxVertexIndex >= surface.VertexCount)
            {
                Console.WriteLine($"警告: 面索引超出顶点范围 (最大索引: {maxVertexIndex}, 顶点数: {surface.VertexCount})");
            }

            // 检查顶点坐标范围
            float minX = float.MaxValue, maxX = float.MinValue;
            float minY = float.MaxValue, maxY = float.MinValue;
            float minZ = float.MaxValue, maxZ = float.MinValue;

            for (int i = 0; i < surface.Vertices.Length; i += 3)
            {
                minX = Math.Min(minX, surface.Vertices[i]);
                maxX = Math.Max(maxX, surface.Vertices[i]);
                minY = Math.Min(minY, surface.Vertices[i + 1]);
                maxY = Math.Max(maxY, surface.Vertices[i + 1]);
                minZ = Math.Min(minZ, surface.Vertices[i + 2]);
                maxZ = Math.Max(maxZ, surface.Vertices[i + 2]);
            }

            Console.WriteLine($"顶点坐标范围: X[{minX:F1}, {maxX:F1}], Y[{minY:F1}, {maxY:F1}], Z[{minZ:F1}, {maxZ:F1}]");
        }

        /// <summary>
        /// 加载脑表面数据（自动尝试转换后的文件）
        /// </summary>
        public static BrainSurface LoadBrainSurfaceWithFallback(string filePath)
        {
            try
            {
                Console.WriteLine($"尝试加载原始文件: {filePath}");
                return LoadBrainSurface(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"原始文件加载失败: {ex.Message}");

                // 尝试加载转换后的文件
                string directory = Path.GetDirectoryName(filePath);
                string filename = Path.GetFileNameWithoutExtension(filePath);
                string extension = Path.GetExtension(filePath);
                string convertedPath = Path.Combine(directory, $"{filename}_converted{extension}");

                if (File.Exists(convertedPath))
                {
                    Console.WriteLine($"尝试加载转换后的文件: {convertedPath}");
                    return LoadBrainSurface(convertedPath);
                }
                else
                {
                    throw new Exception($"原始文件加载失败且找不到转换后的文件: {convertedPath}");
                }
            }
        }

        /// <summary>
        /// 获取.mat文件中的变量列表
        /// </summary>
        public static List<string> GetVariableNames(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"找不到MAT文件: {filePath}");

            var matDict = MatlabReader.ReadAll<double>(filePath);
            return new List<string>(matDict.Keys);
        }

        /// <summary>
        /// 从.mat文件获取特定变量
        /// </summary>
        public static Matrix<double> GetVariable(string filePath, string variableName)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"找不到MAT文件: {filePath}");

            var matDict = MatlabReader.ReadAll<double>(filePath);
            if (matDict.ContainsKey(variableName))
                return matDict[variableName];
            else
                throw new Exception($"在MAT文件中找不到变量: {variableName}");
        }
    }
}