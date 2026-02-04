using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Contrul_tester
{
    public partial class MainWindow : Window
    {
        private MainFunction mainFunc;
        private DispatcherTimer uiTimer;

        public MainWindow()
        {
            InitializeComponent();

            mainFunc = new MainFunction();
            
            // Subscribe Events
            mainFunc.OnLog += Log;
            mainFunc.OnConnectionChanged += OnConnectionStatusChanged;
            mainFunc.OnStatusReceived += OnStatusUpdate;
            mainFunc.OnPacketSent += OnPacketSent;

            // Timer (updates logic periodically)
            uiTimer = new DispatcherTimer();
            uiTimer.Interval = TimeSpan.FromMilliseconds(200);
            uiTimer.Tick += (s, e) => mainFunc.UpdateControl();
            uiTimer.Start();

            // Init UI
            SetModeUI(DriveMode.FourWS);
        }

        // ================= Event Callbacks =================
        private void Log(string msg)
        {
            Dispatcher.Invoke(() =>
            {
                txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
                txtLog.ScrollToEnd();
            });
        }

        private void OnConnectionStatusChanged(bool connected)
        {
            Dispatcher.Invoke(() =>
            {
                if (connected)
                {
                    statusLed.Fill = Brushes.Green;
                    lblStatus.Text = "Connected";
                    btnConnect.IsEnabled = false;
                    btnDisconnect.IsEnabled = true;
                }
                else
                {
                    statusLed.Fill = Brushes.Gray;
                    lblStatus.Text = "Disconnected";
                    btnConnect.IsEnabled = true;
                    btnDisconnect.IsEnabled = false;
                }
            });
        }

        private DateTime lastRenderTime = DateTime.MinValue;

        private void OnStatusUpdate(double[] vals, int err, double ms)
        {
            // Limit UI Update to ~30FPS (33ms)
            if ((DateTime.Now - lastRenderTime).TotalMilliseconds < 33) return;
            lastRenderTime = DateTime.Now;

            Dispatcher.Invoke(() =>
            {
                lblStatAccel.Text = vals[0].ToString("F2");
                lblStatRF.Text    = vals[1].ToString("F2");
                lblStatRR.Text    = vals[2].ToString("F2");
                lblStatLF.Text    = vals[3].ToString("F2");
                lblStatLR.Text    = vals[4].ToString("F2");
                lblStatErr.Text   = err.ToString();
                lblStatCycle.Text = ms.ToString("F0");
            });
        }

        private void OnPacketSent(string packet)
        {
            Dispatcher.Invoke(() =>
            {
                lblLastPacket.Text = packet;
            });
        }

        // ================= UI Actions =================
        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            string ip = txtIP.Text;
            if (!int.TryParse(txtPort.Text, out int port)) return;
            
            btnConnect.IsEnabled = false; // Prevent double click
            try
            {
                await mainFunc.ConnectAsync(ip, port);
            }
            catch
            {
                btnConnect.IsEnabled = true; // Re-enable if failed immediate
            }
        }

        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            mainFunc.Disconnect();
        }

        // Mode Switching
        private void SetModeUI(DriveMode mode)
        {
            mainFunc.SetMode(mode);

            // Reset Colors
            btnMode4WS.Background = Brushes.LightGray;
            btnModeCrab.Background = Brushes.LightGray;
            btnModePivot.Background = Brushes.LightGray;

            switch (mode)
            {
                case DriveMode.FourWS:
                    btnMode4WS.Background = Brushes.LightBlue;
                    btnLeft.Content = "Left (Steer)";
                    btnRight.Content = "Right (Steer)";
                    btnForward.IsEnabled = true;
                    btnBackward.IsEnabled = true;
                    break;
                case DriveMode.Crab:
                    btnModeCrab.Background = Brushes.LightBlue;
                    btnLeft.Content = "Left (Steer)";
                    btnRight.Content = "Right (Steer)";
                    btnForward.IsEnabled = true;
                    btnBackward.IsEnabled = true;
                    break;
                case DriveMode.Pivot:
                    btnModePivot.Background = Brushes.LightBlue;
                    btnLeft.Content = "CCW Rotation (Gas)";
                    btnRight.Content = "CW Rotation (Gas)";
                    btnForward.IsEnabled = false;
                    btnBackward.IsEnabled = false;
                    break;
            }
        }

        private void BtnMode4WS_Click(object sender, RoutedEventArgs e) => SetModeUI(DriveMode.FourWS);
        private void BtnModeCrab_Click(object sender, RoutedEventArgs e) => SetModeUI(DriveMode.Crab);
        private void BtnModePivot_Click(object sender, RoutedEventArgs e) => SetModeUI(DriveMode.Pivot);

        // Inputs
        // To avoid managing state in UI, we pass calls to MainFunction
        
        // We need to sync the boolean states. MainFunction has SetInput(f,b,l,r).
        // Let's keep local bools here just to aggregate MouseDown/Up/Key and then push to MainFunction?
        // OR push immediately.

        private bool f, b, l, r;
        private void UpdateInputs() => mainFunc.SetInput(f, b, l, r);

        private void BtnForward_Down(object sender, MouseButtonEventArgs e) { f=true; UpdateInputs(); }
        private void BtnBackward_Down(object sender, MouseButtonEventArgs e) { b=true; UpdateInputs(); }
        private void BtnLeft_Down(object sender, MouseButtonEventArgs e) { l=true; UpdateInputs(); }
        private void BtnRight_Down(object sender, MouseButtonEventArgs e) { r=true; UpdateInputs(); }

        private void BtnInput_Up(object sender, MouseButtonEventArgs e)
        {
            f=false; b=false; l=false; r=false;
            UpdateInputs();
        }

        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            f=false; b=false; l=false; r=false;
            UpdateInputs();
            mainFunc.SetMode(mainFunc.CurrentMode); // Reset internal values
            Log("Emergency STOP");
        }

        // Keywords
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.W) f=true;
            if (e.Key == Key.S) b=true;
            if (e.Key == Key.A) l=true;
            if (e.Key == Key.D) r=true;
            if (e.Key == Key.Space) BtnStop_Click(null, null);
            UpdateInputs();
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.W) f=false;
            if (e.Key == Key.S) b=false;
            if (e.Key == Key.A) l=false;
            if (e.Key == Key.D) r=false;
            UpdateInputs();
        }
    }
}