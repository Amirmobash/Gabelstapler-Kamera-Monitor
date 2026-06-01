using System;
using System.Configuration;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace GabelstaplerKameraMonitor
{
    public class MainForm : Form
    {
        private readonly Panel _headerPanel;
        private readonly Label _titleLabel;
        private readonly Label _statusLabel;
        private readonly Panel _previewPanel;
        private readonly Panel _footerPanel;
        private readonly Button _refreshButton;
        private readonly Button _startButton;
        private readonly Button _stopButton;
        private readonly Button _recordButton;
        private readonly Button _lineUpButton;
        private readonly Button _lineDownButton;
        private readonly Label _hintLabel;
        private readonly Label _recordingLabel;
        private readonly Panel _guideLine;
        private readonly Label _guideLabel;
        private readonly Timer _recordingTimer;

        private DirectShowCamera _camera;
        private DirectShowCamera.CameraDevice _currentCamera;
        private MjpegAviRecorder _recorder;
        private string _lastRecordingPath;
        private int _guideLineY;
        private int _guideLineStep;
        private int _recordingFps;
        private long _jpegQuality;
        private bool _isRecording;

        private static readonly Color Orange = Color.FromArgb(255, 122, 0);
        private static readonly Color OrangeDark = Color.FromArgb(202, 76, 0);
        private static readonly Color OrangeLight = Color.FromArgb(255, 180, 75);
        private static readonly Color Background = Color.FromArgb(25, 25, 25);
        private static readonly Color PanelDark = Color.FromArgb(38, 38, 38);
        private static readonly Color GuideRed = Color.FromArgb(230, 0, 0);

        public MainForm()
        {
            Text = "Gabelstapler Kamera-Monitor";
            MinimumSize = new Size(980, 640);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Background;
            Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);
            KeyPreview = true;

            _guideLineStep = GetIntSetting("GuideLineStepPixels", 8, 1, 100);
            _recordingFps = GetIntSetting("RecordingFps", 15, 1, 60);
            _jpegQuality = GetIntSetting("RecordingJpegQuality", 85, 1, 100);

            _headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 104,
                BackColor = Orange
            };

            _titleLabel = new Label
            {
                AutoSize = false,
                Text = "Gabelstapler Kamera-Monitor",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 22F, FontStyle.Bold, GraphicsUnit.Point),
                Location = new Point(24, 12),
                Size = new Size(850, 42)
            };

            _statusLabel = new Label
            {
                AutoSize = false,
                Text = "Kamera wird gesucht...",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 10F, FontStyle.Regular, GraphicsUnit.Point),
                Location = new Point(27, 60),
                Size = new Size(880, 28)
            };

            _headerPanel.Controls.Add(_titleLabel);
            _headerPanel.Controls.Add(_statusLabel);

            _footerPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 132,
                BackColor = PanelDark,
                Padding = new Padding(18)
            };

            _refreshButton = CreateButton("Aktualisieren", 132);
            _refreshButton.Location = new Point(18, 18);
            _refreshButton.Click += (s, e) => RestartCamera();

            _startButton = CreateButton("Kamera starten", 132);
            _startButton.Location = new Point(160, 18);
            _startButton.Click += (s, e) => StartCamera();

            _stopButton = CreateButton("Kamera stoppen", 132);
            _stopButton.Location = new Point(302, 18);
            _stopButton.Enabled = false;
            _stopButton.Click += (s, e) => StopCamera("Kamera gestoppt.");

            _recordButton = CreateButton("Aufnahme starten", 164);
            _recordButton.Location = new Point(444, 18);
            _recordButton.Enabled = false;
            _recordButton.Click += (s, e) => ToggleRecording();

            _lineUpButton = CreateButton("Linie hoch", 118);
            _lineUpButton.Location = new Point(18, 72);
            _lineUpButton.Click += (s, e) => MoveGuideLine(-_guideLineStep);

            _lineDownButton = CreateButton("Linie runter", 118);
            _lineDownButton.Location = new Point(146, 72);
            _lineDownButton.Click += (s, e) => MoveGuideLine(_guideLineStep);

            _recordingLabel = new Label
            {
                AutoSize = false,
                Text = "Aufnahme: aus",
                ForeColor = Color.Gainsboro,
                Location = new Point(625, 25),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                Size = new Size(330, 24)
            };

            _hintLabel = new Label
            {
                AutoSize = false,
                Text = "Mausrad im Kamerabild bewegt die rote GABELSPITZE-Linie. Kamera und Aufnahme werden in App.config eingestellt.",
                ForeColor = Color.Gainsboro,
                Location = new Point(282, 78),
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top,
                Size = new Size(670, 30)
            };

            _footerPanel.Controls.Add(_refreshButton);
            _footerPanel.Controls.Add(_startButton);
            _footerPanel.Controls.Add(_stopButton);
            _footerPanel.Controls.Add(_recordButton);
            _footerPanel.Controls.Add(_lineUpButton);
            _footerPanel.Controls.Add(_lineDownButton);
            _footerPanel.Controls.Add(_recordingLabel);
            _footerPanel.Controls.Add(_hintLabel);

            _previewPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                TabStop = true
            };

            _previewPanel.MouseEnter += (s, e) => _previewPanel.Focus();
            _previewPanel.MouseWheel += PreviewPanelMouseWheel;
            _previewPanel.Resize += (s, e) =>
            {
                _camera?.ResizeVideo(_previewPanel.ClientRectangle);
                ClampAndPlaceGuideLine();
            };

            _guideLine = new Panel
            {
                BackColor = GuideRed,
                Height = 4,
                Left = 0,
                Top = 0,
                Width = 100
            };

            _guideLabel = new Label
            {
                AutoSize = false,
                Text = "GABELSPITZE",
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = GuideRed,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                Size = new Size(118, 24),
                Left = 12,
                Top = 0
            };

            _guideLine.MouseWheel += PreviewPanelMouseWheel;
            _guideLabel.MouseWheel += PreviewPanelMouseWheel;
            _guideLine.MouseEnter += (s, e) => _previewPanel.Focus();
            _guideLabel.MouseEnter += (s, e) => _previewPanel.Focus();

            _previewPanel.Controls.Add(_guideLine);
            _previewPanel.Controls.Add(_guideLabel);

            var previewFrame = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(18),
                BackColor = Background
            };

            previewFrame.Controls.Add(_previewPanel);

            Controls.Add(previewFrame);
            Controls.Add(_footerPanel);
            Controls.Add(_headerPanel);

            _recordingTimer = new Timer();
            _recordingTimer.Interval = Math.Max(1, 1000 / _recordingFps);
            _recordingTimer.Tick += (s, e) => RecordCurrentFrame();

            Shown += (s, e) =>
            {
                PlaceGuideLineFromConfig();
                StartCamera();
            };

            FormClosing += (s, e) => CleanupAll();
        }

        private Button CreateButton(string text, int width)
        {
            var button = new Button
            {
                Text = text,
                Width = width,
                Height = 40,
                FlatStyle = FlatStyle.Flat,
                BackColor = Orange,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold, GraphicsUnit.Point),
                Cursor = Cursors.Hand
            };

            button.FlatAppearance.BorderColor = OrangeLight;
            button.FlatAppearance.MouseOverBackColor = OrangeLight;
            button.FlatAppearance.MouseDownBackColor = OrangeDark;

            return button;
        }

        private void RestartCamera()
        {
            StopCamera("Kamera wird neu geladen...");
            StartCamera();
        }

        private void StartCamera()
        {
            try
            {
                CleanupCamera();

                _currentCamera = FindCamera();

                if (_currentCamera == null)
                {
                    _startButton.Enabled = true;
                    _stopButton.Enabled = false;
                    _recordButton.Enabled = false;
                    return;
                }

                _camera = new DirectShowCamera(_currentCamera);
                _camera.StartPreview(_previewPanel.Handle, _previewPanel.ClientRectangle);

                BringGuideLineToFront();
                SetStatus("Aktiv: " + _currentCamera.Name + " | Gabelkamera bereit.");

                _startButton.Enabled = false;
                _stopButton.Enabled = true;
                _recordButton.Enabled = true;
            }
            catch (Exception ex)
            {
                CleanupCamera();

                _startButton.Enabled = true;
                _stopButton.Enabled = false;
                _recordButton.Enabled = false;

                SetStatus("Fehler: " + ex.Message);

                MessageBox.Show(
                    this,
                    ex.Message,
                    "Kamera-Fehler",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private DirectShowCamera.CameraDevice FindCamera()
        {
            var cameras = DirectShowCamera.EnumerateVideoDevices();

            if (cameras.Count == 0)
            {
                SetStatus("Keine USB- oder Webcam-Kamera gefunden.");

                MessageBox.Show(
                    this,
                    "Keine USB- oder Webcam-Kamera gefunden.",
                    "Kamera",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);

                return null;
            }

            var targetName = GetSetting("TargetCameraName");
            var matchMode = GetSetting("CameraMatchMode", "Contains");

            if (string.IsNullOrWhiteSpace(targetName))
                return cameras[0];

            var comparison = StringComparison.OrdinalIgnoreCase;

            var camera = matchMode.Equals("Exact", comparison)
                ? cameras.FirstOrDefault(x => string.Equals(x.Name, targetName, comparison))
                : cameras.FirstOrDefault(x => x.Name.IndexOf(targetName, comparison) >= 0);

            if (camera != null)
                return camera;

            var foundCameras = string.Join(Environment.NewLine, cameras.Select(x => "- " + x.Name));

            SetStatus("Zielkamera nicht gefunden: " + targetName);

            MessageBox.Show(
                this,
                "Die Zielkamera wurde nicht gefunden:" +
                Environment.NewLine +
                targetName +
                Environment.NewLine +
                Environment.NewLine +
                "Gefundene Kameras:" +
                Environment.NewLine +
                foundCameras,
                "Zielkamera nicht gefunden",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);

            return null;
        }

        private void StopCamera(string status)
        {
            StopRecording(false);
            CleanupCamera();

            SetStatus(status);

            _startButton.Enabled = true;
            _stopButton.Enabled = false;
            _recordButton.Enabled = false;
        }

        private void CleanupCamera()
        {
            if (_camera == null)
                return;

            _camera.Dispose();
            _camera = null;
        }

        private void CleanupAll()
        {
            StopRecording(false);
            CleanupCamera();
        }

        private void ToggleRecording()
        {
            if (_isRecording)
                StopRecording(true);
            else
                StartRecording();
        }

        private void StartRecording()
        {
            if (_camera == null)
            {
                MessageBox.Show(this, "Die Kamera ist nicht aktiv.", "Aufnahme", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (_previewPanel.ClientSize.Width <= 0 || _previewPanel.ClientSize.Height <= 0)
                return;

            try
            {
                var folder = GetRecordingFolder();
                Directory.CreateDirectory(folder);

                var fileName = "Gabelstapler_Aufnahme_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".avi";
                _lastRecordingPath = Path.Combine(folder, fileName);

                _recorder = new MjpegAviRecorder(
                    _lastRecordingPath,
                    _previewPanel.ClientSize.Width,
                    _previewPanel.ClientSize.Height,
                    _recordingFps,
                    _jpegQuality);

                _isRecording = true;
                _recordingTimer.Start();

                _recordButton.Text = "Aufnahme stoppen";
                _recordingLabel.Text = "Aufnahme: läuft";
                SetStatus("Aufnahme läuft: " + fileName);
            }
            catch (Exception ex)
            {
                StopRecording(false);

                MessageBox.Show(
                    this,
                    ex.Message,
                    "Aufnahme-Fehler",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void StopRecording(bool showMessage)
        {
            if (!_isRecording && _recorder == null)
                return;

            _recordingTimer.Stop();
            _isRecording = false;

            try
            {
                _recorder?.Close();
            }
            catch
            {
            }
            finally
            {
                _recorder = null;
            }

            _recordButton.Text = "Aufnahme starten";
            _recordingLabel.Text = "Aufnahme: aus";

            if (showMessage && !string.IsNullOrWhiteSpace(_lastRecordingPath))
            {
                SetStatus("Aufnahme gespeichert: " + _lastRecordingPath);

                MessageBox.Show(
                    this,
                    "Die Aufnahme wurde gespeichert:" + Environment.NewLine + _lastRecordingPath,
                    "Aufnahme gespeichert",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        private void RecordCurrentFrame()
        {
            if (!_isRecording || _recorder == null)
                return;

            if (WindowState == FormWindowState.Minimized)
                return;

            try
            {
                BringGuideLineToFront();

                var size = _previewPanel.ClientSize;

                if (size.Width <= 0 || size.Height <= 0)
                    return;

                using (var bitmap = new Bitmap(size.Width, size.Height))
                using (var graphics = Graphics.FromImage(bitmap))
                {
                    var source = _previewPanel.PointToScreen(Point.Empty);
                    graphics.CopyFromScreen(source, Point.Empty, size);
                    _recorder.WriteFrame(bitmap);
                }
            }
            catch (Exception ex)
            {
                StopRecording(false);

                MessageBox.Show(
                    this,
                    ex.Message,
                    "Aufnahme-Fehler",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private void PreviewPanelMouseWheel(object sender, MouseEventArgs e)
        {
            MoveGuideLine(e.Delta > 0 ? -_guideLineStep : _guideLineStep);
        }

        private void MoveGuideLine(int delta)
        {
            _guideLineY += delta;
            ClampAndPlaceGuideLine();
        }

        private void PlaceGuideLineFromConfig()
        {
            var percent = GetIntSetting("GuideLinePositionPercent", 70, 0, 100);
            _guideLineY = Math.Max(0, _previewPanel.ClientSize.Height * percent / 100);
            ClampAndPlaceGuideLine();
        }

        private void ClampAndPlaceGuideLine()
        {
            var height = Math.Max(1, _previewPanel.ClientSize.Height);
            var width = Math.Max(1, _previewPanel.ClientSize.Width);

            _guideLineY = Math.Max(0, Math.Min(height - _guideLine.Height, _guideLineY));

            _guideLine.Left = 0;
            _guideLine.Top = _guideLineY;
            _guideLine.Width = width;

            _guideLabel.Left = 14;
            _guideLabel.Top = Math.Max(0, _guideLineY - _guideLabel.Height - 4);

            BringGuideLineToFront();
        }

        private void BringGuideLineToFront()
        {
            _guideLine.BringToFront();
            _guideLabel.BringToFront();
        }

        private string GetRecordingFolder()
        {
            var folder = GetSetting("RecordingFolder", "Aufnahmen");

            if (Path.IsPathRooted(folder))
                return folder;

            return Path.Combine(Application.StartupPath, folder);
        }

        private static string GetSetting(string key, string fallback = "")
        {
            var value = ConfigurationManager.AppSettings[key];

            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            return value.Trim();
        }

        private static int GetIntSetting(string key, int fallback, int min, int max)
        {
            var text = GetSetting(key);

            if (!int.TryParse(text, out var value))
                return fallback;

            return Math.Max(min, Math.Min(max, value));
        }

        private void SetStatus(string text)
        {
            _statusLabel.Text = text;
        }
    }
}
