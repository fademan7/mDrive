using System.Buffers.Binary;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using PhoneWheel.Core;

namespace PhoneWheel.Host;

internal sealed class WendyService(IPAddress bind, byte[] key, int port = 26762, int telemetryPort = 20777) : IDisposable
{
    private readonly object lifecycle = new();
    private CancellationTokenSource? lifetime;
    private Task? run;
    private string display = "F1 Engineer: OFF | Telemetry: Waiting | Wendy: Idle | Flag: UNKNOWN";
    public string Display => Volatile.Read(ref display);
    private string diagnostics = "Heard: —\nIntent: —\nResponse: —";
    private string recommendations = "No saved recommendations.";
    public string Diagnostics => Volatile.Read(ref diagnostics);
    public string Recommendations => Volatile.Read(ref recommendations);
    public bool Enabled { get { lock (lifecycle) return lifetime != null; } }
    public int Port => port;
    public void SetEnabled(bool enabled)
    {
        lock (lifecycle)
        {
            if (!enabled) { lifetime?.Cancel(); lifetime = null; Volatile.Write(ref display, "F1 Engineer: OFF | Telemetry: Waiting | Wendy: Idle | Flag: UNKNOWN"); return; }
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
        using var model = new CpuIntentModel();
        var store = new WendyRecommendationStore();
        engineer.Recommendations.AddRange(store.Load());
        void ShowRecommendations() => Volatile.Write(ref recommendations, string.Join("\n\n", engineer.Recommendations.Select(r => $"{r.CreatedAt:u} · {r.Observation}\n{r.Recommendation}\nEvidence: {r.Evidence}")));
        ShowRecommendations();
        var voice = "IDLE"; var voiceAt = double.NegativeInfinity;
        void Show()
        {
            lock (lifecycle) if (lifetime == owner)
                Volatile.Write(ref display, $"F1 Engineer: ON | Telemetry: {(race.Connected(Now()) ? "Connected" : "Waiting")}\nWendy: {(F1RaceState.Fresh(voiceAt, Now(), 3000) ? voice : "IDLE")} | Flag: {race.Flag(Now())}");
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
                        lock (sync) if (race.Ingest(packet.Buffer, Now())) { source = packet.RemoteEndPoint; sourceAt = Now(); engineer.Observe(race, Now()); }
                    }
                } catch (OperationCanceledException) { }
                catch (SocketException) { lock (lifecycle) if (lifetime == owner) Volatile.Write(ref display, "F1 Engineer: ERROR | Telemetry socket stopped. Toggle OFF/ON."); owner.Cancel(); }
            }, token);
            var refresh = Task.Run(async () => {
                try { using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500)); while (await timer.WaitForNextTickAsync(token)) lock (sync) Show(); }
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
                    string heard = ""; double heardConfidence = -1; int heardGeneration = -1;
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
                            var command = root.TryGetProperty("text", out var t) ? t.GetString() ?? "" : "";
                            var allowAlerts = state == "LISTENING" && root.TryGetProperty("allowAlerts", out var alerts) && alerts.ValueKind == JsonValueKind.True;
                            var confidence = root.TryGetProperty("confidence", out var c) && c.TryGetDouble(out var cf) ? cf : -1;
                            if (command.Length > 240) throw new ProtocolException("Voice request too long.");
                            // Classify outside the telemetry lock; polling and flags keep flowing.
                            if (command.Length > 0 && work == null) {
                                heard = command; heardConfidence = confidence;
                                lock (sync) heardGeneration = race.Generation;
                                work = model.Classify(command, clientLifetime.Token);
                                Volatile.Write(ref diagnostics, $"Heard: {heard}\nIntent: PROCESSING\nResponse: —");
                            }
                            IntentResult? classified = null;
                            if (work?.IsCompleted == true) { classified = await work; work = null; }
                            byte[] response;
                            WendyReply? reply; SetupRecommendation[]? saved = null;
                            string flag; bool telemetryConnected;
                            lock (sync)
                            {
                                voice = state; voiceAt = Now();
                                reply = classified != null && state == "PROCESSING" ?
                                    heardGeneration == race.Generation ? engineer.Answer(classified.Value, heard, heardConfidence, race, Now()) : new WendyReply("Session changed. Please ask again.") :
                                    work == null && command.Length == 0 && (state == "IDLE" || allowAlerts) ? engineer.Alert(race, Now()) : null;
                                if (reply?.Kind == "saved") { saved = engineer.Recommendations.ToArray(); ShowRecommendations(); }
                                telemetryConnected = race.Connected(Now()); flag = race.Flag(Now());
                                Show();
                            }
                            if (saved != null && !await store.Save(saved, token)) reply = new("Saved in memory only; storage is unavailable. No game setting changed.", "saved");
                            if (classified != null) Volatile.Write(ref diagnostics, $"Heard: {heard}\nIntent: {classified.Value.Intent} / {classified.Value.Symptom} / {classified.Value.Phase} / {classified.Value.Condition}\nSource: {classified.Source} · {classified.ElapsedMs:0} ms\nResponse: {reply?.Text ?? "Discarded: speech no longer pending."}\n{model.Status}");
                            response = JsonSerializer.SerializeToUtf8Bytes(new { enabled = true, telemetry = telemetryConnected, flag, text = reply?.Text ?? "", kind = reply?.Kind ?? (work != null ? "pending" : "status"),
                                heard = classified != null ? heard : "", intent = classified?.Value.Intent.ToString() ?? "", source = classified?.Source ?? "" });
                            var encoded = WendyWire.Encode(response, key, nonce, 1, sequence);
                            BinaryPrimitives.WriteInt32BigEndian(prefix, encoded.Length);
                            await stream.WriteAsync(prefix, deadline.Token); await stream.WriteAsync(encoded, deadline.Token);
                            // Bounded rate even if a paired client sends continuously.
                            await Task.Delay(200, token);
                        }
                    }
                    catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ProtocolException or JsonException or InvalidOperationException or KeyNotFoundException) { }
                    finally { clientLifetime.Cancel(); if (work != null) try { await work; } catch (OperationCanceledException) { } }
                    lock (sync) { engineer.ResetConversation(); voice = "IDLE"; voiceAt = double.NegativeInfinity; }
                }
            }
            finally { owner.Cancel(); await Task.WhenAll(receive, refresh); }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException) {
            if (!token.IsCancellationRequested) lock (lifecycle) if (lifetime == owner) Volatile.Write(ref display, $"F1 Engineer: ERROR | Port {port}/{telemetryPort} unavailable. Toggle OFF/ON.");
        }
        catch (Exception) { lock (lifecycle) if (lifetime == owner) Volatile.Write(ref display, "F1 Engineer: ERROR | Wendy stopped. Controller unaffected; toggle OFF/ON."); }
        finally { listener?.Stop(); lock (lifecycle) { if (lifetime == owner) lifetime = null; owner.Dispose(); } }
    }
    public void Dispose() {
        SetEnabled(false);
        Task? stopped; lock (lifecycle) stopped = run;
        try { stopped?.Wait(TimeSpan.FromSeconds(3)); } catch (AggregateException) { }
    }
}
