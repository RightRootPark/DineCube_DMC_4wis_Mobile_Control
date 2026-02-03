using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Contrul_tester
{
    public enum DriveMode
    {
        FourWS,
        Crab,
        Pivot
    }

    public class MainFunction
    {
        // Events for UI Updates
        public event Action<string> OnLog;
        public event Action<bool> OnConnectionChanged;
        public event Action<double[], int> OnStatusReceived; // [Accel, RF, RR, LF, LR], ErrorCode
        public event Action<string> OnPacketSent;

        // TCP Components
        private TcpClient client;
        private NetworkStream stream;
        private StreamWriter writer;
        private bool isConnected = false;
        private DateTime lastReceivedTime;

        // Control State
        public DriveMode CurrentMode { get; private set; } = DriveMode.FourWS;
        
        // Input State
        private bool isForward = false;
        private bool isBackward = false;
        private bool isLeft = false;
        private bool isRight = false;

        // Internal Calculations
        private double currentX = 0;
        private double currentC = 0;

        // Constants
        private const double ACCEL_RATE = 2.0;
        private const double STEER_RATE = 2.0;
        private const double MAX_SPEED = 100.0;
        private const double MAX_STEER_4WS = 89.0; // Limited to 89.0 to avoid 90-degree singularity
        private const double MAX_STEER_CRAB = 135.0;
        private const double L = 1050.0;
        private const double W = 1050.0;

        public bool IsConnected => isConnected;

        public MainFunction()
        {
        }

        public void SetMode(DriveMode mode)
        {
            CurrentMode = mode;
            currentX = 0;
            currentC = 0;
            OnLog?.Invoke($"Mode Changed to: {mode}");
        }

        public void SetInput(bool forward, bool backward, bool left, bool right)
        {
            isForward = forward;
            isBackward = backward;
            isLeft = left;
            isRight = right;
        }

        public async Task ConnectAsync(string ip, int port)
        {
            try
            {
                client = new TcpClient();
                var connectTask = client.ConnectAsync(ip, port);
                var timeoutTask = Task.Delay(3000);

                if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                {
                    throw new TimeoutException("Connection Timed Out (3s)");
                }
                await connectTask;

                stream = client.GetStream();
                writer = new StreamWriter(stream, Encoding.ASCII);
                isConnected = true;
                lastReceivedTime = DateTime.Now;

                OnConnectionChanged?.Invoke(true);
                OnLog?.Invoke("Connected to " + ip);

                _ = Task.Run(() => ReceiveLoop());
            }
            catch (Exception ex)
            {
                OnLog?.Invoke("Connection Failed: " + ex.Message);
                Disconnect();
                throw; // Re-throw to let UI know
            }
        }

        public void Disconnect()
        {
            isConnected = false;
            try
            {
                writer?.Close();
                stream?.Close();
                client?.Close();
            }
            catch { }

            client = null;
            OnConnectionChanged?.Invoke(false);
            OnLog?.Invoke("Disconnected");
        }

        public void UpdateControl()
        {
            // Watchdog Check
            if (isConnected)
            {
                if ((DateTime.Now - lastReceivedTime).TotalSeconds > 2.0)
                {
                    OnLog?.Invoke("Connection Timeout");
                    Disconnect();
                    return;
                }
            }

            if (client == null || !client.Connected) return;

            // Target Values
            double targetAccel = 0;
            double angleRF = 0, angleRR = 0, angleLF = 0, angleLR = 0;

            // 1. Process Inputs
            if (CurrentMode == DriveMode.Pivot)
            {
                // Pivot Mode Logic
                angleRF = -45.0; angleLF = 45.0; angleRR = 45.0; angleLR = -45.0;

                if (isLeft) // CCW
                {
                    currentX += ACCEL_RATE;
                    if (currentX > 50) currentX = 50;
                }
                else if (isRight) // CW
                {
                    currentX -= ACCEL_RATE;
                    if (currentX < -50) currentX = -50;
                }
                else
                {
                    currentX = 0;
                }
                targetAccel = currentX;
            }
            else
            {
                // 4WS or Crab
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

                if (isLeft) currentC += STEER_RATE;
                else if (isRight) currentC -= STEER_RATE;

                double limit = (CurrentMode == DriveMode.Crab) ? MAX_STEER_CRAB : MAX_STEER_4WS;
                if (currentC > limit) currentC = limit;
                if (currentC < -limit) currentC = -limit;

                if (CurrentMode == DriveMode.Crab)
                {
                    angleRF = angleLF = angleRR = angleLR = currentC;
                }
                else // 4WS
                {
                    Calculate4WS(currentC, out angleRF, out angleLF, out angleRR, out angleLR);
                }
            }

            // Send Packet
            SendPacket(targetAccel, angleRF, angleRR, angleLF, angleLR);
        }

        private void Calculate4WS(double c, out double rf, out double lf, out double rr, out double lr)
        {
            if (Math.Abs(c) > 0.1)
            {
                double radC = c * Math.PI / 180.0;
                double R_abs = Math.Abs((L / 2.0) / Math.Tan(radC));
                double angIn = Math.Atan2(L / 2.0, R_abs - W / 2.0) * 180.0 / Math.PI;
                double angOut = Math.Atan2(L / 2.0, R_abs + W / 2.0) * 180.0 / Math.PI;

                if (c > 0) // Left
                {
                    lf = angIn; lr = -angIn;
                    rf = angOut; rr = -angOut;
                }
                else // Right
                {
                    rf = -angIn; rr = angIn;
                    lf = -angOut; lr = angOut;
                }
            }
            else
            {
                rf = lf = rr = lr = 0;
            }
        }

        private async void ReceiveLoop()
        {
            try
            {
                byte[] buffer = new byte[24];
                int collected = 0;

                while (isConnected && client != null && client.Connected)
                {
                    if (stream != null)
                    {
                        // Optimization: Removed polling with DataAvailable + Task.Delay(10)
                        // This reduces latency significantly (from ~8ms to <1ms) by waking up immediately on packet receipt.
                        int read = await stream.ReadAsync(buffer, collected, 24 - collected);
                        if (read > 0)
                        {
                            collected += read;
                            lastReceivedTime = DateTime.Now;

                            if (collected == 24)
                            {
                                ProcessPacket(buffer);
                                collected = 0;
                            }
                        }
                        else
                        {
                             // 0 bytes read means the server closed the connection
                             OnLog?.Invoke("Server closed connection.");
                             Disconnect();
                             return;
                        }
                    }
                    else
                    {
                        await Task.Delay(100); // Should not happen if connected
                    }
                }
            }
            catch (Exception)
            {
                 // Handle disconnect primarily
            }
        }

        private void ProcessPacket(byte[] buffer)
        {
            try
            {
                double[] vals = new double[5];
                int err = 0;

                for (int i = 0; i < 6; i++)
                {
                    int idx = i * 4;
                    int raw = (buffer[idx] << 24) | (buffer[idx + 1] << 16) | (buffer[idx + 2] << 8) | buffer[idx + 3];

                    if (i < 5) vals[i] = raw / 100.0;
                    else err = raw;
                }
                OnStatusReceived?.Invoke(vals, err);
            }
            catch { }
        }

        private async void SendPacket(double accel, double rf, double rr, double lf, double lr)
        {
            try
            {
                if (client != null && client.Connected && writer != null)
                {
                    string pkt = $"{accel:F1},{rf:F1},{rr:F1},{lf:F1},{lr:F1}";
                    await writer.WriteAsync(pkt);
                    await writer.FlushAsync();
                    OnPacketSent?.Invoke(pkt);
                }
            }
            catch (Exception ex)
            {
                OnLog?.Invoke("Send Error: " + ex.Message);
                Disconnect();
            }
        }
    }
}
