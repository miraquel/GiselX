using System.Net;
using System.Net.Sockets;
using System.Text;

namespace GiselX.Tests;

/// <summary>
/// A minimal SMTP server that accepts one conversation and keeps what was sent, so the mailer can be
/// exercised for real — connect, hand-shake, envelope, DATA — without an account anywhere. Until this
/// existed, nothing verified that EmailService could actually deliver; the tests all stopped at the
/// interface.
/// </summary>
public sealed class SmtpSink : IDisposable
{
    private readonly TcpListener _listener;
    private readonly Task _loop;
    private readonly CancellationTokenSource _stopping = new();

    /// <summary>Whether the server offers AUTH, the way an open internal relay would not.</summary>
    private readonly bool _advertiseAuth;

    public SmtpSink(bool advertiseAuth = true)
    {
        _advertiseAuth = advertiseAuth;
        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _loop = Task.Run(AcceptAsync);
    }

    public int Port { get; }

    /// <summary>Raw message bodies received, in order.</summary>
    public List<string> Messages { get; } = [];

    /// <summary>Envelope recipients seen across all messages.</summary>
    public List<string> Recipients { get; } = [];

    public bool Authenticated { get; private set; }

    private async Task AcceptAsync()
    {
        try
        {
            while (!_stopping.IsCancellationRequested)
            {
                using var client = await _listener.AcceptTcpClientAsync(_stopping.Token);
                await ConverseAsync(client);
            }
        }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
        catch (SocketException) { }
    }

    private async Task ConverseAsync(TcpClient client)
    {
        await using var stream = client.GetStream();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\r\n" };

        await writer.WriteLineAsync("220 localhost GiselX test sink");

        while (await reader.ReadLineAsync() is { } line)
        {
            if (line.StartsWith("EHLO", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("HELO", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("250-localhost");
                await writer.WriteLineAsync("250-SIZE 10485760");
                await writer.WriteLineAsync(_advertiseAuth ? "250-AUTH PLAIN LOGIN" : "250-8BITMIME");
                await writer.WriteLineAsync("250 HELP");
            }
            else if (line.StartsWith("AUTH", StringComparison.OrdinalIgnoreCase))
            {
                Authenticated = true;
                await writer.WriteLineAsync("235 2.7.0 Authentication successful");
            }
            else if (line.StartsWith("MAIL FROM", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("250 2.1.0 Ok");
            }
            else if (line.StartsWith("RCPT TO", StringComparison.OrdinalIgnoreCase))
            {
                Recipients.Add(ExtractAddress(line));
                await writer.WriteLineAsync("250 2.1.5 Ok");
            }
            else if (line.StartsWith("DATA", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("354 End data with <CR><LF>.<CR><LF>");
                var body = new StringBuilder();
                while (await reader.ReadLineAsync() is { } dataLine && dataLine != ".")
                {
                    body.AppendLine(dataLine);
                }
                Messages.Add(body.ToString());
                await writer.WriteLineAsync("250 2.0.0 Ok: queued");
            }
            else if (line.StartsWith("QUIT", StringComparison.OrdinalIgnoreCase))
            {
                await writer.WriteLineAsync("221 2.0.0 Bye");
                return;
            }
            else
            {
                await writer.WriteLineAsync("250 2.0.0 Ok");
            }
        }
    }

    private static string ExtractAddress(string line)
    {
        var open = line.IndexOf('<');
        var close = line.IndexOf('>');
        return open >= 0 && close > open ? line[(open + 1)..close] : line;
    }

    public void Dispose()
    {
        _stopping.Cancel();
        _listener.Stop();
        try { _loop.Wait(TimeSpan.FromSeconds(2)); } catch { /* shutting down */ }
        _stopping.Dispose();
    }
}

/// <summary>Hands the mailer a fixed configuration, standing in for the stored settings.</summary>
public sealed class FixedEmailSettingsProvider(GiselX.Service.EmailSettings settings)
    : GiselX.Service.IEmailSettingsProvider
{
    public Task<GiselX.Service.EmailSettings> GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(settings);
}
