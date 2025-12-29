using HDF.PInvoke;
using HDF5CSharp;
using ScottPlot;
using ScottPlot.Hatches;
using ScottPlot.Plottables;
using ScottPlot.WinForms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Button;

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

        // ==================== 修正的 SNIRF 文件处理方法 ====================

        private string ProcessSnirfFile(string snirfFilePath)
        {
            try
            {
                StringBuilder csvBuilder = new StringBuilder();
                csvBuilder.AppendLine("Time(sec),Channel,HbO,HbR,HbT");

                long fileId = H5F.open(snirfFilePath, H5F.ACC_RDONLY);
                if (fileId < 0)
                {
                    throw new Exception("无法打开 SNIRF 文件");
                }

                try
                {
                    // 读取数据维度
                    var datasetId = H5D.open(fileId, "/nirs/data1/dataTimeSeries");
                    var spaceId = H5D.get_space(datasetId);

                    ulong[] dims = new ulong[2];
                    ulong[] maxDims = new ulong[2];
                    int rank = H5S.get_simple_extent_dims(spaceId, dims, maxDims);

                    int numChannels = (int)dims[0];      // 44个通道
                    int numTimePoints = (int)dims[1];    // 15618个时间点

                    H5S.close(spaceId);
                    H5D.close(datasetId);

                    Console.WriteLine($"=== SNIRF文件信息 ===");
                    Console.WriteLine($"数据维度: {numChannels} 通道 × {numTimePoints} 时间点");

                    // 读取时间数据
                    double[] timeData = ReadTimeData(fileId, numTimePoints);
                    Console.WriteLine($"时间数据长度: {timeData?.Length ?? 0}");

                    // 读取波长信息
                    double[] wavelengths = ReadWavelengths(fileId);
                    Console.WriteLine($"波长: {string.Join(", ", wavelengths)} nm");

                    // 读取测量列表信息
                    var measurementInfo = ReadMeasurementListInfo(fileId, numChannels);

                    // 自动生成 source-detector -> channel 映射（按距离优先选前8对，缺失坐标则按出现顺序）
                    var channelMap = BuildAutoChannelMap(fileId, measurementInfo, 8);
                    // 创建 OxygenConverter 实例
                    var converter = new FNIRS_OxygenAlgorithm.OxygenConverter();

                    // 处理所有时间点
                    for (int timeIdx = 0; timeIdx < numTimePoints; timeIdx++)
                    {
                        double time = timeData != null && timeIdx < timeData.Length ? timeData[timeIdx] : timeIdx * 0.1;

                        // 读取单个时间点的所有通道数据
                        double[] allChannelData = ReadTimePointData(fileId, timeIdx, numChannels, numTimePoints);

                        if (allChannelData != null)
                        {
                            // 按通道处理数据
                            ProcessChannelsDataCorrected(csvBuilder, time, allChannelData, measurementInfo, channelMap, converter, wavelengths);
                        }

                        // 显示进度
                        if (timeIdx % 1000 == 0)
                        {
                            Console.WriteLine($"进度: {timeIdx}/{numTimePoints} 时间点");
                            Application.DoEvents();
                        }
                    }

                    Console.WriteLine($"=== 处理完成 ===");
                    return csvBuilder.ToString();
                }
                finally
                {
                    H5F.close(fileId);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"SNIRF 文件处理失败: {ex.Message}");
            }
        }

        // 修正时间数据读取
        private double[] ReadTimeData(long fileId, int expectedLength)
        {
            try
            {
                // 尝试不同的时间数据路径
                string[] timePaths = {
                    "/nirs/data1/time",
                    "/nirs/data1/time0",
                    "/nirs/data1/t"
                };

                foreach (string path in timePaths)
                {
                    if (H5L.exists(fileId, path) > 0)
                    {
                        Console.WriteLine($"找到时间数据路径: {path}");
                        var data = ReadSimpleDoubleArray(fileId, path, expectedLength);
                        if (data != null && data.Length > 0)
                        {
                            Console.WriteLine($"成功读取时间数据，长度: {data.Length}");
                            Console.WriteLine($"时间数据范围: {data.Min():F3} 到 {data.Max():F3}");
                            return data;
                        }
                    }
                }

                // 如果都找不到，创建默认时间轴
                Console.WriteLine("未找到时间数据，使用默认时间轴");
                double[] defaultTime = new double[expectedLength];
                for (int i = 0; i < expectedLength; i++)
                {
                    defaultTime[i] = i * 0.1; // 假设10Hz采样率
                }
                return defaultTime;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取时间数据失败: {ex.Message}");
                return null;
            }
        }

        // 修正：读取单个时间点的所有通道数据
        private double[] ReadTimePointData(long fileId, int timeIndex, int numChannels, int numTimePoints)
        {
            try
            {
                var datasetId = H5D.open(fileId, "/nirs/data1/dataTimeSeries");
                var spaceId = H5D.get_space(datasetId);

                ulong[] dims = new ulong[2];
                ulong[] maxDims = new ulong[2];
                int rank = H5S.get_simple_extent_dims(spaceId, dims, maxDims);

                // 选择数据切片
                ulong[] start = new ulong[] { 0, (ulong)timeIndex };
                ulong[] count = new ulong[] { (ulong)numChannels, 1 };

                H5S.select_hyperslab(spaceId, H5S.seloper_t.SET, start, null, count, null);

                // 创建内存空间
                var memSpaceId = H5S.create_simple(1, new ulong[] { (ulong)numChannels }, null);

                double[] channelData = new double[numChannels];
                GCHandle handle = GCHandle.Alloc(channelData, GCHandleType.Pinned);
                try
                {
                    H5D.read(datasetId, H5T.NATIVE_DOUBLE, memSpaceId, spaceId, H5P.DEFAULT, handle.AddrOfPinnedObject());
                }
                finally
                {
                    handle.Free();
                }

                H5S.close(memSpaceId);
                H5S.close(spaceId);
                H5D.close(datasetId);

                return channelData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取时间点 {timeIndex} 数据失败: {ex.Message}");
                return null;
            }
        }

        // 修正：处理通道数据的方法
        private void ProcessChannelsDataCorrected(StringBuilder csvBuilder, double time, double[] allChannelData,
                                                List<MeasurementInfo> measurementInfo, Dictionary<string, int> channelMap, FNIRS_OxygenAlgorithm.OxygenConverter converter, double[] wavelengths)
        {
            // 按通道分组（根据测量列表信息）
            var channelData = new Dictionary<int, ChannelWavelengthData>();

            foreach (var measurement in measurementInfo)
            {
                int channel = -1;
                string key = $"{measurement.SourceIndex}-{measurement.DetectorIndex}";
                if (channelMap != null && channelMap.TryGetValue(key, out int ch)) channel = ch;

                if (channel >= 1 && channel <= 8)
                {
                    if (!channelData.ContainsKey(channel))
                        channelData[channel] = new ChannelWavelengthData();

                    // 根据波长索引存储数据
                    if (measurement.DataColumnIndex < allChannelData.Length)
                    {
                        double intensity = allChannelData[measurement.DataColumnIndex];

                        if (measurement.WavelengthIndex == 0) // 第一个波长
                        {
                            channelData[channel].Wavelength1 = intensity;
                        }
                        else if (measurement.WavelengthIndex == 1) // 第二个波长
                        {
                            channelData[channel].Wavelength2 = intensity;
                        }
                    }
                }
            }

            // 为每个通道计算血红蛋白浓度
            foreach (var channel in channelData)
            {
                var data = channel.Value;
                if (data.HasBothWavelengths()) // 确保有2个波长的数据
                {
                    try
                    {
                        // 使用两个波长的光强度数据
                        double[] intensities = { data.Wavelength1, data.Wavelength2 };
                        double[] hemoglobinData = converter.ConvertTwoWavelengths(intensities);

                        // 写入CSV
                        csvBuilder.AppendLine($"{time:F1},{channel.Key},{hemoglobinData[0]:F6},{hemoglobinData[1]:F6},{hemoglobinData[2]:F6}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"通道 {channel.Key} 数据转换失败: {ex.Message}");
                        // 写入零值
                        csvBuilder.AppendLine($"{time:F1},{channel.Key},0,0,0");
                    }
                }
            }
        }

        // 通道波长数据类
        private class ChannelWavelengthData
        {
            public double Wavelength1 { get; set; } // 第一个波长
            public double Wavelength2 { get; set; } // 第二个波长

            public bool HasBothWavelengths()
            {
                return Wavelength1 > 0 && Wavelength2 > 0;
            }
        }

        // 读取波长信息
        private double[] ReadWavelengths(long fileId)
        {
            try
            {
                return ReadSimpleDoubleArray(fileId, "/nirs/probe/wavelengths", 2);
            }
            catch
            {
                // 如果读取失败，使用默认波长
                return new double[] { 756, 852 };
            }
        }

        // 读取测量列表信息
        private List<MeasurementInfo> ReadMeasurementListInfo(long fileId, int totalMeasurements)
        {
            var measurementInfo = new List<MeasurementInfo>();

            int mlCount = 0;
            int i = 1;
            while (mlCount < totalMeasurements)
            {
                string mlPath = $"/nirs/data1/measurementList{i}";
                if (H5L.exists(fileId, mlPath) > 0)
                {
                    try
                    {
                        int sourceIndex = (int)ReadSimpleDoubleArray(fileId, mlPath + "/sourceIndex", 1)[0];
                        int detectorIndex = (int)ReadSimpleDoubleArray(fileId, mlPath + "/detectorIndex", 1)[0];
                        int wavelengthIndex = (int)ReadSimpleDoubleArray(fileId, mlPath + "/wavelengthIndex", 1)[0];

                        measurementInfo.Add(new MeasurementInfo
                        {
                            DataColumnIndex = mlCount,
                            SourceIndex = sourceIndex,
                            DetectorIndex = detectorIndex,
                            WavelengthIndex = wavelengthIndex
                        });

                        mlCount++;
                        i++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"读取测量列表{i}失败: {ex.Message}");
                        i++;
                        continue;
                    }
                }
                else
                {
                    break;
                }
            }

            Console.WriteLine($"总共读取 {measurementInfo.Count} 个测量列表");
            return measurementInfo;
        }

        // ==================== ChannelMap 自动化（对齐 Homer3 的“按实际 source-detector 形成通道”逻辑） ====================

        /// <summary>
        /// 自动根据“真实连接关系”生成 (sourceIndex-detectorIndex)->channel 的映射（更贴近 Homer3）。
        /// 编号规则（严格顺序）：
        /// 1) 若存在 /nirs/probe/link：按 link 表中出现的 source-detector 顺序编号（去重后取前 maxChannels）。
        /// 2) 否则：按 measurementList 中首次出现的 source-detector 顺序编号（去重后取前 maxChannels）。
        ///
        /// 说明：
        /// - 这会得到类似你期望的 {"1-1",1},{"3-1",2},{"2-1",3} 这种“遇到一个新 pair 就+1”的编号。
        /// - 不再按距离排序（距离排序更像“挑近的通道”，但不是“按连接关系/顺序编号”）。
        /// </summary>
        private Dictionary<string, int> BuildAutoChannelMap(long fileId, List<MeasurementInfo> measurementInfo, int maxChannels)
        {
            // 1) 优先 probe/link（如果有就用它的顺序）
            var linkPairs = TryReadProbeLinkPairs(fileId);

            var orderedPairs = new List<(int s, int d)>();
            var seen = new HashSet<string>();

            if (linkPairs != null && linkPairs.Count > 0)
            {
                foreach (var p in linkPairs)
                {
                    string key = $"{p.s}-{p.d}";
                    if (seen.Add(key))
                        orderedPairs.Add((p.s, p.d));
                    if (orderedPairs.Count >= maxChannels) break;
                }
            }
            else
            {
                // 2) 退化：按 measurementList 首次出现顺序
                foreach (var m in measurementInfo.OrderBy(x => x.DataColumnIndex))
                {
                    string key = $"{m.SourceIndex}-{m.DetectorIndex}";
                    if (seen.Add(key))
                        orderedPairs.Add((m.SourceIndex, m.DetectorIndex));
                    if (orderedPairs.Count >= maxChannels) break;
                }
            }

            if (orderedPairs.Count == 0)
                throw new Exception("No valid source-detector pairs found (probe/link and measurementList are empty).");

            var map = new Dictionary<string, int>();
            for (int i = 0; i < orderedPairs.Count; i++)
            {
                var p = orderedPairs[i];
                map[$"{p.s}-{p.d}"] = i + 1;
            }

            // 调试输出
            Console.WriteLine("=== Auto channelMap (strict pair order) ===");
            foreach (var kv in map)
                Console.WriteLine($"{kv.Key} => {kv.Value}");

            return map;
        }

        /// <summary>
        /// 尝试读取 /nirs/probe/link（如果存在）。
        /// link 常见为 N×K 表（至少包含 sourceIndex、detectorIndex 两列；类型/波长等列忽略）。
        /// 为了兼容不同 SNIRF 写法，这里：
        /// - 支持 float/double/int 等数值类型（最终强转为 int）
        /// - 支持 1-based 索引（SNIRF 通常就是 1-based）
        /// </summary>
        private List<(int s, int d)> TryReadProbeLinkPairs(long fileId)
        {
            string path = "/nirs/probe/link";
            try
            {
                if (H5L.exists(fileId, path) <= 0) return null;

                long ds = H5D.open(fileId, path);
                long sp = H5D.get_space(ds);

                ulong[] dims = new ulong[2];
                ulong[] maxDims = new ulong[2];
                int rank = H5S.get_simple_extent_dims(sp, dims, maxDims);

                if (rank != 2)
                {
                    H5S.close(sp);
                    H5D.close(ds);
                    return null;
                }

                int n = (int)dims[0];
                int k = (int)dims[1];
                if (k < 2)
                {
                    H5S.close(sp);
                    H5D.close(ds);
                    return null;
                }

                // 统一按 double 读（再转 int），能兼容多数数值类型
                double[] flat = new double[n * k];
                GCHandle h = GCHandle.Alloc(flat, GCHandleType.Pinned);
                try
                {
                    // 用 NATIVE_DOUBLE 读取：如果实际是 int/float，HDF5 会做类型转换
                    H5D.read(ds, H5T.NATIVE_DOUBLE, H5S.ALL, H5S.ALL, H5P.DEFAULT, h.AddrOfPinnedObject());
                }
                finally
                {
                    h.Free();
                }

                H5S.close(sp);
                H5D.close(ds);

                var pairs = new List<(int s, int d)>(n);
                for (int i = 0; i < n; i++)
                {
                    int s = (int)Math.Round(flat[i * k + 0]);
                    int d = (int)Math.Round(flat[i * k + 1]);
                    if (s > 0 && d > 0)
                        pairs.Add((s, d));
                }

                if (pairs.Count > 0)
                    Console.WriteLine($"probe/link found: {pairs.Count} rows");

                return pairs;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Read probe/link failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 读取 probe 的 source/detector 位置矩阵（N×2 或 N×3）。
        /// </summary>
        private double[,] ReadProbePosMatrix(long fileId, string[] candidatePaths)
        {
            foreach (var path in candidatePaths)
            {
                try
                {
                    if (H5L.exists(fileId, path) <= 0) continue;

                    long ds = H5D.open(fileId, path);
                    long sp = H5D.get_space(ds);

                    ulong[] dims = new ulong[2];
                    ulong[] maxDims = new ulong[2];
                    int rank = H5S.get_simple_extent_dims(sp, dims, maxDims);

                    if (rank != 2)
                    {
                        H5S.close(sp);
                        H5D.close(ds);
                        continue;
                    }

                    int n = (int)dims[0];
                    int d = (int)dims[1];
                    if (d < 2)
                    {
                        H5S.close(sp);
                        H5D.close(ds);
                        continue;
                    }

                    double[] flat = new double[n * d];
                    GCHandle h = GCHandle.Alloc(flat, GCHandleType.Pinned);
                    try
                    {
                        H5D.read(ds, H5T.NATIVE_DOUBLE, H5S.ALL, H5S.ALL, H5P.DEFAULT, h.AddrOfPinnedObject());
                    }
                    finally
                    {
                        h.Free();
                    }

                    H5S.close(sp);
                    H5D.close(ds);

                    var mat = new double[n, d];
                    int k = 0;
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < d; j++)
                            mat[i, j] = flat[k++];

                    Console.WriteLine($"读取 probe 坐标成功: {path}, dims={n}x{d}");
                    return mat;
                }
                catch
                {
                    // 继续尝试下一个 path
                }
            }
            return null;
        }

        private double EstimateSourceDetectorDistance(double[,] srcPos, double[,] detPos, int sourceIndex, int detectorIndex)
        {
            // SNIRF 索引通常从 1 开始
            int si = sourceIndex - 1;
            int di = detectorIndex - 1;

            int srcN = srcPos.GetLength(0);
            int srcD = srcPos.GetLength(1);
            int detN = detPos.GetLength(0);
            int detD = detPos.GetLength(1);

            if (si < 0 || si >= srcN || di < 0 || di >= detN)
                return double.NaN;

            int dim = Math.Min(srcD, detD);
            dim = Math.Min(dim, 3); // 最多用前三维

            double sum = 0;
            for (int k = 0; k < dim; k++)
            {
                double dx = srcPos[si, k] - detPos[di, k];
                sum += dx * dx;
            }
            return Math.Sqrt(sum);
        }



        // 测量信息类
        private class MeasurementInfo
        {
            public int DataColumnIndex { get; set; }
            public int SourceIndex { get; set; }
            public int DetectorIndex { get; set; }
            public int WavelengthIndex { get; set; }
        }

        // 简单的 double 数组读取
        private double[] ReadSimpleDoubleArray(long fileId, string path, int expectedLength)
        {
            try
            {
                var datasetId = H5D.open(fileId, path);
                var spaceId = H5D.get_space(datasetId);

                ulong[] dims = new ulong[1];
                ulong[] maxDims = new ulong[1];
                int rank = H5S.get_simple_extent_dims(spaceId, dims, maxDims);

                if (rank != 1 || dims[0] != (ulong)expectedLength)
                {
                    H5S.close(spaceId);
                    H5D.close(datasetId);
                    return null;
                }

                double[] data = new double[expectedLength];

                GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
                try
                {
                    H5D.read(datasetId, H5T.NATIVE_DOUBLE, H5S.ALL, H5S.ALL, H5P.DEFAULT, handle.AddrOfPinnedObject());
                }
                finally
                {
                    handle.Free();
                }

                H5S.close(spaceId);
                H5D.close(datasetId);

                return data;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取 {path} 失败: {ex.Message}");
                return null;
            }
        }
    }
}