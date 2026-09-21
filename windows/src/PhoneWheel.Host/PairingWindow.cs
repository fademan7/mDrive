using QRCoder;

namespace PhoneWheel.Host;

// The UI has its own thread; QR rendering never runs on the safety watchdog.
internal sealed class PairingWindow : IDisposable
{
    private readonly CancellationTokenSource close = new();
    private string status = "Waiting · scan this QR on your phone";

    public PairingWindow(string payload, string address, Action stop, bool usb = false, WendyService? wendy = null)
    {
        var thread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                using var form = new Form {
                    Text = usb ? "PhoneWheel · USB connection" : "PhoneWheel · Wi-Fi QR connection", ClientSize = new Size(660, 835),
                    StartPosition = FormStartPosition.CenterScreen,
                    FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false,
                    BackColor = Color.FromArgb(22, 27, 33), ForeColor = Color.White,
                    Font = new Font("Segoe UI", 11)
                };
                using var generator = new QRCodeGenerator();
                using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
                using var png = new PngByteQRCode(data);
                using var stream = new MemoryStream(png.GetGraphic(10));
                using var bitmap = new Bitmap(stream);
                var title = new Label { Text = "Wi-Fi mode · scan the new QR", Bounds = new Rectangle(20, 16, 620, 40), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 17, FontStyle.Bold) };
                var qr = new PictureBox { Image = bitmap, SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(125, 68, 410, 410), BackColor = Color.White };
                var instructions = new Label { Text = $"1. Connect PC and phone to the same Wi-Fi\n2. PhoneWheel > Options > QR / PC connection\n3. Hold comfortably and release controls to start\nPC address: {address}", Bounds = new Rectangle(35, 490, 590, 105), TextAlign = ContentAlignment.MiddleCenter };
                var state = new Label { Bounds = new Rectangle(15, 602, 630, 125), TextAlign = ContentAlignment.MiddleCenter, ForeColor = Color.LightGreen };
                form.Controls.AddRange([title, qr, instructions, state]);
                var engineerToggle = new CheckBox { Text = "F1 Engineer: ON / OFF", Checked = wendy?.Enabled == true, Bounds = new Rectangle(25, 738, 250, 28), AutoSize = false };
                var engineerStatus = new Label { Bounds = new Rectangle(25, 770, 610, 58), ForeColor = Color.LightSkyBlue, Font = new Font("Segoe UI", 10) };
                engineerToggle.CheckedChanged += (_, _) => wendy?.SetEnabled(engineerToggle.Checked);
                form.Controls.AddRange([engineerToggle, engineerStatus]);
                var debug = new Button { Text = "Wendy details", Bounds = new Rectangle(350, 735, 170, 32) };
                var about = new Button { Text = "About", Bounds = new Rectangle(535, 735, 95, 32) };
                about.Click += (_, _) => {
                    using var credits = new Form { Text = "mDrive · Creator", ClientSize = new Size(465, 175), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
                    credits.Controls.Add(new Label { Text = "mDrive 0.5.3 · Created by fademan7 / neojshin", Bounds = new Rectangle(20, 18, 430, 35) });
                    var website = new LinkLabel { Text = "https://fademan7.github.io/", Bounds = new Rectangle(20, 65, 420, 30) };
                    var email = new LinkLabel { Text = "neojshin@gmail.com", Bounds = new Rectangle(20, 105, 420, 30) };
                    void Open(string uri) { try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri) { UseShellExecute = true }); } catch { MessageBox.Show(credits, uri, "Creator contact"); } }
                    website.LinkClicked += (_, _) => Open("https://fademan7.github.io/");
                    email.LinkClicked += (_, _) => Open("mailto:neojshin@gmail.com");
                    credits.Controls.AddRange([website, email]); credits.ShowDialog(form);
                };
                form.Controls.Add(about);
                debug.Click += (_, _) => {
                    using var details = new Form { Text = "Wendy · Heard / Intent / Response", Size = new Size(780, 580), StartPosition = FormStartPosition.CenterParent };
                    var text = new TextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
                    details.Controls.Add(text);
                    using var update = new System.Windows.Forms.Timer { Interval = 500 };
                    void Refresh() { var value = ((wendy?.Diagnostics ?? "OFF") + "\n\nSaved garage recommendations (not applied):\n" + wendy?.Recommendations).Replace("\n", "\r\n"); if (text.Text != value) text.Text = value; }
                    update.Tick += (_, _) => Refresh(); Refresh(); update.Start(); details.ShowDialog(form);
                };
                form.Controls.Add(debug);
                if (usb) {
                    title.Text = "Automatic USB connection";
                    qr.Visible = false;
                    form.Controls.Add(new Label { Text = "USB\n\nNo Wi-Fi or QR required\n\nPhone app starts automatically",
                        Bounds = qr.Bounds, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 22, FontStyle.Bold) });
                    instructions.Text = "Unlock phone and authorize USB debugging\nHold comfortably and release controls to start\nAfter recovery, release controls and center\nRestart receiver if phone/PC app was closed";
                }
                using var timer = new System.Windows.Forms.Timer { Interval = 250 };
                timer.Tick += (_, _) => { if (close.IsCancellationRequested) form.Close(); else { state.Text = Volatile.Read(ref status); engineerStatus.Text = wendy?.Display ?? "F1 Engineer: OFF"; } };
                form.FormClosed += (_, _) => stop();
                timer.Start();
                Application.Run(form);
            }
            catch (Exception ex) { Console.Error.WriteLine($"QR window error: {ex.GetType().Name}. Use the manual pairing information in the console."); }
        }) { IsBackground = true, Name = "PhoneWheel pairing UI" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public void Update(string value) => Volatile.Write(ref status, value);
    public void Dispose() => close.Cancel();
}
