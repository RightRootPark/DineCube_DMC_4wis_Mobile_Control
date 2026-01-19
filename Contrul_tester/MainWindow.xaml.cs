using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Contrul_tester
{
    // Define Drive Modes
    public enum DriveMode
    {
        FourWS,
        Crab,
        Pivot
    }

    public partial class MainWindow : Window
    {
        private TcpClient client;
        private NetworkStream stream;
        private StreamWriter writer;
        private DispatcherTimer controlTimer;

        // Current Mode
        private DriveMode currentMode = DriveMode.FourWS;

        // Input Flags
        private bool isForward = false;
        private bool isBackward = false;
        private bool isLeft = false;  // Pivot: CCW
        private bool isRight = false; // Pivot: CW

        // Current Values
        private double currentX = 0; // Accel
        private double currentC = 0; // Steering Angle (or Virtual Angle)

        // Constants
        private const double ACCEL_RATE = 2.0;    // per 100ms
        private const double STEER_RATE = 1.0;    // per 100ms
        private const double MAX_SPEED = 100.0;
        private const double MAX_STEER = 90.0;
        
        // Robot Dimensions
        private const double L = 1050.0;
        private const double W = 1050.0;

        public MainWindow()
        {
            InitializeComponent();

            // Set Initial Mode
            SetMode(DriveMode.FourWS);

            // Setup Timer (100ms interval)
            controlTimer = new DispatcherTimer();
            controlTimer.Interval = TimeSpan.FromMilliseconds(100);
            controlTimer.Tick += ControlTimer_Tick;
            controlTimer.Start();
        }

        // ================= Mode Switching Logic =================
        private void SetMode(DriveMode mode)
        {
            currentMode = mode;
            
            // Reset Values
            currentX = 0;
            currentC = 0;
            
            // Reset UI Colors
            btnMode4WS.Background = Brushes.LightGray;
            btnModeCrab.Background = Brushes.LightGray;
            btnModePivot.Background = Brushes.LightGray;

            // Update UI based on Mode
            switch (mode)
            {
                case DriveMode.FourWS:
                    btnMode4WS.Background = Brushes.LightBlue;
                    btnLeft.Content = "Left (Steer)";
                    btnRight.Content = "Right (Steer)";
                    btnForward.IsEnabled = true;
                    btnBackward.IsEnabled = true;
                    Log("Mode: 4WS (Normal Drive)");
                    break;
                    
                case DriveMode.Crab:
                    btnModeCrab.Background = Brushes.LightBlue;
                    btnLeft.Content = "Left (Steer)";
                    btnRight.Content = "Right (Steer)";
                    btnForward.IsEnabled = true;
                    btnBackward.IsEnabled = true;
                    Log("Mode: Crab (Sideways Drive)");
                    break;
                    
                case DriveMode.Pivot:
                    btnModePivot.Background = Brushes.LightBlue;
                    btnLeft.Content = "CCW Rotation (Gas)";
                    btnRight.Content = "CW Rotation (Gas)";
                    btnForward.IsEnabled = false;
                    btnBackward.IsEnabled = false;
                    Log("Mode: Pivot (Spot Turn)");
                    break;
            }
        }

        private void BtnMode4WS_Click(object sender, RoutedEventArgs e) { SetMode(DriveMode.FourWS); }
        private void BtnModeCrab_Click(object sender, RoutedEventArgs e) { SetMode(DriveMode.Crab); }
        private void BtnModePivot_Click(object sender, RoutedEventArgs e) { SetMode(DriveMode.Pivot); }


        // ================= Main Control Logic =================
        private void ControlTimer_Tick(object sender, EventArgs e)
        {
            if (client == null || !client.Connected) return;

            // Target Values to Send
            double targetAccel = 0;
            double angleRF = 0;
            double angleRR = 0;
            double angleLF = 0;
            double angleLR = 0;

            // --- 1. Processing Inputs based on Mode ---
            if (currentMode == DriveMode.Pivot)
            {
                // [PIVOT MODE]
                // Left Button -> CCW Rotation (Accel)
                // Right Button -> CW Rotation (Accel)
                // Forward/Backward are disabled.
                
                // Fixed Diamond Geometry for Pivot
                // CCW: RF(-), LF(+), RR(+), LR(-)  (Tangential)
                // Let's used fixed 45 degrees.
                // Or maybe user wants geometry? "Pivot 일때는 45도, 135도 가면서 상태 초기화"
                // Assuming standard spot turn angles.
                // Standard Spot View from Top:
                // FL(45)  FR(-45)
                // RL(-45) RR(45)  <-- This depends on Zero angle definition.
                // Let's assume standard Diamond:
                // RF (Front Right): -45
                // LF (Front Left):  +45
                // RR (Rear Right):  +45
                // LR (Rear Left):   -45
                
                angleRF = -45.0;
                angleLF = 45.0;
                angleRR = 45.0;
                angleLR = -45.0;

                // Accel Control
                if (isLeft) // CCW Button
                {
                    currentX += ACCEL_RATE; // Using currentX as Accel proxy
                    if(currentX > 50) currentX = 50; // Limit rotation speed
                }
                else if (isRight) // CW Button
                {
                    currentX -= ACCEL_RATE; // Negative Accel? 
                    // Usually for turning, Accel is scalar Speed. Direction comes from Propellers?
                    // No, here Wheel Angles determine direction.
                    // If Wheels are fixed at Diamond, Positive Torque rotates one way, Negative the other?
                    // OR do we flip angles?
                    // Let's assume Positive Accel = CCW, Negative Accel = CW (if hydraulics support bidirectional flow).
                    // Or keep Accel Positive and flip angles?
                    // Let's try: CCW Button -> Positive Accel. CW Button -> Negative Accel.
                     if(currentX < -50) currentX = -50;
                }
                else
                {
                     currentX = 0;
                }
                targetAccel = currentX;
            }
            else
            {
                // [4WS & CRAB MODE]
                // 1. Accel Logic (Forward/Backward)
                if (isForward)
                {
                    currentX += ACCEL_RATE;
                    if (currentX > MAX_SPEED) currentX = MAX_SPEED;
                }
                else if (isBackward)
                {
                    currentX -= ACCEL_RATE;
                    if (currentX < -MAX_SPEED) currentX = -MAX_SPEED;
                }
                else
                {
                    currentX = 0;
                }
                targetAccel = currentX;

                // 2. Steering Logic (Left/Right)
                if (isLeft)
                {
                    currentC += STEER_RATE; // C+
                }
                else if (isRight)
                {
                    currentC -= STEER_RATE; // C-
                }
                // Keep angle when released

                // Clamp
                if (currentC > MAX_STEER) currentC = MAX_STEER;
                if (currentC < -MAX_STEER) currentC = -MAX_STEER;

                // 3. Wheel Angle Calculation
                if (currentMode == DriveMode.Crab)
                {
                    // Crab: All wheels same angle
                    angleRF = currentC;
                    angleLF = currentC;
                    angleRR = currentC; // Usually parallel
                    angleLR = currentC; // Usually parallel
                }
                else // 4WS
                {
                    // Ackermann 4WS
                    if (Math.Abs(currentC) > 0.1)
                    {
                        double radC = currentC * Math.PI / 180.0;
                        double R_abs = Math.Abs((L / 2.0) / Math.Tan(radC));
                        
                        double angIn = Math.Atan2(L / 2.0, R_abs - W / 2.0) * 180.0 / Math.PI;
                        double angOut = Math.Atan2(L / 2.0, R_abs + W / 2.0) * 180.0 / Math.PI;
                        
                        if (currentC > 0) // Left Turn
                        {
                            angleLF = angIn;  angleLR = -angIn;
                            angleRF = angOut; angleRR = -angOut;
                        }
                        else // Right Turn
                        {
                            angleRF = -angIn; angleRR = angIn;
                            angleLF = -angOut; angleLR = angOut;
                        }
                    }
                    else
                    {
                        angleRF = angleLF = angleRR = angleLR = 0;
                    }
                }
            }

            // 4. Send Packet (5 Values)
            SendPacket(targetAccel, angleRF, angleRR, angleLF, angleLR);
        }

        private async void SendPacket(double accel, double rf, double rr, double lf, double lr)
        {
            try
            {
                if (client != null && client.Connected && writer != null)
                {
                    // Format: "accel,rf,rr,lf,lr"
                    string packet = $"{accel:F1},{rf:F1},{rr:F1},{lf:F1},{lr:F1}";
                    await writer.WriteAsync(packet);
                    await writer.FlushAsync();

                    if (lblLastPacket != null) lblLastPacket.Text = packet;
                }
            }
            catch (Exception ex)
            {
                Log("Send Error: " + ex.Message);
                Disconnect();
            }
        }

        // ================= Connection Logic =================
        private async void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            string ip = txtIp.Text;
            if(!int.TryParse(txtPort.Text, out int port)) return;

            try
            {
                btnConnect.IsEnabled = false;
                client = new TcpClient();
                await client.ConnectAsync(ip, port);
                
                stream = client.GetStream();
                writer = new StreamWriter(stream, Encoding.ASCII);
                
                statusLed.Fill = Brushes.Green;
                lblStatus.Text = "Connected";
                btnDisconnect.IsEnabled = true;
                Log("Connected to " + ip);
            }
            catch (Exception ex)
            {
                Log("Connection Failed: " + ex.Message);
                btnConnect.IsEnabled = true;
            }
        }

        private void btnDisconnect_Click(object sender, RoutedEventArgs e)
        {
            Disconnect();
        }

        private void Disconnect()
        {
            try
            {
                writer?.Close();
                stream?.Close();
                client?.Close();
            }
            catch { }

            client = null;
            
            if (statusLed != null)
            {
                statusLed.Fill = Brushes.Gray;
                lblStatus.Text = "Disconnected";
                btnConnect.IsEnabled = true;
                btnDisconnect.IsEnabled = false;
            }
        }

        private void Log(string msg)
        {
            if (txtLog == null) return;
            txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            txtLog.ScrollToEnd();
        }

        // ================= Input Handlers =================

        // Center Column (Forward/Backward)
        private void BtnForward_Down(object sender, MouseButtonEventArgs e) { isForward = true; }
        private void BtnBackward_Down(object sender, MouseButtonEventArgs e) { isBackward = true; }
        
        // Left Column (Left Steer OR CCW Gas)
        private void BtnLeft_Down(object sender, MouseButtonEventArgs e) { isLeft = true; }
        
        // Right Column (Right Steer OR CW Gas)
        private void BtnRight_Down(object sender, MouseButtonEventArgs e) { isRight = true; }

        // Common Up
        private void BtnInput_Up(object sender, MouseButtonEventArgs e)
        {
            isForward = false;
            isBackward = false;
            isLeft = false;
            isRight = false;
        }

        // Stop (Reset All)
        private void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            isForward = false;
            isBackward = false;
            isLeft = false;
            isRight = false;
            
            currentX = 0;
            currentC = 0;
            Log("Emergency STOP pressed");
        }

        // Keyboard Shortcuts
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.W) isForward = true;
            if (e.Key == Key.S) isBackward = true;
            if (e.Key == Key.A) isLeft = true;
            if (e.Key == Key.D) isRight = true;
            if (e.Key == Key.Space) BtnStop_Click(null, null);
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.W) isForward = false;
            if (e.Key == Key.S) isBackward = false;
            if (e.Key == Key.A) isLeft = false;
            if (e.Key == Key.D) isRight = false;
        }
    }
}