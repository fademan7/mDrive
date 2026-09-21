using System.Diagnostics;

namespace PhoneWheel.Host;

internal sealed record UsbConnector(string AdbPath, string Serial)
{
    public static async Task<UsbConnector?> FindAsync()
    {
        var candidates = new List<string>();
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
            candidates.Add(Path.Combine(dir.FullName, ".tools", "android-sdk", "platform-tools", "adb.exe"));
        foreach (var variable in new[] { "ANDROID_HOME", "ANDROID_SDK_ROOT" })
            if (Environment.GetEnvironmentVariable(variable) is string sdk)
                candidates.Add(Path.Combine(sdk, "platform-tools", "adb.exe"));
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk", "platform-tools", "adb.exe"));
        foreach (var dir in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
            candidates.Add(Path.Combine(dir, "adb.exe"));
        var path = candidates.FirstOrDefault(File.Exists);
        if (path is null) return null;
        var output = await RunAsync(path, ["devices"]);
        var devices = output.Split('\n').Select(line => line.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 2 && parts[1] == "device" && !parts[0].Contains(':') && !parts[0].StartsWith("emulator-"))
            .Select(parts => parts[0]).ToArray();
        if (devices.Length > 1) throw new InvalidOperationException("Connect only one USB phone.");
        return devices.Length == 1 ? new(path, devices[0]) : null;
    }

    public async Task ConnectAsync(int port, string payload)
    {
        await EnsureReverseAsync(port);
        // Only a locally generated, fixed-alphabet URI is sent to the explicit app.
        // Do not log command arguments or persist the session key.
        if (payload.Any(c => !(char.IsAsciiLetterOrDigit(c) || ":/?=&._-".Contains(c))))
            throw new ArgumentException("Invalid USB pairing payload.");
        await RunAsync(AdbPath, ["-s", Serial, "shell", "am", "start", "-n", "dev.phonewheel/.MainActivity",
            "--es", "phonewheel_usb", $"'{payload}'"]);
    }

    public async Task EnsureReverseAsync(int port)
    {
        // --no-rebind prevents overwriting another app's port forwarding rule.
        var mapping = $"tcp:{port}";
        var existing = await RunAsync(AdbPath, ["-s", Serial, "reverse", "--list"]);
        var ours = existing.Split('\n').Any(line => {
            var fields = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            return fields.Length >= 3 && fields[^2] == mapping && fields[^1] == mapping;
        });
        if (!ours) await RunAsync(AdbPath, ["-s", Serial, "reverse", "--no-rebind", mapping, mapping]);
    }

    private static async Task<string> RunAsync(string file, string[] args)
    {
        using var process = new Process { StartInfo = new ProcessStartInfo(file) {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        } };
        foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
        process.Start();
        var output = process.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try { await process.WaitForExitAsync(deadline.Token); }
        catch (OperationCanceledException) { process.Kill(); throw new IOException("USB timed out · check cable and debugging authorization."); }
        var text = await output;
        await error; // Avoid logging adb output, which may contain pairing data.
        if (process.ExitCode != 0 || text.Contains("Error:")) throw new IOException("USB setup failed · unlock phone, authorize debugging and install the app.");
        return text;
    }
}
