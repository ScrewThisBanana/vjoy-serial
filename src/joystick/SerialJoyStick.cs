namespace SerialFeeder.joystick
{
    using System;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using vJoyInterfaceWrap;

    public class SerialJoyStick : IDisposable
    {
        const int MIN = 1000;
        const int MAX = 2000;

        const int MIN_VJOY = 0x00;
        const int MAX_VJOY = 0x7FFF;

        private readonly vJoy _joystick;
        private readonly uint JOYSTICK_ID;
        private bool _initialized = false;

        private vJoy.JoystickState _state = new vJoy.JoystickState();
        private bool disposedValue;

        private readonly ILogger _logger;

        public SerialJoyStick(IOptions<JoystickConfiguration> joystickConfig, ILogger<SerialJoyStick> logger)
        {
            _logger = logger;
            _joystick = new vJoy();
            JOYSTICK_ID = joystickConfig.Value.JoystickId;
        }

        public bool Initialize()
        {            
            // Get the driver attributes (Vendor ID, Product ID, Version Number)
            if (!_joystick.vJoyEnabled())
            {
                _logger.LogCritical("vJoy driver not enabled: Failed Getting vJoy attributes.\n");
                return false;
            }
            else
                _logger.LogInformation("Vendor: {0}\nProduct :{1}\nVersion Number:{2}\n",
                _joystick.GetvJoyManufacturerString(),
                _joystick.GetvJoyProductString(),
                _joystick.GetvJoySerialNumberString());

            // Get the state of the requested device
            VjdStat status = _joystick.GetVJDStatus(JOYSTICK_ID);
            switch (status)
            {
                case VjdStat.VJD_STAT_OWN:
                    _logger.LogDebug("vJoy Device {0} is already owned by this feeder\n", JOYSTICK_ID);
                    break;
                case VjdStat.VJD_STAT_FREE:
                    _logger.LogDebug("vJoy Device {0} is free\n", JOYSTICK_ID);
                    break;
                case VjdStat.VJD_STAT_BUSY:
                    _logger.LogError(
                    "vJoy Device {0} is already owned by another feeder\nCannot continue\n", JOYSTICK_ID);
                    return false;
                case VjdStat.VJD_STAT_MISS:
                     _logger.LogError(
                    "vJoy Device {0} is not installed or disabled\nCannot continue\n", JOYSTICK_ID);
                    return false;
                default:
                     _logger.LogError("vJoy Device {0} general error\nCannot continue\n", JOYSTICK_ID);
                    return false;
            }

            ///// vJoy Device properties
            int nBtn = _joystick.GetVJDButtonNumber(JOYSTICK_ID);
            int nDPov = _joystick.GetVJDDiscPovNumber(JOYSTICK_ID);
            int nCPov = _joystick.GetVJDContPovNumber(JOYSTICK_ID);
            bool X_Exist = _joystick.GetVJDAxisExist(JOYSTICK_ID, HID_USAGES.HID_USAGE_X);
            bool Y_Exist = _joystick.GetVJDAxisExist(JOYSTICK_ID, HID_USAGES.HID_USAGE_Y);
            bool Z_Exist = _joystick.GetVJDAxisExist(JOYSTICK_ID, HID_USAGES.HID_USAGE_Z);
            bool RX_Exist = _joystick.GetVJDAxisExist(JOYSTICK_ID, HID_USAGES.HID_USAGE_RX);
            bool RY_Exist = _joystick.GetVJDAxisExist(JOYSTICK_ID, HID_USAGES.HID_USAGE_RY);
            bool RZ_Exist = _joystick.GetVJDAxisExist(JOYSTICK_ID, HID_USAGES.HID_USAGE_RZ);
            var prt = String.Format("Device[{0}]: Buttons={1}; DiscPOVs:{2}; ContPOVs:{3}", JOYSTICK_ID, nBtn, nDPov, nCPov);
             _logger.LogDebug(prt);
            var prt2 = string.Format("X_Exist: {0}, Y_Exist: {1}, Z_Exist: {2}, RX_Exist: {3}, RY_Exist: {4}, RZ_Exist: {5}", X_Exist, Y_Exist, Z_Exist, RX_Exist, RY_Exist, RZ_Exist);
            _logger.LogDebug(prt2);

            if(!Y_Exist | !Z_Exist || !RX_Exist || !X_Exist || !RY_Exist || !RZ_Exist)
            {
                _logger.LogError("Required Axis do not exist on device {0}", JOYSTICK_ID);
                return false;
            }

            VjdStat statusa;
            statusa = _joystick.GetVJDStatus(JOYSTICK_ID);
            // Acquire the target
            if ((statusa == VjdStat.VJD_STAT_OWN) || ((statusa == VjdStat.VJD_STAT_FREE) && (!_joystick.AcquireVJD(JOYSTICK_ID))))
                prt = String.Format("Failed to acquire vJoy device number {0}.", JOYSTICK_ID);
            else
                prt = String.Format("Acquired: vJoy device number {0}.", JOYSTICK_ID);
            _logger.LogInformation(prt);

            _initialized = true;
            return true;
        }

        public void Update(AxisStruct axisData)
        {
            if(!_initialized)
            {
                _logger.LogCritical("Joystick not initialized.");
                return;
            }

            _state.bDevice = (byte)JOYSTICK_ID;
            _state.AxisX = Calculcate(axisData.AILERON);
            _state.AxisZ = Calculcate(axisData.THROTTLE);
            _state.AxisXRot = Calculcate(axisData.RUDDER);
            _state.AxisY = Calculcate(axisData.ELEV);
            _state.AxisYRot = Calculcate(axisData.AUX1);
            _state.AxisZRot = Calculcate(axisData.AUX2);

            if (!_joystick.UpdateVJD(JOYSTICK_ID, ref _state))
            {
                _logger.LogWarning("Failed to update vJoy device.");
                _joystick.AcquireVJD(JOYSTICK_ID);
            }
        }

        private int Calculcate(int value)
        {
            double absolute = value - MIN;
            if(absolute < 0){absolute = 0;}

            int calc = (int)(absolute / (MAX - MIN) * MAX_VJOY-MIN_VJOY) + MIN_VJOY;

            return calc;
        }

        public static AxisStruct Convert(string input)
        {
            string[] parts = input.Split("|");
            if(parts.Length < 6)
                throw new ArgumentException("Invalid input format.");
            
            int[] values = new int[parts.Length];
            for(int idx = 0; idx < parts.Length; idx++)
            {
                string itm = parts[idx];
                if(itm.Contains(":"))
                {
                    string val = parts[idx].Split(":")[1].Trim();
                    values[idx] = int.Parse(val);
                }
            }

            AxisStruct result = new AxisStruct
            {
                AILERON = values[0],
                ELEV = values[1],
                THROTTLE = values[2],
                RUDDER = values[3],
                AUX1 = values[4],
                AUX2 = values[5]
            };

            return result;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    _logger.LogInformation($"Releasing vJoy device {JOYSTICK_ID}.");
                    _joystick.RelinquishVJD(JOYSTICK_ID);
                }

                disposedValue = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~SerialJoyStick()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
