using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Windows.ApplicationModel.Background;
using Windows.Devices.Gpio;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;
using Windows.System.Threading;

// The Background Application template is documented at http://go.microsoft.com/fwlink/?LinkID=533884&clcid=0x409

namespace App1
{
    public sealed class StartupTask : IBackgroundTask
    {
        private BackgroundTaskDeferral deferral;
        private ThreadPoolTimer timer;
        private const int LED_PIN = 5;
        private GpioPin pin;
        private GpioPinValue pinValue;

        private const int HTU21DF_I2CADDR = 0x40;
        private const int HTU21DF_READTEMP = 0xE3;
        private const int HTU21DF_READHUM = 0xE5;
        private const int HTU21DF_WRITEREG = 0xE6;
        private const int HTU21DF_READREG = 0xE7;
        private const int HTU21DF_RESET = 0xFE;

        private const char degreeSymbol = (char)176;

        I2cController controller;
        I2cDevice device;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            // 
            // TODO: Insert code to perform background work
            //
            // If you start any asynchronous methods here, prevent the task
            // from closing prematurely by using BackgroundTaskDeferral as
            // described in http://aka.ms/backgroundtaskdeferral
            //

            deferral = taskInstance.GetDeferral();

            //InitGpio();
            InitI2C();
            while (controller == null)
            { }
            ResetI2C();
            StartTimer_I2C();
        }

        private void InitGpio()
        {
            GpioController gpio = GpioController.GetDefault();
            pin = gpio.OpenPin(LED_PIN);
            pinValue = GpioPinValue.High;
            pin.Write(pinValue);
            pin.SetDriveMode(GpioPinDriveMode.Output);
        }

        private void StartTimer_GPIO()
        {
            timer = ThreadPoolTimer.CreatePeriodicTimer((timer) =>
            {
                if (GpioPinValue.High == pinValue)
                    pinValue = GpioPinValue.Low;
                else
                    pinValue = GpioPinValue.High;

                pin.Write(pinValue);
            },
            new TimeSpan(0, 0, 1));
        }

        private async void InitI2C()
        {
            // Create an I2cDevice with the specified I2C settings
            controller = await I2cController.GetDefaultAsync();

            // 0x40 is the I2C device address
            var settings = new I2cConnectionSettings(HTU21DF_I2CADDR);
            // FastMode = 400KHz
            settings.BusSpeed = I2cBusSpeed.FastMode;
            device = controller.GetDevice(settings);
        }

        private void ResetI2C()
        {
            // Reset
            byte[] writeBuf = { HTU21DF_RESET };
            device.Write(writeBuf);
            System.Threading.SpinWait.SpinUntil(() => false, 15);

            // Init
            writeBuf = new byte[] { HTU21DF_READREG };
            device.Write(writeBuf);
            byte[] readBuf = new byte[32];
            device.Read(readBuf);
            // After reset, readBuf should be 0x2
            System.Diagnostics.Debug.WriteLine(readBuf[0].ToString());
        }

        private void StartTimer_I2C()
        {
            timer = ThreadPoolTimer.CreatePeriodicTimer((timer) =>
            {
                double temperature = ReadTemperature();
                double fahrenheit = temperature * 1.8 + 32;

                double humidity = ReadHumidity();

                System.Diagnostics.Debug.WriteLine(String.Format("Temp: {0}{1}F Humidity: {2}%" , 
                    Math.Round(fahrenheit,2, MidpointRounding.AwayFromZero),
                    degreeSymbol,
                    Math.Round(humidity)
                    )
                    );
            },
            new TimeSpan(0, 0, 2));
        }

        private double ReadTemperature()
        {
            byte[] writeBuf = new byte[] { HTU21DF_READTEMP };
            device.Write(writeBuf);
            byte[] readBuf = new byte[3];
            device.Read(readBuf);

            ushort t = readBuf[0];
            t <<= 8;
            t |= readBuf[1];

            double temp = t;
            temp *= 175.72;
            temp /= 65536;
            temp -= 46.85;

            return temp;
        }

        private double ReadHumidity()
        {
            byte[] writeBuf = new byte[] { HTU21DF_READHUM };
            device.Write(writeBuf);
            byte[] readBuf = new byte[3];
            device.Read(readBuf);

            ushort h = readBuf[0];
            h <<= 8;
            h |= readBuf[1];

            double humidity = h;
            humidity *= 125;
            humidity /= 65536;
            humidity -= 6;

            return humidity;
        }
    }
}
