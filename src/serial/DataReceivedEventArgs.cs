namespace SerialFeeder.serial
{
    using System;
    public class DataReceivedEventArgs : EventArgs
    {
        private string _data;
        public DataReceivedEventArgs(string data)
        {
            _data = data;
        }
        public string Data
        {
            get { return _data; }
        }
    }
}