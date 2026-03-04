using MathNet.Numerics.Data.Matlab;
using MathNet.Numerics.LinearAlgebra;
using Nifti;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.IO;

namespace FnirsExe
{
    public class BrainInterpolationRenderer
    {
        private float[] _vertices;//存储脑表面模型的顶点坐标
        private uint[] _indices;//存储三角形面的索引
        private float[] _vertexColors;//存储每个顶点的颜色值
        private object _brainMask;//存储nifti脑掩模数据
        private bool _isInitialized = false;//标记OpenGL是否初始化成功

        private List<Vector3> _channelPositions = new List<Vector3>();//存储fnirs通道的3D位置
        private List<float> _channelValues = new List<float>();//存储每个通道对应的测量值

        private int _vao, _vbo, _ebo, _colorVbo;// _vao：顶点数组对象，管理所有顶点属性

        // ====== Realtime color mapping controls ======
        // 颜色映射动态范围（[-ColorScale, ColorScale]）。
        // BrainViewerForm 会在每次更新数据时根据当前通道值的幅度自动设置该值。
        // 设为较小值可让小幅度 Hb 变化更“显色”。
        public float ColorScale { get; set; } = 2.5f;

        // 值接近 0 时保持灰色的阈值（避免噪声把整脑染色）。
        public float ZeroEpsilon { get; set; } = 1e-6f;
        // _vbo：顶点缓冲区对象，存储顶点坐标
        // _ebo：元素缓冲区对象，存储三角形索引
        // _colorVbo：颜色缓冲区对象，存储顶点颜色

        public bool Initialize()
        {
            try
            {
                // 检查OpenGL上下文
                if (GL.GetString(StringName.Version) == null)
                {
                    Console.WriteLine("错误: OpenGL上下文未初始化");
                    return false;
                }

                Console.WriteLine($"OpenGL版本: {GL.GetString(StringName.Version)}");
                Console.WriteLine($"显卡: {GL.GetString(StringName.Renderer)}");

                _isInitialized = true;
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"OpenGL初始化检查失败: {ex.Message}");
                return false;
            }
        }

        public void LoadTemplates(string matFilePath, string niftiFilePath)
        {//分别加载脑表面模型和nifti参考数据
            LoadBrainSurface(matFilePath);//脑表面模型文件的地址
            if (!string.IsNullOrEmpty(niftiFilePath))
            {
                LoadBrainMask(niftiFilePath);//脑部参考图片文件的地址
            }
        }

        private void LoadBrainSurface(string matFilePath)
        {//脑表面加载
            try
            {
                // 使用回退方法加载脑表面数据
                var surface = MatParser.LoadBrainSurfaceWithFallback(matFilePath);

                //// 添加详细的数据验证
                //Console.WriteLine($"=== 脑表面数据验证 ===");
                //Console.WriteLine($"顶点数量: {surface.VertexCount}");
                //Console.WriteLine($"面数量: {surface.FaceCount}");
                //Console.WriteLine($"顶点数组长度: {surface.Vertices?.Length ?? 0}");
                //Console.WriteLine($"面数组长度: {surface.Faces?.Length ?? 0}");

                if (surface.Vertices == null || surface.Vertices.Length == 0)
                {
                    throw new Exception("顶点数据为空");
                }

                if (surface.Faces == null || surface.Faces.Length == 0)
                {
                    throw new Exception("面数据为空");
                }

                // 检查数据范围
                if (surface.VertexCount == 0 || surface.FaceCount == 0)
                {
                    throw new Exception("顶点或面数量为0");
                }

                _vertices = surface.Vertices;
                _indices = surface.Faces;
                _vertexColors = new float[surface.Vertices.Length];

                // 初始化颜色为默认值（灰色）
                for (int i = 0; i < _vertexColors.Length; i++)
                {
                    _vertexColors[i] = 0.6f; // 默认灰色
                }

                Console.WriteLine($"成功加载脑表面: {surface.VertexCount}顶点, {surface.FaceCount}面片");
            }
            catch (Exception ex)
            {
                throw new Exception($"加载脑表面失败: {ex.Message}", ex);
            }
        }

        private void LoadBrainMask(string niftiFilePath)
        {
            try
            {
                if (File.Exists(niftiFilePath))
                {
                    var (image, info) = NiftiParser.LoadNifti(niftiFilePath);
                    _brainMask = image; // 使用 object 类型
                    Console.WriteLine($"NIfTI参考文件已记录: {info.Dimensions[0]}x{info.Dimensions[1]}x{info.Dimensions[2]}");
                }
                else
                {
                    Console.WriteLine("NIfTI文件不存在，使用默认脑模板设置");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"加载NIfTI文件失败（可继续运行）: {ex.Message}");
            }
        }

        public void SetChannelData(List<Vector3> positions, List<float> values)
        {
            _channelPositions = positions ?? new List<Vector3>();//?? 操作符的意思是：如果左边为null，就用右边的新建空列表
            _channelValues = values ?? new List<float>();

            Console.WriteLine($"设置通道数据: {_channelPositions.Count}个位置, {_channelValues.Count}个值");

            if (_channelPositions.Count > 0)
            {
                InterpolateToSurface();
                UpdateColorBuffer();
            }
        }

        private void InterpolateToSurface()
        {
            if (_channelPositions.Count == 0 || _vertices == null) return;

            Console.WriteLine($"开始插值: {_channelPositions.Count}个通道位置, {_channelValues.Count}个值");

            // 检查是否有有效数据（避免用一个过大的阈值导致小幅度Hb变化始终显示灰色）
            bool hasValidData = false;
            for (int i = 0; i < _channelValues.Count; i++)
            {
                if (!float.IsNaN(_channelValues[i]) && !float.IsInfinity(_channelValues[i]) && Math.Abs(_channelValues[i]) > ZeroEpsilon)
                {
                    hasValidData = true;
                    break;
                }
            }

            Console.WriteLine($"是否有有效数据: {hasValidData}");

            for (int i = 0; i < _vertices.Length / 3; i++)
            {
                Vector3 vertex = new Vector3(
                    _vertices[i * 3],
                    _vertices[i * 3 + 1],
                    _vertices[i * 3 + 2]
                );

                // 获取当前顶点所在的脑区
                BrainRegions.Region vertexRegion = BrainRegions.GetRegionFromPosition(vertex);

                // 只有在有有效数据且顶点在有效脑区内时才进行插值，否则保持灰色
                if (hasValidData && vertexRegion != BrainRegions.Region.Unknown)
                {
                    float interpolatedValue = IDWInterpolation(vertex);

                    // 只有当插值结果足够大时才显示颜色，否则保持灰色
                    if (!float.IsNaN(interpolatedValue) && !float.IsInfinity(interpolatedValue) && Math.Abs(interpolatedValue) > ZeroEpsilon)
                    {
                        var color = ValueToColor(interpolatedValue);
                        _vertexColors[i * 3] = color.X;
                        _vertexColors[i * 3 + 1] = color.Y;
                        _vertexColors[i * 3 + 2] = color.Z;
                    }
                    else
                    {
                        // 插值结果为0，保持灰色
                        _vertexColors[i * 3] = 0.6f;
                        _vertexColors[i * 3 + 1] = 0.6f;
                        _vertexColors[i * 3 + 2] = 0.6f;
                    }
                }
                else
                {
                    // 没有有效数据或不在已知脑区时保持灰色
                    _vertexColors[i * 3] = 0.6f;
                    _vertexColors[i * 3 + 1] = 0.6f;
                    _vertexColors[i * 3 + 2] = 0.6f;
                }
            }

            Console.WriteLine("插值完成，颜色缓冲区已更新");
        }

        private float IDWInterpolation(Vector3 targetPoint, float power = 2.0f)
        {
            float numerator = 0f;
            float denominator = 0f;
            const float distanceEps = 0.001f;
            bool hasNonZeroData = false;

            for (int i = 0; i < _channelPositions.Count; i++)
            {
                // 如果通道值接近0（或为无效值），跳过该通道的插值计算
                if (float.IsNaN(_channelValues[i]) || float.IsInfinity(_channelValues[i]) || Math.Abs(_channelValues[i]) < ZeroEpsilon)
                    continue;

                hasNonZeroData = true;
                float distance = Vector3.Distance(targetPoint, _channelPositions[i]);
                if (distance < distanceEps)
                    return _channelValues[i];

                float weight = 1.0f / (float)Math.Pow(distance, power);
                numerator += weight * _channelValues[i];
                denominator += weight;
            }

            // 如果没有非零数据，返回0（将显示为灰色）
            return hasNonZeroData ? (denominator > 0 ? numerator / denominator : 0f) : 0f;
        }

        private Vector3 ValueToColor(float value)
        {
            // 如果值为0（或接近0），返回灰色
            if (float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) < ZeroEpsilon)
            {
                return new Vector3(0.6f, 0.6f, 0.6f); // 灰色
            }

            // 将值归一化到 [0,1] 范围用于颜色映射
            float scale = Math.Max(ZeroEpsilon, ColorScale);
            value = Math.Max(-scale, Math.Min(scale, value)); // 动态范围

            // 将值从 [-scale, scale] 映射到 [0, 1]
            float normalizedValue = (value + scale) / (2.0f * scale);

            // 使用热力图颜色映射：蓝色（低值）到红色（高值）
            if (normalizedValue < 0.25f)
            {
                // 蓝色到青色：0.0-0.25
                float t = normalizedValue / 0.25f;
                return new Vector3(0.0f, t, 1.0f);
            }
            else if (normalizedValue < 0.5f)
            {
                // 青色到绿色：0.25-0.5
                float t = (normalizedValue - 0.25f) / 0.25f;
                return new Vector3(0.0f, 1.0f, 1.0f - t);
            }
            else if (normalizedValue < 0.75f)
            {
                // 绿色到黄色：0.5-0.75
                float t = (normalizedValue - 0.5f) / 0.25f;
                return new Vector3(t, 1.0f, 0.0f);
            }
            else
            {
                // 黄色到红色：0.75-1.0
                float t = (normalizedValue - 0.75f) / 0.25f;
                return new Vector3(1.0f, 1.0f - t, 0.0f);
            }
        }

        private void UpdateColorBuffer()
        {
            if (_colorVbo != 0 && _vertexColors != null)
            {
                GL.BindBuffer(BufferTarget.ArrayBuffer, _colorVbo);
                GL.BufferData(BufferTarget.ArrayBuffer, _vertexColors.Length * sizeof(float),
                            _vertexColors, BufferUsageHint.DynamicDraw);
            }
        }

        public void Setup()
        {
            if (_vao != 0)
            {
                Console.WriteLine("OpenGL缓冲区已初始化，跳过重复设置");
                return;
            }
            try
            {
                Console.WriteLine("开始设置OpenGL缓冲区...");
                CheckOpenGLContext();
                if (_vertices == null || _vertices.Length == 0)
                {
                    throw new InvalidOperationException("顶点数据未加载或为空");
                }

                // 生成VAO
                _vao = GL.GenVertexArray();
                Console.WriteLine($"生成VAO成功:{_vao}");
                GL.BindVertexArray(_vao);

                // 设置顶点缓冲区
                _vbo = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
                int verticesSize = _vertices.Length * sizeof(float);
                Console.WriteLine($"顶点缓冲区大小: {verticesSize} 字节, 顶点数: {_vertices.Length / 3}");

                GL.BufferData(BufferTarget.ArrayBuffer, verticesSize, _vertices, BufferUsageHint.StaticDraw);
                GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
                GL.EnableVertexAttribArray(0);

                // 设置颜色缓冲区
                _colorVbo = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ArrayBuffer, _colorVbo);
                int colorsSize = _vertexColors.Length * sizeof(float);
                Console.WriteLine($"颜色缓冲区大小: {colorsSize} 字节");

                GL.BufferData(BufferTarget.ArrayBuffer, colorsSize, _vertexColors, BufferUsageHint.DynamicDraw);
                GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 3 * sizeof(float), 0);
                GL.EnableVertexAttribArray(1);

                // 设置元素缓冲区（索引）
                _ebo = GL.GenBuffer();
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
                int indicesSize = _indices.Length * sizeof(uint);
                Console.WriteLine($"索引缓冲区大小: {indicesSize} 字节, 三角形数: {_indices.Length / 3}");

                GL.BufferData(BufferTarget.ElementArrayBuffer, indicesSize, _indices, BufferUsageHint.StaticDraw);

                // 解绑
                GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
                GL.BindVertexArray(0);

                Console.WriteLine("OpenGL缓冲区设置完成");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"设置OpenGL缓冲区时出错: {ex.Message}");

                // 清理资源
                Cleanup();
                throw;
            }
        }

        private void CheckOpenGLContext()
        {
            try
            {
                string version = GL.GetString(StringName.Version);
                string renderer = GL.GetString(StringName.Renderer);

                if (string.IsNullOrEmpty(version))
                {
                    throw new InvalidOperationException("OpenGL上下文无效");
                }

                Console.WriteLine($"OpenGL上下文验证成功: {version}, {renderer}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"OpenGL上下文检查失败: {ex.Message}");
            }
        }

        public void Render(Matrix4 view, Matrix4 projection, Matrix4 model)
        {
            try
            {
                if (_vertices == null || _vertices.Length == 0)
                {
                    Console.WriteLine("警告: 无可渲染的顶点数据");
                    return;
                }

                if (_vao == 0)
                {
                    Console.WriteLine("警告: VAO未初始化");
                    return;
                }

                GL.BindVertexArray(_vao);

                var shader = ShaderManager.GetBrainShader();
                shader.Use();
                shader.SetMatrix4("view", view);
                shader.SetMatrix4("projection", projection);
                shader.SetMatrix4("model", model);

                // 确保使用填充模式渲染，而不是线框模式
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

                GL.DrawElements(PrimitiveType.Triangles, _indices.Length, DrawElementsType.UnsignedInt, 0);
                GL.BindVertexArray(0);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"渲染时出错: {ex.Message}");
            }
        }

        public void Cleanup()
        {
            if (_vao != 0)
            {
                GL.DeleteVertexArray(_vao);
                _vao = 0;
            }
            if (_vbo != 0)
            {
                GL.DeleteBuffer(_vbo);
                _vbo = 0;
            }
            if (_ebo != 0)
            {
                GL.DeleteBuffer(_ebo);
                _ebo = 0;
            }
            if (_colorVbo != 0)
            {
                GL.DeleteBuffer(_colorVbo);
                _colorVbo = 0;
            }
        }

        public bool IsSurfaceLoaded()
        {
            return _vertices != null && _vertices.Length > 0;
        }

        /// <summary>
        /// 检查是否已加载NIfTI参考数据
        /// </summary>
        public bool IsNiftiLoaded()
        {
            return _brainMask != null;
        }

        /// <summary>
        /// 获取脑表面统计信息
        /// </summary>
        public (int vertexCount, int faceCount) GetSurfaceStats()
        {
            if (_vertices == null)
                return (0, 0);

            return (_vertices.Length / 3, _indices.Length / 3);
        }
    }
}