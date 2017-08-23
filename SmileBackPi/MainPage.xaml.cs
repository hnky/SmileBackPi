using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Windows.Devices.Gpio;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Xaml.Controls;
using Microsoft.ProjectOxford.Emotion;
using Microsoft.ProjectOxford.Emotion.Contract;

namespace SmileBackPi
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly string _emotionServiceKey = "<EMOTION'S API CODE>";

        private MediaCapture _mediaCapture;
        private bool _motion = false;
        private EmotionServiceClient _client;
        private GpioPin _ledPin;
        private GpioPin _motionPin;
        
        public MainPage()
        {
            this.InitializeComponent();
            Init_App();
        }

        private async void Init_App()
        {
            // Show the IP address
            DebugBox.Text = GetIPAddress().ToString();
           
            // Init the LED
            var gpio = GpioController.GetDefault();
            _ledPin = gpio.OpenPin(6);
            if (_ledPin != null)
            {
                _ledPin.Write(GpioPinValue.High);
                _ledPin.SetDriveMode(GpioPinDriveMode.Output);
            }

            // Init the motion Sensor
            _motionPin = gpio.OpenPin(5);
            if (_motionPin != null)
            {
                _motionPin.SetDriveMode(GpioPinDriveMode.Input);
                _motionPin.ValueChanged += PirSensorChanged;
            }

            // Init the media stream from default camera
            _mediaCapture = new MediaCapture();
            await _mediaCapture.InitializeAsync();

            // Init the EmotionService Client
            _client = new EmotionServiceClient(_emotionServiceKey);

            DebugBox.Text = GetIPAddress().ToString();
            // Detect emotions in Mediastream every 250 milisecond if motion is detected
            while (true)
            {
                if (_motion)
                {
                    await DetectEmmotion();
                }
                await Task.Delay(TimeSpan.FromMilliseconds(250));
            }
        }

        private void PirSensorChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            _motion = (args.Edge == GpioPinEdge.RisingEdge);
            _ledPin.Write(_motion ? GpioPinValue.Low : GpioPinValue.High);
        }

        private async Task<MemoryStream> ConvertFromInMemoryRandomAccessStream(InMemoryRandomAccessStream inputStream)
        {
            var reader = new DataReader(inputStream.GetInputStreamAt(0));
            var bytes = new byte[inputStream.Size];
            await reader.LoadAsync((uint)inputStream.Size);
            reader.ReadBytes(bytes);

            var outputStream = new MemoryStream(bytes);
            return outputStream;
        }

        public IPAddress GetIPAddress()
        {
            List<string> ipAddress = new List<string>();
            var Hosts = Windows.Networking.Connectivity.NetworkInformation.GetHostNames().ToList();
            foreach (var Host in Hosts)
            {
                string IP = Host.DisplayName;
                ipAddress.Add(IP);
            }
            IPAddress address = IPAddress.Parse(ipAddress.Last());
            return address;
        }

        private async Task DetectEmmotion()
        {
            try
            {
                var stream = new InMemoryRandomAccessStream();
                await _mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);

                var memoryStream = await ConvertFromInMemoryRandomAccessStream(stream);

                Emotion[] result = await _client.RecognizeAsync(memoryStream);

                if (result.Any())
                {

                    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                        {
                            // Hiding all score smileys
                            foreach (var scoreGridChild in ScoreGrid.Children)
                            {
                                scoreGridChild.Opacity = 0;
                            }

                            double scoreHappy = (result.Sum(a => a.Scores.Happiness) / result.Length);
                            double scoreAnger = (result.Sum(a => a.Scores.Anger) / result.Length)*2;

                            // Happy
                            if (scoreHappy > scoreAnger)
                            {
                                if (scoreHappy > 0.5)
                                {
                                    Score5.Opacity = 1;
                                }
                                else
                                {
                                    Score4.Opacity = 1;
                                }
                            }

                            // Angry
                            if (scoreHappy < scoreAnger)
                            {
                                if (scoreAnger > 0.5)
                                {
                                    Score1.Opacity = 1;
                                }
                                else
                                {
                                    Score2.Opacity = 1;
                                }
                            }

                            // Neutral
                            if (scoreHappy < 0.2 && scoreAnger < 0.2)
                            {
                                Score1.Opacity = 0;
                                Score2.Opacity = 0;
                                Score3.Opacity = 0;
                                Score4.Opacity = 0;
                                Score5.Opacity = 0;

                                Score3.Opacity = 1;
                            }
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception when detection emotion: {0}", ex.Message);
            }
        }
    }
}
