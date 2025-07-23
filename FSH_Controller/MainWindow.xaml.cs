using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Ivi.Visa;
using NationalInstruments.Visa;
using System.Net.Sockets; //For ROS Connection

namespace FSH_Controller
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private FSHController _fshController;
        private bool _isConnected = false;

        private string _logFilePath;
        private StringBuilder _measurementLog = new StringBuilder();
        private const string LogDirectory = "MeasurementLogs";
        private const string LogFilePrefix = "FSH_Log_";

        public MainWindow()
        {
            InitializeComponent();
            UpdateStatus("Application started. Ready to connect.");
            InitializeLogging();
            this.Closed += MainWindow_Closed;
        }

        private void InitializeLogging()
        {
            // Create log directory if it doesn't exist
            Directory.CreateDirectory(LogDirectory);

            // Generate timestamped log file name
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            _logFilePath = $"{LogDirectory}\\{LogFilePrefix}{timestamp}.txt";  //Path.Combine(LogDirectory, $"{LogFilePrefix}{timestamp}.txt");

            // Write initial log header
            _measurementLog.AppendLine($"FSH Measurement Log - {DateTime.Now}");
            _measurementLog.AppendLine(new string('=', 40));
            UpdateLogDisplay();
        }

        // 250721 Add ROS Connection Checking
        
        private void btnCheckRos_Click(object sender, RoutedEventArgs e) //CheckRosStatusViaSocket
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    // Connect with 2 second timeout
                    var result = client.BeginConnect("192.168.0.136", 65432, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));

                    if (!success)
                    {
                        UpdateStatus("Connection timeout");
                        return;
                    }

                    client.EndConnect(result);

                    using (NetworkStream stream = client.GetStream())
                    {
                        // Send request first
                        string request = "STATUS";
                        byte[] requestData = Encoding.UTF8.GetBytes(request);
                        stream.Write(requestData, 0, requestData.Length);

                        // Set read timeout
                        stream.ReadTimeout = 2000; // 2 seconds

                        // Read response
                        byte[] buffer = new byte[1024];
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        dynamic status = Newtonsoft.Json.JsonConvert.DeserializeObject(response);
                        Dispatcher.Invoke(() =>
                        {
                            UpdateStatus($"ROS: {status.ros}");
                            UpdateStatus($"Mapping: {status.mapping}");
                            UpdateStatus($"Camera: {status.camera}");
                            UpdateStatus($"VM IP: {status.vm_ip}");
                        });
                    }
                }
            }
            catch (SocketException ex)
            {
                Dispatcher.Invoke(() => UpdateStatus($"Network error: {ex.SocketErrorCode}"));
            }
            catch (IOException ex) when (ex.InnerException is SocketException sex)
            {
                Dispatcher.Invoke(() => UpdateStatus($"Network error: {sex.SocketErrorCode}"));
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => UpdateStatus($"Error: {ex.Message}"));
            }
        }

        private void SendMoveCommand(double distance, double velocity)
        {
            try
            {
                using (TcpClient client = new TcpClient())
                {
                    // Connect with 2 second timeout
                    var result = client.BeginConnect("192.168.0.136", 65432, null, null);
                    bool success = result.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(2));

                    if (!success)
                    {
                        UpdateStatus("Connection timeout");
                        return;
                    }

                    client.EndConnect(result);

                    using (NetworkStream stream = client.GetStream())
                    {
                        // Set timeouts
                        stream.WriteTimeout = 2000;
                        stream.ReadTimeout = 2000;

                        // Send command
                        string command = $"CMD:MOVE:{distance},{velocity}";
                        byte[] data = Encoding.UTF8.GetBytes(command);
                        stream.Write(data, 0, data.Length);

                        // Read response
                        byte[] buffer = new byte[1024];
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        string response = Encoding.UTF8.GetString(buffer, 0, bytesRead);

                        Dispatcher.Invoke(() =>
                        {
                            UpdateStatus($"Command sent: {command}");
                            UpdateStatus($"Response: {response}");
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => UpdateStatus($"Command error: {ex.Message}"));
            }
        }

        private void btnTestMove_Click(object sender, RoutedEventArgs e)
        {
            // Example: Move forward 1m at 0.2m/s
            SendMoveCommand(1.0, 0.2);
        }


        private void btnCopyLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(txtMeasurementLog.Text);
                AddLogEntry("Log contents copied to clipboard");
            }
            catch (Exception ex)
            {
                AddLogEntry($"Failed to copy log: {ex.Message}");
            }
        }

        private void btnSaveLog_Click(object sender, RoutedEventArgs e)
        {
            SaveLogToFile();
        }

        private void SaveLogToFile()
        {
            try
            {
                File.WriteAllText(_logFilePath, _measurementLog.ToString());
                AddLogEntry($"Log saved to: {_logFilePath}");
            }
            catch (Exception ex)
            {
                AddLogEntry($"Failed to save log: {ex.Message}");
            }
        }

        private void MainWindow_Closed(object sender, EventArgs e)
        {
            // Auto-save when window closes
            if (_measurementLog.Length > 0)
            {
                SaveLogToFile();
            }
        }

        private void AddLogEntry(string message)
        {
            string timestampedMessage = $"{DateTime.Now:HH:mm:ss} - {message}";

            // Add to in-memory log
            _measurementLog.AppendLine(timestampedMessage);

            // Update UI
            Dispatcher.Invoke(() =>
            {
                txtMeasurementLog.AppendText(timestampedMessage + Environment.NewLine);
                txtMeasurementLog.ScrollToEnd();
            });
        }

        private void UpdateLogDisplay()
        {
            txtMeasurementLog.Text = _measurementLog.ToString();
            txtMeasurementLog.ScrollToEnd();
        }

        // ... [Rest of your existing FSH controller code] ...

        // Modify your existing AddMeasurementLog method to use AddLogEntry:
        private void AddMeasurementLog(string message)
        {
            AddLogEntry(message);
        }


        private void UpdateStatus(string message)
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.AppendText($"{DateTime.Now:HH:mm:ss} - {message}\n");
                txtStatus.ScrollToEnd();
            });
        }

        // Add these fields to the class
        private CancellationTokenSource _measurementCancellationToken;
        private bool _isMeasurementRunning = false;
        //private string _baseSavePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "FSH_Measurements");

        // Add this converter class for XAML binding
        public class IndexToBoolConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                int selectedIndex = (int)value;
                int compareToIndex = System.Convert.ToInt32(parameter);
                return selectedIndex == compareToIndex;
            }

            public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        // Measurement control methods
        private async void btnRunSingle_Click(object sender, RoutedEventArgs e)
        {
            await StartMeasurement(1);
        }

        private async void btnRunMultiple_Click(object sender, RoutedEventArgs e)
        {
            if (int.TryParse(txtIterations.Text, out int iterations))
            {
                await StartMeasurement(iterations);
            }
            else
            {
                AddMeasurementLog("Invalid iteration count");
            }
        }

        private void btnStopMeasurement_Click(object sender, RoutedEventArgs e)
        {
            StopMeasurement();
        }

        private async Task StartMeasurement(int iterations)
        {
            if (!_isConnected)
            {
                AddMeasurementLog("Not connected to FSH");
                return;
            }

            if (_isMeasurementRunning)
            {
                AddMeasurementLog("Measurement already in progress");
                return;
            }

            _isMeasurementRunning = true;
            _measurementCancellationToken = new CancellationTokenSource();
            btnRunSingle.IsEnabled = false;
            btnRunMultiple.IsEnabled = false;
            btnStopMeasurement.IsEnabled = true;

            try
            {
                string venue = txtVenue.Text;
                string testCase = txtTestCase.Text;
                string antenna = txtAntennaModel.Text;
                string position = txtPosition.Text;
                int maxHoldTime = int.Parse(txtMaxHoldTime.Text);
                bool includeTimestamp = chkIncludeTimestamp.IsChecked == true;
                bool saveCSV = chkSaveCSV.IsChecked == true;
                bool savePNG = chkSavePNG.IsChecked == true;

                // Create directory for today's measurements
                string dateFolder = DateTime.Now.ToString("ddMMyy");

                // Get user-specified paths
                string csvBasePath = txtCsvPath.Text.TrimEnd('\\') + "\\";
                string pngBasePath = txtPngPath.Text.TrimEnd('\\') + "\\";

                for (int i = 1; i <= iterations; i++)
                {
                    if (_measurementCancellationToken.IsCancellationRequested)
                        break;

                    AddMeasurementLog($"Starting iteration {i}/{iterations}");

                    // Update progress
                    progressMeasurement.Value = (i - 1) * 100.0 / iterations;
                    txtProgressStatus.Text = $"Running iteration {i} of {iterations}";

                    // 1. Set Trace Mode to Clear/Write
                    _fshController.SendCommand("DISP:TRAC1:MODE WRIT");
                    AddMeasurementLog("Trace mode set to Clear/Write");

                    // 2. Max Hold for specified time
                    _fshController.SendCommand("DISP:TRAC1:MODE MAXH");
                    AddMeasurementLog($"Running max hold for {maxHoldTime} seconds...");

                    DateTime endTime = DateTime.Now.AddSeconds(maxHoldTime);
                    while (DateTime.Now < endTime)
                    {
                        if (_measurementCancellationToken.IsCancellationRequested)
                            break;

                        TimeSpan remaining = endTime - DateTime.Now;
                        txtRemainingTime.Text = $"Remaining: {remaining.TotalSeconds:F0}s";
                        await Task.Delay(1000);
                    }

                    if (_measurementCancellationToken.IsCancellationRequested)
                        break;

                    // Generate filename components
                    string timestamp = includeTimestamp ? "_" + DateTime.Now.ToString("ddMMyy_HHmmss") : "";
                    string baseName = $"{venue}.{testCase}.{antenna}.{position}";

                    // 3. Screen capture
                    if (savePNG)
                    {
                        string pngFileName = $"ScreenShot_{baseName}{timestamp}.png";
                        string pngFullPath = $"{pngBasePath}{pngFileName}";

                        AddMeasurementLog("Starting screenshot capture...");
                        _fshController.SendCommand("HCOP:DEV:LANG PNG");
                        _fshController.SendCommand($"MMEM:NAME '{pngFullPath}'");
                        _fshController.SendCommand("HCOP");

                        AddMeasurementLog("File saving in progress (PNG)...");
                        await Task.Delay(2000); // 2 second delay for PNG save

                        AddMeasurementLog($"Screenshot saved: {pngFullPath}");
                    }

                    // 4. Save CSV data
                    if (saveCSV)
                    {
                        string csvFileName = $"Dataset_{baseName}{timestamp}.csv";
                        string csvFullPath = $"{csvBasePath}{csvFileName}";

                        AddMeasurementLog("Starting CSV data export...");
                        _fshController.SendCommand($"MMEM:STOR:CSV:STAT 1,'{csvFullPath}'");

                        AddMeasurementLog("File saving in progress (CSV)...");
                        await Task.Delay(2000); // 2 second delay for CSV save

                        AddMeasurementLog($"CSV data saved: {csvFullPath}");
                    }

                    // 5. Reset Trace Mode
                    _fshController.SendCommand("DISP:TRAC1:MODE WRIT");
                }

                progressMeasurement.Value = 100;
                txtProgressStatus.Text = _measurementCancellationToken.IsCancellationRequested ?
                    "Measurement stopped" : "Measurement completed";
            }
            catch (Exception ex)
            {
                AddMeasurementLog($"Measurement error: {ex.Message}");
            }
            finally
            {
                _isMeasurementRunning = false;
                btnRunSingle.IsEnabled = true;
                btnRunMultiple.IsEnabled = true;
                btnStopMeasurement.IsEnabled = false;
                _measurementCancellationToken?.Dispose();
            }
        }

        private void StopMeasurement()
        {
            if (_isMeasurementRunning && _measurementCancellationToken != null)
            {
                _measurementCancellationToken.Cancel();
                AddMeasurementLog("Stopping measurement...");
            }
        }

        //private void AddMeasurementLog(string message)
        //{
        //    Dispatcher.Invoke(() =>
        //    {
        //        txtMeasurementLog.AppendText($"{DateTime.Now:HH:mm:ss} - {message}\n");
        //        txtMeasurementLog.ScrollToEnd();
        //    });
        //}

        private void ApplyIsotropicAntennaSettings()
        {
            if (!_isConnected) return;

            bool isEnabled = chkIsotropicAntenna.IsChecked == true;
            _fshController.SendCommand($"INP:ANT:STAT {(isEnabled ? "ON" : "OFF")}");

            if (isEnabled)
            {
                string mode = "AUTO";
                if (rbX.IsChecked == true) mode = "X";
                else if (rbY.IsChecked == true) mode = "Y";
                else if (rbZ.IsChecked == true) mode = "Z";

                _fshController.SendCommand($"INP:ANT:MEAS {mode}");
                UpdateStatus($"Isotropic Antenna enabled with {mode} measurement");
            }
            else
            {
                UpdateStatus("Isotropic Antenna disabled");
            }
        }

        private void ApplyDetectionMode()
        {
            if (!_isConnected) return;

            var selectedItem = cmbDetectionMode.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                string command = selectedItem.Tag.ToString();
                _fshController.SendCommand($"DET {command}");
                UpdateStatus($"Detection mode set to {selectedItem.Content}");
            }
        }

        private void chkIsotropicAntenna_Checked(object sender, RoutedEventArgs e)
        {
            ApplyIsotropicAntennaSettings();
        }

        private void chkIsotropicAntenna_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplyIsotropicAntennaSettings();
        }

        private void AntennaMode_Checked(object sender, RoutedEventArgs e)
        {
            if (_isConnected && chkIsotropicAntenna.IsChecked == true)
            {
                ApplyIsotropicAntennaSettings();
            }
        }

        private void btnConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_isConnected)
                {
                    _fshController.Disconnect();
                    _isConnected = false;
                    btnConnect.Content = "Connect";
                    UpdateStatus("Disconnected from FSH");
                    return;
                }

                string resourceAddress = txtResourceAddress.Text.Trim();
                _fshController = new FSHController(resourceAddress);

                if (_fshController.Connect())
                {
                    _isConnected = true;
                    btnConnect.Content = "Disconnect";
                    UpdateStatus($"Connected to {resourceAddress}");
                }
                else
                {
                    UpdateStatus("Connection failed");
                }
            }
            catch (Exception ex)
            {
                UpdateStatus($"Connection error: {ex.Message}");
            }
        }

        private void btnReset_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected)
            {
                UpdateStatus("Not connected to FSH");
                return;
            }

            try
            {
                _fshController.SendCommand("*RST; *CLS");
                UpdateStatus("Instrument reset and status cleared");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Reset error: {ex.Message}");
            }
        }


        private void btnApplySettings_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected)
            {
                UpdateStatus("Not connected to FSH");
                return;
            }

            try
            {
                // Frequency settings
                _fshController.SendCommand($"FREQ:CENT {txtCenterFreq.Text}");
                _fshController.SendCommand($"FREQ:SPAN {txtSpan.Text}");

                // Bandwidth settings
                _fshController.SendCommand($"BAND {txtRBW.Text}");
                _fshController.SendCommand($"BAND:VID {txtVBW.Text}");

                // Amplitude settings
                _fshController.SendCommand($"DISP:TRAC:Y:RLEV {txtRefLevel.Text}");
                _fshController.SendCommand($"INP:ATT {txtAttenuation.Text}");

                // Sweep settings
                _fshController.SendCommand($"SWE:TIME {txtSweepTime.Text}");

                // Detection mode
                ApplyDetectionMode();
                //string detectionMode = (cmbDetectionMode.SelectedItem as ComboBoxItem)?.Content.ToString();
                //_fshController.SendCommand($"DET {detectionMode}");

                // Transducer settings
                string transducerFile = (cmbTransducer.SelectedItem as ComboBoxItem)?.Content.ToString();
                _fshController.SendCommand($"SENS:CORR:TRAN:SEL '{transducerFile}'");
                _fshController.SendCommand($"SENS:CORR:TRAN:STAT {(chkTransducerEnabled.IsChecked == true ? "ON" : "OFF")}");

                UpdateStatus("All settings applied successfully");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Settings error: {ex.Message}");
            }
        }

        private void btnSetMarkers_Click(object sender, RoutedEventArgs e)
        {
            if (!_isConnected)
            {
                UpdateStatus("Not connected to FSH");
                return;
            }

            try
            {
                SetMarker(1, txtMarker1.Text);
                SetMarker(2, txtMarker2.Text);
                SetMarker(3, txtMarker3.Text);
                SetMarker(4, txtMarker4.Text);
                SetMarker(5, txtMarker5.Text);

                UpdateStatus("All markers set successfully");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Marker error: {ex.Message}");
            }
        }

        private void SetMarker(int markerNumber, string frequency)
        {
            // Remove "GHz" if present in the input
            string cleanFreq = frequency.Replace("GHz", "").Trim();

            _fshController.SendCommand($"CALC:MARK{markerNumber}:MODE POS");
            _fshController.SendCommand($"CALC:MARK{markerNumber}:FUNC:BPOW:TYPE M");
            _fshController.SendCommand($"CALC:MARK{markerNumber}:X {cleanFreq}GHz");

            UpdateStatus($"Marker {markerNumber} set to {cleanFreq} GHz");
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (_isConnected)
            {
                _fshController.Disconnect();
            }
        }
    }

    public class FSHController
    {
        private IMessageBasedSession _session;
        private string _resourceName;

        public FSHController(string resourceName)
        {
            _resourceName = resourceName;
        }

        public bool Connect()
        {
            try
            {
                var rmSession = new ResourceManager();
                _session = rmSession.Open(_resourceName) as IMessageBasedSession;

                if (_session != null)
                {
                    _session.TimeoutMilliseconds = 2000; // 2 second timeout
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                throw new Exception($"Connection error: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            try
            {
                if (_session != null)
                {
                    _session.Dispose();
                    _session = null;
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Disconnection error: {ex.Message}");
            }
        }

        //public bool SendCommand(string command)
        //{
        //    try
        //    {
        //        _session.RawIO.Write(command + "\n");
        //        Thread.Sleep(50);
        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        throw new Exception($"Command error: {ex.Message}");
        //    }
        //}

        public bool SendCommand(string command, int maxRetries = 3, int retryDelayMs = 10)
        {
            int attempt = 0;
            bool shouldRetry;

            do
            {
                shouldRetry = false;
                attempt++;

                try
                {
                    _session.RawIO.Write(command + "\n");
                    // Optional: Wait for operation complete
                    //_session.RawIO.Write("*OPC?\n");
                    //string response = _session.RawIO.ReadString();
                    //if(response != "1") throw new Exception("Operation failed");
                    Thread.Sleep(50+950);  // Original delay you had
                    return true;
                }
                catch (IOTimeoutException ex)  // Specific timeout exception
                {
                    if (attempt >= maxRetries)
                    {
                        throw new Exception($"Command failed after {maxRetries} attempts: {ex.Message}");
                    }

                    shouldRetry = true;
                    _session.Clear();  // Clear device IO buffers
                    Thread.Sleep(retryDelayMs);  // Short delay before retry
                }
                catch (Exception ex)
                {
                    throw new Exception($"Command error: {ex.Message}");
                }

            } while (shouldRetry && attempt < maxRetries);

            return false;  // Shouldn't reach here due to the throw above
        }


    }
}
