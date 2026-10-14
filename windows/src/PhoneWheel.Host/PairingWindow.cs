using QRCoder;
using PhoneWheel.Core;

namespace PhoneWheel.Host;

// The UI has its own thread; QR rendering never runs on the safety watchdog.
internal sealed class PairingWindow : IDisposable
{
    private readonly CancellationTokenSource close = new();
    private string status = "Waiting · scan this QR on your phone";

    public PairingWindow(string payload, string address, Action stop, WendyService? wendy = null,bool checkAudio=false)
    {
        var thread = new Thread(() =>
        {
            try
            {
                Application.EnableVisualStyles();
                using var appIcon=Icon.ExtractAssociatedIcon(Environment.ProcessPath!);
                using var form = new Form {
                    Icon = appIcon,
                    Text = "mDrive · Wi-Fi & Wendy", ClientSize = new Size(640, 775),
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
                var title = new Label { Text = "mDrive · Wi-Fi & Wendy", Bounds = new Rectangle(20, 12, 600, 38), TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 17, FontStyle.Bold) };
                var qr = new PictureBox { Image = bitmap, SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(20, 62, 205, 205), BackColor = Color.White };
                var instructions = new Label { Text = $"Same Wi-Fi network\nPhone: mDrive > QR connection\nHold level and release controls to start\nPC: {address}", Bounds = new Rectangle(245, 64, 375, 102) };
                var state = new Label { Bounds = new Rectangle(245, 175, 375, 138), Font=new Font("Segoe UI",9), ForeColor = Color.LightGreen };
                form.Controls.AddRange([title, qr, instructions, state]);
                var engineerToggle = new CheckBox { Text = "Wendy · F1 2025 / 2026", Checked = wendy?.Enabled == true, Bounds = new Rectangle(25, 300, 400, 28), AutoSize = false };
                var engineerStatus = new Label { Bounds = new Rectangle(25, 337, 590, 58), ForeColor = Color.LightSkyBlue, Font = new Font("Segoe UI", 10) };
                engineerToggle.CheckedChanged += (_, _) => wendy?.SetEnabled(engineerToggle.Checked);
                form.Controls.AddRange([engineerToggle, engineerStatus]);
                var input=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Bounds=new(115,405,170,30)};
                var output=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Bounds=new(440,405,170,30)};
                input.Items.AddRange(["PC","Android"]);output.Items.AddRange(["PC","Android"]);
                input.SelectedIndex=(int)(wendy?.Audio.Input??WendyAudioDevice.PC);output.SelectedIndex=(int)(wendy?.Audio.Output??WendyAudioDevice.PC);
                var volume=new TrackBar {Minimum=0,Maximum=100,Value=wendy?.Audio.Volume??100,TickFrequency=10,Bounds=new(135,453,350,45)};
                var volumeLabel=new Label {Text=$"{volume.Value}%",Bounds=new(505,460,80,30)};
                var audioHealth=new Label {Bounds=new(25,513,590,76),Font=new Font("Segoe UI",10)};
                var saved=true;
                void ApplyAudio() { if(wendy==null)return;wendy.ConfigureAudio((WendyAudioDevice)input.SelectedIndex,(WendyAudioDevice)output.SelectedIndex,volume.Value);volumeLabel.Text=$"{volume.Value}%"; }
                input.SelectedIndexChanged+=(_,_)=>ApplyAudio();output.SelectedIndexChanged+=(_,_)=>ApplyAudio();volume.ValueChanged+=(_,_)=>ApplyAudio();
                // Debounce preference writes on this non-controller UI thread.
                using var saveTimer=new System.Windows.Forms.Timer {Interval=600};
                saveTimer.Tick+=(_,_)=>{saveTimer.Stop();if(wendy!=null)saved=WendyAudioPreferences.Save(wendy.Audio);};
                input.SelectedIndexChanged+=(_,_)=>{saveTimer.Stop();saveTimer.Start();};output.SelectedIndexChanged+=(_,_)=>{saveTimer.Stop();saveTimer.Start();};volume.ValueChanged+=(_,_)=>{saveTimer.Stop();saveTimer.Start();};
                form.Controls.AddRange([new Label {Text="Microphone",Bounds=new(25,408,90,30)},input,new Label {Text="Voice output",Bounds=new(325,408,115,30)},output,
                    new Label {Text="Wendy volume",Bounds=new(25,460,120,30)},volume,volumeLabel,audioHealth,
                    new Label {Text="PC audio uses Windows default microphone / playback device.\nUse the Windows volume mixer to balance the game and Wendy.",Bounds=new(25,593,590,45),Font=new Font("Segoe UI",9)}]);
                var voices=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Bounds=new(135,642,475,30)};
                var voiceNames=new List<string>{""};
                voices.Items.Add("Automatic English voice");
                voices.SelectedIndex=0;
                form.Controls.AddRange([new Label{Text="PC voice",Bounds=new(25,645,100,30)},voices]);
                form.Shown+=async (_,_)=>{
                    var installed=await Task.Run(()=>{
                        try {using var synth=new System.Speech.Synthesis.SpeechSynthesizer();
                            return synth.GetInstalledVoices().Where(v=>v.Enabled&&v.VoiceInfo.Culture.TwoLetterISOLanguageName=="en")
                                .Select(v=>(Name:v.VoiceInfo.Name, Label:$"{v.VoiceInfo.Name} · {v.VoiceInfo.Culture.Name} · {v.VoiceInfo.Gender}")).ToArray();
                        }catch{return Array.Empty<(string Name,string Label)>();}
                    });
                    if(form.IsDisposed)return;
                    foreach(var v in installed){voiceNames.Add(v.Name);voices.Items.Add(v.Label);}
                    voices.SelectedIndex=Math.Max(0,voiceNames.IndexOf(wendy?.Audio.Voice??""));
                    voices.SelectedIndexChanged+=(_,_)=>{
                        if(wendy!=null)wendy.ConfigureAudio((WendyAudioDevice)input.SelectedIndex,(WendyAudioDevice)output.SelectedIndex,volume.Value,voiceNames[voices.SelectedIndex]);
                        saveTimer.Stop();saveTimer.Start();
                    };
                };
                var debug = new Button { Text = "Wendy details", Bounds = new Rectangle(25, 715, 180, 32) };
                var about = new Button { Text = "About", Bounds = new Rectangle(520, 715, 95, 32) };
                about.Click += (_, _) => {
                    using var credits = new Form { Text = "mDrive · Creator", ClientSize = new Size(465, 175), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false };
                    credits.Controls.Add(new Label { Text = "mDrive 0.5.14 · Created by fademan7 / neojshin", Bounds = new Rectangle(20, 18, 430, 35) });
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
                var audioCheck=new Button{Text="Audio check",Bounds=new Rectangle(225,715,180,32)};
                audioCheck.Click+=async (_,_)=> {
                    audioCheck.Enabled=false;
                    try {
                        var result=await Task.Run(()=> {
                            using var voice=new System.Speech.Synthesis.SpeechSynthesizer();
                            var voices=voice.GetInstalledVoices().Count(v=>v.Enabled&&v.VoiceInfo.Culture.TwoLetterISOLanguageName=="en");
                            var model=File.Exists(Path.Combine(VoskSpeechWorker.ModelPath,"am","final.mdl"));
                            var microphones=NAudio.Wave.WaveInEvent.DeviceCount;
                            return $"Offline English model: {(model?"Installed":"Missing - reinstall mDrive")}\nMicrophones: {microphones}\nEnglish output voices: {voices}\n\n"+
                                (voices==0?"Install an English TTS voice in Windows language options, or select Android output.\n":"")+
                                "Select your headset as Windows default input/output. Enable Wendy, then say 'Radio check'.\nNo Windows English recognition pack is needed.\nFor British male / American female output, install a matching English voice in Windows language options; only voices available to this app appear in PC voice. Android output uses its own TTS voice.";
                        });
                        MessageBox.Show(form,result,"mDrive audio check");
                    }catch(Exception){MessageBox.Show(form,"Audio devices/English output could not be checked. Select Android audio or check Windows speech settings.","mDrive audio check");}
                    finally{if(!audioCheck.IsDisposed)audioCheck.Enabled=true;}
                };
                form.Controls.Add(audioCheck);
                if(checkAudio)form.Shown+=(_,_)=>audioCheck.PerformClick();
                using var timer = new System.Windows.Forms.Timer { Interval = 250 };
                timer.Tick += (_, _) => { if (close.IsCancellationRequested) form.Close(); else { state.Text = Volatile.Read(ref status); engineerStatus.Text = wendy?.Display ?? "F1 Engineer: OFF";audioHealth.Text=(wendy?.AudioHealth??"")+(saved?"":"\nAudio preferences could not be saved."); } };
                form.FormClosed += (_, _) => {if(wendy!=null)WendyAudioPreferences.Save(wendy.Audio);stop();};
                timer.Start();
                Application.Run(form);
            }
            catch (Exception ex) { MessageBox.Show($"mDrive window failed: {ex.Message}","mDrive");stop(); }
        }) { IsBackground = true, Name = "PhoneWheel pairing UI" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
    }

    public void Update(string value) => Volatile.Write(ref status, value);
    public void Dispose() => close.Cancel();
}
