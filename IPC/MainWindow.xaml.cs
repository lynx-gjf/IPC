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

            // 获取所有可用串口端口，并添加到comboBoxCOM
            RefreshPortList();

            // 订阅数据接收事件（非 UI 线程触发）
            _spManager.DataReceived += SpManager_DataReceived;

            // 初始化编码选择（默认 UTF-8）
            if (comboBoxEncoding != null)
            {
                comboBoxEncoding.SelectedIndex = 0;
                UpdateEncodingFromSelection();
            }
        }
        private void TrafficLightControl_Loaded(object? sender, System.Windows.RoutedEventArgs e)
        {
            // 如果需要对该控件初始化，可在此处理
            // var tl = sender as IPC.TrafficLightControl;
        }

        /// <summary>
        /// 刷新串口列表（同时更新两个 ComboBox）
        /// </summary>
        private void RefreshPortList()
        {
            string[] ports = _spManager.GetPortNames();

            // 同步更新两个下拉框的项
            comboBoxCOM.ItemsSource = ports;
            comboBoxCOM1.ItemsSource = ports;

            if (ports.Length > 0)
            {
                comboBoxCOM.SelectedIndex = 0;
                comboBoxCOM1.SelectedIndex = 0;
            }
            else
            {
                comboBoxCOM.SelectedIndex = -1;
                comboBoxCOM1.SelectedIndex = -1;
            }
        }

        /// <summary>
        /// 来自 SerialPortManager 的数据接收事件
        /// </summary>
        private void SpManager_DataReceived(string data)
        {
            this.Dispatcher.Invoke(new Action(() =>
            {
                textBoxRecv.AppendText(data);
                textBoxRecv.ScrollToEnd();
            }));
        }

        /// <summary>
        /// 根据选择更新编码
        /// </summary>
        private void UpdateEncodingFromSelection()
        {
            if (comboBoxEncoding == null) return;

            Encoding chosen = comboBoxEncoding.SelectedIndex switch
            {
                0 => Encoding.UTF8,
                1 => Encoding.ASCII,
                2 => Encoding.GetEncoding("GB2312"),
                _ => Encoding.UTF8
            };

            _spManager.Encoding = chosen;
            Debug.WriteLine($"编码已切换为: {chosen.EncodingName}");
        }

        /// <summary>
        /// 编码选择改变事件
        /// </summary>
        private void ComboBoxEncoding_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateEncodingFromSelection();
        }

        /// <summary>
        /// 打开/关闭串口
        /// </summary>
        private void BtnOpenCloseCom_Click(object sender, RoutedEventArgs e)
        {
            if (_spManager.IsOpen)
            {
                _spManager.Close();
                btnOpenCloseCom.Content = "打开串口";
                Debug.WriteLine("关闭串口成功");

                // 启用配置控件
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
                    MessageBox.Show("请选择串口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int baud = comboBoxBaudRate.SelectedIndex switch
                {
                    0 => 9600,
                    1 => 19200,
                    2 => 38400,
                    3 => 115200,
                    4 => 1000000,
                    _ => 9600
                };

                try
                {
                    // 在打开前确保使用当前选择的编码
                    UpdateEncodingFromSelection();

                    _spManager.Open(portName, baud);
                    btnOpenCloseCom.Content = "关闭串口";
                    Debug.WriteLine($"打开串口成功: {portName}, 波特率: {baud}");

                    // 禁用配置控件
                    comboBoxBaudRate.IsEnabled = false;
                    comboBoxCOM.IsEnabled = false;
                    comboBoxDataBit.IsEnabled = false;
                    comboBoxStopBit.IsEnabled = false;
                    comboBoxSdd.IsEnabled = false;
                    comboBoxlik.IsEnabled = false;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"打开串口失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        /// <summary>
        /// 清空接收区
        /// </summary>
        private void BtnClearRecv_Click(object sender, RoutedEventArgs e)
        {
            _spManager.ClearBuffer();
            textBoxRecv.Clear();
        }

        private async void pumpOpenClose_Click(string add, object sender, RoutedEventArgs e)
        {
            if (!_spManager.IsOpen)
            {
                MessageBox.Show("请先打开串口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            string pumpOpen = "P1,G1,1";
            string pumpClose = "P1,G1,0";

            pumpOpen = add + pumpOpen;
            pumpClose = add + pumpClose;

            int pumpState = 0; // 0: 关闭, 1: 开启

            if (pumpState == 1)
            {
                _spManager.SendString(pumpClose);
                await Task.Delay(1000); // 延时1秒（非阻塞）
                btnOpenCloseCom.Content = "泵关闭";
                pumpState = 0;
            }
            else if (pumpState == 0)
            {
                _spManager.SendString(pumpOpen);
                btnOpenCloseCom.Content = "泵开启";
                pumpState = 1;
            }

            try
            {
                _spManager.SendString(pumpOpen);
                Debug.WriteLine("已发送: " + pumpOpen);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 把 HandleRateSet 改为接收 Tag 字符串（例如 "R1"/"R2"/"R3"）
        private void HandleRateSet(string add)
        {
            var tb = add switch
            {
                "R1" => textBoxFlowRate1,
                "R2" => textBoxFlowRate2,
                "R3" => textBoxFlowRate3,
                _ => textBoxFlowRate1
            };

            if (tb == null) return;

            string txt = tb.Text?.Trim() ?? string.Empty;
            if (!double.TryParse(txt, System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out double val))
            {
                MessageBox.Show("流速格式不正确，请输入数字（0.000 - 3.000）", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                tb.Text = "0.000";
                return;
            }

            // 限定范围并格式化为三位小数
            val = Math.Clamp(val, 0.0, 3.0);
            tb.Text = val.ToString("F3", System.Globalization.CultureInfo.InvariantCulture);

            // 如需发送到串口，可以使用 Tag（add）结合协议发送
            // _spManager.SendString($"{add},FLOW,{val:F3}");
        }

        // 使用 Tag 为 "R1"/"R2"/"R3" 的按钮通用处理器：调用 pumpOpenClose_Click
        private void PumpOpenCloseButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string add)
            {
                pumpOpenClose_Click(add, sender, e);
            }
        }

        // 使用 Tag 为 "R1"/"R2"/"R3" 的按钮通用处理器：调用 HandleRateSet
        private void RateSetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button btn && btn.Tag is string add)
            {
                HandleRateSet(add);
            }
        }

        /// <summary>
        /// 串口下拉框选择改变事件（在 XAML 中引用）
        /// </summary>
        private void comboBoxCOM_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // 如果需要响应端口选择变化，可在此处理。
            // 当前仅记录调试信息并确保选中的端口字符串可用。
            if (comboBoxCOM?.SelectedItem is string port)
            {
                Debug.WriteLine($"已选择串口: {port}");
            }
        }

        /// <summary>
        /// 串口下拉框展开时刷新列表
        /// </summary>
        private void ComboBoxCOM_DropDownOpened(object sender, EventArgs e)
        {
            RefreshPortList();
        }

        /// <summary>
        /// 发送按钮1 - 发送字符串
        /// </summary>
        private void BtnSend_Click(object sender, RoutedEventArgs e)
        {
            if (!_spManager.IsOpen)
            {
                MessageBox.Show("请先打开串口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string toSend = textBoxSend?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(toSend))
            {
                MessageBox.Show("发送内容为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 如果勾选了自动添加回车换行,则添加\r\n
            if (checkBoxAddCRLF?.IsChecked == true)
            {
                toSend += "\r\n";
            }

            try
            {
                _spManager.SendString(toSend);
                Debug.WriteLine("已发送: " + toSend);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 发送按钮2 - 发送字符串
        /// </summary>
        private void btnSend_Click2(object sender, RoutedEventArgs e)
        {
            if (!_spManager.IsOpen)
            {
                MessageBox.Show("请先打开串口", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string toSend = textBoxSend2?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(toSend))
            {
                MessageBox.Show("发送内容为空", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 如果勾选了自动添加回车换行,则添加\r\n
            if (checkBoxAddCRLF?.IsChecked == true)
            {
                toSend += "\r\n";
            }

            try
            {
                _spManager.SendString(toSend);
                Debug.WriteLine("已发送: " + toSend);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发送失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // 新增：为 XAML 中声明的数字输入事件添加处理器
        private void Numeric_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            if (sender is TextBox tb)
            {
                // 构造输入后文本以便验证（考虑选中文本会被替换的情况）
                string current = tb.Text ?? string.Empty;
                if (tb.SelectionLength > 0)
                    current = current.Remove(tb.SelectionStart, tb.SelectionLength);
                string proposed = current.Insert(tb.CaretIndex, e.Text);

                // 允许数字和小数点，且小数位最多 3 位
                if (!Regex.IsMatch(proposed, @"^\d*(\.\d{0,3})?$"))
                {
                    e.Handled = true;
                }
            }
        }

        private void Numeric_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // 禁止空格，其它控制键（退格、删除、方向键、Tab、Enter）保持默认行为
            if (e.Key == Key.Space)
            {
                e.Handled = true;
            }
        }

        private void Numeric_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                string txt = tb.Text?.Trim() ?? string.Empty;
                if (!double.TryParse(txt, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double val))
                {
                    tb.Text = "0.000";
                    return;
                }

                val = Math.Clamp(val, 0.0, 3.0);
                tb.Text = val.ToString("F3", CultureInfo.InvariantCulture);
            }
        }

        // 新增：XAML 中引用的 TextChanged 事件处理器，避免缺失引用错误
        private void textBoxFlowRate_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                // 只在控件失去焦点时格式化，避免用户输入过程中的干扰
                if (!tb.IsFocused)
                {
                    string txt = tb.Text?.Trim() ?? string.Empty;
                    if (double.TryParse(txt, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double val))
                    {
                        val = Math.Clamp(val, 0.0, 3.0);
                        string formatted = val.ToString("F3", CultureInfo.InvariantCulture);
                        if (formatted != tb.Text)
                        {
                            tb.Text = formatted;
                        }
                    }
                }
            }
        }

        // 新增：修复 XAML 中引用但未在代码中实现的 textBoxRecv_TextChanged 事件处理器
        private void textBoxRecv_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                // 当文本由后台线程追加时，将光标置于末尾并滚动到底部，避免 UI 混乱
                tb.CaretIndex = tb.Text?.Length ?? 0;
                tb.ScrollToEnd();
            }
        }

        // 将 XAML 中引用的 pumpOpenClose_Click 转发到已有的通用处理器
        private void pumpOpenClose_Click(object sender, RoutedEventArgs e)
        {
            PumpOpenCloseButton_Click(sender, e);
        }

        /// <summary>
        /// 为 XAML 中可能引用的 comboBoxCOM1 添加 SelectionChanged 事件处理器，复用 comboBoxCOM 的逻辑
        /// </summary>
        private void comboBoxCOM1_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            // 如果两个组合框都需要相同处理，直接复用已有实现
            comboBoxCOM_SelectionChanged(sender, e);
        }

        /// <summary>
        /// 顶部面板的“刷新端口”按钮处理器（复用 RefreshPortList）
        /// </summary>
        private void BtnRefreshPorts_Click(object sender, RoutedEventArgs e)
        {
            RefreshPortList();
            Debug.WriteLine("已刷新串口列表（由 btnRefreshPorts1 触发）");
        }
    }
}