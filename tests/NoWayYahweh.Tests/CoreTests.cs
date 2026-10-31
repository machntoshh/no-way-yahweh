using System.Text;
using System.Text.Json;
using NoWayYahweh.Core;

namespace NoWayYahweh.Tests;

public class DomainsTests
{
    [Theory]
    [InlineData("pornhub.com", "pornhub.com")]
    [InlineData("  WWW.Example.COM ", "example.com")]
    [InlineData("sub.example.co.uk", "sub.example.co.uk")]
    [InlineData("xn--bcher-kva.example", "xn--bcher-kva.example")]
    public void Normalize_accepts_bare_domains(string input, string expected) =>
        Assert.Equal(expected, Domains.Normalize(input));

    [Theory]
    [InlineData("https://example.com")]
    [InlineData("example.com/path")]
    [InlineData("localhost")]
    [InlineData("-bad.com")]
    [InlineData("exa mple.com")]
    [InlineData("")]
    public void Normalize_rejects_anything_else(string input) => Assert.Null(Domains.Normalize(input));

    [Fact]
    public void ReadList_skips_comments_invalid_and_duplicates()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("l.txt"), "# comentário\nb.com\n\nwww.a.com\nnot a domain\nB.com\n");
        Assert.Equal(["a.com", "b.com"], Domains.ReadList(dir.File("l.txt")));
    }

    [Fact]
    public void ReadList_missing_file_is_empty() => Assert.Empty(Domains.ReadList(@"Z:\nao\existe.txt"));
}

public class NativeMessagingTests
{
    private static MemoryStream Frame(byte[] body, uint? length = null)
    {
        var s = new MemoryStream();
        s.Write(BitConverter.GetBytes(length ?? (uint)body.Length));
        s.Write(body);
        s.Position = 0;
        return s;
    }

    [Fact]
    public void Write_then_Read_round_trips()
    {
        var s = new MemoryStream();
        NativeMessaging.Write(s, new { type = "ack" });
        s.Position = 0;
        Assert.Equal("""{"type":"ack"}""", Encoding.UTF8.GetString(NativeMessaging.Read(s)!));
        Assert.Null(NativeMessaging.Read(s));
    }

    [Fact]
    public void Read_rejects_oversized_message() =>
        Assert.Throws<InvalidDataException>(() => NativeMessaging.Read(Frame([], NativeMessaging.MaxIncomingBytes + 1)));

    [Fact]
    public void Read_rejects_truncated_body() =>
        Assert.Throws<InvalidDataException>(() => NativeMessaging.Read(Frame("{}"u8.ToArray(), 10)));

    [Fact]
    public void Read_rejects_truncated_header() =>
        Assert.Throws<InvalidDataException>(() => NativeMessaging.Read(new MemoryStream([1, 0])));
}

public class MessageHandlerTests : IDisposable
{
    private readonly TempDir _dir = new();
    private readonly StateStore _store;
    private readonly MessageHandler _handler;

    public MessageHandlerTests()
    {
        _store = new StateStore(_dir.File("state.json"));
        _handler = new MessageHandler(_store, () => ["extra.com"]);
    }

    public void Dispose() => _dir.Dispose();

    private JsonElement Send(string json) =>
        JsonSerializer.SerializeToElement(_handler.Handle(Encoding.UTF8.GetBytes(json)));

    [Fact]
    public void Hello_records_browser_and_returns_extra_domains()
    {
        var reply = Send("""{"type":"hello","browser":"edge","version":"0.1.0"}""");
        Assert.Equal("state", reply.GetProperty("type").GetString());
        Assert.Equal("extra.com", reply.GetProperty("extraDomains")[0].GetString());
        Assert.True(_store.Read().BrowsersLastSeen.ContainsKey("edge"));
    }

    [Fact]
    public void Blocked_adds_count()
    {
        Send("""{"type":"blocked","count":2}""");
        var reply = Send("""{"type":"blocked","count":3}""");
        Assert.Equal("ack", reply.GetProperty("type").GetString());
        Assert.Equal(5, _store.Read().BlockCount);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("""{"type":"shutdown"}""")]
    [InlineData("""{"type":"hello","browser":"firefox","version":"1"}""")]
    [InlineData("""{"type":"hello","browser":"chrome","version":"1; rm"}""")]
    [InlineData("""{"type":"hello","browser":"chrome","version":"1","url":"https://x"}""")]
    [InlineData("""{"type":"blocked","count":0}""")]
    [InlineData("""{"type":"blocked","count":10001}""")]
    [InlineData("""{"type":"blocked","count":"5"}""")]
    [InlineData("""{"type":"blocked","count":1.5}""")]
    [InlineData("""{"type":"blocked","count":1,"domain":"x.com"}""")]
    public void Invalid_messages_are_rejected_without_side_effects(string json)
    {
        Assert.Equal("error", Send(json).GetProperty("type").GetString());
        var state = _store.Read();
        Assert.Equal(0, state.BlockCount);
        Assert.Empty(state.BrowsersLastSeen);
    }
}

public class StateStoreTests
{
    [Fact]
    public void Concurrent_updates_are_not_lost()
    {
        using var dir = new TempDir();
        var store = new StateStore(dir.File("state.json"));
        Parallel.For(0, 200, _ => store.Update(s => s.BlockCount++));
        Assert.Equal(200, store.Read().BlockCount);
    }

    [Fact]
    public void Corrupt_file_does_not_throw()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("state.json"), "{ quebrado");
        Assert.Equal(0, new StateStore(dir.File("state.json")).Read().BlockCount);
    }

    [Fact]
    public void ProtectedSince_survives_reload()
    {
        using var dir = new TempDir();
        var first = new StateStore(dir.File("state.json")).Update(_ => { }).ProtectedSince;
        Assert.Equal(first, new StateStore(dir.File("state.json")).Read().ProtectedSince);
    }
}

public class HostRegistrationTests
{
    [Fact]
    public void Manifest_names_host_path_and_only_allowed_origins()
    {
        using var doc = JsonDocument.Parse(HostRegistration.ManifestJson(@"C:\app\NoWayYahweh.Host.exe"));
        var root = doc.RootElement;
        Assert.Equal("com.nowayyahweh.host", root.GetProperty("name").GetString());
        Assert.Equal("stdio", root.GetProperty("type").GetString());
        // Chromium refuses manifests without a description ("Specified native messaging host not found").
        Assert.False(string.IsNullOrWhiteSpace(root.GetProperty("description").GetString()));
        Assert.Equal(@"C:\app\NoWayYahweh.Host.exe", root.GetProperty("path").GetString());
        Assert.Equal(HostRegistration.AllowedOrigins, root.GetProperty("allowed_origins").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public void Allowed_origin_matches_extension_manifest_key()
    {
        // The extension ID is derived from the public key: SHA-256, first 32 hex digits mapped 0-f → a-p.
        var manifest = Path.Combine(RepoRoot.Path, "extension", "manifest.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
        var key = Convert.FromBase64String(doc.RootElement.GetProperty("key").GetString()!);
        var hex = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(key))[..32];
        var id = string.Concat(hex.Select(c => (char)('a' + Convert.ToInt32(c.ToString(), 16))));
        Assert.Contains($"chrome-extension://{id}/", HostRegistration.AllowedOrigins);
    }
}

internal static class RepoRoot
{
    public static string Path { get; } = Find();

    private static string Find()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
            if (File.Exists(System.IO.Path.Combine(d.FullName, "NoWayYahweh.sln"))) return d.FullName;
        throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
