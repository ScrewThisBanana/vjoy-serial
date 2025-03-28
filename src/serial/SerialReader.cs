namespace SerialFeeder.serial
{
    using System;
    using System.Linq;
    using System.Text;
    using System.IO.Ports;
    using Microsoft.Extensions.Options;
    using Microsoft.Extensions.Logging;

    public class SerialReader : IDisposable
    {
        private readonly SerialPort _serialPort;
        private readonly ILogger _logger;
        public event DataReceivedEventHandler? DataReceived;

        public SerialReader(IOptions<PortConfiguration> portConfig, ILogger<SerialReader> logger)
        {
            _logger = logger;
            _serialPort = new SerialPort();
            _serialPort.PortName = portConfig.Value.SerialPort;
            _serialPort.BaudRate = portConfig.Value.BaudRate;
            _serialPort.Parity = Parity.None;
            _serialPort.DataBits = 8;
            _serialPort.StopBits = StopBits.One;
            _serialPort.Handshake = Handshake.None;
            _serialPort.DataReceived += new SerialDataReceivedEventHandler(OnDataReceived);
        }

        public void Open()
        {
            if (!_serialPort.IsOpen)
            {
                _logger.LogInformation($"Opening serial port {_serialPort.PortName} with baud rate {_serialPort.BaudRate}");
                _serialPort.Open();
            }
        }

        StringBuilder sb = new StringBuilder();
        private object _lock = new object();
        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            // Handle data received from the serial port
            SerialPort sp = (SerialPort)sender;
            string indata = sp.ReadExisting();

            _logger.LogDebug($"Data Received triggered. Indata [{indata.Replace(Environment.NewLine, "\\r\\n")}]");
            lock (_lock)
            {
                if (indata.Contains(Environment.NewLine)) // aa bb cc
                {
                    string[] parts = indata.Split(Environment.NewLine);

                    sb.Append(parts[0]);
                    _logger.LogDebug($"Triggering serial received event with data: [{sb}]");
                    DataReceived?.Invoke(this, new DataReceivedEventArgs(sb.ToString()));
                    sb.Clear();

                    for (int idx = 1; idx < parts.Length - 1; idx++)
                    {
                        sb.Append(parts[idx]);
                        _logger.LogDebug($"Triggering serial received event with data: [{sb}]");
                        DataReceived?.Invoke(this, new DataReceivedEventArgs(sb.ToString()));
                        sb.Clear();
                    }

                    sb.Append(parts[parts.Length - 1]);
                }
                else
                {
                    sb.Append(indata);
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                _logger.LogInformation($"Closing serial port {_serialPort.PortName} with baud rate {_serialPort.BaudRate}");
                _serialPort.Close();
                _serialPort.Dispose();
            }
        }
    }
}