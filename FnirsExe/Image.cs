using HDF.PInvoke;
using HDF5CSharp;
using ScottPlot;
using ScottPlot.Hatches;
using ScottPlot.Plottables;
using ScottPlot.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Button;
using FnirsExe.Snirf.IO;
using FnirsExe.Snirf.Processing;

namespace FnirsExe
{
    public partial class Image : Form
    {
        private Software softwareForm;//引用software窗体
        // 按通道存储数据（8个通道）
        private Dictionary<int, List<double>> timePointsByChannel = new Dictionary<int, List<double>>();
        private Dictionary<int, List<double>> hboValuesByChannel = new Dictionary<int, List<double>>();
        private Dictionary<int, List<double>> hbrValuesByChannel = new Dictionary<int, List<double>>();
        private Dictionary<int, List<double>> hbtValuesByChannel = new Dictionary<int, List<double>>();
        // 曲线颜色映射
        private Dictionary<string, ScottPlot.Color> curveColors = new Dictionary<string, ScottPlot.Color>()
        {
            { "HbO", Colors.Red },
            { "HbR", Colors.Green },
            { "HbT", Colors.Blue }
        };
        // 最大显示点数
        private const int MAX_POINTS = 1000;
        // 存储当前选择的通道
        public Image() : this(null)   // 调用有参数的构造函数，传递 null 或默认值
        {
        }
        public Image(Software software)
        {
            softwareForm = software; //software窗体引用
            //订阅数据事件
            Plot myPlot = new Plot();
            myPlot.Font.Set("宋体");//设置绘图中使用的字体
            InitializeComponent();//初始化窗体
            InitializePlot();//初始化绘图
            if (software != null)
            {
                // softwareForm = software;
                softwareForm.OnOxygenDataReceived += UpdateOxygenPlot;
                // 订阅主程序的OnOxygenDataReceived事件：当主程序有新脑氧数据时，触发当前窗体的UpdateOxygenPlot方法
            }
            // 初始化8个通道的数据结构
            for (int i = 1; i <= 8; i++)
            {
                timePointsByChannel[i] = new List<double>();
                hboValuesByChannel[i] = new List<double>();
                hbrValuesByChannel[i] = new List<double>();
                hbtValuesByChannel[i] = new List<double>();
            }
        }
        private void InitializePlot()
        {
            // 初始化8个图表的设置
            var plots = new[] { fpImage1, fpImage2, fpImage3, fpImage4, fpImage5, fpImage6, fpImage7, fpImage8 };
            for (int i = 0; i < plots.Length; i++)
            {
                int channel = i + 1;
                plots[i].Plot.Clear();
                plots[i].Plot.Title($"通道 {channel}");
                plots[i].Refresh();
            }
        }
        //实时更新图表的方法
        private void UpdateOxygenPlot(List<double[]> frames)//实时更新图表数据，List<double[]与software中的Action<List<double[]>>保持一致，void与action保持一致
        {//frames数据帧的集合，frame单个数据帧
            if (frames == null || frames.Count == 0) return;
            if (InvokeRequired)
            {
                Invoke(new Action<List<double[]>>(UpdateOxygenPlot), frames);
                return;
            }
            try
            {
                for (int i = 0; i < frames.Count; i++)
                {
                    double[] frame = frames[i];
                    Console.WriteLine($"帧 {i}: 时间={frame[0]:F1}s, 通道={frame[1]}, HbO₂={frame[2]:F3}, HbR={frame[3]:F3}, HbT={frame[4]:F3}");
                }
                // 添加所有接收到的数据
                foreach (var frame in frames)
                {//frame格式；[时间，通道，数值]
                    if (frame.Length >= 5)// [时间, 通道, HbO, HbR, HbT]
                    {
                        double time = frame[0];
                        int channel = (int)frame[1];//获取哪个通道
                        double hbo = frame[2];
                        double hbr = frame[3];
                        double hbt = frame[4];
                        //确保通道在有效范围内
                        if (channel >= 1 && channel <= 8)
                        {
                            // 添加数据到对应通道
                            timePointsByChannel[channel].Add(time);
                            hboValuesByChannel[channel].Add(hbo);
                            hbrValuesByChannel[channel].Add(hbr);
                            hbtValuesByChannel[channel].Add(hbt);
                            //限制数据量
                            if (timePointsByChannel[channel].Count > MAX_POINTS)
                            {
                                timePointsByChannel[channel].RemoveAt(0);
                                hboValuesByChannel[channel].RemoveAt(0);
                                hbrValuesByChannel[channel].RemoveAt(0);
                                hbtValuesByChannel[channel].RemoveAt(0);
                            }
                        }
                    }
                }
                // 更新绘图数据
                UpdateAllPlots();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"更新图表错误: {ex.Message}");
            }
        }
        private void UpdateAllPlots()//刷新曲线数据
        {
            Console.WriteLine("开始更新所有图表...");
            var plots = new[] { fpImage1, fpImage2, fpImage3, fpImage4, fpImage5, fpImage6, fpImage7, fpImage8 };//数组索引从0开始
            // 为每个有数据的通道添加曲线
            for (int channel = 1; channel <= 8; channel++)
            {
                var plot = plots[channel - 1];
                plot.Plot.Clear();
                // 根据复选框状态显示相应的曲线
                if (timePointsByChannel[channel].Count > 0 && IsChannelSelected(channel))
                {
                    if (cbHbO.Checked)
                        AddCurveToPlot(plot, "HbO", timePointsByChannel[channel], hboValuesByChannel[channel]);

                    if (cbHbR.Checked)
                        AddCurveToPlot(plot, "HbR", timePointsByChannel[channel], hbrValuesByChannel[channel]);

                    if (cbHbT.Checked)
                        AddCurveToPlot(plot, "HbT", timePointsByChannel[channel], hbtValuesByChannel[channel]);
                    double xMin = timePointsByChannel[channel].First();
                    double xMax = timePointsByChannel[channel].Last() * 1.05;
                    double yMin = GetMinValue(channel) * 1.1;
                    double yMax = GetMaxValue(channel) * 1.1;
                    double yRange = yMax - yMin;
                    plot.Plot.Axes.SetLimits(xMin, xMax, yMin - yRange * 0.1, yMax + yRange * 0.1);
                }
                plot.Refresh();
            }
        }
        private double GetMinValue(int channel)
        {
            if (!IsChannelSelected(channel)) return 0;

            var values = new List<double>();
            if (cbHbO.Checked && hboValuesByChannel[channel].Count > 0)
                values.Add(hboValuesByChannel[channel].Min());
            if (cbHbR.Checked && hbrValuesByChannel[channel].Count > 0)
                values.Add(hbrValuesByChannel[channel].Min());
            if (cbHbT.Checked && hbtValuesByChannel[channel].Count > 0)
                values.Add(hbtValuesByChannel[channel].Min());
            return values.Count > 0 ? values.Min() : 0;
        }
        private double GetMaxValue(int channel)
        {
            if (!IsChannelSelected(channel)) return 1;
            var values = new List<double>();
            if (cbHbO.Checked && hboValuesByChannel[channel].Count > 0)
                values.Add(hboValuesByChannel[channel].Max());
            if (cbHbR.Checked && hbrValuesByChannel[channel].Count > 0)
                values.Add(hbrValuesByChannel[channel].Max());
            if (cbHbT.Checked && hbtValuesByChannel[channel].Count > 0)
                values.Add(hbtValuesByChannel[channel].Max());
            return values.Count > 0 ? values.Max() : 1;
        }
        private void AddCurveToPlot(FormsPlot plot, string curveType,
                                   List<double> times, List<double> values)
        {//UpdateAllPlots调用AddCurveToPlot绘制曲线
            var scatter = plot.Plot.Add.Scatter(times.ToArray(), values.ToArray());//创建散点曲线
            scatter.Color = curveColors[curveType];
            scatter.LegendText = curveType;
            scatter.LineWidth = 0.8f;
            scatter.MarkerSize = 0;
        }

        // 清理资源
        protected override void OnFormClosed(FormClosedEventArgs e)//窗体关闭时清理资源
                                                                   //窗体关闭时，取消事件订阅并清理数据，防止内存泄漏和无效数据处理。
        {
            // 取消事件订阅
            if (softwareForm != null)
            {
                softwareForm.OnOxygenDataReceived -= UpdateOxygenPlot;
            }
            // 清理数据
            timePointsByChannel.Clear();
            hboValuesByChannel.Clear();
            hbrValuesByChannel.Clear();
            hbtValuesByChannel.Clear();
            base.OnFormClosed(e);
        }

        private void Image_Load(object sender, EventArgs e)//窗体加载事件
        {
            InitializePlot();
        }
        private void btnLoadData_Click(object sender, EventArgs e)//定义btnLoadData_Click方法
        {       //创建文件选择对话框
            //OpenFileDialog是 Windows 窗体提供的文件选择对话框控件，用于让用户浏览并选择文件。
            //using语句确保对话框使用完毕后自动释放资源，避免内存泄漏
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = @"E:\source\FnirsExe\AD"; // 设置对话框打开时的初始目录，与Software.cs中的保存路径一致
                openFileDialog.Filter = "SNIRF文件 (*.snirf)|*.snirf|CSV文件 (*.csv)|*.csv|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*"; ;
                /*第一部添加CSV文件，"文本文件 (*.txt)|*.txt"表示显示扩展名为.txt的文本文件。
                第二部分"所有文件 (*.*)|*.*"表示可以显示所有类型的文件。
                竖线|用于分隔显示文本和筛选规则。*/
                openFileDialog.FilterIndex = 1;//设置默认选中的筛选规则索引（从 1 开始），这里默认选中 "CSV文件 (*.csv)"。
                openFileDialog.RestoreDirectory = true;//设置对话框关闭后，恢复到用户上次选择的目录

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {//DialogResult.OK 是 Windows Forms 中用于表示对话框返回结果的枚举值，
                 //专门用来判断用户与对话框（如 OpenFileDialog、SaveFileDialog、MessageBox 等）交互后的操作结果
                    try
                    {
                        //string fileContent = File.ReadAllText(openFileDialog.FileName);
                        string filePath = openFileDialog.FileName;
                        string fileExtension = Path.GetExtension(filePath).ToLower();
                        if (fileExtension == ".snirf")
                        {
                            MessageBox.Show("正在处理 SNIRF 文件并转换为血红蛋白数据...", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);

                            // 使用新的处理方法
                            string csvContent = ProcessSnirfFile(filePath);
                            PlotOxygenData(csvContent);

                            MessageBox.Show("SNIRF 文件处理完成并已载入图表", "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }//PlotOxygenData(fileContent);
                        else
                        {
                            // 处理其他文件（CSV、TXT等）
                            string fileContent = File.ReadAllText(filePath);
                            PlotOxygenData(fileContent);
                        }
                    }
                    catch (Exception ex)//异常处理
                    {
                        MessageBox.Show($"载入数据失败: {ex.Message}", "错误",
                                      MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
        }
        //绘制脑氧浓度数据,包含三条曲线
        private void PlotOxygenData(string dataContent)
        {
            // 清空现有数据
            foreach (var channel in timePointsByChannel.Keys.ToList())
            {
                timePointsByChannel[channel].Clear();
                hboValuesByChannel[channel].Clear();
                hbrValuesByChannel[channel].Clear();
                hbtValuesByChannel[channel].Clear();
            }

            try
            {
                var lines = dataContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                bool hasHeader = lines.Length > 0 && lines[0].Contains("Time(sec)");//如果文件不为空且第一行包含"Time(sec)"文本，则认为该文件有表头行。
                for (int i = hasHeader ? 1 : 0; i < lines.Length; i++)//? 1 : 0：如果有表头，从索引1开始；否则从索引0开始
                {
                    var parts = lines[i].Split(',');//将CSV文件的一行数据按逗号分割成多个字段
                    // 调试输出，查看解析结果
                    Console.WriteLine($"解析行 {i}: {string.Join("|", parts)}");

                    // 每5列一个通道数据
                    for (int ch = 0; ch < parts.Length; ch += 5)
                    {
                        if (ch + 4 < parts.Length) // 确保有完整的5列数据
                        {
                            try
                            {
                                double time = double.Parse(parts[ch].Trim(), CultureInfo.InvariantCulture);
                                int channel = int.Parse(parts[ch + 1].Trim());
                                double hbo = double.Parse(parts[ch + 2].Trim(), CultureInfo.InvariantCulture);
                                double hbr = double.Parse(parts[ch + 3].Trim(), CultureInfo.InvariantCulture);
                                double hbt = double.Parse(parts[ch + 4].Trim(), CultureInfo.InvariantCulture);

                                if (IsChannelSelected(channel))
                                {
                                    timePointsByChannel[channel].Add(time);
                                    hboValuesByChannel[channel].Add(hbo);
                                    hbrValuesByChannel[channel].Add(hbr);
                                    hbtValuesByChannel[channel].Add(hbt);

                                    Console.WriteLine($"通道 {channel}: 时间={time}, HbO={hbo}, HbR={hbr}, HbT={hbt}");
                                }
                            }
                            catch (FormatException ex)
                            {
                                Console.WriteLine($"解析错误在列 {ch}-{ch + 4}: {ex.Message}");
                                Console.WriteLine($"数据: {string.Join(",", parts, ch, Math.Min(5, parts.Length - ch))}");
                            }
                        }
                    }
                }

                // 检查数据是否成功加载
                foreach (var channel in timePointsByChannel.Keys)
                {
                    Console.WriteLine($"通道 {channel} 数据点数: {timePointsByChannel[channel].Count}");
                }

                UpdateAllPlots();
                MessageBox.Show("数据载入完成", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"数据解析错误: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private bool IsChannelSelected(int channel)
        {
            // 获取 Software 窗体中的通道选择状态
            if (softwareForm == null) return false;

            // 根据 channel 判断对应的 CheckBox 是否被选中
            switch (channel)
            {
                case 1: return softwareForm.cb1.Checked;
                case 2: return softwareForm.cb2.Checked;
                case 3: return softwareForm.cb3.Checked;
                case 4: return softwareForm.cb4.Checked;
                case 5: return softwareForm.cb5.Checked;
                case 6: return softwareForm.cb6.Checked;
                case 7: return softwareForm.cb7.Checked;
                case 8: return softwareForm.cb8.Checked;
                default: return false;
            }
        }
        private void label4_Click(object sender, EventArgs e)
        {
        }
        private void fpImage1_Load(object sender, EventArgs e)
        {
        }
        private void fpImage2_Load(object sender, EventArgs e)
        {
        }

        private void cbHbO_CheckedChanged(object sender, EventArgs e)
        {
            UpdateAllPlots();
        }

        private void cbHbR_CheckedChanged(object sender, EventArgs e)
        {
            UpdateAllPlots();
        }

        private void cbHbT_CheckedChanged(object sender, EventArgs e)
        {
            UpdateAllPlots();
        }

        private void fpImage4_Load(object sender, EventArgs e)
        {
        }

        private void btnInspectSnirf_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "SNIRF文件 (*.snirf)|*.snirf";
                openFileDialog.Title = "选择 SNIRF 文件";

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    SimpleInspectSnirfFile(openFileDialog.FileName);
                }
            }
        }
        private void SimpleInspectSnirfFile(string snirfFilePath)
        {
            try
            {
                StringBuilder info = new StringBuilder();
                info.AppendLine("SNIRF 文件结构分析:");
                info.AppendLine($"文件: {Path.GetFileName(snirfFilePath)}");
                info.AppendLine($"大小: {new FileInfo(snirfFilePath).Length} 字节");
                info.AppendLine();

                long fileId = H5F.open(snirfFilePath, H5F.ACC_RDONLY);
                if (fileId < 0)
                {
                    MessageBox.Show("无法打开文件", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    info.AppendLine("✅ 文件打开成功");
                    info.AppendLine();

                    // 重点检查关键数据集
                    info.AppendLine("关键数据集维度信息:");

                    CheckDatasetSimple(info, fileId, "/nirs/data1/time", "时间数据");
                    CheckDatasetSimple(info, fileId, "/nirs/data1/dataTimeSeries", "数据时间序列");
                    CheckDatasetSimple(info, fileId, "/nirs/probe/wavelengths", "波长数据");
                    CheckDatasetSimple(info, fileId, "/nirs/probe/sourcePos", "光源位置");
                    CheckDatasetSimple(info, fileId, "/nirs/probe/detectorPos", "探测器位置");

                    info.AppendLine("\n其他重要路径检查:");
                    string[] otherPaths = {
                "/nirs/data1/measurementList",
                "/nirs/probe/sourceLabels",
                "/nirs/probe/detectorLabels",
                "/nirs/metaDataTags"
            };

                    foreach (string path in otherPaths)
                    {
                        bool exists = H5L.exists(fileId, path) > 0;
                        info.AppendLine(exists ? $"✅ {path}" : $"❌ {path}");
                    }

                }
                finally
                {
                    H5F.close(fileId);
                }

                MessageBox.Show(info.ToString(), "SNIRF 文件结构", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"分析文件失败: {ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void CheckDatasetSimple(StringBuilder info, long fileId, string path, string description)
        {
            try
            {
                bool exists = H5L.exists(fileId, path) > 0;
                if (exists)
                {
                    var datasetId = H5D.open(fileId, path);
                    var spaceId = H5D.get_space(datasetId);

                    ulong[] dims = new ulong[2];
                    ulong[] maxDims = new ulong[2];
                    int rank = H5S.get_simple_extent_dims(spaceId, dims, maxDims);

                    if (rank == 1)
                    {
                        info.AppendLine($"✅ {description}: {dims[0]} 个元素");
                    }
                    else if (rank == 2)
                    {
                        info.AppendLine($"✅ {description}: {dims[0]} × {dims[1]} 矩阵");

                        // 对于数据时间序列，推测通道数
                        if (path.Contains("dataTimeSeries"))
                        {
                            int numChannels = (int)dims[0]; // 修正：dims[0]是通道数
                            int numTimePoints = (int)dims[1]; // dims[1]是时间点数
                            info.AppendLine($"    正确维度: {numChannels} 通道 × {numTimePoints} 时间点");
                        }
                    }
                    else
                    {
                        info.AppendLine($"✅ {description}: {rank} 维数据");
                    }

                    H5S.close(spaceId);
                    H5D.close(datasetId);
                }
                else
                {
                    info.AppendLine($"❌ {description}: 不存在");
                }
            }
            catch (Exception ex)
            {
                info.AppendLine($"❌ {description}: 读取失败 - {ex.Message}");
            }
        }

        // ==================== Homer3 对齐的 SNIRF 文件处理 ====================

        private string ProcessSnirfFile(string snirfFilePath)
        {
            try
            {
                var snirf = SnirfLoaderApi.Load(snirfFilePath);

                var options = new Homer3ProcessingPipeline.Options
                {
                    DataBlockIndex = 0,
                    MaxChannelsToExport = 8
                };

                var pipelineResult = Homer3ProcessingPipeline.Run(snirf, options);

                foreach (var warn in pipelineResult.Warnings)
                {
                    Console.WriteLine($"[Homer3 pipeline warning] {warn}");
                }

                return Homer3ProcessingPipeline.BuildHemoglobinCsv(pipelineResult, options.MaxChannelsToExport);
            }
            catch (Exception ex)
            {
                throw new Exception($"SNIRF 文件处理失败: {ex.Message}");
            }
        }
    }
}
