using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using PhoneWheel.Core;

namespace PhoneWheel.Host;

internal sealed class WendyService(IPAddress bind, byte[] key, int port = 26762, int telemetryPort = 20777, IWendyPcVoice? pcVoice = null) : IDisposable
{
    private WendyAudioSettings audio = new();
    public WendyAudioSettings Audio => Volatile.Read(ref audio);
    public string AudioHealth => Audio.Input==WendyAudioDevice.PC ? pcVoice?.Health ?? "PC audio backend unavailable." :
        Audio.Output==WendyAudioDevice.Android ? "Android microphone and voice output. Check phone permissions and media volume." :
        pcVoice?.Health.StartsWith("PC output unavailable",StringComparison.Ordinal)==true ? pcVoice.Health : "Android microphone · PC voice output. Windows default playback device.";
    private int radioHeld;
    private long radioUntil;
    // Atomic mailbox only. Called after authenticated control input, never invokes speech APIs.
    public void ControllerRadio(bool held) {
        var previous=Interlocked.Exchange(ref radioHeld,held?1:0);
        if(held || previous==1) Interlocked.Exchange(ref radioUntil,Stopwatch.GetTimestamp()+8*Stopwatch.Frequency);
    }
    private bool ControllerRadioActive => Volatile.Read(ref radioHeld)!=0 || Stopwatch.GetTimestamp()<Interlocked.Read(ref radioUntil);
    public void ConfigureAudio(WendyAudioDevice input,WendyAudioDevice output,int volume, string? voiceName = null) {
        if(!Enum.IsDefined(input)||!Enum.IsDefined(output))throw new ArgumentException("Invalid audio route.");
        lock(lifecycle) {
            var old=Audio;Volatile.Write(ref audio,new(input,output,Math.Clamp(volume,0,100),old.Revision+(old.Input!=input||old.Output!=output||voiceName!=null&&old.Voice!=voiceName?1:0),voiceName??old.Voice));
        }
    }
    private readonly object lifecycle = new();
    private CancellationTokenSource? lifetime;
    private Task? run;
    private string display = "F1 2025/2026: OFF | Telemetry: Waiting | Wendy: Idle | Flag: UNKNOWN";
    public string Display => Volatile.Read(ref display);
    private string diagnostics = "Heard: —\nIntent: —\nResponse: —";
    private string recommendations = "No saved recommendations.";
    public string Diagnostics => Volatile.Read(ref diagnostics);
    public string Recommendations => Volatile.Read(ref recommendations);
    private long controllerConnection;
    // Rare reconnect notification only: no Wendy lock or work on the control thread.
    public void ControllerReconnected() { Interlocked.Exchange(ref radioHeld,0); Interlocked.Exchange(ref radioUntil,0); Interlocked.Increment(ref controllerConnection); }
    public bool Enabled { get { lock (lifecycle) return lifetime != null; } }
    public int Port => port;
    public void SetEnabled(bool enabled)
    {
        lock (lifecycle)
        {
            if (!enabled) { pcVoice?.Configure(false,true,Audio.Volume);pcVoice?.Cancel();pcVoice?.ReleaseInput(); lifetime?.Cancel(); lifetime = null; Volatile.Write(ref display, "F1 2025/2026: OFF | Telemetry: Waiting | Wendy: Idle | Flag: UNKNOWN"); return; }
            if (lifetime != null) return;
            var owner = new CancellationTokenSource(); lifetime = owner;
            var prior = run;
            run = Task.Run(async () => { if (prior != null) try { await prior; } catch (Exception) { /* A failed optional service must remain restartable. */ } await Run(owner); });
        }
    }
    private async Task Run(CancellationTokenSource owner)
    {
        var token = owner.Token;
        var clock = Stopwatch.GetTimestamp();
        double Now() => Stopwatch.GetElapsedTime(clock).TotalMilliseconds;
        var race = new F1RaceState(); var engineer = new WendyEngineer(); var sync = new object();
        var observedController = Interlocked.Read(ref controllerConnection);
        void CheckControllerConnection() {
            var current = Interlocked.Read(ref controllerConnection);
            if(current != observedController) { observedController = current; engineer.ResetConversation();pcVoice?.Cancel(); }
        }
        using var model = new CpuIntentModel();
        var store = new WendyRecommendationStore();
        engineer.Recommendations.AddRange(store.Load());
        void ShowRecommendations() => Volatile.Write(ref recommendations, string.Join("\n\n", engineer.Recommendations.Select(r => $"{r.CreatedAt:u} · {r.Observation}\n{r.Recommendation}\nEvidence: {r.Evidence}")));
        ShowRecommendations();
        var voice = "IDLE"; var voiceAt = double.NegativeInfinity;
        var phoneState="ERROR";var phoneAt=double.NegativeInfinity;var phoneRadio=false;
        WendyReply? phoneReply=null;double phoneReplyAt=0;double outputWaitUntil=0;
        Task<IntentResult>? pcWork=null;string pcHeard="";double pcConfidence=-1;int pcGeneration=-1;long pcConnection=-1;
        var audioRevision=Audio.Revision;
        void Show()
        {
            lock (lifecycle) if (lifetime == owner)
                Volatile.Write(ref display, $"F1 2025/2026: ON | Telemetry: {(race.Connected(Now()) ? "Connected" : "Waiting")}\nWendy: {(F1RaceState.Fresh(voiceAt, Now(), 3000) ? voice : "IDLE")} | Flag: {race.Flag(Now())} | Mode: {(race.Live(Now()) ? "Racing" : "Chat")}");
        }
        TcpListener? listener = null;
        try
        {
            token.ThrowIfCancellationRequested();
            using var telemetry = new UdpClient(new IPEndPoint(IPAddress.Loopback, telemetryPort));
            telemetry.Client.ReceiveBufferSize = 65536;
            listener = new TcpListener(bind, port); listener.Start(1);
            using var closeListener = token.Register(listener.Stop);
            var receive = Task.Run(async () => {
                IPEndPoint? source = null; double sourceAt = double.NegativeInfinity;
                try {
                    while (!token.IsCancellationRequested)
                    {
                        var packet = await telemetry.ReceiveAsync(token);
                        if (source != null && !packet.RemoteEndPoint.Equals(source) && Now() - sourceAt < 3000) continue;
                        lock (sync) if (race.Ingest(packet.Buffer, Now())) { CheckControllerConnection(); source = packet.RemoteEndPoint; sourceAt = Now(); engineer.Observe(race, Now()); }
                    }
                } catch (OperationCanceledException) { }
                catch (SocketException) { lock (lifecycle) if (lifetime == owner) Volatile.Write(ref display, "F1 2025/2026: ERROR | Telemetry socket stopped. Toggle OFF/ON."); owner.Cancel(); }
            }, token);
            var refresh = Task.Run(async () => {
                try { using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(100)); while (await timer.WaitForNextTickAsync(token)) {
                    WendyReply? pcReply=null;SetupRecommendation[]? saved=null;
                    var route=Audio;var radio=ControllerRadioActive;
                    lock(sync) {
                        CheckControllerConnection();engineer.TickActions(race,Now());
                        radio |= F1RaceState.Fresh(phoneAt,Now(),1500)&&phoneRadio;
                        if(audioRevision!=route.Revision || radio) { engineer.ResetConversation();pcGeneration=-1;phoneReply=null;outputWaitUntil=0; }
                        if(audioRevision!=route.Revision) { audioRevision=route.Revision;pcVoice?.Cancel(); }
                        var remoteOutputBusy=route.Output==WendyAudioDevice.Android && (Now()<outputWaitUntil || F1RaceState.Fresh(phoneAt,Now(),1500)&&phoneState is "SPEAKING" or "PROCESSING");
                        var pcSpeaking=pcVoice?.Speaking==true;
                        pcVoice?.SelectVoice(route.Voice);
                        pcVoice?.Configure(route.Input==WendyAudioDevice.PC && pcWork==null && !remoteOutputBusy,!Enabled||radio,route.Volume);
                        if(radio) {pcVoice?.Cancel();pcVoice?.TakeInput();voice="GAME RADIO";voiceAt=Now();}
                        else if(route.Input==WendyAudioDevice.PC) {
                            if(pcWork==null && !remoteOutputBusy && !pcSpeaking && pcVoice?.TakeInput() is { } input) {
                                pcHeard=input.Text;pcConfidence=input.Confidence;pcGeneration=race.Generation;pcConnection=observedController;
                                var utterance=pcHeard;
                                pcWork=race.Live(Now()) ? Task.Run(()=>model.Classify(utterance,token),token) : Task.FromResult(new IntentResult(WendyLanguage.Fallback(utterance), "Race rules", 0));
                            }
                            if(pcWork?.IsCompleted==true) {
                                if(pcWork.IsCompletedSuccessfully && pcGeneration==race.Generation && pcConnection==observedController)
                                    pcReply=engineer.Answer(pcWork.Result.Value,pcHeard,pcConfidence,race,Now());
                                Volatile.Write(ref diagnostics,$"Heard: {pcHeard}\nResponse: {pcReply?.Text??"Discarded after state change."}");pcWork=null;
                            } else if(pcWork==null && !remoteOutputBusy && !pcSpeaking && pcVoice?.HearingSpeech!=true)
                                pcReply=engineer.Alert(race,Now());
                            voice=pcWork!=null?"PROCESSING":remoteOutputBusy?"SPEAKING":pcVoice?.State??"ERROR";voiceAt=Now();
                            if(pcReply?.Kind=="saved") {saved=engineer.Recommendations.ToArray();ShowRecommendations();}
                            if(pcReply!=null && route.Output==WendyAudioDevice.Android && F1RaceState.Fresh(phoneAt,Now(),1500)) {
                                phoneReply=pcReply;phoneReplyAt=Now();outputWaitUntil=Now()+2500;
                            }
                        }
                        Show();
                    }
                    if(saved!=null) await store.Save(saved,token);
                    if(pcReply!=null && route.Output==WendyAudioDevice.PC && !radio && !ControllerRadioActive && route.Revision==Audio.Revision && !token.IsCancellationRequested) pcVoice?.Speak(pcReply.Text);
                } }
                catch (OperationCanceledException) { }
            }, token);
            try
            {
                while (!token.IsCancellationRequested)
                {
                    using var client = await listener.AcceptTcpClientAsync(token);
                    client.NoDelay = true;
                    using var clientLifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
                    Task<IntentResult>? work = null;
                    string heard = ""; double heardConfidence = -1; int heardGeneration = -1; long heardController = -1;
                    var gameRadioWasActive = false;
                    var heardAudioRevision=Audio.Revision;
                    try
                    {
                        var stream = client.GetStream(); var nonce = RandomNumberGenerator.GetBytes(32);
                        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(token)) { deadline.CancelAfter(2000); await stream.WriteAsync(nonce, deadline.Token); }
                        ulong sequence = 0;
                        while (!token.IsCancellationRequested)
                        {
                            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(2000);
                            var prefix = new byte[4]; await stream.ReadExactlyAsync(prefix, deadline.Token);
                            var size = BinaryPrimitives.ReadInt32BigEndian(prefix);
                            if (size is < 42 or > WendyWire.MaxJson + 40) throw new ProtocolException("Invalid Wendy frame size.");
                            var bytes = new byte[size]; await stream.ReadExactlyAsync(bytes, deadline.Token);
                            var json = WendyWire.Decode(bytes, key, nonce, 0, ++sequence);
                            using var request = JsonDocument.Parse(json);
                            var root = request.RootElement;
                            var state = root.GetProperty("state").GetString();
                            if (state is not ("IDLE" or "LISTENING" or "PROCESSING" or "SPEAKING" or "ERROR")) throw new ProtocolException("Invalid voice state.");
                            var gameRadio = root.TryGetProperty("gameRadio", out var radio) && radio.ValueKind == JsonValueKind.True;
                            var route=Audio;
                            var peerRevision=root.TryGetProperty("audioRevision",out var revisionValue)&&revisionValue.TryGetInt64(out var rev)?rev:route.Revision;
                            var command = route.Input==WendyAudioDevice.Android && peerRevision==route.Revision && !gameRadio && root.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                            var allowAlerts = state == "LISTENING" && root.TryGetProperty("allowAlerts", out var alerts) && alerts.ValueKind == JsonValueKind.True;
                            var confidence = root.TryGetProperty("confidence", out var c) && c.TryGetDouble(out var cf) ? cf : -1;
                            if (command.Length > 240) throw new ProtocolException("Voice request too long.");
                            // Classify outside the telemetry lock; polling and flags keep flowing.
                            if (command.Length > 0 && work == null) {
                                heard = command; heardConfidence = confidence;
                                lock (sync) { heardGeneration = race.Generation; heardController = Interlocked.Read(ref controllerConnection);heardAudioRevision=route.Revision; }
                                bool live; lock(sync) live=race.Live(Now());
                                work = live ? model.Classify(command, clientLifetime.Token) : Task.FromResult(new IntentResult(WendyLanguage.Fallback(command), "Race rules", 0));
                                Volatile.Write(ref diagnostics, $"Heard: {heard}\nIntent: PROCESSING\nResponse: —");
                            }
                            IntentResult? classified = null;
                            if (work?.IsCompleted == true) { classified = await work; work = null; }
                            byte[] response;
                            WendyReply? reply; SetupRecommendation[]? saved = null;
                            string flag, summary, commandState; bool telemetryConnected;
                            lock (sync)
                            {
                                CheckControllerConnection();
                                phoneState=state!;phoneAt=Now();phoneRadio=gameRadio;
                                gameRadio |= ControllerRadioActive;
                                if (gameRadio && !gameRadioWasActive) { engineer.ResetConversation(); heardGeneration = -1; }
                                gameRadioWasActive = gameRadio;
                                if(route.Input==WendyAudioDevice.Android) { voice = gameRadio ? "GAME RADIO" : pcVoice?.Speaking==true?"SPEAKING":state; voiceAt = Now(); }
                                reply = gameRadio ? null : route.Input==WendyAudioDevice.PC ?
                                    phoneReply!=null && Now()-phoneReplyAt<2000 ? phoneReply : null :
                                    classified != null && state == "PROCESSING" && heardAudioRevision==route.Revision ?
                                    heardGeneration == race.Generation && heardController == observedController ? engineer.Answer(classified.Value, heard, heardConfidence, race, Now()) : new WendyReply("Session changed. Please ask again.") :
                                    work == null && command.Length == 0 && pcVoice?.Speaking!=true && (state == "IDLE" || allowAlerts) ? engineer.Alert(race, Now()) : null;
                                if(route.Input==WendyAudioDevice.PC) phoneReply=null;
                                if (reply?.Kind == "saved") { saved = engineer.Recommendations.ToArray(); ShowRecommendations(); }
                                telemetryConnected = race.Connected(Now()); flag = race.Flag(Now());
                                summary = engineer.Summary(race, Now()); commandState = engineer.CommandState.ToString();
                                Show();
                            }
                            if (saved != null && !await store.Save(saved, token)) reply = new("Saved in memory only; storage is unavailable. No game setting changed.", "saved");
                            if(reply!=null && route.Input==WendyAudioDevice.Android && route.Output==WendyAudioDevice.PC && !gameRadio && !ControllerRadioActive && route.Revision==Audio.Revision && !token.IsCancellationRequested) pcVoice?.Speak(reply.Text);
                            if (classified != null) Volatile.Write(ref diagnostics, $"Heard: {heard}\nIntent: {classified.Value.Intent} / {classified.Value.Symptom} / {classified.Value.Phase} / {classified.Value.Condition}\nSource: {classified.Source} · {classified.ElapsedMs:0} ms\nResponse: {reply?.Text ?? "Discarded: speech no longer pending."}\n{model.Status}");
                            var responseText = reply?.Text ?? "";
                            byte[] Serialize(string heardText) => JsonSerializer.SerializeToUtf8Bytes(new { enabled = true, telemetry = telemetryConnected, flag, text = responseText, kind = reply?.Kind ?? (work != null ? "pending" : "status"),
                                heard = heardText, intent = classified?.Value.Intent.ToString() ?? "", source = classified?.Source ?? "", summary, commandState,
                                audioInput=route.Input.ToString(),audioOutput=route.Output.ToString(),volume=route.Volume,audioRevision=route.Revision,
                                voiceState=voice,pcSpeaking=pcVoice?.Speaking==true });
                            response = Serialize(classified != null ? heard : "");
                            if(response.Length > WendyWire.MaxJson) response = Serialize(""); // omit display echo, never break voice link for long damage detail
                            if(response.Length > WendyWire.MaxJson || responseText.Length > 1600) {
                                var end = responseText.LastIndexOf('.', Math.Min(900, responseText.Length-1));
                                responseText = responseText[..(end >= 0 ? end+1 : Math.Min(900,responseText.Length))] + " Ask about one component for more detail.";
                                response = Serialize("");
                            }
                            var encoded = WendyWire.Encode(response, key, nonce, 1, sequence);
                            BinaryPrimitives.WriteInt32BigEndian(prefix, encoded.Length);
                            await stream.WriteAsync(prefix, deadline.Token); await stream.WriteAsync(encoded, deadline.Token);
                            // Bounded rate even if a paired client sends continuously.
                            await Task.Delay(200, token);
                        }
                    }
                    catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ProtocolException or JsonException or InvalidOperationException or KeyNotFoundException) { }
                    finally { lock(sync) {engineer.ResetConversation();phoneAt=double.NegativeInfinity;phoneState="ERROR";phoneRadio=false;phoneReply=null;} clientLifetime.Cancel(); if (work != null) try { await work; } catch (OperationCanceledException) { } }
                    lock (sync) { engineer.ResetConversation(); voice = "IDLE"; voiceAt = double.NegativeInfinity; }
                }
            }
            finally { lock(sync) engineer.ResetConversation(); owner.Cancel();pcVoice?.Configure(false,true,Audio.Volume);pcVoice?.Cancel();await Task.WhenAll(receive, refresh);if(pcWork!=null)try{await pcWork;}catch(OperationCanceledException){} }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException) {
            if (!token.IsCancellationRequested) lock (lifecycle) if (lifetime == owner) Volatile.Write(ref display, $"F1 2025/2026: ERROR | Port {port}/{telemetryPort} unavailable. Toggle OFF/ON.");
        }
        catch (Exception) { lock (lifecycle) if (lifetime == owner) Volatile.Write(ref display, "F1 2025/2026: ERROR | Wendy stopped. Controller unaffected; toggle OFF/ON."); }
        finally { listener?.Stop(); lock (lifecycle) { if (lifetime == owner) lifetime = null; owner.Dispose(); } }
    }
    public void Dispose() {
        SetEnabled(false);
        Task? stopped; lock (lifecycle) stopped = run;
        try { stopped?.Wait(TimeSpan.FromSeconds(3)); } catch (AggregateException) { }
    }
}
