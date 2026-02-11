using System;
using System.IO.Ports;
using System.Text;

namespace IPC
{
    /// <summary>
    /// 串口管理类：简单的字符串收发
    /// </summary>
    public class SerialPortManager : IDisposable
    {
        private readonly SerialPort _serialPort = new();
        private readonly object _writeLock = new();

        /// <summary>
        /// 当接收到数据时触发，参数为接收到的字符串（根据当前编码解析）
        /// 注意：回调在非 UI 线程触发，订阅者需在 UI 线程更新控件
        /// </summary>
        public event Action<string>? DataReceived;

        /// <summary>
        /// 编码设置
        /// </summary>
        public Encoding Encoding { get; set; } = Encoding.UTF8;

        public bool IsOpen => _serialPort.IsOpen;

        public string[] GetPortNames() => SerialPort.GetPortNames();

        public void Open(string portName, int baudRate)
        {
            if (string.IsNullOrEmpty(portName)) 
                throw new ArgumentNullException(nameof(portName));
            
            if (_serialPort.IsOpen) 
                return;

            _serialPort.PortName = portName;
            _serialPort.BaudRate = baudRate;
            _serialPort.DataBits = 8;
            _serialPort.StopBits = StopBits.One;
            _serialPort.Parity = Parity.None;
            _serialPort.Handshake = Handshake.None;
            _serialPort.Encoding = this.Encoding;
            _serialPort.DataReceived += InternalDataReceived;

            _serialPort.Open();
        }

        public void Close()
        {
            if (!_serialPort.IsOpen) 
                return;

            _serialPort.DataReceived -= InternalDataReceived;
            _serialPort.Close();
        }

        public void ClearBuffer()
        {
            if (_serialPort.IsOpen && _serialPort.BytesToRead > 0)
            {
                _serialPort.DiscardInBuffer();
            }
        }

        private void InternalDataReceived(object? sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort.BytesToRead <= 0) 
                    return;

                string receivedData = _serialPort.ReadExisting();
                
                if (!string.IsNullOrEmpty(receivedData))
                {
                    DataReceived?.Invoke(receivedData);
                }
            }
            catch
            {
                // 忽略读取错误
            }
        }

        /// <summary>
        /// 发送字符串
        /// </summary>
        public void SendString(string text)
        {
            if (text is null) 
                throw new ArgumentNullException(nameof(text));
            
            if (!_serialPort.IsOpen) 
                throw new InvalidOperationException("串口未打开");

            lock (_writeLock)
            {
                _serialPort.Write(text);
            }
        }

        public void Dispose()
        {
            try
            {
                Close();
                _serialPort.Dispose();
            }
            catch { }
        }
    }
}