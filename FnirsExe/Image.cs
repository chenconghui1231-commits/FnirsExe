using HDF.PInvoke;
using ScottPlot;
using ScottPlot.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using FnirsExe.Snirf.IO;
using FnirsExe.Snirf.Models;
using FnirsExe.Snirf.Processing;
using System.Diagnostics;

namespace FnirsExe
{
    public partial class Image : Form
    {
        private Software softwareForm;

        private Dictionary<int, List<double>> timePointsByChannel = new Dictionary<int, List<double>>();
        private Dictionary<int, List<double>> hboValuesByChannel = new Dictionary<int, List<double>>();
        private Dictionary<int, List<double>> hbrValuesByChannel = new Dictionary<int, List<double>>();
        private Dictionary<int, List<double>> hbtValuesByChannel = new Dictionary<int, List<double>>();

        // 注意：这里的通道 Ch1..Ch8 采用 OD2Conc 的 pair 顺序（更接近 Homer3 的显示）
        private bool[] channelActiveStatus = null;

        private Dictionary<string, ScottPlot.Color> curveColors = new Dictionary<string, ScottPlot.Color>()
        {
            { "HbO", Colors.Red }, { "HbR", Colors.Green }, { "HbT", Colors.Blue }
        };

        // 对齐 Homer3 显示常见单位（Molar mm）
        private const string YLabel = "Conc (uM*mm)";

        // ---- debug log file ----
        private readonly string logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "FnirsExeLogs"
        );

        private string logFilePath;

        public Image() : this(null) { }

        public Image(Software software)
        {
            softwareForm = software;
            InitializeComponent();
            InitializePlot();

            if (software != null)
                softwareForm.OnOxygenDataReceived += UpdateOxygenPlot;

            for (int i = 1; i <= 8; i++)
            {
                timePointsByChannel[i] = new List<double>();
                hboValuesByChannel[i] = new List<double>();
                hbrValuesByChannel[i] = new List<double>();
                hbtValuesByChannel[i] = new List<double>();
            }

            EnsureLogFile();
            Log("Image form constructed.");
        }

        private void EnsureLogFile()
        {
            try
            {
                Directory.CreateDirectory(logDir);
                logFilePath = Path.Combine(logDir, $"ImageLog_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.AppendAllText(logFilePath, $"===== Log start {DateTime.Now:O} =====\r\n");
            }
            catch
            {
                logFilePath = null;
            }
        }

        private void Log(string msg)
        {
            string line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
            Debug.WriteLine(line);
            if (!string.IsNullOrEmpty(logFilePath))
            {
                try { File.AppendAllText(logFilePath, line + "\r\n"); } catch { }
            }
        }

        private void InitializePlot()
        {
            var plots = new[] { fpImage1, fpImage2, fpImage3, fpImage4, fpImage5, fpImage6, fpImage7, fpImage8 };
            for (int i = 0; i < plots.Length; i++)
            {
                plots[i].Plot.Clear();
                plots[i].Plot.Title($"Ch {i + 1}");
                plots[i].Plot.Axes.Left.Label.Text = YLabel;
                plots[i].Plot.Axes.Bottom.Label.Text = "Time (s)";
                plots[i].Refresh();
            }
        }

        private void ProcessAndPlotSnirf(string snirfFilePath)
        {
            Log($"ProcessAndPlotSnirf START: {snirfFilePath}");

            try
            {
                // 清理旧数据
                foreach (var k in timePointsByChannel.Keys)
                {
                    timePointsByChannel[k].Clear();
                    hboValuesByChannel[k].Clear();
                    hbrValuesByChannel[k].Clear();
                    hbtValuesByChannel[k].Clear();
                }

                // 1) Load
                Log("Step 1: Load SNIRF...");
                var loader = new SnirfLoader();
                SnirfFile snirf = loader.Load(snirfFilePath);
                if (snirf?.Data == null || snirf.Data.Count == 0) throw new Exception("Invalid SNIRF file.");
                Log($"Loaded. Data blocks = {snirf.Data.Count}");

                var block = snirf.Data[0];
                int T = block.DataTimeSeries.GetLength(0);
                int Ch = block.DataTimeSeries.GetLength(1);
                Log($"Block0: T={T}, Channels(meas)={Ch}, TimeLen={(block.Time != null ? block.Time.Length : -1)}");

                if (block.Time == null || block.Time.Length < 2)
                    Log("WARN: time vector is missing/too short. Bandpass fs may be wrong.");

                // 2) backup raw intensity for prune
                Log("Step 2: Backup raw intensity for prune...");
                double[,] rawIntensity = new double[T, Ch];
                Array.Copy(block.DataTimeSeries, rawIntensity, T * Ch);

                var snirfForPruning = new SnirfFile
                {
                    Probe = snirf.Probe,
                    Data = new List<NirsDataBlock>()
                };
                snirfForPruning.Data.Add(new NirsDataBlock
                {
                    DataTimeSeries = rawIntensity,
                    MeasurementList = block.MeasurementList,
                    Time = block.Time,
                    Stim = block.Stim
                });

                // 3) PruneChannels
                Log("Step 3: PruneChannels...");
                var pruneOpt = new PruneChannels.Options()
                {
                    DRange = new double[] { 0.0, 1.0e2 },
                    SnrThresh = 0.8,
                    SdRange = new double[] { 0.0, 45.0 }
                };
                bool[] intensityActive = PruneChannels.ComputeActiveChannels(snirfForPruning, 0, null, pruneOpt);
                Log($"PruneChannels done. ActiveMaskLen={(intensityActive != null ? intensityActive.Length : -1)} ActiveCount={(intensityActive != null ? intensityActive.Count(x => x) : -1)}");

                // 4) Intensity -> OD
                Log("Step 4: IntensityToOd...");
                IntensityToOd.ApplyInPlace(snirf, new IntensityToOd.Options
                {
                    RepairNaNInf = true,
                    FixNegative = IntensityToOd.Options.NegativeFixMode.SetToEps,
                    ApplyMedian3 = false
                });
                Log("IntensityToOd done.");
                LogMatrixStats("After IntensityToOd (OD)", block.DataTimeSeries);

                // 5) MotionArtifactByChannel
                Log("Step 5: MotionArtifactByChannel...");
                var motionOpt = new MotionArtifactByChannel.Options
                {
                    TMotion = 0.5,
                    TMask = 1.0,
                    StdThresh = 50.0,
                    AmpThresh = 5.0
                };
                var artRes = MotionArtifactByChannel.Compute(snirf, 0, null, intensityActive, null, motionOpt);
                Log($"MotionArtifactByChannel done. tIncLen={(artRes?.TInc != null ? artRes.TInc.Length : -1)}");

                // 6) MotionCorrectWavelet
                Log("Step 6: MotionCorrectWavelet START ...");
                MotionCorrectWavelet.Apply(snirf, new MotionCorrectWavelet.Options
                {
                    IQR = 1.5,
                    TurnOn = true,
                    ActiveMask = intensityActive,
                    L = 4
                });
                Log("Step 6: MotionCorrectWavelet DONE.");
                LogMatrixStats("After Wavelet", block.DataTimeSeries);

                // 7) Bandpass
                Log("Step 7: BandpassFilt...");
                double dt = (block.Time != null && block.Time.Length > 1) ? (block.Time[1] - block.Time[0]) : 0.1;
                double fs = (dt > 0) ? (1.0 / dt) : 10.0;
                Log($"Bandpass params: fs={fs:F6}, hpf=0.01, lpf=0.1");
                BandpassFilt.ApplyInPlace(block.DataTimeSeries, fs, 0.01, 0.1);
                Log("BandpassFilt done.");
                LogMatrixStats("After Bandpass", block.DataTimeSeries);

                // 8) OD2Conc
                Log("Step 8: OD2Conc...");
                var conc = Od2Conc.ComputeStrictHomer3(snirf, 0, new double[] { 6.0, 6.0 });
                if (conc == null) throw new Exception("OD2Conc failed.");

                int nPairs = conc.HbO.GetLength(1);
                int nTime = conc.HbO.GetLength(0);
                Log($"OD2Conc done. nPairs={nPairs}, HbO shape=({nTime},{nPairs})");

                // ====== 关键：自动判断 HbO/HbR 的单位（M / mM / uM）并选择缩放到 uM ======
                // scaleToMicro: M->uM = 1e6, mM->uM = 1e3, uM->uM = 1
                double[] scaleToMicroByPair = new double[nPairs];
                for (int p = 0; p < nPairs; p++)
                {
                    double maxAbs = GetMaxAbsFinite(conc.HbO, conc.HbR, p, Math.Min(80, nTime)); // 看前80个点足够
                    double scaleToMicro = DecideScaleToMicro(maxAbs);
                    scaleToMicroByPair[p] = scaleToMicro;

                    // 同时记录 rho(mm)
                    double rhoMm = 30.0;
                    if (conc.Pairs != null && p < conc.Pairs.Count)
                        rhoMm = NormalizeRhoToMm(conc.Pairs[p].Rho);

                    Log($"Pair {p}: maxAbs(Hb)={maxAbs:E3}, scaleToMicro={scaleToMicro:E0}, rhoMm={rhoMm:F3}");
                }
                // ====================================================================

                // 9) Map meas-active mask -> pair-active mask
                Log("Step 9: Map active mask to pairs...");
                channelActiveStatus = new bool[nPairs];
                for (int p = 0; p < nPairs; p++)
                {
                    bool good = true;

                    if (intensityActive != null && conc.Pairs != null && p < conc.Pairs.Count)
                    {
                        int c1 = conc.Pairs[p].Wavelength1Index;
                        int c2 = conc.Pairs[p].Wavelength2Index;

                        if (c1 >= 0 && c1 < intensityActive.Length &&
                            c2 >= 0 && c2 < intensityActive.Length)
                        {
                            good = intensityActive[c1] && intensityActive[c2];
                        }
                    }
                    channelActiveStatus[p] = good;
                }
                Log($"Pair active computed. PairActiveCount={channelActiveStatus.Count(x => x)}");

                // 10) Fill plot buffers (first 8 pairs)
                Log("Step 10: Fill plot buffers...");
                int fillPairs = Math.Min(8, nPairs);

                for (int t = 0; t < nTime; t++)
                {
                    double time = block.Time[t];
                    for (int p = 0; p < fillPairs; p++)
                    {
                        int chIdx = p + 1;
                        timePointsByChannel[chIdx].Add(time);

                        double rhoMm = 30.0;
                        if (conc.Pairs != null && p < conc.Pairs.Count)
                            rhoMm = NormalizeRhoToMm(conc.Pairs[p].Rho);

                        double s = scaleToMicroByPair[p]; // ✅单位自动缩放到 uM
                        // 最终显示：uM*mm
                        hboValuesByChannel[chIdx].Add(conc.HbO[t, p] * s * rhoMm);
                        hbrValuesByChannel[chIdx].Add(conc.HbR[t, p] * s * rhoMm);
                        hbtValuesByChannel[chIdx].Add(conc.HbT[t, p] * s * rhoMm);
                    }
                }

                if (fillPairs > 0 && timePointsByChannel[1].Count > 5)
                {
                    double rhoMm0 = (conc.Pairs != null && conc.Pairs.Count > 0) ? NormalizeRhoToMm(conc.Pairs[0].Rho) : 30.0;
                    Log($"Sample Ch1: t0={timePointsByChannel[1][0]:F3}, HbO0(uM*mm)={hboValuesByChannel[1][0]:F6}, rhoMm={rhoMm0:F3}, scaleToMicro={scaleToMicroByPair[0]:E0}");
                }

                Log("UpdateAllPlots...");
                UpdateAllPlots();
                Log("ProcessAndPlotSnirf DONE.");
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex);
                MessageBox.Show(ex.Message);
            }
        }

        // ======== 自动单位判断 ========
        // maxAbs 很小：说明是 M 级（1e-6~1e-4），用 1e6 -> uM
        // maxAbs 中等：说明是 mM 级（1e-3~1e-1），用 1e3 -> uM
        // maxAbs 很大：说明已经是 uM 级（>=1），用 1
        private static double DecideScaleToMicro(double maxAbs)
        {
            if (double.IsNaN(maxAbs) || double.IsInfinity(maxAbs) || maxAbs <= 0)
                return 1e6; // 默认按 M 处理

            // 经验阈值（足够把“500 vs 500000”这种 1000 倍问题直接拉回来）
            if (maxAbs < 1e-4) return 1e6;   // M -> uM
            if (maxAbs < 1e-1) return 1e3;   // mM -> uM
            return 1.0;                      // 已是 uM
        }

        private static double GetMaxAbsFinite(double[,] hbo, double[,] hbr, int p, int scanT)
        {
            int T = hbo.GetLength(0);
            scanT = Math.Min(scanT, T);

            double maxAbs = 0.0;
            for (int t = 0; t < scanT; t++)
            {
                double a = hbo[t, p];
                if (!double.IsNaN(a) && !double.IsInfinity(a))
                {
                    double abs = Math.Abs(a);
                    if (abs > maxAbs) maxAbs = abs;
                }
                double b = hbr[t, p];
                if (!double.IsNaN(b) && !double.IsInfinity(b))
                {
                    double abs = Math.Abs(b);
                    if (abs > maxAbs) maxAbs = abs;
                }
            }
            return maxAbs;
        }
        // ============================

        private void LogMatrixStats(string tag, double[,] m)
        {
            try
            {
                if (m == null) { Log($"{tag}: matrix is null"); return; }

                int r = m.GetLength(0);
                int c = m.GetLength(1);
                long total = (long)r * (long)c;
                long nan = 0, inf = 0;
                double min = double.PositiveInfinity, max = double.NegativeInfinity;
                double sum = 0.0;
                long n = 0;

                for (int i = 0; i < r; i++)
                {
                    for (int j = 0; j < c; j++)
                    {
                        double v = m[i, j];
                        if (double.IsNaN(v)) { nan++; continue; }
                        if (double.IsInfinity(v)) { inf++; continue; }
                        if (v < min) min = v;
                        if (v > max) max = v;
                        sum += v;
                        n++;
                    }
                }

                double mean = n > 0 ? sum / n : double.NaN;
                Log($"{tag}: size=({r},{c}) total={total} nan={nan} inf={inf} nan%={(total > 0 ? (100.0 * nan / total) : 0):F3}% min={min:F6} max={max:F6} mean={mean:F6}");
            }
            catch (Exception e)
            {
                Log($"{tag}: stats failed: {e.Message}");
            }
        }

        private void UpdateAllPlots()
        {
            var plots = new[] { fpImage1, fpImage2, fpImage3, fpImage4, fpImage5, fpImage6, fpImage7, fpImage8 };

            for (int ch = 1; ch <= 8; ch++)
            {
                var fp = plots[ch - 1];
                fp.Plot.Clear();

                bool isGood = true;
                if (channelActiveStatus != null && (ch - 1) < channelActiveStatus.Length)
                    isGood = channelActiveStatus[ch - 1];

                fp.Plot.Title(isGood ? $"Ch {ch}" : $"Ch {ch} (BAD)");
                fp.Plot.Axes.Left.Label.Text = YLabel;
                fp.Plot.Axes.Bottom.Label.Text = "Time (s)";

                if (IsChannelSelected(ch) && timePointsByChannel[ch].Count > 0 && isGood)
                {
                    // 线程安全：把当前窗口数据复制出来再绘图，避免后台追加数据导致枚举异常
                    List<double> tCopy, hboCopy, hbrCopy, hbtCopy;
                    lock (_rtLock)
                    {
                        tCopy = new List<double>(timePointsByChannel[ch]);
                        hboCopy = hboValuesByChannel.ContainsKey(ch) ? new List<double>(hboValuesByChannel[ch]) : new List<double>();
                        hbrCopy = hbrValuesByChannel.ContainsKey(ch) ? new List<double>(hbrValuesByChannel[ch]) : new List<double>();
                        hbtCopy = hbtValuesByChannel.ContainsKey(ch) ? new List<double>(hbtValuesByChannel[ch]) : new List<double>();
                    }

                    if (cbHbO.Checked) AddCurveSafe(fp, "HbO", tCopy, hboCopy);
                    if (cbHbR.Checked) AddCurveSafe(fp, "HbR", tCopy, hbrCopy);
                    if (cbHbT.Checked) AddCurveSafe(fp, "HbT", tCopy, hbtCopy);

                    fp.Plot.Axes.AutoScale();
                }

                fp.Refresh();
            }
        }

        // 过滤掉 NaN/Inf，让曲线断开（不要画成0）
        private void AddCurveSafe(FormsPlot p, string name, List<double> tList, List<double> vList)
        {
            if (tList == null || vList == null) return;
            int n = Math.Min(tList.Count, vList.Count);
            if (n < 2) return;

            var filtered = new List<(double t, double v)>(n);
            for (int i = 0; i < n; i++)
            {
                double v = vList[i];
                if (double.IsNaN(v) || double.IsInfinity(v)) continue;
                filtered.Add((tList[i], v));
            }
            if (filtered.Count < 2) return;

            double[] ts = filtered.Select(x => x.t).ToArray();
            double[] vs = filtered.Select(x => x.v).ToArray();

            var s = p.Plot.Add.Scatter(ts, vs);
            s.Color = curveColors[name];
            s.LineWidth = 1.5f;
            s.MarkerSize = 0;
        }

        private void btnLoadData_Click(object sender, EventArgs e)
        {
            Log("btnLoadData_Click");
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "SNIRF|*.snirf";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    Log($"Selected file: {ofd.FileName}");
                    ProcessAndPlotSnirf(ofd.FileName);
                }
                else
                {
                    Log("OpenFileDialog canceled.");
                }
            }
        }

        private readonly object _rtLock = new object();
        private int _rtLastUiUpdateTick = 0;
        private const int _rtMinUiUpdateIntervalMs = 80; // ~12.5 FPS

        // =========================
        // Real-time sliding window
        // =========================
        // 只显示最近多少秒的数据（超过窗口的旧数据会被丢弃，从而“滑动”过去）
        // 你可以按需要改成 30 / 60 / 120 等
        private const double _rtWindowSeconds = 10.0;

        // 额外的硬上限（防止异常情况下窗口修剪失效导致内存上涨）
        private const int _rtHardMaxPointsPerChannel = 20000;

        // 返回第一个 >= value 的索引（假设 list 单调递增）
        private static int LowerBound(List<double> list, double value)
        {
            int lo = 0;
            int hi = list.Count;
            while (lo < hi)
            {
                int mid = lo + ((hi - lo) >> 1);
                if (list[mid] < value) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }

        // 在 _rtLock 持有期间调用
        private void TrimRealtimeChannel_NoLock(int ch)
        {
            if (!timePointsByChannel.ContainsKey(ch)) return;

            var tList = timePointsByChannel[ch];
            if (tList == null || tList.Count == 0) return;

            // 若时间回退（例如重新开始采集，时间从0再起），清掉旧数据避免混在一起
            double latestT = tList[tList.Count - 1];
            double earliestT = tList[0];

            // 按窗口裁剪
            double cutoff = latestT - _rtWindowSeconds;
            if (cutoff > earliestT)
            {
                int keepFrom = LowerBound(tList, cutoff);
                if (keepFrom > 0)
                {
                    // 4个列表必须同步裁剪
                    int rm = keepFrom;
                    if (hboValuesByChannel.ContainsKey(ch)) rm = Math.Min(rm, hboValuesByChannel[ch].Count);
                    if (hbrValuesByChannel.ContainsKey(ch)) rm = Math.Min(rm, hbrValuesByChannel[ch].Count);
                    if (hbtValuesByChannel.ContainsKey(ch)) rm = Math.Min(rm, hbtValuesByChannel[ch].Count);
                    rm = Math.Min(rm, tList.Count);

                    if (rm > 0)
                    {
                        tList.RemoveRange(0, rm);
                        if (hboValuesByChannel.ContainsKey(ch) && hboValuesByChannel[ch].Count >= rm) hboValuesByChannel[ch].RemoveRange(0, rm);
                        if (hbrValuesByChannel.ContainsKey(ch) && hbrValuesByChannel[ch].Count >= rm) hbrValuesByChannel[ch].RemoveRange(0, rm);
                        if (hbtValuesByChannel.ContainsKey(ch) && hbtValuesByChannel[ch].Count >= rm) hbtValuesByChannel[ch].RemoveRange(0, rm);
                    }
                }
            }

            // 硬上限保护（一般不会触发）
            if (tList.Count > _rtHardMaxPointsPerChannel)
            {
                int rm = tList.Count - _rtHardMaxPointsPerChannel;
                tList.RemoveRange(0, rm);
                if (hboValuesByChannel.ContainsKey(ch) && hboValuesByChannel[ch].Count >= rm) hboValuesByChannel[ch].RemoveRange(0, rm);
                if (hbrValuesByChannel.ContainsKey(ch) && hbrValuesByChannel[ch].Count >= rm) hbrValuesByChannel[ch].RemoveRange(0, rm);
                if (hbtValuesByChannel.ContainsKey(ch) && hbtValuesByChannel[ch].Count >= rm) hbtValuesByChannel[ch].RemoveRange(0, rm);
            }
        }

        // 在 _rtLock 持有期间调用
        private void TrimRealtimeAllChannels_NoLock()
        {
            for (int ch = 1; ch <= 8; ch++)
                TrimRealtimeChannel_NoLock(ch);
        }

        private void UpdateOxygenPlot(List<double[]> frames)
        {
            if (frames == null || frames.Count == 0)
                return;

            // 1) 追加数据（线程安全）
            lock (_rtLock)
            {
                foreach (var row in frames)
                {
                    if (row == null || row.Length < 5)
                        continue;

                    double t = row[0];
                    int ch = (int)Math.Round(row[1]);
                    if (ch < 1 || ch > 8)
                        continue;

                    double hbo = row[2];
                    double hbr = row[3];
                    double hbt = row[4];

                    if (!timePointsByChannel.ContainsKey(ch))
                    {
                        timePointsByChannel[ch] = new List<double>();
                        hboValuesByChannel[ch] = new List<double>();
                        hbrValuesByChannel[ch] = new List<double>();
                        hbtValuesByChannel[ch] = new List<double>();
                    }

                    // 若时间回退（例如重新开始采集），先清空该通道缓冲，避免新旧数据叠在一起
                    if (timePointsByChannel[ch].Count > 0)
                    {
                        double lastT = timePointsByChannel[ch][timePointsByChannel[ch].Count - 1];
                        if (t < lastT - 1e-9)
                        {
                            timePointsByChannel[ch].Clear();
                            hboValuesByChannel[ch].Clear();
                            hbrValuesByChannel[ch].Clear();
                            hbtValuesByChannel[ch].Clear();
                        }
                    }

                    timePointsByChannel[ch].Add(t);
                    hboValuesByChannel[ch].Add(hbo);
                    hbrValuesByChannel[ch].Add(hbr);
                    hbtValuesByChannel[ch].Add(hbt);
                }

                // 裁剪到最近窗口，形成“滑动显示”
                TrimRealtimeAllChannels_NoLock();
            }

            // 2) UI 刷新（限频，避免串口高频导致卡顿）
            int now = Environment.TickCount;
            int elapsed = unchecked(now - _rtLastUiUpdateTick);
            if (elapsed < _rtMinUiUpdateIntervalMs)
                return;

            _rtLastUiUpdateTick = now;

            if (IsDisposed) return;

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke((Action)(() =>
                    {
                        try
                        {
                            UpdateAllPlots();
                        }
                        catch (Exception ex)
                        {
                            Log("UpdateAllPlots error (realtime): " + ex);
                        }
                    }));
                }
                catch { /* ignore */ }
            }
            else
            {
                try
                {
                    UpdateAllPlots();
                }
                catch (Exception ex)
                {
                    Log("UpdateAllPlots error (realtime): " + ex);
                }
            }
        }

        private bool IsChannelSelected(int c)
        {
            if (softwareForm == null) return true;
            switch (c)
            {
                case 1: return softwareForm.cb1.Checked;
                case 2: return softwareForm.cb2.Checked;
                case 3: return softwareForm.cb3.Checked;
                case 4: return softwareForm.cb4.Checked;
                case 5: return softwareForm.cb5.Checked;
                case 6: return softwareForm.cb6.Checked;
                case 7: return softwareForm.cb7.Checked;
                case 8: return softwareForm.cb8.Checked;
            }
            return false;
        }

        private void cbHbO_CheckedChanged(object sender, EventArgs e) => UpdateAllPlots();
        private void cbHbR_CheckedChanged(object sender, EventArgs e) => UpdateAllPlots();
        private void cbHbT_CheckedChanged(object sender, EventArgs e) => UpdateAllPlots();

        private void Image_Load(object sender, EventArgs e)
        {
            Log("Image_Load");
            InitializePlot();
        }

        private void label4_Click(object sender, EventArgs e) { }

        // =========================
        // 设计器绑定事件：保持存在
        // =========================
        private void btnInspectSnirf_Click(object sender, EventArgs e)
        {
            Log("btnInspectSnirf_Click");
            btnLoadData_Click(sender, e);
        }

        private void fpImage1_Load(object sender, EventArgs e) { }
        private void fpImage2_Load(object sender, EventArgs e) { }
        private void fpImage3_Load(object sender, EventArgs e) { }
        private void fpImage4_Load(object sender, EventArgs e) { }
        private void fpImage5_Load(object sender, EventArgs e) { }
        private void fpImage6_Load(object sender, EventArgs e) { }
        private void fpImage7_Load(object sender, EventArgs e) { }
        private void fpImage8_Load(object sender, EventArgs e) { }

        // rho 单位归一：cm->mm（0.1~10 视为 cm）
        private static double NormalizeRhoToMm(double rho)
        {
            if (double.IsNaN(rho) || double.IsInfinity(rho) || rho <= 0)
                return 30.0;

            if (rho > 0.1 && rho < 10.0)
                rho *= 10.0;

            if (rho < 0.1)
                rho = 30.0;

            return rho;
        }
    }
}
