using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using ECommons.GameHelpers;
using WrathCombo.Core;

namespace WrathCombo.Window;

/// <summary>
///     Serves the Next Action tracker (job + Single-Target / AoE next action +
///     burst state) as a tiny web page on loopback (127.0.0.1). Open it in a
///     browser window that your game-capture/recorder does NOT include, and it
///     stays off your stream/recording — far more reliable than the in-game
///     window's WDA capture-exclusion.
/// </summary>
/// <remarks>
///     Thread-safety: the state snapshot is built on the framework (game) thread
///     in <see cref="UpdateSnapshot" />; the HTTP worker thread only ever reads
///     that snapshot (under a lock) and never touches game memory. The snapshot
///     is only refreshed while a browser is actually polling, so it costs nothing
///     when the page isn't open.
/// </remarks>
internal static class TrackerWebServer
{
    private const int FirstPort = 9876;
    private const int LastPort = 9885;

    private static TcpListener? _listener;
    private static Thread? _thread;
    private static volatile bool _running;
    private static long _lastPollTick;

    /// <summary> The port the page is being served on, or 0 if not started. </summary>
    public static int Port { get; private set; }

    private static readonly object _lock = new();
    private static string _job = "—", _st = "—", _aoe = "—", _burst = "—";
    private static bool _stHas, _aoeHas;

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
            Svc.Log.Warning(
                "[MyTweak] tracker web page: no free port in range, not started.");
            return;
        }

        _running = true;
        _thread = new Thread(AcceptLoop)
        {
            IsBackground = true,
            Name = "MyTweakTrackerWeb",
        };
        _thread.Start();

        Svc.Framework.Update += UpdateSnapshot;
        Svc.Log.Information(
            $"[MyTweak] Next-action tracker web page at http://127.0.0.1:{Port}");
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

    /// <summary>Refresh the snapshot on the game thread — only while watched.</summary>
    private static void UpdateSnapshot(IFramework _)
    {
        // No one is looking at the page right now — do nothing.
        if (Environment.TickCount64 - Volatile.Read(ref _lastPollTick) > 2000)
            return;

        try
        {
            if (!Player.Available)
            {
                lock (_lock)
                {
                    _job = "—";
                    _stHas = _aoeHas = false;
                    _st = _aoe = "—";
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
            var stName = stHas ? ActionResolution.ActionName(st) : "—";
            var aoeName = aoeHas ? ActionResolution.ActionName(aoe) : "—";

            lock (_lock)
            {
                _job = job;
                _stHas = stHas;
                _aoeHas = aoeHas;
                _st = stName;
                _aoe = aoeName;
                _burst = burst;
            }
        }
        catch
        {
            // Never let a snapshot hiccup spam the log or break the frame.
        }
    }

    private static void AcceptLoop()
    {
        while (_running)
        {
            TcpClient client;
            try
            {
                client = _listener!.AcceptTcpClient();
            }
            catch
            {
                break; // listener stopped
            }

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

        // Read just the request line ("GET /path HTTP/1.1").
        var sb = new StringBuilder(64);
        int b;
        while ((b = stream.ReadByte()) != -1)
        {
            if (b == '\n') break;
            if (b != '\r') sb.Append((char)b);
        }

        var path = "/";
        var parts = sb.ToString().Split(' ');
        if (parts.Length >= 2)
            path = parts[1];

        var json = path.StartsWith("/state", StringComparison.Ordinal);
        var body = Encoding.UTF8.GetBytes(json ? StateJson() : Html);
        var contentType = json ? "application/json" : "text/html";

        var header = Encoding.ASCII.GetBytes(
            "HTTP/1.1 200 OK\r\n" +
            $"Content-Type: {contentType}; charset=utf-8\r\n" +
            $"Content-Length: {body.Length}\r\n" +
            "Cache-Control: no-store\r\n" +
            "Connection: close\r\n\r\n");

        stream.Write(header, 0, header.Length);
        stream.Write(body, 0, body.Length);
        stream.Flush();
    }

    private static string StateJson()
    {
        lock (_lock)
            return "{" +
                   $"\"job\":\"{Esc(_job)}\"," +
                   $"\"stHas\":{(_stHas ? "true" : "false")}," +
                   $"\"st\":\"{Esc(_st)}\"," +
                   $"\"aoeHas\":{(_aoeHas ? "true" : "false")}," +
                   $"\"aoe\":\"{Esc(_aoe)}\"," +
                   $"\"burst\":\"{Esc(_burst)}\"" +
                   "}";
    }

    private static string Esc(string s) =>
        s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private const string Html =
        """
        <!doctype html>
        <html lang="en">
        <head>
        <meta charset="utf-8">
        <meta name="viewport" content="width=device-width,initial-scale=1">
        <title>MyTweak — Next Action</title>
        <style>
          html,body{margin:0;height:100%;background:#0f1117;color:#e6e6e6;
            font-family:Segoe UI,Roboto,sans-serif;}
          #wrap{padding:18px 22px;}
          #job{color:#7aa2f7;font-size:18px;font-weight:600;letter-spacing:1px;
            margin-bottom:10px;}
          .row{font-size:30px;line-height:1.35;}
          .lbl{display:inline-block;width:70px;color:#6b7280;font-size:18px;
            vertical-align:middle;}
          #burst{margin-top:12px;font-size:22px;font-weight:600;}
          .armed{color:#7dcfff;} .held{color:#e0af68;} .none{color:#6b7280;}
          #off{color:#6b7280;font-size:14px;margin-top:16px;}
        </style>
        </head>
        <body>
        <div id="wrap">
          <div id="job">—</div>
          <div class="row"><span class="lbl">ST</span><span id="st">—</span></div>
          <div class="row"><span class="lbl">AoE</span><span id="aoe">—</span></div>
          <div id="burst" class="none">Burst: —</div>
          <div id="off"></div>
        </div>
        <script>
        async function tick(){
          try{
            const r = await fetch('/state',{cache:'no-store'});
            const d = await r.json();
            document.getElementById('job').textContent = d.job;
            document.getElementById('st').textContent  = d.stHas ? d.st : '—';
            document.getElementById('aoe').textContent = d.aoeHas ? d.aoe : '—';
            const b = document.getElementById('burst');
            b.textContent = 'Burst: ' + d.burst;
            b.className = d.burst==='ARMED' ? 'armed'
                        : d.burst==='HELD'  ? 'held' : 'none';
            document.getElementById('off').textContent = '';
          }catch(e){
            document.getElementById('off').textContent = '(game not running)';
          }
        }
        setInterval(tick, 250);
        tick();
        </script>
        </body>
        </html>
        """;
}
