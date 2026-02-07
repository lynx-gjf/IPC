using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.IO.Ports;

namespace IPC
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly SerialPortManager _spManager = new();

        public MainWindow()
        {
            InitializeComponent();

            // 注册 CodePages 提供器，以便支持 GBK/GB2312 等编码（仅需一次）
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // 获取所有可用串口端口，并添加到comboBoxCOM
            string[] ports = _spManager.GetPortNames();
            comboBoxCOM.ItemsSource = ports;
            if (ports.Length > 0)
                comboBoxCOM.SelectedIndex = 0;  // 默认选择索引

            // 订阅数据接收事件（非 UI 线程触发）
            _spManager.TextReceived += SpManager_TextReceived;
            _spManager.AdcArrayReceived += SpManager_AdcArrayReceived;
            _spManager.DataReceived += SpManager_DataReceived;

            // 初始化编码选择（默认 UTF-8）
            comboBoxEncoding.SelectedIndex = 0;
            UpdateEncodingFromSelection();
        }

        // 来自 SerialPortManager 的文本片段（例如包含 $$ADC[...]ADC$$ 的片段）
        private void SpManager_TextReceived(string fullText)
        {
            // 如果当前为 Hex 模式则不显示文本片段
            if (IsHexMode()) return;

            this.Dispatcher.Invoke(new Action(() =>
            {
                textBoxRecv.AppendText(fullText + Environment.NewLine);
                textBoxRecv.ScrollToEnd();
            }));
        }

        // 来自 SerialPortManager 的 8 字节帧十六进制字符串（例如 "AA BB CC ..."）
        private void SpManager_DataReceived(string hex)
        {
            // 仅在 Hex 模式下显示十六进制帧
            if (!IsHexMode()) return;

            this.Dispatcher.Invoke(new Action(() =>
            {
                textBoxRecv.AppendText(hex + Environment.NewLine);
                textBoxRecv.ScrollToEnd();
            }));
        }

        // 来自 SerialPortManager 的 ADC 数组解析结果
        private void SpManager_AdcArrayReceived(double[] values)
        {
            this.Dispatcher.Invoke(new Action(() =>
            {
                if (values != null && values.Length > 0)
                {
                    double adc1 = values[0];
                    Debug.WriteLine($"adc1 = {adc1}");
                }
            }));
        }

        private bool IsHexMode()
        {
            return comboBoxEncoding?.SelectedIndex == 3;
        }

        private void UpdateEncodingFromSelection()
        {
            if (comboBoxEncoding == null) return;

            Encoding chosen = Encoding.UTF8;
            switch (comboBoxEncoding.SelectedIndex)
            {
                case 0: // UTF-8
                    chosen = Encoding.UTF8;
                    break;
                case 1: // ASCII
                    chosen = Encoding.ASCII;
                    break;
                case 2: // GBK
                    chosen = Encoding.GetEncoding("GB2312"); // 或使用 codepage 936
                    break;
                case 3: // Hex 模式：编码仍保持 UTF-8 以避免异常，但显示以 Hex 为准
                    chosen = Encoding.UTF8;
                    break;
                default:
                    chosen = Encoding.UTF8;
                    break;
            }

            // 更新 SerialPortManager（会在串口已打开时同步到 SerialPort）
            _spManager.Encoding = chosen;
        }

        private void ComboBoxEncoding_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateEncodingFromSelection();
        }

        /// <summary>
        /// 打开关闭串口
        /// </summary>
        private void BtnOpenCloseCom_Click(object sender, RoutedEventArgs e)
        {
            if (_spManager.IsOpen)
            {
                _spManager.Close();
                btnOpenCloseCom.Content = "打开串口";
                Console.WriteLine("关闭串口成功");
                Debug.WriteLine("关闭串口成功");
                comboBoxBaudRate.IsEnabled = true;
                comboBoxCOM.IsEnabled = true;
                comboBoxDataBit.IsEnabled = true;
                comboBoxStopBit.IsEnabled = true;
                comboBoxSdd.IsEnabled = true;
                comboBoxlik.IsEnabled = true;
            }
            else
            {
                string? portName = comboBoxCOM.SelectedItem as string;
                if (string.IsNullOrEmpty(portName))
                {
                    MessageBox.Show("请选择串口");
                    return;
                }

                int baud = 1000000;

                switch (comboBoxBaudRate.SelectedIndex)
                {
                    case 0:
                        Console.WriteLine("baudrate: 9600");
                        baud = 9600;
                        break;
                    case 1:
                        baud = 19200;
                        Console.WriteLine("baudrate: 19200");
                        break;
                    case 2:
                        baud = 38400;
                        Console.WriteLine("baudrate: 38400");
                        break;
                    case 3:
                        Console.WriteLine("baudrate: 115200");
                        baud = 115200;
                        break;
                    case 4:
                        Console.WriteLine("baudrate: 1000000");
                        baud = 1000000;
                        break;
                    default:
                        Console.WriteLine("default baudrate!");
                        baud = 9600;
                        break;
                }

                try
                {
                    // 在打开前确保 SerialPortManager 使用当前选择的编码
                    UpdateEncodingFromSelection();

                    _spManager.Open(portName, baud);
                    btnOpenCloseCom.Content = "关闭串口";
                    Console.WriteLine("打开串口成功");
                    Debug.WriteLine("打开串口成功");

                    comboBoxBaudRate.IsEnabled = false;
                    comboBoxCOM.IsEnabled = false;
                    comboBoxDataBit.IsEnabled = false;
                    comboBoxStopBit.IsEnabled = false;
                    comboBoxSdd.IsEnabled = false;
                    comboBoxlik.IsEnabled = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
        }

        /// <summary>
        /// 清空串口接收数据
        /// </summary>
        private void BtnClearRecv_Click(object sender, RoutedEventArgs e)
        {
            _spManager.ClearBuffer();
            textBoxRecv.Clear();
        }

        /// <summary>
        /// 串口端口选择（下拉时刷新列表）
        /// </summary>
        private void ComboBoxCOM_Drop(object sender, DragEventArgs e)
        {
            string[] ports = _spManager.GetPortNames();
            comboBoxCOM.ItemsSource = ports;
            if (ports.Length > 0)
                comboBoxCOM.SelectedIndex = 0;
        }

        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            if (!_spManager.IsOpen)
            {
                MessageBox.Show("请先打开串口");
                return;
            }
            string toSend = (textBoxSend?.Text ?? string.Empty) + "\r\n";
            if (string.IsNullOrWhiteSpace(toSend))
            {
                MessageBox.Show("发送内容为空");
                return;
            }

            try
            {
                _spManager.SendHex(toSend);
                Debug.WriteLine("已发送(HEX): " + toSend);
            }
            catch (Exception ex)
            {
                MessageBox.Show("发送失败: " + ex.Message);
            }
        }

        private void btnSend_Click2(object sender, RoutedEventArgs e)
        {
            if (!_spManager.IsOpen)
            {
                MessageBox.Show("请先打开串口");
                return;
            }

            string toSend = textBoxSend2?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(toSend))
            {
                MessageBox.Show("发送内容为空");
                return;
            }

            try
            {
                _spManager.SendString(toSend, false);
                Debug.WriteLine("已发送(STR): " + toSend);
            }
            catch (Exception ex)
            {
                MessageBox.Show("发送失败: " + ex.Message);
            }
        }
    }
}