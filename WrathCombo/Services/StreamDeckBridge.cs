using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using WrathCombo.Core;

namespace WrathCombo.Services;

/// <summary>
///     A tiny loopback (127.0.0.1) HTTP API that a companion Stream Deck plugin
///     polls to show the current job's next Single-Target / AoE action and burst
///     state, and to toggle burst. Everything the deck shows lives on the deck,
///     so it never appears in a game capture.
/// </summary>
/// <remarks>
///     Endpoints:
///       GET /state        → JSON snapshot (job, st, aoe, burst).
///       GET /burst/toggle → toggles burst, returns the new state as JSON.
///     Thread-safety: the snapshot is built on the framework (game) thread in
///     <see cref="UpdateSnapshot" />; the HTTP worker thread only reads that
///     snapshot under a lock, and marshals the burst toggle back onto the
///     framework thread. It only refreshes while the deck is actively polling.
/// </remarks>
internal static class StreamDeckBridge
{
    private const int FirstPort = 9876;
    private const int LastPort = 9885;

    private static TcpListener? _listener;
    private static Thread? _thread;
    private static volatile bool _running;
    private static long _lastPollTick;

    public static int Port { get; private set; }

    private static readonly object _lock = new();
    private static string _job = "—", _burst = "—";
    private static bool _stHas, _aoeHas;
    private static uint _stId, _aoeId;
    private static string _stName = "—", _aoeName = "—";
    private static ushort _stIcon, _aoeIcon;

    public static void Start()
    {
        if (_running)
            return;

        for (var p = FirstPort; p <= LastPort; p++)
        {
            try
            {
                var l = new TcpListener(IPAddress.Loopback, p);
                l.Start();
                _listener = l;
                Port = p;
                break;
            }
            catch (SocketException)
            {
                // Port busy — try the next one.
            }
        }

        if (_listener is null)
        {
            Svc.Log.Warning("[MyTweak] Stream Deck bridge: no free port, not started.");
            return;
        }

        _running = true;
        _thread = new Thread(AcceptLoop)
        {
            IsBackground = true,
            Name = "MyTweakStreamDeck",
        };
        _thread.Start();

        Svc.Framework.Update += UpdateSnapshot;
        Svc.Log.Information($"[MyTweak] Stream Deck bridge on http://127.0.0.1:{Port}");
    }

    public static void Stop()
    {
        if (!_running && _listener is null)
            return;

        _running = false;
        Svc.Framework.Update -= UpdateSnapshot;

        try { _listener?.Stop(); } catch { /* already down */ }
        _listener = null;
        Port = 0;

        try { _thread?.Join(1000); } catch { /* ignore */ }
        _thread = null;
    }

    private static void UpdateSnapshot(IFramework _)
    {
        // Nothing is polling right now — skip the work.
        if (Environment.TickCount64 - Volatile.Read(ref _lastPollTick) > 3000)
            return;

        try
        {
            if (!Player.Available)
            {
                lock (_lock)
                {
                    _job = "—";
                    _stHas = _aoeHas = false;
                    _stName = _aoeName = "—";
                    _stId = _aoeId = 0;
                    _stIcon = _aoeIcon = 0;
                    _burst = "—";
                }
                return;
            }

            ActionResolution.Refresh(); // throttled internally (~100ms)

            var job = Player.Job.ToString();
            var stHas = ActionResolution.TryGetSingleTarget(out var st);
            var aoeHas = ActionResolution.TryGetAoE(out var aoe);
            var burst = ActionResolution.IsBurstHeld() switch
            {
                true => "HELD",
                false => "ARMED",
                _ => "—",
            };

            lock (_lock)
            {
                _job = job;
                _stHas = stHas;
                _stId = st;
                _stName = stHas ? ActionResolution.ActionName(st) : "—";
                _stIcon = stHas ? ActionResolution.ActionIcon(st) : (ushort)0;
                _aoeHas = aoeHas;
                _aoeId = aoe;
                _aoeName = aoeHas ? ActionResolution.ActionName(aoe) : "—";
                _aoeIcon = aoeHas ? ActionResolution.ActionIcon(aoe) : (ushort)0;
                _burst = burst;
            }
        }
        catch
        {
            // Never let a snapshot hiccup break the frame.
        }
    }

    private static void AcceptLoop()
    {
        while (_running)
        {
            TcpClient client;
            try { client = _listener!.AcceptTcpClient(); }
            catch { break; } // listener stopped

            try { Handle(client); }
            catch { /* ignore per-connection errors */ }
            finally { try { client.Close(); } catch { /* ignore */ } }
        }
    }

    private static void Handle(TcpClient client)
    {
        using var stream = client.GetStream();
        stream.ReadTimeout = 2000;
        stream.WriteTimeout = 2000;

        Volatile.Write(ref _lastPollTick, Environment.TickCount64);

        // Read the full request head ("GET /path HTTP/1.1" + headers) up to the
        // blank line. Draining it matters: closing the socket with unread data
        // still buffered sends a TCP RST, and Chromium (the Stream Deck runtime)
        // treats that as a failed request even after the response was sent.
        var sb = new StringBuilder(512);
        var read = 0;
        int b;
        while ((b = stream.ReadByte()) != -1)
        {
            sb.Append((char)b);
            if (++read >= 8192)
                break;
            if (read >= 4 &&
                sb[read - 4] == '\r' && sb[read - 3] == '\n' &&
                sb[read - 2] == '\r' && sb[read - 1] == '\n')
                break;
        }

        var head = sb.ToString();
        var lineEnd = head.IndexOf('\r');
        var requestLine = lineEnd > 0 ? head[..lineEnd] : head;
        var parts = requestLine.Split(' ');
        var method = parts.Length >= 1 ? parts[0] : "GET";
        var path = parts.Length >= 2 ? parts[1] : "/";

        // Shared CORS headers: the Stream Deck plugin fetches from a different
        // origin, and newer Chromium also gates local-network requests behind a
        // preflight (Private Network Access).
        const string cors =
            "Access-Control-Allow-Origin: *\r\n" +
            "Access-Control-Allow-Methods: GET, OPTIONS\r\n" +
            "Access-Control-Allow-Headers: *\r\n" +
            "Access-Control-Allow-Private-Network: true\r\n";

        if (method == "OPTIONS")
        {
            var preflight = Encoding.ASCII.GetBytes(
                "HTTP/1.1 204 No Content\r\n" + cors +
                "Content-Length: 0\r\n" +
                "Connection: close\r\n\r\n");
            stream.Write(preflight, 0, preflight.Length);
            stream.Flush();
            return;
        }

        string body;
        if (path.StartsWith("/burst/toggle", StringComparison.Ordinal))
            body = ToggleBurstJson();
        else
            body = StateJson();

        var bytes = Encoding.UTF8.GetBytes(body);
        var header = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n" +
            "Content-Type: application/json; charset=utf-8\r\n" +
            $"Content-Length: {bytes.Length}\r\n" +
            "Cache-Control: no-store\r\n" + cors +
            "Connection: close\r\n\r\n");

        stream.Write(header, 0, header.Length);
        stream.Write(bytes, 0, bytes.Length);
        stream.Flush();
    }

    private static string ToggleBurstJson()
    {
        var state = "—";
        try
        {
            var t = Svc.Framework.RunOnFrameworkThread(() =>
            {
                ActionResolution.ToggleBurst(out var s);
                return s;
            });
            if (t.Wait(2000))
                state = t.Result;
        }
        catch
        {
            // Fall through with the placeholder state.
        }

        return $"{{\"burst\":\"{Esc(state)}\"}}";
    }

    private static string StateJson()
    {
        lock (_lock)
            return "{" +
                   $"\"job\":\"{Esc(_job)}\"," +
                   $"\"st\":{{\"has\":{Bool(_stHas)},\"id\":{_stId}," +
                   $"\"name\":\"{Esc(_stName)}\",\"icon\":{_stIcon}}}," +
                   $"\"aoe\":{{\"has\":{Bool(_aoeHas)},\"id\":{_aoeId}," +
                   $"\"name\":\"{Esc(_aoeName)}\",\"icon\":{_aoeIcon}}}," +
                   $"\"burst\":\"{Esc(_burst)}\"" +
                   "}";
    }

    private static string Bool(bool v) => v ? "true" : "false";

    private static string Esc(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
