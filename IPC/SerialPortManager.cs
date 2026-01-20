using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace IPC
{
    /// <summary>
    /// 串口管理类（只收发固定 8 字节的十六进制数据）：封装 SerialPort，提供打开/关闭、接收 8Byte 帧的事件与发送方法
    /// 接收回调提供单个 8 字节帧的十六进制字符串（空格分隔，大写）
    /// 另外增加对文本消息的解析：当收到包含 $$ADC:[...]ADC$$ 时触发事件并返回解析后的 ADC 数组
    /// 注意：回调在非 UI 线程触发，订阅者需在 UI 线程更新控件
    /// </summary>
    public class SerialPortManager : IDisposable
    {
        private readonly SerialPort _serialPort = new();
        private readonly Queue<byte> _buffer = new();
        private readonly StringBuilder _asciiBuffer = new();
        private readonly StringBuilder _textBuffer = new(); // 新增：文本缓冲，用于解析 $$ADC:[...]ADC$$
        private readonly object _writeLock = new();

        /// <summary>
        /// 当接收到完整 8 字节帧后触发，参数为该 8 字节的十六进制字符串（空格分隔，大写，例如 "AA BB CC DD EE FF 00 11"）
        /// 注意：回调在非 UI 线程触发，订阅者需在 UI 线程更新控件
        /// </summary>
        public event Action<string>? DataReceived;

        /// <summary>
        /// 当接收到任意文本（经解码）时触发，参数为接收到的文本片段（例如包含 $$ADC[...]ADC$$ 的字符串）
        /// 注意：回调在非 UI 线程触发
        /// </summary>
        public event Action<string>? TextReceived;

        /// <summary>
        /// 当解析到 $$ADC:[...]ADC$$ 并成功解析为数值数组时触发，参数为解析后的 double[]（按顺序）
        /// 注意：回调在非 UI 线程触发
        /// </summary>
        public event Action<double[]>? AdcArrayReceived;

        /// <summary>
        /// 保留编码属性（对固定字节帧模式不使用）
        /// </summary>
        public Encoding Encoding { get; set; } = Encoding.UTF8;

        public bool IsOpen => _serialPort.IsOpen;

        public string[] GetPortNames() => SerialPort.GetPortNames();

        public void Open(string portName, int baudRate)
        {
            if (string.IsNullOrEmpty(portName)) throw new ArgumentNullException(nameof(portName));
            if (_serialPort.IsOpen) return;

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
            if (!_serialPort.IsOpen) return;

            _serialPort.DataReceived -= InternalDataReceived;
            _serialPort.Close();
        }

        public void ClearBuffer()
        {
            lock (_buffer)
            {
                _buffer.Clear();
                _asciiBuffer.Clear();
                _textBuffer.Clear();
            }
        }

        private void InternalDataReceived(object? sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                // 等待短暂时间以确保一帧数据到齐
                Thread.Sleep(1);

                int count = _serialPort.BytesToRead;
                if (count <= 0) return;

                byte[] data = new byte[count];
                int read = _serialPort.Read(data, 0, count);
                if (read <= 0) return;

                // 尝试把接收到的数据按文本解码，追加到文本缓冲，用于查找 $$ADC:[...]ADC$$
                try
                {
                    string incomingText = _serialPort.Encoding.GetString(data, 0, read);
                    if (!string.IsNullOrEmpty(incomingText))
                    {
                        lock (_textBuffer)
                        {
                            _textBuffer.Append(incomingText);
                            // 快速通知订阅者接收的文本片段（原样）
                            TextReceived?.Invoke(incomingText);

                            // 尝试解析可能包含的 $$ADC:[...]ADC$$ 模式（可能有多个）
                            const string startToken = "$$ADC:[";
                            const string endToken = "]ADC$$";

                            while (true)
                            {
                                string tbStr = _textBuffer.ToString();
                                int index_start = tbStr.IndexOf(startToken, StringComparison.Ordinal);
                                int index_end = tbStr.IndexOf(endToken, StringComparison.Ordinal);
                                if (index_start >= 0 && index_end > index_start)
                                {
                                    int contentStart = index_start + startToken.Length;
                                    int contentLength = index_end - contentStart;
                                    string content = tbStr.Substring(contentStart, contentLength);

                                    // 解析以逗号分隔的数字
                                    string[] parts = content.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                                    var values = new List<double>(parts.Length);
                                    foreach (var p in parts)
                                    {
                                        if (double.TryParse(p.Trim(), out double v))
                                            values.Add(v);
                                    }

                                    if (values.Count > 0)
                                    {
                                        AdcArrayReceived?.Invoke(values.ToArray());
                                    }

                                    // 移除已处理的部分（包括结束标记）
                                    int removeUntil = index_end + endToken.Length;
                                    _textBuffer.Remove(0, removeUntil);
                                    // 继续循环，处理同一缓冲区中可能存在的下一个匹配
                                    continue;
                                }

                                // 没有完整匹配时，提前退出（保留部分可能的未完成片段）
                                break;
                            }
                        }
                    }
                }
                catch
                {
                    // 解码失败则忽略文本解析部分，但不影响其他逻辑
                }

                // 判断当前到达的数据是否为 ASCII 十六进制流（包含 0-9 A-F a-f 分隔符 空格/逗号/0x）
                bool isAsciiHex = true;
                for (int i = 0; i < read; i++)
                {
                    byte b = data[i];
                    if (!IsHexAsciiByte(b) && b != (byte)' ' && b != (byte)',' && b != (byte)':' && b != (byte)';' &&
                        b != (byte)'\r' && b != (byte)'\n' && b != (byte)'x' && b != (byte)'X' && b != (byte)'0')
                    {
                        isAsciiHex = false;
                        break;
                    }
                }

                lock (_buffer)
                {
                    if (isAsciiHex)
                    {
                        // 把 ASCII 字节追加到 asciiBuffer，提取有效十六进制字符
                        for (int i = 0; i < read; i++)
                            _asciiBuffer.Append((char)data[i]);

                        // 只保留 0-9A-Fa-f 字符，连续计数，每两位生成一个字节
                        var hexOnly = new StringBuilder(_asciiBuffer.Length);
                        foreach (char c in _asciiBuffer.ToString())
                        {
                            if (IsHexChar(c)) hexOnly.Append(c);
                        }

                        // 将已处理的字符长度截断（保留不足两位的尾部）
                        int hexCount = hexOnly.Length;
                        int completePairs = hexCount / 2;
                        int completeBytes = completePairs; // 每 pair -> 1 byte

                        int processedChars = completePairs * 2;
                        // 生成完整的 8 字节帧（需要 16 hex 字符）
                        int idx = 0;
                        while (completeBytes >= 8)
                        {
                            var frame = new byte[8];
                            for (int j = 0; j < 8; j++)
                            {
                                string hex = hexOnly.ToString(idx * 2, 2);
                                frame[j] = Convert.ToByte(hex, 16);
                                idx++;
                            }

                            // 通知并继续
                            string hexStr = BytesToHex(frame);
                            DataReceived?.Invoke(hexStr);
                            completeBytes -= 8;
                        }

                        // 清理 asciiBuffer：保留未处理的尾部 hex 字符
                        string remaining = hexOnly.ToString(idx * 2, completeBytes * 2);
                        _asciiBuffer.Clear();
                        _asciiBuffer.Append(remaining);
                    }
                    else
                    {
                        // 二进制模式：按字节入队，组成 8 字节帧
                        for (int i = 0; i < read; i++) _buffer.Enqueue(data[i]);

                        while (_buffer.Count >= 8)
                        {
                            var frame = new byte[8];
                            for (int j = 0; j < 8; j++) frame[j] = _buffer.Dequeue();

                            string hex = BytesToHex(frame);
                            DataReceived?.Invoke(hex);
                        }
                    }
                }
            }
            catch
            {
                // 忽略读取错误
            }
        }

        private static bool IsHexAsciiByte(byte b)
        {
            return (b >= (byte)'0' && b <= (byte)'9')
                   || (b >= (byte)'A' && b <= (byte)'F')
                   || (b >= (byte)'a' && b <= (byte)'f');
        }

        private static bool IsHexChar(char c)
        {
            return (c >= '0' && c <= '9') ||
                   (c >= 'A' && c <= 'F') ||
                   (c >= 'a' && c <= 'f');
        }

        private static string BytesToHex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 3 - 1);
            for (int i = 0; i < bytes.Length; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(bytes[i].ToString("X2"));
            }
            return sb.ToString();
        }

        /// <summary>
        /// 发送 8 字节十六进制字符串。支持 "AA BB 0C" 或 "AABB0C..." 等格式，最终必须正好为 8 字节
        /// </summary>
        public void SendHex(string hexString)
        {
            if (hexString is null) throw new ArgumentNullException(nameof(hexString));
            if (!_serialPort.IsOpen) throw new InvalidOperationException("串口未打开");

            // 只保留十六进制字符
            var sb = new StringBuilder(hexString.Length);
            foreach (char c in hexString)
            {
                if ((c >= '0' && c <= '9') ||
                    (c >= 'a' && c <= 'f') ||
                    (c >= 'A' && c <= 'F'))
                    sb.Append(c);
            }

            string s = sb.ToString();
            if (s.Length % 2 != 0) throw new ArgumentException("hexString 长度应为偶数（每字节两位）");

            byte[] data = new byte[s.Length / 2];
            for (int i = 0; i < data.Length; i++)
            {
                data[i] = Convert.ToByte(s.Substring(i * 2, 2), 16);
            }

            if (data.Length != 8) throw new ArgumentException("只支持发送正好 8 字节的数据");

            lock (_writeLock)
            {
                _serialPort.Write(data, 0, data.Length);
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