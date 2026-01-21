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

            // 获取所有可用串口端口，并添加到comboBoxCOM
            string[] ports = _spManager.GetPortNames();
            comboBoxCOM.ItemsSource = ports;
            if (ports.Length > 0)
                comboBoxCOM.SelectedIndex = 0;  // 默认选择索引

            // 订阅数据接收事件（非 UI 线程触发）
            // 原来直接订阅 DataReceived 并在这里解析，现在把解析移到 SerialPortManager：
            _spManager.TextReceived += SpManager_TextReceived;
            _spManager.AdcArrayReceived += SpManager_AdcArrayReceived;
        }

        // 来自 SerialPortManager 的文本片段（例如包含 $$ADC[...]ADC$$ 的片段）
        private void SpManager_TextReceived(string fullText)
        {
            // 在 UI 线程更新控件
            this.Dispatcher.Invoke(new Action(() =>
            {
                textBlockRecv.Text = fullText;  // 显示接收到的文本内容（最新片段）
            }));
        }

        // 来自 SerialPortManager 的 ADC 数组解析结果
        private void SpManager_AdcArrayReceived(double[] values)
        {
            // 在 UI 线程或后台都可以处理；这里仅示例输出第一个值到调试窗口
            this.Dispatcher.Invoke(new Action(() =>
            {
                if (values != null && values.Length > 0)
                {
                    double adc1 = values[0];
                    Debug.WriteLine($"adc1 = {adc1}");
                }
            }));
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
                // 替换原有的获取 portName 代码，添加 null 检查和转换
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
            textBlockRecv.Text = string.Empty;
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

        /// <summary>
        /// 发送按钮示例：读取 textBoxSend 的文本并发送（不带换行）
        /// 在 XAML 中请确保有 textBoxSend 与 btnSend，并把 btnSend 的 Click 绑定到此方法
        /// </summary>
        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            if (!_spManager.IsOpen)
            {
                MessageBox.Show("请先打开串口");
                return;
            }

            string toSend = textBoxSend?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(toSend))
            {
                MessageBox.Show("发送内容为空");
                return;
            }

            try
            {
                // 使用 SendHex 发送十六进制字符串（SerialPortManager 会验证是否正好为 8 字节）
                _spManager.SendHex(toSend);
                Debug.WriteLine("已发送(HEX): " + toSend);
            }
            catch (Exception ex)
            {
                MessageBox.Show("发送失败: " + ex.Message);
            }
        }

        /// <summary>
        /// 新增：发送字符串（使用 textBoxSend2 的内容），按当前编码发送，不追加换行
        /// 绑定到 XAML 中 btnSend2 的 Click 事件（已存在）
        /// </summary>
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
                // 调用 SerialPortManager 的 SendString 方法发送文本（不追加 NewLine）
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