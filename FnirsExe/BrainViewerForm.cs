using OpenTK;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FnirsExe
{
    public partial class BrainViewerForm : Form
    {
        // 3D脑图像相关字段
        private BrainInterpolationRenderer _brainRendererHbO;
        private BrainInterpolationRenderer _brainRendererHbR;
        private BrainInterpolationRenderer _brainRendererHbT;

        // 数据存储
        private Dictionary<int, List<double>> hboValuesByChannel = new Dictionary<int, List<double>>();
        private Dictionary<int, List<double>> hbrValuesByChannel = new Dictionary<int, List<double>>();
        private Dictionary<int, List<double>> hbtValuesByChannel = new Dictionary<int, List<double>>();

        // 颜色映射范围
        private readonly double[] valueRange = new double[] { -2.5, -1.3, 0.0, 1.3, 2.6 };

        // 公开事件，用于从Image窗体接收数据
        public event Action<List<double[]>> OnOxygenDataReceived;

        public BrainViewerForm()
        {
            InitializeComponent();
            InitializeDataStructures();
            InitializeBrainViewers();
        }

        private void InitializeDataStructures()
        {
            // 初始化8个通道的数据结构
            for (int i = 1; i <= 8; i++)
            {
                hboValuesByChannel[i] = new List<double>();
                hbrValuesByChannel[i] = new List<double>();
                hbtValuesByChannel[i] = new List<double>();
            }
        }

        private void InitializeBrainViewers()
        {
            try
            {//为三种血红蛋白参数分别创建独立的渲染器
                _brainRendererHbO = new BrainInterpolationRenderer();
                _brainRendererHbR = new BrainInterpolationRenderer();
                _brainRendererHbT = new BrainInterpolationRenderer();

                string matFile = FindExistingFile("surf_mni_icbm152_gm_tal_nlin_sym_09a.mat");
                string niftiFile = FindExistingFile("mni_icbm152_gm_tal_nlin_sym_09a_mask.nii");

                if (matFile == null)
                {
                    MessageBox.Show("找不到脑表面模板文件，3D脑图像功能将不可用", "警告",
                                  MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                //为三个渲染器加载相同的脑模型模板
                _brainRendererHbO.LoadTemplates(matFile, niftiFile);
                _brainRendererHbR.LoadTemplates(matFile, niftiFile);
                _brainRendererHbT.LoadTemplates(matFile, niftiFile);
                //将渲染器与对应的UI控件绑定
                SetupGLControl(glControlHbO, _brainRendererHbO, "HbO");
                SetupGLControl(glControlHbR, _brainRendererHbR, "HbR");
                SetupGLControl(glControlHbT, _brainRendererHbT, "HbT");

                AddSampleChannelData();//初始时显示灰色的脑模型

                Console.WriteLine("脑图像窗体初始化成功");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"脑图像窗体初始化失败: {ex.Message}", "错误",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SetupGLControl(GLControl glControl, BrainInterpolationRenderer renderer, string type)
        {
            if (glControl != null)//确保传入的GLControl对象不为空
            {
                glControl.VSync = false;

                // 绑定事件
                glControl.Paint += (s, e) => GlControl_Paint(s, e, renderer, type);//绑定绘制事件
                glControl.Resize += GlControl_Resize;
                glControl.Load += GlControl_Load;
            }
        }

        private void GlControl_Load(object sender, EventArgs e)
        {
            Console.WriteLine("GLControl加载事件触发");
            var glControl = sender as GLControl;//sender 是事件源对象，需要转换为具体的GLControl类型
            if (glControl != null)
            {
                try
                {
                    glControl.MakeCurrent();//设置当前OpenGL上下文
                    //获取OpenGL信息
                    string version = GL.GetString(StringName.Version);
                    string rendererStr = GL.GetString(StringName.Renderer);
                    Console.WriteLine($"OpenGL上下文: {version}, 显卡: {rendererStr}");
                    //加载和编译着色器程序
                    ShaderManager.GetBrainShader();
                    //建立控件与渲染器的对应关系
                    BrainInterpolationRenderer brainRenderer = null;
                    if (glControl == glControlHbO) brainRenderer = _brainRendererHbO;
                    else if (glControl == glControlHbR) brainRenderer = _brainRendererHbR;
                    else if (glControl == glControlHbT) brainRenderer = _brainRendererHbT;

                    if (brainRenderer != null && brainRenderer.IsSurfaceLoaded())//确保找到了对应的渲染器和脑模型数据已加载完成
                    {
                        Console.WriteLine("开始设置OpenGL缓冲区...");
                        brainRenderer.Setup();
                        Console.WriteLine("OpenGL缓冲区设置完成");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"GLControl加载失败: {ex.Message}");
                }
            }
        }

        private string FindExistingFile(string fileName)
        {//脑模型文件查找
            string[] possiblePaths = new[]
            {
                Path.Combine("Resources", fileName),
                Path.Combine("..", "..", "Resources", fileName),
                fileName,
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", fileName),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName)
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    Console.WriteLine($"找到文件: {Path.GetFullPath(path)}");
                    return path;
                }
            }

            Console.WriteLine($"未找到文件: {fileName}");
            return null;
        }

        private void AddSampleChannelData()
        {
            var positions = new List<Vector3>
            {
                new Vector3(-40, 30, 50),   // 通道1
                new Vector3(40, 30, 50),    // 通道2  
                new Vector3(-50, 0, 60),    // 通道3
                new Vector3(50, 0, 60),     // 通道4
                new Vector3(-30, -40, 40),  // 通道5
                new Vector3(30, -40, 40),   // 通道6
                new Vector3(-40, -20, 30),  // 通道7
                new Vector3(40, -20, 30)    // 通道8
            };

            // 为所有通道创建初始的血氧浓度值
            var valuesHbO = new List<float> { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
            var valuesHbR = new List<float> { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
            var valuesHbT = new List<float> { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };
            //将初始数据传递给三个独立的渲染器
            _brainRendererHbO?.SetChannelData(positions, valuesHbO);
            _brainRendererHbR?.SetChannelData(positions, valuesHbR);
            _brainRendererHbT?.SetChannelData(positions, valuesHbT);

            Console.WriteLine("设置初始示例数据为灰色");
        }

        private void GlControl_Resize(object sender, EventArgs e)
        {
            var glControl = sender as GLControl;
            if (glControl != null && glControl.ClientSize.Width > 0 && glControl.ClientSize.Height > 0)
            {
                glControl.MakeCurrent();
                GL.Viewport(0, 0, glControl.ClientSize.Width, glControl.ClientSize.Height);
                glControl.Invalidate();
            }
        }

        private void GlControl_Paint(object sender, PaintEventArgs e, BrainInterpolationRenderer renderer, string type)
        {
            if (renderer == null)
            {
                Console.WriteLine($"渲染器未初始化 for {type}");
                return;
            }
            //获取当前的GLControl实例，需要具体的控件来执行OpenGL操作
            var glControl = sender as GLControl;
            if (glControl == null) return;

            try
            {
                glControl.MakeCurrent();
                GL.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);
                GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

                // 启用深度测试
                GL.Enable(EnableCap.DepthTest);
                GL.DepthFunc(DepthFunction.Less);

                // 禁用背面剔除，渲染双面以避免虚线
                GL.Disable(EnableCap.CullFace);

                // 确保使用填充模式
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

                var view = Matrix4.LookAt(
                    eye: new OpenTK.Vector3(0, 300, 350),
                    target: OpenTK.Vector3.Zero,
                    up: new OpenTK.Vector3(0, 0, 1)
                );

                float aspectRatio = (float)glControl.ClientSize.Width / glControl.ClientSize.Height;
                var projection = Matrix4.CreatePerspectiveFieldOfView(
                    OpenTK.MathHelper.DegreesToRadians(45f),
                    aspectRatio,
                    0.1f,
                    1000.0f
                );

                var model = Matrix4.Identity;

                renderer.Render(view, projection, model);
                glControl.SwapBuffers();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{type} 3D渲染错误: {ex.Message}");
            }
        }

        // 颜色渐变条绘制
        private void colorBarPanel_Paint(object sender, PaintEventArgs e)
        {
            Panel panel = sender as Panel;
            if (panel == null) return;

            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;//抗锯齿，颜色过渡更平滑
            g.Clear(panel.BackColor);

            // 去除边距，让颜色条完全贴合panel边界
            int barWidth = panel.Width;
            int barHeight = panel.Height;
            int barX = 0;
            int barY = 0;

            // 创建从蓝色到红色的线性渐变
            using (LinearGradientBrush brush = new LinearGradientBrush(
                new Point(barX, barY),
                new Point(barX, barY + barHeight),
                Color.Blue,
                Color.Red))
            {
                // 设置颜色混合位置
                ColorBlend colorBlend = new ColorBlend();
                colorBlend.Positions = new float[] { 0.0f, 0.25f, 0.5f, 0.75f, 1.0f };
                colorBlend.Colors = new Color[]
                {
                    Color.Red,           // 2.6
                    Color.Yellow,         // 1.3
                    Color.Green,          // 0.0
                    Color.Cyan,           // -1.3
                    Color.Blue           // -2.5
                    
                };
                brush.InterpolationColors = colorBlend;

                // 绘制渐变条，完全填充panel
                g.FillRectangle(brush, barX, barY, barWidth, barHeight);
            }
        }

        // 根据浓度值获取对应的颜色
        public Color GetColorForValue(double value)
        {
            // 确保值在范围内
            double minValue = valueRange[0];
            double maxValue = valueRange[valueRange.Length - 1];

            if (value <= minValue) return Color.Blue;
            if (value >= maxValue) return Color.Red;

            // 找到值所在的范围
            for (int i = 0; i < valueRange.Length - 1; i++)
            {
                if (value >= valueRange[i] && value <= valueRange[i + 1])
                {
                    double range = valueRange[i + 1] - valueRange[i];
                    double position = (value - valueRange[i]) / range;

                    // 根据位置在对应的颜色间插值
                    return InterpolateColor(GetColorForRange(i), GetColorForRange(i + 1), position);
                }
            }

            return Color.Gray; // 默认颜色
        }

        // 获取对应范围的颜色
        private Color GetColorForRange(int index)
        {
            switch (index)
            {
                case 0: return Color.Blue;     // -2.5
                case 1: return Color.Cyan;     // -1.3
                case 2: return Color.Green;    // 0.0
                case 3: return Color.Yellow;   // 1.3
                case 4: return Color.Red;      // 2.6
                default: return Color.Gray;
            }
        }

        // 颜色插值
        private Color InterpolateColor(Color color1, Color color2, double position)
        {
            int r = (int)(color1.R + (color2.R - color1.R) * position);
            int g = (int)(color1.G + (color2.G - color1.G) * position);
            int b = (int)(color1.B + (color2.B - color1.B) * position);

            r = Math.Max(0, Math.Min(255, r));
            g = Math.Max(0, Math.Min(255, g));
            b = Math.Max(0, Math.Min(255, b));

            return Color.FromArgb(r, g, b);
        }

        // 公开方法：从Image窗体更新数据
        public void UpdateBrainData(List<double[]> frames)
        {
            if (frames == null || frames.Count == 0) return;
            if (InvokeRequired)
            {
                Invoke(new Action<List<double[]>>(UpdateBrainData), frames);
                return;
            }

            try
            {
                // 添加所有接收到的数据
                foreach (var frame in frames)
                {
                    if (frame.Length >= 5)
                    {
                        double time = frame[0];
                        int channel = (int)frame[1];
                        double hbo = frame[2];
                        double hbr = frame[3];
                        double hbt = frame[4];

                        if (channel >= 1 && channel <= 8)
                        {
                            hboValuesByChannel[channel].Add(hbo);
                            hbrValuesByChannel[channel].Add(hbr);
                            hbtValuesByChannel[channel].Add(hbt);
                        }
                    }
                }

                // 更新脑图像
                UpdateBrainImages();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新脑图像数据错误: {ex.Message}");
            }
        }

        private void UpdateBrainImages()
        {
            try
            {
                Console.WriteLine("开始更新脑图像...");

                // 获取最新的通道数据
                var positions = new List<Vector3>
                {
                    new Vector3(-40, 30, 50),   // 通道1
                    new Vector3(40, 30, 50),    // 通道2  
                    new Vector3(-50, 0, 60),    // 通道3
                    new Vector3(50, 0, 60),     // 通道4
                    new Vector3(-30, -40, 40),  // 通道5
                    new Vector3(30, -40, 40),   // 通道6
                    new Vector3(-40, -20, 30),  // 通道7
                    new Vector3(40, -20, 30)    // 通道8
                };

                // 计算每个通道的最新值
                var valuesHbO = new List<float>();
                var valuesHbR = new List<float>();
                var valuesHbT = new List<float>();

                bool hasRealData = false;

                for (int i = 1; i <= 8; i++)
                {
                    if (i <= 8 && hboValuesByChannel[i].Count > 0)
                    {
                        double latestHbO = hboValuesByChannel[i].Last();
                        double latestHbR = hbrValuesByChannel[i].Last();
                        double latestHbT = hbtValuesByChannel[i].Last();

                        valuesHbO.Add((float)latestHbO);
                        valuesHbR.Add((float)latestHbR);
                        valuesHbT.Add((float)latestHbT);

                        Console.WriteLine($"通道{i}: HbO={latestHbO:F4}, HbR={latestHbR:F4}, HbT={latestHbT:F4}");

                        // 检查是否有非零的有效数据
                        if (Math.Abs(latestHbO) > 0.001f || Math.Abs(latestHbR) > 0.001f || Math.Abs(latestHbT) > 0.001f)
                        {
                            hasRealData = true;
                        }
                    }
                    else
                    {
                        // 初始状态使用0值，将显示为灰色
                        valuesHbO.Add(0f);
                        valuesHbR.Add(0f);
                        valuesHbT.Add(0f);
                        Console.WriteLine($"通道{i}: 使用初始值0（灰色）");
                    }
                }

                // 根据当前数据动态调整颜色范围：如果Hb变化幅度很小，固定[-2.5,2.5]会导致一直接近0颜色不明显。
                // 这里用“当前8通道最大绝对值”作为动态范围（并给一点余量），让颜色随实时数据变化更敏感。
                float scaleHbO = ComputeAutoScale(valuesHbO);
                float scaleHbR = ComputeAutoScale(valuesHbR);
                float scaleHbT = ComputeAutoScale(valuesHbT);

                if (_brainRendererHbO != null) _brainRendererHbO.ColorScale = scaleHbO;
                if (_brainRendererHbR != null) _brainRendererHbR.ColorScale = scaleHbR;
                if (_brainRendererHbT != null) _brainRendererHbT.ColorScale = scaleHbT;

                // 更新三个脑图像
                _brainRendererHbO?.SetChannelData(positions, valuesHbO);
                _brainRendererHbR?.SetChannelData(positions, valuesHbR);
                _brainRendererHbT?.SetChannelData(positions, valuesHbT);

                // 触发重绘
                glControlHbO?.Invalidate();
                glControlHbR?.Invalidate();
                glControlHbT?.Invalidate();
                colorBarPanel?.Invalidate();

                Console.WriteLine($"脑图像更新完成，{(hasRealData ? "显示真实数据" : "显示初始灰色")}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新脑图像错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据当前通道值自动计算颜色映射范围。
        /// 返回值用于 BrainInterpolationRenderer.ColorScale（[-scale, +scale]）。
        /// </summary>
        private float ComputeAutoScale(List<float> values)
        {
            float maxAbs = 0f;
            for (int i = 0; i < values.Count; i++)
            {
                float v = values[i];
                if (float.IsNaN(v) || float.IsInfinity(v))
                    continue;
                maxAbs = Math.Max(maxAbs, Math.Abs(v));
            }

            // 没数据时保持默认 2.5（和原逻辑一致）
            if (maxAbs < 1e-6f)
                return 2.5f;

            // 给 20% 余量，避免总是顶到边界变“纯红/纯蓝”
            float scale = maxAbs * 1.2f;

            // 给一个下限，避免抖动时scale太小导致颜色噪声满屏
            return Math.Max(scale, 0.05f);
        }

        // 清理资源
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _brainRendererHbO?.Cleanup();
            _brainRendererHbR?.Cleanup();
            _brainRendererHbT?.Cleanup();
            ShaderManager.Cleanup();

            hboValuesByChannel.Clear();
            hbrValuesByChannel.Clear();
            hbtValuesByChannel.Clear();

            base.OnFormClosed(e);
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }
}