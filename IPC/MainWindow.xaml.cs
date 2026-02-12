using System;
using System.Diagnostics;
using System.Text;
using System.Windows;

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

        /// <summary>
        /// 刷新串口列表
        /// </summary>
        private void RefreshPortList()
        {
            string[] ports = _spManager.GetPortNames();
            comboBoxCOM.ItemsSource = ports;
            if (ports.Length > 0)
                comboBoxCOM.SelectedIndex = 0;
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

        private void textBoxRecv_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {

        }
    }
}