using System;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;

// CometenIRLAlerts - main Streamer.bot IRL controller
//
// BELABOX ingest watchdog v3
// --------------------------
// Uses the same principle as NOALBS: monitor the actual BELABOX ingest stats
// instead of relying on OBS Media Source state or only checking if the ROCK 5B+
// itself is alive.
//
// BELABOX Cloud stats format:
//   {
//     "publishers": {
//       "<stream-id>": {
//         "connected": true/false,
//         "latency": 0,
//         "network": 0,
//         "bitrate": 1234,
//         "rtt": 10,
//         "dropped_pkts": 0
//       }
//     }
//   }
//
// Main failover rules:
//   - connected == false -> bad signal
//   - bitrate <= 0       -> bad signal
//   - publisher missing  -> bad signal
//   - stats request fails repeatedly -> bad signal
//   - after sustained failure switch to IRL - SIGNAL MISTET
//   - after sustained recovery restore the scene active before failover
//
// Required Streamer.bot persisted global:
//   CometenIRL_BelaboxStreamId
//
// Optional globals:
//   CometenIRL_BelaboxStatsBaseUrl     string, default http://use.srt.belabox.net:8080
//   CometenIRL_FallbackScene           string, default IRL - SIGNAL MISTET
//   CometenIRL_BelaboxFailChecks       int, default 2
//   CometenIRL_BelaboxQueryFailChecks  int, default 3
//   CometenIRL_BelaboxRecoverChecks    int, default 5
//
// Runtime/status globals written by this controller:
//   CometenIRL_BelaboxConnected
//   CometenIRL_BelaboxBitrate
//   CometenIRL_BelaboxRtt
//   CometenIRL_BelaboxDroppedPackets
//   CometenIRL_BelaboxState
//   CometenIRL_BelaboxFailCount
//   CometenIRL_BelaboxRecoverCount
//   CometenIRL_BelaboxQueryFailCount
//   CometenIRL_SrtFallbackActive
//   CometenIRL_SrtReturnScene
//
// Run from the existing 1-second Streamer.bot timed action.

public class CPHInline
{
    private static readonly HttpClient Http = CreateHttpClient();

    private const int ObsConnection = 0;

    private const string DefaultStatsBaseUrl = "http://use.srt.belabox.net:8080";
    private const string DefaultFallbackScene = "IRL - SIGNAL MISTET";
    private const int DefaultFailChecks = 2;
    private const int DefaultQueryFailChecks = 3;
    private const int DefaultRecoverChecks = 5;

    private const string VarStreamId = "CometenIRL_BelaboxStreamId";
    private const string VarStatsBaseUrl = "CometenIRL_BelaboxStatsBaseUrl";
    private const string VarFallbackScene = "CometenIRL_FallbackScene";
    private const string VarFailChecks = "CometenIRL_BelaboxFailChecks";
    private const string VarQueryFailChecks = "CometenIRL_BelaboxQueryFailChecks";
    private const string VarRecoverChecks = "CometenIRL_BelaboxRecoverChecks";

    private const string VarConnected = "CometenIRL_BelaboxConnected";
    private const string VarBitrate = "CometenIRL_BelaboxBitrate";
    private const string VarRtt = "CometenIRL_BelaboxRtt";
    private const string VarDroppedPackets = "CometenIRL_BelaboxDroppedPackets";
    private const string VarState = "CometenIRL_BelaboxState";

    private const string VarFailCount = "CometenIRL_BelaboxFailCount";
    private const string VarRecoverCount = "CometenIRL_BelaboxRecoverCount";
    private const string VarQueryFailCount = "CometenIRL_BelaboxQueryFailCount";
    private const string VarFallbackActive = "CometenIRL_SrtFallbackActive";
    private const string VarReturnScene = "CometenIRL_SrtReturnScene";

    public bool Execute()
    {
        bool obsConnected = CPH.ObsIsConnected(ObsConnection);
        bool obsStreaming = CPH.ObsIsStreaming(ObsConnection);

        if (!obsConnected)
        {
            LogStateChange("obs-disconnected", "CometenIRL Watchdog: OBS is not connected.", true);
            return true;
        }

        // Never switch IRL scenes while OBS is not actually streaming.
        if (!obsStreaming)
        {
            ResetRuntimeState();
            SetStatus(false, 0, 0.0, 0, "standby");
            return true;
        }

        string streamId = (CPH.GetGlobalVar<string>(VarStreamId, true) ?? string.Empty).Trim();
        string statsBaseUrl = (CPH.GetGlobalVar<string>(VarStatsBaseUrl, true) ?? string.Empty).Trim();
        string fallbackScene = (CPH.GetGlobalVar<string>(VarFallbackScene, true) ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(statsBaseUrl))
        {
            statsBaseUrl = DefaultStatsBaseUrl;
        }

        if (string.IsNullOrWhiteSpace(fallbackScene))
        {
            fallbackScene = DefaultFallbackScene;
        }

        if (string.IsNullOrWhiteSpace(streamId))
        {
            SetStatus(false, 0, 0.0, 0, "configuration-error");
            LogStateChange(
                "configuration-error",
                "CometenIRL Watchdog: Missing persisted global " + VarStreamId + ".",
                true
            );
            return true;
        }

        int failChecks = GetPositiveInt(VarFailChecks, DefaultFailChecks);
        int queryFailChecks = GetPositiveInt(VarQueryFailChecks, DefaultQueryFailChecks);
        int recoverChecks = GetPositiveInt(VarRecoverChecks, DefaultRecoverChecks);

        BelaboxStats stats;
        string error;

        if (!TryGetBelaboxStats(statsBaseUrl, streamId, out stats, out error))
        {
            HandleStatsQueryFailure(error, queryFailChecks, fallbackScene);
            return true;
        }

        CPH.SetGlobalVar(VarQueryFailCount, 0, true);

        bool signalOk = stats.PublisherFound && stats.Connected && stats.Bitrate > 0;
        string state = signalOk ? "online" : "offline";

        SetStatus(
            stats.Connected,
            stats.Bitrate,
            stats.Rtt,
            stats.DroppedPackets,
            state
        );

        if (signalOk)
        {
            HandleHealthySignal(stats, recoverChecks, fallbackScene);
        }
        else
        {
            HandleBadSignal(stats, failChecks, fallbackScene);
        }

        return true;
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(2.5);
        return client;
    }

    private bool TryGetBelaboxStats(
        string statsBaseUrl,
        string streamId,
        out BelaboxStats stats,
        out string error)
    {
        stats = new BelaboxStats();
        error = string.Empty;

        string endpoint = statsBaseUrl.TrimEnd('/') + "/" + Uri.EscapeDataString(streamId);

        string json;

        try
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint))
            {
                request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");
                request.Headers.TryAddWithoutValidation("User-Agent", "CometenIRLAlerts/0.6");

                using (HttpResponseMessage response = Http.SendAsync(request).GetAwaiter().GetResult())
                {
                    json = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    if (!response.IsSuccessStatusCode)
                    {
                        error = "HTTP " + (int)response.StatusCode + ": " + json;
                        return false;
                    }
                }
            }
        }
        catch (Exception exception)
        {
            error = exception.Message;
            return false;
        }

        string body;
        if (!TryExtractPublisherObject(json, streamId, out body))
        {
            // The stats endpoint itself answered correctly but this publisher is
            // absent. That is a valid OFFLINE state, not a query failure.
            stats.PublisherFound = false;
            stats.Connected = false;
            stats.Bitrate = 0;
            stats.Rtt = 0.0;
            stats.DroppedPackets = 0;
            return true;
        }

        stats.PublisherFound = true;

        bool connected;
        int bitrate;
        double rtt;
        int dropped;

        if (!TryExtractBool(body, "connected", out connected))
        {
            error = "publisher JSON has no connected field";
            return false;
        }

        if (!TryExtractInt(body, "bitrate", out bitrate))
        {
            error = "publisher JSON has no bitrate field";
            return false;
        }

        if (!TryExtractDouble(body, "rtt", out rtt))
        {
            rtt = 0.0;
        }

        if (!TryExtractInt(body, "dropped_pkts", out dropped))
        {
            dropped = 0;
        }

        stats.Connected = connected;
        stats.Bitrate = bitrate;
        stats.Rtt = rtt;
        stats.DroppedPackets = dropped;

        return true;
    }

    private void HandleHealthySignal(
        BelaboxStats stats,
        int recoverChecks,
        string fallbackScene)
    {
        CPH.SetGlobalVar(VarFailCount, 0, true);

        bool fallbackActive = CPH.GetGlobalVar<bool>(VarFallbackActive, true);

        if (!fallbackActive)
        {
            CPH.SetGlobalVar(VarRecoverCount, 0, true);
            LogOnlineStatsOnce(stats);
            return;
        }

        int recoverCount = CPH.GetGlobalVar<int>(VarRecoverCount, true) + 1;
        CPH.SetGlobalVar(VarRecoverCount, recoverCount, true);

        CPH.LogInfo(
            "CometenIRL Watchdog: BELABOX recovery "
            + recoverCount + "/" + recoverChecks
            + " - bitrate=" + stats.Bitrate + " kbps"
            + " rtt=" + stats.Rtt.ToString("0.0", CultureInfo.InvariantCulture) + " ms."
        );

        if (recoverCount >= recoverChecks)
        {
            RestoreAfterRecovery(fallbackScene);
        }
    }

    private void HandleBadSignal(
        BelaboxStats stats,
        int failChecks,
        string fallbackScene)
    {
        CPH.SetGlobalVar(VarRecoverCount, 0, true);

        int failCount = CPH.GetGlobalVar<int>(VarFailCount, true) + 1;
        CPH.SetGlobalVar(VarFailCount, failCount, true);

        bool fallbackActive = CPH.GetGlobalVar<bool>(VarFallbackActive, true);

        if (failCount == 1 || failCount == failChecks)
        {
            CPH.LogWarn(
                "CometenIRL Watchdog: BELABOX ingest unhealthy "
                + failCount + "/" + failChecks
                + " - publisher=" + (stats.PublisherFound ? "yes" : "no")
                + " connected=" + stats.Connected
                + " bitrate=" + stats.Bitrate + " kbps"
                + " rtt=" + stats.Rtt.ToString("0.0", CultureInfo.InvariantCulture) + " ms"
                + " dropped=" + stats.DroppedPackets + "."
            );
        }

        if (fallbackActive || failCount < failChecks)
        {
            return;
        }

        ActivateFallback(
            fallbackScene,
            "ingest offline - connected=" + stats.Connected
            + ", bitrate=" + stats.Bitrate + " kbps"
        );
    }

    private void HandleStatsQueryFailure(
        string error,
        int queryFailChecks,
        string fallbackScene)
    {
        SetStatus(false, 0, 0.0, 0, "stats-unavailable");
        CPH.SetGlobalVar(VarRecoverCount, 0, true);

        int queryFailCount = CPH.GetGlobalVar<int>(VarQueryFailCount, true) + 1;
        CPH.SetGlobalVar(VarQueryFailCount, queryFailCount, true);

        if (queryFailCount == 1 || queryFailCount == queryFailChecks)
        {
            CPH.LogWarn(
                "CometenIRL Watchdog: BELABOX stats unavailable "
                + queryFailCount + "/" + queryFailChecks
                + " - " + error
            );
        }

        bool fallbackActive = CPH.GetGlobalVar<bool>(VarFallbackActive, true);

        if (fallbackActive || queryFailCount < queryFailChecks)
        {
            return;
        }

        ActivateFallback(fallbackScene, "BELABOX stats unavailable");
    }

    private void ActivateFallback(string fallbackScene, string reason)
    {
        string currentScene = CPH.ObsGetCurrentScene(ObsConnection) ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(currentScene)
            && !string.Equals(currentScene, fallbackScene, StringComparison.Ordinal))
        {
            CPH.SetGlobalVar(VarReturnScene, currentScene, true);
        }

        CPH.SetGlobalVar(VarFallbackActive, true, true);
        CPH.SetGlobalVar(VarRecoverCount, 0, true);

        CPH.LogWarn(
            "CometenIRL Watchdog: SIGNAL LOST - " + reason
            + ". Scene '" + currentScene + "' -> '" + fallbackScene + "'."
        );

        if (!string.Equals(currentScene, fallbackScene, StringComparison.Ordinal))
        {
            CPH.ObsSetScene(fallbackScene, ObsConnection);
        }
    }

    private void RestoreAfterRecovery(string fallbackScene)
    {
        string currentScene = CPH.ObsGetCurrentScene(ObsConnection) ?? string.Empty;
        string returnScene = (CPH.GetGlobalVar<string>(VarReturnScene, true) ?? string.Empty).Trim();

        if (string.Equals(currentScene, fallbackScene, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(returnScene))
        {
            CPH.LogInfo(
                "CometenIRL Watchdog: BELABOX ingest stable again. Scene '"
                + fallbackScene + "' -> '" + returnScene + "'."
            );

            CPH.ObsSetScene(returnScene, ObsConnection);
        }
        else if (!string.Equals(currentScene, fallbackScene, StringComparison.Ordinal))
        {
            CPH.LogInfo(
                "CometenIRL Watchdog: BELABOX ingest stable again, but scene was changed manually to '"
                + currentScene + "'. Auto-return skipped."
            );
        }
        else
        {
            CPH.LogWarn(
                "CometenIRL Watchdog: BELABOX ingest stable again, but no return scene was stored."
            );
        }

        CPH.SetGlobalVar(VarFailCount, 0, true);
        CPH.SetGlobalVar(VarRecoverCount, 0, true);
        CPH.SetGlobalVar(VarQueryFailCount, 0, true);
        CPH.SetGlobalVar(VarFallbackActive, false, true);
        CPH.SetGlobalVar(VarReturnScene, string.Empty, true);
    }

    private void ResetRuntimeState()
    {
        CPH.SetGlobalVar(VarFailCount, 0, true);
        CPH.SetGlobalVar(VarRecoverCount, 0, true);
        CPH.SetGlobalVar(VarQueryFailCount, 0, true);
        CPH.SetGlobalVar(VarFallbackActive, false, true);
        CPH.SetGlobalVar(VarReturnScene, string.Empty, true);
    }

    private void SetStatus(bool connected, int bitrate, double rtt, int dropped, string state)
    {
        CPH.SetGlobalVar(VarConnected, connected, true);
        CPH.SetGlobalVar(VarBitrate, bitrate, true);
        CPH.SetGlobalVar(VarRtt, rtt, true);
        CPH.SetGlobalVar(VarDroppedPackets, dropped, true);
        CPH.SetGlobalVar(VarState, state, true);
    }

    private void LogOnlineStatsOnce(BelaboxStats stats)
    {
        string previousState = (CPH.GetGlobalVar<string>("CometenIRL_BelaboxLoggedState", true) ?? string.Empty).Trim();

        if (string.Equals(previousState, "online", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CPH.SetGlobalVar("CometenIRL_BelaboxLoggedState", "online", true);

        CPH.LogInfo(
            "CometenIRL Watchdog: BELABOX ingest ONLINE"
            + " - bitrate=" + stats.Bitrate + " kbps"
            + " rtt=" + stats.Rtt.ToString("0.0", CultureInfo.InvariantCulture) + " ms"
            + " dropped=" + stats.DroppedPackets + "."
        );
    }

    private void LogStateChange(string state, string message, bool warning)
    {
        string previous = (CPH.GetGlobalVar<string>("CometenIRL_BelaboxLoggedState", true) ?? string.Empty).Trim();

        if (string.Equals(previous, state, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CPH.SetGlobalVar("CometenIRL_BelaboxLoggedState", state, true);

        if (warning)
        {
            CPH.LogWarn(message);
        }
        else
        {
            CPH.LogInfo(message);
        }
    }

    private int GetPositiveInt(string variableName, int fallback)
    {
        int value = CPH.GetGlobalVar<int>(variableName, true);
        return value > 0 ? value : fallback;
    }

    private static bool TryExtractPublisherObject(string json, string streamId, out string body)
    {
        body = string.Empty;

        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(streamId))
        {
            return false;
        }

        Match match = Regex.Match(
            json,
            "\\\"publishers\\\"\\s*:\\s*\\{[\\s\\S]*?\\\""
            + Regex.Escape(streamId)
            + "\\\"\\s*:\\s*\\{(?<body>[^{}]*)\\}",
            RegexOptions.IgnoreCase
        );

        if (!match.Success)
        {
            return false;
        }

        body = match.Groups["body"].Value;
        return true;
    }

    private static bool TryExtractBool(string json, string key, out bool value)
    {
        value = false;

        Match match = Regex.Match(
            json ?? string.Empty,
            "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(true|false)",
            RegexOptions.IgnoreCase
        );

        if (!match.Success)
        {
            return false;
        }

        value = string.Equals(match.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static bool TryExtractInt(string json, string key, out int value)
    {
        value = 0;

        Match match = Regex.Match(
            json ?? string.Empty,
            "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(-?[0-9]+)",
            RegexOptions.IgnoreCase
        );

        return match.Success
            && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryExtractDouble(string json, string key, out double value)
    {
        value = 0.0;

        Match match = Regex.Match(
            json ?? string.Empty,
            "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(-?[0-9]+(?:\\.[0-9]+)?)",
            RegexOptions.IgnoreCase
        );

        return match.Success
            && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private sealed class BelaboxStats
    {
        public bool PublisherFound { get; set; }
        public bool Connected { get; set; }
        public int Bitrate { get; set; }
        public double Rtt { get; set; }
        public int DroppedPackets { get; set; }
    }
}
