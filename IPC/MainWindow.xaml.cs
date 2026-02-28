using System;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Controls;
using System.Globalization;

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

            // 注册 CodePages 提供器,以便支持 GBK/GB2312 等编码（仅需一次）
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            RefreshPortList();

            // 仅保留泵连接页按钮
            SetOpenCloseButtonsContent("打开串口");

            // 订阅数据接收事件（非 UI 线程触发）
            _spManager.DataReceived += SpManager_DataReceived;

            // 串口设置页已删除：编码固定 UTF-8
            _spManager.Encoding = Encoding.UTF8;
        }

        private void SetOpenCloseButtonsContent(string text)
        {
            if (btnOpenCloseCom1 != null)
                btnOpenCloseCom1.Content = text;
        }

        /// <summary>
        /// 刷新串口列表（仅更新泵连接页 COM）
        /// </summary>
        private void RefreshPortList()
        {
            string[] ports = _spManager.GetPortNames();

            comboBoxCOM1.ItemsSource = ports;
            comboBoxCOM1.SelectedIndex = ports.Length > 0 ? 0 : -1;
        }

        /// <summary>
        /// 来自 SerialPortManager 的数据接收事件
        /// </summary>
        private void SpManager_DataReceived(string data)
        {
            this.Dispatcher.Invoke(new Action(() =>
            {
                string ts = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture);
                string line = $"[{ts}] {data}";

                textBoxDebugRecv.AppendText(line);
                if (!line.EndsWith("\n", StringComparison.Ordinal))
                {
                    textBoxDebugRecv.AppendText(Environment.NewLine);
                }
                textBoxDebugRecv.ScrollToEnd();
            }));
        }

        /// <summary>
        /// 串口设置页已删除：固定 UTF-8
        /// </summary>
        private void UpdateEncodingFromSelection()
        {
            _spManager.Encoding = Encoding.UTF8;
        }

        private void ComboBoxEncoding_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateEncodingFromSelection();
        }

        // 波特率固定 115200
        private int GetSelectedBaudRate() => 115200;

        /// <summary>
        /// 打开/关闭串口（仅使用 comboBoxCOM1，波特率固定 115200）
        /// </summary>
        private void BtnOpenCloseCom_Click(object sender, RoutedEventArgs e)
        {
            if (_spManager.IsOpen)
            {
                _spManager.Close();
                SetOpenCloseButtonsContent("打开串口");
                Debug.WriteLine("关闭串口成功");
                comboBoxCOM1.IsEnabled = true;
            }
            else
            {
                string? portName = comboBoxCOM1.SelectedItem as string;
                if (string.IsNullOrEmpty(portName))
                {
                    MessageBox.Show("请选择串口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                const int baud = 115200;

                try
                {
                    _spManager.Open(portName, baud);
                    SetOpenCloseButtonsContent("关闭串口");
                    Debug.WriteLine($"打开串口成功: {portName}, 波特率: {baud}");
                    comboBoxCOM1.IsEnabled = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"打开串口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BtnClearRecv_Click(object sender, RoutedEventArgs e)
        {
            _spManager.ClearBuffer();
            textBoxDebugRecv.Clear();
        }

        private void comboBoxCOM_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.SelectedItem is string port)
            {
                Debug.WriteLine($"已选择串口: {port}");
            }
        }

        private void comboBoxCOM1_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            comboBoxCOM_SelectionChanged(sender, e);
        }

        private void BtnRefreshPorts_Click(object sender, RoutedEventArgs e)
        {
            RefreshPortList();
        }

        private void Numeric_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is not TextBox textBox)
            {
                e.Handled = true;
                return;
            }

            e.Handled = !Regex.IsMatch(e.Text, "^[0-9.]$")
                        || (e.Text == "." && textBox.Text.Contains('.'));
        }

        private void Numeric_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void Numeric_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (double.TryParse(textBox.Text, out var value))
                {
                    textBox.Text = value.ToString("0.000", CultureInfo.InvariantCulture);
                }
                else
                {
                    textBox.Text = "0.000";
                }
            }
        }

        private void textBoxFlowRate_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 预留：可在此实现流速联动逻辑
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            // 预留：用于静态文本框事件占位
        }

        private void pumpOpenClose_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn)
            {
                return;
            }

            string tag = btn.Tag?.ToString() ?? string.Empty;
            string pumpNo = tag switch
            {
                "R1" => "1",
                "R2" => "2",
                "R3" => "3",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(pumpNo))
            {
                return;
            }

            bool isOpenAction = Equals(btn.Content, "开启注射泵");
            string b = isOpenAction ? "1" : "0";
            string cmd = $"R{pumpNo},P1,G1,{b}";

            try
            {
                _spManager.SendString(cmd);
                btn.Content = isOpenAction ? "关闭注射泵" : "开启注射泵";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RateSetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button)
            {
                return;
            }

            string tag = button.Tag?.ToString() ?? string.Empty;
            string pumpNo = tag switch
            {
                "R1" => "1",
                "R2" => "2",
                "R3" => "3",
                _ => string.Empty
            };

            TextBox? source = tag switch
            {
                "R1" => textBoxFlowRate1,
                "R2" => textBoxFlowRate2,
                "R3" => textBoxFlowRate3,
                _ => null
            };

            if (source is null || string.IsNullOrEmpty(pumpNo))
            {
                return;
            }

            if (tag == "R1") RateShow1.Text = source.Text;
            if (tag == "R2") RateShow2.Text = source.Text;
            if (tag == "R3") RateShow3.Text = source.Text;

            // 支持用户输入逗号或点
            string input = source.Text.Trim().Replace(',', '.');
            if (!decimal.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal rate))
            {
                MessageBox.Show("流速格式无效", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (rate < 0m)
            {
                MessageBox.Show("流速不能为负数", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 改这里：不要再限制 0.3
            const decimal maxRate = 9.9999m; // 协议5位(×10000)可表示到 9.9999
            if (rate > maxRate)
            {
                MessageBox.Show($"流速不能大于 {maxRate:0.####}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int raw = (int)Math.Round(rate * 10000m, MidpointRounding.AwayFromZero);
            string value = raw.ToString("D5", CultureInfo.InvariantCulture); // 0.1 -> 01000
            string cmd = $"R{pumpNo},P1,S3,{value}";

            try
            {
                _spManager.SendString(cmd);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDebugSend_Click(object sender, RoutedEventArgs e)
        {
            string text = textBoxDebugSend.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            try
            {
                _spManager.SendString(text);
                textBoxDebugSend.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void btnSend_Click2(object sender, RoutedEventArgs e)
        {
            string text = textBoxSend2.Text;
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            try
            {
                _spManager.SendString(text);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void textBoxRecv_TextChanged(object sender, TextChangedEventArgs e)
        {
            textBoxRecv.ScrollToEnd();
        }

        private void TrafficLightControl_Loaded(object sender, RoutedEventArgs e)
        {
            // TODO: 初始化逻辑
        }
    }
}