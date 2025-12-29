using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using FNIRS_OxygenAlgorithm;

namespace FnirsExe
{
    public partial class Software : Form
    {
        public Software()//类的构造函数
        {
            InitializeComponent();//在创建窗体对象时，完成窗体及其上所有控件的初始化设置
        }
        //定义一个判断串口是否打开的变量
        bool m_p = true;
        SerialPort serialport = new SerialPort();//创建串口类的对象,创建对象是使用串口功能的前提条件
        public static string comData = null;//接收串口数据并解析
        private bool isADCollecting = false;//判断是否正在进行AD数据采集,初始状态为false
        private string saveFolderPath = @"E:\source\FnirsExe\AD"; // 数据保存路径
        private OxygenConverter oxygenConverter = new OxygenConverter();//当前类中创建了一个私有的OxygenConverter类型对象，
                                                                        //后续可以通过oxygenConverter变量来调用该类的方法或访问其属性，实现脑氧信号转换相关的业务逻辑
        //private double[] _gValues = new double[3]; //衰减参数G
        private StringBuilder concentrationData = new StringBuilder(); // 存储浓度数据，StringBuilder可以高效的拼接或修改字符串，
        private List<int> selectedChannels = new List<int>();//选中的通道
        private List<string> selectedCurveTypes = new List<string>();//选择要显示的曲线
        private Dictionary<int, List<double[]>> channelHistory = new Dictionary<int, List<double[]>>();//按通道存储所有历史数据
        private double startTime = 0; // 记录采集开始时间
        private Dictionary<int, Dictionary<int, double>> wavelengthCache = new Dictionary<int, Dictionary<int, double>>();//临时缓存来自串口的原始光强数据
        private const double SCALE_FACTOR = 10000.0; //进行数据规格化，缩放数据
        public event Action<List<double[]>> OnOxygenDataReceived; // 定义一个事件实现对象间的通信，外部类只能订阅或取消订阅事件，Image中的UpdateOxygenPlot方法订阅该事件
        //Action 系列委托表示没有返回值的方法，Action<List<double[]>事件委托，表示能接受一个List<double[]>参数并且返回void的方法。事件名称OnOxygenDataReceived，
        //在数据接收方法（Serialport_Datareceived）中，当成功解析并计算出一批新的脑氧数据（allChannelFrames）后，
        //会用一行代码触发（Invoke） 这个事件：OnOxygenDataReceived?.Invoke(allChannelFrames);
        /// // 添加一个标志来跟踪是否已经清空过接收框
        private bool hasClearedOnStart = false;
        private const double TIME_INTERVAL = 0.1;//时间间隔0.1
        private int dataFrameCount = 0; // 数据帧计数器
        private Dictionary<int, int> channelFrameCount = new Dictionary<int, int>(); // 每个通道的时间
        private string patientName = "";
        private string patientGender = "";
        private string patientAge = "";
        private List<byte> dataBuffer = new List<byte>();//字节缓存区，暂存未解析的串口数据
        private BrainViewerForm _brainViewerForm;
        private void Software_Load(object sender, EventArgs e)//事件处理方法
        {
            //设置串口号 com1
            this.cbPort.SelectedIndex = 0;
            //设置波特率9600
            this.cbBaud.SelectedIndex = 0;
            //设置数据位8位
            this.cbData.SelectedIndex = 1;
            //设置校验位None
            this.cbParity.SelectedIndex = 0;
            //设置停止位1
            this.cbStop.SelectedIndex = 0;
            this.tbUname.TextChanged += PatientInfo_TextChanged;
            this.tbGender.TextChanged += PatientInfo_TextChanged;
            this.tbAge.TextChanged+= PatientInfo_TextChanged;
            //给按钮控件绑定点击事件
            this.btnSavedata.Click += btnSavedata_Click;//保存数据
            this.btnSearchPort.Click += btnSearchPort_Click;//搜索串口
            btnSearchPort_Click(null, null);//程序启动时自动搜索串口，调用btnSearchPort_Click方法
                                            //第一个 null：指定 sender 为 null，意思是 “没有具体的事件触发源”（因为这是代码主动调用，而非某个控件触发）。 //第二个 null：指定 e 为 null，意思是 “没有事件相关的附加数据”
            serialport.DataReceived += Serialport_Datareceived;   //订阅串口对象的DataReceived事件，DataReceived内置的一个事件，串口对象收到数据之后调用Serialport_Datareceived方法处理                          
            /*// 初始化G参数（三波长）
            _gValues[0] = 0.1; // G_730
            _gValues[1] = 0.08; // G_850
            _gValues[2] = 0.12; // G_940*/
            oxygenConverter = new OxygenConverter();// 初始化脑氧算法
                                                    //oxygenConverter.SetGValues(_gValues); // 传递初始化的G参数
                                                    // 预初始化脑图像窗体（可选）
            _brainViewerForm = new BrainViewerForm();
            this.OnOxygenDataReceived += _brainViewerForm.UpdateBrainData;
            _brainViewerForm.Hide(); // 初始时隐藏
        }
        private void PatientInfo_TextChanged(object sender,EventArgs e)
        {
            patientName = tbUname.Text;
            patientGender = tbGender.Text;
            patientAge = tbAge.Text;
        }
        private void btnOpen_Click(object sender, EventArgs e)
        {
            OpenPort();//调用OpenPort方法
        }
        private void OpenPort()
        {
            if (!serialport.IsOpen)//如果串口是关闭的
            {
                serialport.PortName = cbPort.Text;
                serialport.BaudRate = int.Parse(cbBaud.Text);
                serialport.DataBits = int.Parse(cbData.Text);
                serialport.StopBits = (StopBits)int.Parse(cbStop.Text);
                serialport.Parity = Parity.None;
                serialport.Open();
                btnOpen.Text = "关闭串口";
                startTime = (DateTime.Now - DateTime.Today).TotalSeconds;
            }
            else
            {
                serialport.Close();//关闭串口
                btnOpen.Text = "打开串口";
            }
        }
        //串口数据接收
        void Serialport_Datareceived(object sender, SerialDataReceivedEventArgs e)
        {
            
            int count = serialport.BytesToRead;//用于获取当前接收缓冲区中等待读取的字节数
            byte[] receive = new byte[count];//这个数组将用于存储从串口缓冲区读取到的二进制数据
            serialport.Read(receive, 0, count);//读取操作，把串口中大小为count的数据读取到receive中
            Console.WriteLine($"Received {count} bytes: {BitConverter.ToString(receive)}");
            Console.WriteLine("Serialport_Datareceived");
            if (!isADCollecting)
            { // 未采集时只接收，不显示
                Console.WriteLine("接收到数据但不在采集状态，忽略");
                serialport.DiscardInBuffer(); // 清空缓冲区
                return;
            }
            if (isADCollecting){
                Console.WriteLine("receive data in collecting mode");
                //判断转换成字符串还是十六进制
                if (this.rbString.Checked)//字符串按钮是否被选中
                {
                    Console.WriteLine("rbString");
                    string strReceive = Encoding.Default.GetString(receive);
                    //因为串口数据接收事件在后台线程触发，而 UI 控件（如文本框）只能在主线程更新，
                    //所以通过Invoke将代码块切换到主线程执行（跨线程安全操作 UI）
                    this.Invoke(new MethodInvoker(() =>//MethodInvoker：一种委托类型，用于包装要在主线程执行的代码
                    {//() => { ... }：Lambda 表达式，表示要执行的 UI 更新逻辑
                        try
                        {
                            var parts = strReceive.Split(new[] { ',', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            // 三波长数据格式：每个通道需要3个波长数据（730, 850, 940）
                            int requiredDataCount = selectedChannels.Count * 3;
                            if (parts.Length >= requiredDataCount)
                            {
                                List<double[]> allChannelFrames = new List<double[]>();
                                bool anyDataProcessed = false;
                                // 为每个选中的通道处理数据
                                for (int channelIndex = 0; channelIndex < selectedChannels.Count; channelIndex++)
                                {
                                    int channel = selectedChannels[channelIndex];
                                    // 获取该通道对应的数据列,每个通道3列
                                    int dataIndex = channelIndex * 3;
                                    if (double.TryParse(parts[dataIndex], out double i730) &&
                                        double.TryParse(parts[dataIndex + 1], out double i850) &&
                                        double.TryParse(parts[dataIndex + 2], out double i940))
                                    {
                                        double currentTime = channelFrameCount[channel] * TIME_INTERVAL;
                                        double[] currentIntensity = new double[] { i730, i850, i940 };
                                        //rawIntensityFrames.Add(currentIntensity);//用于存储所有原始强度帧数据
                                        // 为每个通道单独计算浓度变化
                                        double[] concentrationChanges = oxygenConverter.ConvertToHemoglobin(currentIntensity);
                                        // 初始化通道历史数据存储
                                        if (!channelHistory.ContainsKey(channel))
                                        {
                                            channelHistory[channel] = new List<double[]>();
                                        }
                                        // 格式: [时间, 通道, HbO, HbR, HbT]
                                        double[] frameData = new double[] {
                                        currentTime,
                                        channel,
                                        concentrationChanges[0],
                                        concentrationChanges[1],
                                        concentrationChanges[2]
                                    };
                                        channelHistory[channel].Add(frameData);
                                        // 限制每个通道的历史数据量（防止内存溢出）
                                        if (channelHistory[channel].Count > 1000)
                                        {
                                            channelHistory[channel].RemoveAt(0);
                                        }
                                        // 保存数据到CSV格式
                                        concentrationData.AppendLine($"{currentTime:F1},{channel},{concentrationChanges[0]:F4},{concentrationChanges[1]:F4},{concentrationChanges[2]:F4}");
                                        allChannelFrames.Add(frameData);
                                        // 递增该通道的帧计数器
                                        channelFrameCount[channel]++;
                                        anyDataProcessed = true;
                                    }
                                }
                                if (anyDataProcessed)
                                {
                                    dataFrameCount++;
                                }
                                else
                                {
                                    this.tbReceive.Text += strReceive;
                                }
                                //在主线程中触发事件
                                this.Invoke(new Action(() =>
                                {
                                    OnOxygenDataReceived?.Invoke(allChannelFrames);
                                }));
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"数据解析错误: {ex.Message}");
                        }
                    }));
                }
                else if (rb16.Checked) // 十六进制模式 (协议帧)
                {
                    Console.WriteLine("进入十六进制模式处理");
                    dataBuffer.AddRange(receive);//将新数据添加到缓冲区
                    while (dataBuffer.Count >= 39)
                    {
                        //查找帧头AA FF F1 20
                        int frameStartIndex = -1;
                        for ( int i = 0; i <= dataBuffer.Count - 3; i++)
                        {
                            if (dataBuffer[i] == 0xAA &&
                                dataBuffer[i + 1] == 0xFF &&
                                dataBuffer[i + 2] == 0xF1)
                            {
                                frameStartIndex = i;
                                break;
                            }
                        }
                        if(frameStartIndex>=0 && dataBuffer.Count - frameStartIndex >= 39)
                        {
                            // 提取一帧数据（39字节）
                            byte[] frame = dataBuffer.Skip(frameStartIndex).Take(39).ToArray();
                            // 处理这一帧
                            string hexStr = ByteTohexStr(frame);
                            this.Invoke(new MethodInvoker(() =>
                            {
                                tbReceive.Text += hexStr + Environment.NewLine; // 自动换行
                            }));
                            int[] channels = ParseFrame(frame);//解析数据
                            if (channels != null)
                            {
                                int irCode = frame[36]; // 红外编号
                                Console.WriteLine($"红外编号: 0x{irCode:X2}");
                                List<double[]> allChannelFrames = new List<double[]>();//临时存放当前批次处理的所有通道的数据帧。
                                bool anyWavelengthDataProcessed = false;//用于保证时间计数的正确
                                for (int channelIndex = 0; channelIndex < selectedChannels.Count; channelIndex++)
                                {
                                    Console.WriteLine($"=== 处理通道 {selectedChannels[channelIndex]} ===");
                                    Console.WriteLine("rb16.for");
                                    int channel = selectedChannels[channelIndex];
                                    int actualWavelengthCode = irCode;
                                    if (channel % 2 == 0) // 偶数通道 (2,4,6,8)
                                    {
                                        // 偶数通道使用第二组红外编号：0x03(940nm)、0x04(850nm)、0x05(730nm)
                                        // 映射到与奇数通道相同的编码范围：0→940nm, 1→850nm, 2→730nm
                                        actualWavelengthCode = irCode - 3;
                                    }
                                    else // 奇数通道 (1,3,5,7)
                                    {
                                        Console.WriteLine("1,3,5,7");
                                        // 奇数通道使用第一组红外编号：0x00(940nm)、0x01(850nm)、0x02(730nm)
                                        actualWavelengthCode = irCode;
                                    }
                                    // 确保波长编码在有效范围内 (0-2)
                                    if (actualWavelengthCode < 0 || actualWavelengthCode > 2)
                                    {
                                        continue; // 跳过无效波长
                                    }
                                    if (!wavelengthCache.ContainsKey(channel))//防止空引用异常
                                        wavelengthCache[channel] = new Dictionary<int, double>();
                                    // 保存当前波长的数据
                                    wavelengthCache[channel][irCode] = channels[channel - 1]; // SCALE_FACTOR;
                                                                                              //通道1的数据存储在 channels[0]，通道2的数据在 channels[1]，以此类推。wavelengthCache[channel][irCode]标记这是通道N在波长X下测得的光强。
                                    Console.WriteLine($"通道{channel} 波长{irCode:X2} 光强: {wavelengthCache[channel][irCode]}");

                                    // 检查是否收集了三个不同的波长
                                    // 从 wavelengthCache 中找出通道 channel 的缓存字典，然后把这个字典里所有已经存在的波长编号，打包成一个列表，交给变量 wavelengthKeys。
                                    var wavelengthKeys = wavelengthCache[channel].Keys.ToList();
                                    Console.WriteLine($"通道{channel}已收集波长: {string.Join(", ", wavelengthKeys.Select(k => k.ToString("X2")))}");

                                    // 需要收集三个特定的波长：0x00, 0x01, 0x02 或 0x03, 0x04, 0x05
                                    if (wavelengthCache[channel].Count >= 3)
                                    {
                                        // 尝试获取三个波长的数据
                                        Console.WriteLine("wavelengthCache3");
                                        double i730 = 0, i850 = 0, i940 = 0;
                                        bool hasAllWavelengths = true;//是否集齐三个波长

                                        // 根据通道奇偶性选择波长组
                                        if (channel % 2 == 1) // 奇数通道
                                        {//TryGetValue(0x00, out i940)：尝试从字典中查找键为 0x00 的项，如果找到则放入i940，out 关键字表示这个参数用于输出结果，返回值是一个布尔类型
                                            hasAllWavelengths = wavelengthCache[channel].TryGetValue(0x00, out i940) &&
                                                               wavelengthCache[channel].TryGetValue(0x01, out i850) &&
                                                               wavelengthCache[channel].TryGetValue(0x02, out i730);
                                        }
                                        else // 偶数通道
                                        {
                                            hasAllWavelengths = wavelengthCache[channel].TryGetValue(0x03, out i940) &&
                                                               wavelengthCache[channel].TryGetValue(0x04, out i850) &&
                                                               wavelengthCache[channel].TryGetValue(0x05, out i730);
                                        }

                                        if (hasAllWavelengths)
                                        {
                                            Console.WriteLine($"通道{channel} 三个波长数据齐全，开始计算脑氧");
                                            Console.WriteLine($"通道{channel} - 光强值: 730nm={i730:F6}, 850nm={i850:F6}, 940nm={i940:F6}");
                                            double currentTime = channelFrameCount[channel] * TIME_INTERVAL;//时间 = 帧序号 × 采样周期。第0组数据：0 * 0.1 = 0.0 秒。第1组数据：1 * 0.1 = 0.1 秒
                                            double[] currentIntensity = new double[] { i730, i850, i940 };//经过缩放后的实际光强值
                                            double[] concentrationChanges = oxygenConverter.ConvertToHemoglobin(currentIntensity);//将光强信号转换为血红蛋白浓度变化值

                                            // 格式: [时间, 通道, HbO, HbR, HbT]
                                            double[] frameData = new double[] {
                                             currentTime,
                                             channel,
                                             concentrationChanges[0],
                                             concentrationChanges[1],
                                             concentrationChanges[2]
                                         };

                                            if (!channelHistory.ContainsKey(channel))//确保在往某个通道添加数据之前，该通道在字典中已经存在对应的列表。
                                            {
                                                channelHistory[channel] = new List<double[]>();//若没有，创建对应的列表
                                            }

                                            channelHistory[channel].Add(frameData);//往列表中添加数据

                                            if (channelHistory[channel].Count > 1000)//限制数据的最大量
                                            {
                                                channelHistory[channel].RemoveAt(0);
                                            }

                                            // 保存数据到CSV格式，concentrationData浓度数据
                                            concentrationData.AppendLine($"{currentTime:F1},{channel},{concentrationChanges[0]:F4},{concentrationChanges[1]:F4},{concentrationChanges[2]:F4}");
                                            allChannelFrames.Add(frameData);
                                            // 递增该通道的帧计数器
                                            channelFrameCount[channel]++;
                                            anyWavelengthDataProcessed = true; //只要有任何一个通道成功凑齐了三个波长并完成了脑氧计算，就立即将此标志设为 true：

                                        }
                                    }
                                }
                                if (anyWavelengthDataProcessed)
                                {
                                    dataFrameCount++;// 只有真正处理了数据，才增加总帧计数器
                                }
                                this.Invoke(new Action(() =>
                                {
                                    OnOxygenDataReceived?.Invoke(allChannelFrames);
                                }));
                            };
                            dataBuffer.RemoveRange(0, frameStartIndex + 39);
                        }
                        else
                        {
                            // 如果没有找到完整帧，保留数据等待下次接收
                            break;
                        }
                    }
                    // 如果缓冲区过大，清理部分数据（防止内存溢出）
                    if (dataBuffer.Count > 1000)
                    {
                        // 保留最近500字节的数据
                        if (dataBuffer.Count > 500)
                        {
                            dataBuffer = dataBuffer.Skip(dataBuffer.Count - 500).ToList();
                            Console.WriteLine("数据缓冲区过大，已清理");
                        }
                    }
                }
            }
            else
            {
                // 非采集状态下，只记录日志不显示数据
                Console.WriteLine("数据接收但未在采集状态，不显示");
            }

        }
        // 增强ParseFrame方法的调试输出
        public int[] ParseFrame(byte[] frame)
        {
            Console.WriteLine($"解析帧，长度: {frame.Length}");
            //if (frame.Length != 39)
            //{
            //    Console.WriteLine("帧长度不等于39，返回null");
            //    return null;
            //}
            // 帧头检查
            if (frame[0] != 0xAA || frame[1] != 0xFF || frame[2] != 0xF1)
            {
                Console.WriteLine("帧头校验失败");
                return null;
            }
            // 长度检查
            if (frame[3] != 0x20)
            {
                Console.WriteLine("长度字段校验失败");
                return null;
            }
            // 解析通道数据
            int[] channels = new int[8];
            for (int i = 0; i < 8; i++)
            {
                int offset = 4 + i * 4;//计算当前通道数据的起始位置,第一个4是前四个字节的帧头，第二个4是每个通道的数据占四个字节
                //channels[i] = BitConverter.ToInt32(frame, offset);
                //转换后取绝对值
                channels[i] = Math.Abs(BitConverter.ToInt32(frame, offset)); //作用是从字节数组 frame 的指定 offset 位置开始，读取连续的4个字节，并将它们转换成一个Int32
                Console.WriteLine($"通道{i + 1}数据: {channels[i]} (原始字节: {BitConverter.ToString(frame, offset, 4)})");
            }
            // 红外发射编号
            byte irCode = frame[36];
            Console.WriteLine($"红外发射编号: 0x{irCode:X2}");
            // 校验和计算
            byte sumCheck = 0x00;
            byte addCheck = 0x00;
            Console.WriteLine("校验和计算详细过程 (前37字节):");
            for (int i = 0; i < 37; i++)
            {
                sumCheck += frame[i];//sumCheck = sumCheck + frame[i]
                addCheck += sumCheck;//addCheck = addCheck + sumCheck
                Console.WriteLine($"字节[{i}]: 0x{frame[i]:X2}, sumCheck: 0x{sumCheck:X2}, addCheck: 0x{addCheck:X2}");
            }

            Console.WriteLine($"计算校验和: {sumCheck:X2}, {addCheck:X2}");
            Console.WriteLine($"实际校验和: {frame[37]:X2}, {frame[38]:X2}");

            if (frame[37] != sumCheck || frame[38] != addCheck)
            {
                Console.WriteLine("校验失败，丢弃帧");
                return null;
            }

            Console.WriteLine("帧解析成功");
            return channels;
        }
        public static string ByteTohexStr(byte[] bytes)//16进制转成字符串
        {
            if (bytes == null)
            {//字符串为空抛出异常
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder();//拼接字符串
            for (int i = 0; i < bytes.Length; i++)
            {
                sb.Append(bytes[i].ToString("x2")+" ");
            }
            return sb.ToString();//将StringBuilder中拼接好的所有内容转换为string类型，作为函数的返回值。
        }
        private byte[] StrTohexByte(string HEXstring)//字符串转换成16进制
        {
            //去空格
            HEXstring = HEXstring.Replace("\r", "").Replace("\n", "").Replace(" ", "");
            byte[] returnbytes = new byte[HEXstring.Length / 2];//准备缓存数据集
            //转换16进制
            for (int i = 0; i < returnbytes.Length; i++)
            {
                //转换成16进制的byte
                returnbytes[i] = Convert.ToByte(HEXstring.Substring(i * 2, 2).Replace(" ", ""), 16);
            }
            return returnbytes;//返回对应的byte数据

        }
        private void btnClear_Click(object sender, EventArgs e)
        {//清空接收
            this.Invoke(new MethodInvoker(() =>
            {
                tbReceive.Text = "";
            }));
        }
        private void btnSavedata_Click(object sender, EventArgs e)
        {
            if (concentrationData.Length == 0)
            {
                Console.WriteLine("111");
                MessageBox.Show("没有可保存的数据");
                return;
            }
            try
            {
                if (!Directory.Exists(saveFolderPath))//保存文件的目标文件夹路径
                {
                    Directory.CreateDirectory(saveFolderPath);//如若没有创建，则创建目标文件夹路径
                }
                string concentrationFileName = $"Oxygen_Data_{DateTime.Now:yyyyMMdd_HHmmss}.csv";//文件名字
                string concentrationFullPath = Path.Combine(saveFolderPath, concentrationFileName);// 拼接完整路径
                StringBuilder csvHeader = new StringBuilder();
                csvHeader.AppendLine($"Patient Name:,{patientName}");
                csvHeader.AppendLine($"Gender:,{patientGender}");
                csvHeader.AppendLine($"Age:,{patientAge}");
                csvHeader.AppendLine($"Recording Time:,{DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                csvHeader.AppendLine(); // 空行分隔
                // 添加数据表头
                csvHeader.AppendLine("Time(sec),Channel,HbO(μM),HbR(μM),HbT(μM)");
                string csvData = csvHeader.ToString() + concentrationData.ToString();
                File.WriteAllText(concentrationFullPath, csvData, Encoding.UTF8);
                MessageBox.Show($"脑氧数据已保存到：{concentrationFullPath}");
            }// 提示脑氧数据保存成功
            catch (Exception ex)
            {
                MessageBox.Show($"保存数据失败：{ex.Message}");
            }
        }
        
        /*private void SaveToDatabase(string data,string tableName)
        {

            string connectionString = "server=127.0.0.1;port=3306;user=root;password=123456;database=fnirsexe";
            string sql = $"INSERT INTO {tableName} (DataContent, CreateTime) VALUES (@data, @time)";

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    conn.Open();
                    using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                    {
                        cmd.Parameters.AddWithValue("@data", data);
                        cmd.Parameters.AddWithValue("@time", DateTime.Now);
                        cmd.ExecuteNonQuery();
                    }
                }
                MessageBox.Show("数据保存成功！", "数据库操作", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"数据保存失败: {ex.Message}", "数据库错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

        }*/
        private void btnCollect_Click(object sender, EventArgs e)
        {
            patientName = tbUname.Text;
            patientGender = tbGender.Text;
            patientAge = tbAge.Text;
            if (!serialport.IsOpen)//串口没打开
            {
                MessageBox.Show("请先打开串口");
                return;
            }
            // 检查病人信息是否填写
            if (string.IsNullOrEmpty(patientName) || string.IsNullOrEmpty(patientGender) || string.IsNullOrEmpty(patientAge))
            {
                MessageBox.Show("请填写完整的病人信息（姓名、性别、年龄）");
                isADCollecting = false;
                btnCollect.Text = "AD采集";
                return;
            }

            isADCollecting = !isADCollecting;
            if (isADCollecting)//AD采集
            {
                Console.WriteLine("开始采集");
                btnCollect.Text = "停止采集";
                // 清空历史数据
                channelHistory.Clear();
                selectedChannels.Clear();
                concentrationData.Clear();
                wavelengthCache.Clear();
                dataFrameCount = 0; // 重置总帧计数器
                channelFrameCount.Clear(); // 清空通道帧计数器
                // 清空接收框，确保只显示采集开始后的数据
                this.Invoke(new MethodInvoker(() =>
                {
                    tbReceive.Clear();
                }));
                //清空串口接收缓冲区，避免旧数据被处理
                serialport.DiscardInBuffer();  //丢弃缓冲区中未处理的旧数据
                // 获取选中的通道
                if (cb1.Checked) selectedChannels.Add(1);
                if (cb2.Checked) selectedChannels.Add(2);
                if (cb3.Checked) selectedChannels.Add(3);
                if (cb4.Checked) selectedChannels.Add(4);
                if (cb5.Checked) selectedChannels.Add(5);
                if (cb6.Checked) selectedChannels.Add(6);
                if (cb7.Checked) selectedChannels.Add(7);
                if (cb8.Checked) selectedChannels.Add(8);
                if (selectedChannels.Count == 0)
                {
                    MessageBox.Show("请至少选择一个通道");
                    isADCollecting = false;
                    btnCollect.Text = "AD采集";
                    return;
                }
                // 初始化每个通道的帧计数器
                foreach (int channel in selectedChannels)
                {
                    channelFrameCount[channel] = 0;
                }
                // 订阅数据接收事件（确保只在采集状态下订阅）
                // serialport.DataReceived += Serialport_Datareceived;
                MessageBox.Show($"开始采集 {selectedChannels.Count} 个通道的数据");
                Console.WriteLine("数据采集已启动，接收框已清空");
            }
            else
            {
                Console.WriteLine("停止采集");
                btnCollect.Text = "AD采集";
                // 取消订阅数据接收事件
                //serialport.DataReceived -= Serialport_Datareceived;
                MessageBox.Show("数据采集已停止");
            }
        }
        private void btnSearchPort_Click(object sender, EventArgs e)
        { //枚举系统可用串口
            string[] portNames = SerialPort.GetPortNames();
            //清空原有选项，重新加载
            cbPort.Items.Clear();
            cbPort.Items.AddRange(portNames);
            //自动选中第一个串口（可选，提升用户体验）
            if (portNames.Length > 0)
            {
                cbPort.SelectedIndex = 0;
            }
            else
            {
                MessageBox.Show("未检测到可用串口");
            }
        }
        private void btnShowImage_Click(object sender, EventArgs e)
        {
            Image image = new Image(this);
            image.Show();
        }

        private void OpenBrainViewer_Click(object sender, EventArgs e)
        {
            try
            {
                Console.WriteLine("打开脑图像窗体...");
                if (_brainViewerForm == null || _brainViewerForm.IsDisposed)
                {
                    Console.WriteLine("创建新的脑图像窗体实例");
                    _brainViewerForm = new BrainViewerForm();
                    // 订阅数据接收事件，将数据传递给脑图像窗体
                    this.OnOxygenDataReceived -= _brainViewerForm.UpdateBrainData; // 先取消
                    this.OnOxygenDataReceived += _brainViewerForm.UpdateBrainData;
                    Console.WriteLine("脑图像窗体事件订阅完成");
                }
                _brainViewerForm.Show();
                _brainViewerForm.BringToFront();
                // 如果有历史数据，可以立即更新脑图像
                if (channelHistory.Count > 0)
                {
                    var latestFrames = GetLatestChannelData();
                    Console.WriteLine($"向脑图像窗体发送 {latestFrames.Count} 帧历史数据");
                    if (latestFrames.Count > 0)
                    {
                        _brainViewerForm.UpdateBrainData(latestFrames);
                    }
                }
                else
                {
                    Console.WriteLine("没有历史数据可发送到脑图像窗体");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"打开脑图像窗体失败: {ex.Message}");
                MessageBox.Show($"打开脑图像窗体失败: {ex.Message}", "错误",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        private List<double[]> GetLatestChannelData()
        {
            List<double[]> latestFrames = new List<double[]>();
            foreach (var channel in selectedChannels)
            {
                if (channelHistory.ContainsKey(channel) && channelHistory[channel].Count > 0)
                {
                    latestFrames.Add(channelHistory[channel].Last());
                }
            }
            return latestFrames;
        }
    }
}