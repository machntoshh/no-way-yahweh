using System.Diagnostics;
using System.Text.Json;

namespace NoWayYahweh.Core;

public sealed record CheckResult(string Name, bool Ok, string Detail);

/// <summary>
/// What the app can verify about its own components: the host executable, each browser's
/// registration, and that the host answers the way a browser would talk to it.
/// (The extension itself lives inside the browser and is checked by its own "hello".)
/// </summary>
public static class IntegrityCheck
{
    public static IReadOnlyList<CheckResult> Run(string hostExePath)
    {
        var results = new List<CheckResult>
        {
            new("Serviço local", File.Exists(hostExePath), File.Exists(hostExePath) ? hostExePath : "executável não encontrado"),
        };
        foreach (var browser in HostRegistration.BrowserKeys.Keys)
        {
            var ok = HostRegistration.IsRegistered(browser, hostExePath);
            results.Add(new($"Registro no {browser}", ok, ok ? "ok" : "ausente ou apontando para outro lugar — use \"Reparar integração\""));
        }
        results.Add(Ping(hostExePath));
        return results;
    }

    private static CheckResult Ping(string hostExePath)
    {
        const string name = "Resposta do serviço";
        if (!File.Exists(hostExePath)) return new(name, false, "sem executável");
        try
        {
            using var p = Process.Start(new ProcessStartInfo(hostExePath, [HostRegistration.AllowedOrigins[0]])
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            })!;
            NativeMessaging.Write(p.StandardInput.BaseStream, new { type = "ping" });
            var read = Task.Run(() => NativeMessaging.Read(p.StandardOutput.BaseStream));
            if (!read.Wait(TimeSpan.FromSeconds(5)) || read.Result is null)
            {
                p.Kill();
                return new(name, false, "sem resposta em 5 s");
            }
            using var reply = JsonDocument.Parse(read.Result);
            p.StandardInput.Close();
            p.WaitForExit(2000);
            var ok = reply.RootElement.GetProperty("type").GetString() == "pong";
            return new(name, ok, ok ? "respondeu" : "resposta inesperada");
        }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or IOException or InvalidDataException or JsonException or AggregateException)
        {
            return new(name, false, e.Message);
        }
    }
}
