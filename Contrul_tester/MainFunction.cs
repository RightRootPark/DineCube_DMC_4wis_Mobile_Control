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
        public event Action<double[], int, double> OnStatusReceived; // [Accel, RF, RR, LF, LR], ErrorCode, CycleTimeMs
        public event Action<string> OnPacketSent;

        // TCP Components
        private TcpClient client;
        private NetworkStream stream;
        private StreamWriter writer;
        private StreamReader reader; // Not strict usage but good to have
        private bool isConnected = false;
        private DateTime lastReceivedTime;
        private DateTime lastPacketTime = DateTime.Now;

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
                // reader = new StreamReader(stream, Encoding.ASCII); // Not strictly used in binary read
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
                // Requested: 2(RF)=-135, 3(RR)=135.
                // 4(LF)=45, 5(LR)=-45 (Original values kept as per user request)
                angleRF = -135.0; 
                angleRR = 135.0; 
                angleLF = 45.0; 
                angleLR = -45.0;

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
                    lf = -angIn; lr = angIn;    // LF: -angIn (Inverted), LR: angIn (Inverted)
                    rf = angOut; rr = -angOut;  
                }
                else // Right
                {
                    rf = -angIn; rr = angIn;    
                    lf = -(-angOut); lr = -angOut; // LF: angOut (Inverted), LR: -angOut (Inverted)
                    // Simplified: lf = angOut
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
                byte[] buffer = new byte[1024];
                int collected = 0;

                while (isConnected && client != null && client.Connected)
                {
                    if (stream == null) break;

                    int read = await stream.ReadAsync(buffer, collected, buffer.Length - collected);
                    if (read == 0)
                    {
                        Disconnect();
                        break;
                    }

                    collected += read;

                    // Process multiple packets
                    while (collected >= 26) // Header(2) + Data(24) = 26
                    {
                        // Scan for Header 0xFE, 0xFE
                        int headerIdx = -1;
                        for (int i = 0; i < collected - 1; i++)
                        {
                            if (buffer[i] == 0xFE && buffer[i + 1] == 0xFE)
                            {
                                headerIdx = i;
                                break;
                            }
                        }

                        if (headerIdx >= 0)
                        {
                            // If header is not at start, discard garbage before it
                            if (headerIdx > 0)
                            {
                                Array.Copy(buffer, headerIdx, buffer, 0, collected - headerIdx);
                                collected -= headerIdx;
                                continue; // Restart scan from new 0
                            }

                            // Header is at 0. Check if we have full packet
                            if (collected >= 26)
                            {
                                // Extract Data
                                byte[] payload = new byte[24];
                                Array.Copy(buffer, 2, payload, 0, 24);
                                ProcessPacket(payload);
                                lastReceivedTime = DateTime.Now;

                                // Shift remaining
                                int remaining = collected - 26;
                                if (remaining > 0) Array.Copy(buffer, 26, buffer, 0, remaining);
                                collected = remaining;
                            }
                        }
                        else
                        {
                            // No header found in entire scan area?
                            // Keep last byte just in case it's first part of header (0xFE)
                            // Discard rest
                            if (collected > 0)
                            {
                                byte last = buffer[collected - 1];
                                if (last == 0xFE)
                                {
                                    buffer[0] = 0xFE;
                                    collected = 1;
                                }
                                else
                                {
                                    collected = 0;
                                }
                            }
                            break; // Need more data
                        }
                    }
                }
            }
            catch (Exception ex)
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

                double ms = (DateTime.Now - lastPacketTime).TotalMilliseconds;
                lastPacketTime = DateTime.Now;

                OnStatusReceived?.Invoke(vals, err, ms);
            }
            catch { }
        }

        private async void SendPacket(double accel, double rf, double rr, double lf, double lr)
        {
            try
            {
                if (client != null && client.Connected && writer != null)
                {
                    string pkt = $"{accel:F1},{rf:F1},{rr:F1},{lf:F1},{lr:F1};";
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
