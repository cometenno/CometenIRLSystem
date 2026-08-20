using System;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;

// CometenIRLAlerts - BELABOX ingest watchdog v11
// Test mode: CometenIRL_WatchdogLiveOnly missing/false = runs while OBS is offline.
// Production: set CometenIRL_WatchdogLiveOnly = true.
// CometenIRL_WatchdogArmed controls scene authority independently of telemetry.
// Missing CometenIRL_WatchdogArmed defaults to true for backwards compatibility.
// v11 self-heals scene authority: BELABOX SRT auto-arms while OBS is streaming or IRL mode is active.
// OBS scene switching uses CPH.ObsSetScene(), verified against the configured OBS connection.

public class CPHInline
{
    private static readonly HttpClient Http = CreateHttpClient();

    private const int ObsConnection = 0;
    private const string DefaultStatsBaseUrl = "http://use.srt.belabox.net:8080";
    private const string DefaultFallbackScene = "IRL - SIGNAL MISTET";
    private const string DefaultReturnScene = "BELABOX SRT";
    private const int DefaultFailChecks = 2;
    private const int DefaultQueryFailChecks = 3;
    private const int DefaultRecoverChecks = 5;

    private const string VarStreamId = "CometenIRL_BelaboxStreamId";
    private const string VarStatsBaseUrl = "CometenIRL_BelaboxStatsBaseUrl";
    private const string VarFallbackScene = "CometenIRL_FallbackScene";
    private const string VarDefaultReturnScene = "CometenIRL_DefaultReturnScene";
    private const string VarFailChecks = "CometenIRL_BelaboxFailChecks";
    private const string VarQueryFailChecks = "CometenIRL_BelaboxQueryFailChecks";
    private const string VarRecoverChecks = "CometenIRL_BelaboxRecoverChecks";
    private const string VarLiveOnly = "CometenIRL_WatchdogLiveOnly";
    private const string VarArmed = "CometenIRL_WatchdogArmed";
    private const string VarIrlMode = "CometenIRL_IrlMode";

    private const string VarConnected = "CometenIRL_BelaboxConnected";
    private const string VarBitrate = "CometenIRL_BelaboxBitrate";
    private const string VarRtt = "CometenIRL_BelaboxRtt";
    private const string VarDropped = "CometenIRL_BelaboxDroppedPackets";
    private const string VarState = "CometenIRL_BelaboxState";

    private const string VarFailCount = "CometenIRL_BelaboxFailCount";
    private const string VarRecoverCount = "CometenIRL_BelaboxRecoverCount";
    private const string VarQueryFailCount = "CometenIRL_BelaboxQueryFailCount";
    private const string VarFallbackActive = "CometenIRL_SrtFallbackActive";
    private const string VarReturnScene = "CometenIRL_SrtReturnScene";

    public bool Execute()
    {
        if (!CPH.ObsIsConnected(ObsConnection))
        {
            CPH.LogWarn("CometenIRL Watchdog: OBS is not connected.");
            return true;
        }

        bool obsStreaming = CPH.ObsIsStreaming(ObsConnection);
        bool liveOnly = GetBool(VarLiveOnly, false);
        bool armed = GetBool(VarArmed, true);
        bool irlMode = GetBool(VarIrlMode, false);
        string currentScene = CPH.ObsGetCurrentScene(ObsConnection) ?? string.Empty;
        string returnScene = GetString(VarDefaultReturnScene, DefaultReturnScene);

        // Self-heal scene authority. A manual switch to BELABOX SRT must not leave
        // the failover watchdog disarmed during an active stream/IRL session.
        if (!armed
            && string.Equals(currentScene, returnScene, StringComparison.Ordinal)
            && (obsStreaming || irlMode))
        {
            armed = true;
            CPH.SetGlobalVar(VarArmed, true, true);
            CPH.LogWarn(
                "CometenIRL Watchdog: auto-armed on scene '" + currentScene
                + "' because " + (obsStreaming ? "OBS is streaming" : "IRL mode is active") + "."
            );
        }

        CPH.LogInfo(
            "CometenIRL DIAG: tick obsStreaming=" + obsStreaming
            + " liveOnly=" + liveOnly
            + " armed=" + armed
            + " irlMode=" + irlMode
            + " scene='" + currentScene + "'."
        );

        if (liveOnly && !obsStreaming)
        {
            ResetRuntime();
            SetStatus(false, 0, 0.0, 0, "standby");
            return true;
        }

        string streamId = GetString(VarStreamId, string.Empty);
        string statsBaseUrl = GetString(VarStatsBaseUrl, DefaultStatsBaseUrl);
        string fallbackScene = GetString(VarFallbackScene, DefaultFallbackScene);

        if (armed && string.Equals(currentScene, fallbackScene, StringComparison.Ordinal))
        {
            EnsureReturnScene();
        }

        if (string.IsNullOrWhiteSpace(streamId))
        {
            SetStatus(false, 0, 0.0, 0, "configuration-error");
            CPH.LogError("CometenIRL Watchdog: Missing persisted global " + VarStreamId + ".");
            return true;
        }

        BelaboxStats stats;
        string error;

        if (!TryGetStats(statsBaseUrl, streamId, out stats, out error))
        {
            SetStatus(false, 0, 0.0, 0, "stats-unavailable");

            if (!armed)
            {
                ResetRuntime();
                CPH.LogInfo(
                    "CometenIRL Watchdog: scene control is disarmed; stats query failed but no fallback will be triggered."
                );
                return true;
            }

            HandleQueryFailure(
                error,
                GetPositiveInt(VarQueryFailChecks, DefaultQueryFailChecks),
                fallbackScene
            );
            return true;
        }

        CPH.SetGlobalVar(VarQueryFailCount, 0, true);

        bool signalOk = stats.PublisherFound && stats.Connected && stats.Bitrate > 0;
        SetStatus(
            stats.Connected,
            stats.Bitrate,
            stats.Rtt,
            stats.DroppedPackets,
            signalOk ? "online" : "offline"
        );

        CPH.LogInfo(
            "CometenIRL DIAG: stats publisherFound=" + stats.PublisherFound
            + " connected=" + stats.Connected
            + " bitrate=" + stats.Bitrate
            + " rtt=" + stats.Rtt.ToString("0.0", CultureInfo.InvariantCulture)
            + " dropped=" + stats.DroppedPackets
            + " signalOk=" + signalOk + "."
        );

        if (!armed)
        {
            ResetRuntime();
            CPH.LogInfo(
                "CometenIRL Watchdog: telemetry updated; scene control is disarmed, so failover/recovery is skipped."
            );
            return true;
        }

        if (signalOk)
        {
            HandleOnline(
                stats,
                GetPositiveInt(VarRecoverChecks, DefaultRecoverChecks),
                fallbackScene
            );
        }
        else
        {
            HandleOffline(
                stats,
                GetPositiveInt(VarFailChecks, DefaultFailChecks),
                fallbackScene
            );
        }

        return true;
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(2.5);
        return client;
    }

    private bool TryGetStats(
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
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }

        string body;
        if (!TryExtractPublisherObject(json, streamId, out body))
        {
            stats.PublisherFound = false;
            return true;
        }

        stats.PublisherFound = true;

        if (!TryExtractBool(body, "connected", out stats.Connected))
        {
            error = "publisher JSON has no connected field";
            return false;
        }

        if (!TryExtractInt(body, "bitrate", out stats.Bitrate))
        {
            error = "publisher JSON has no bitrate field";
            return false;
        }

        if (!TryExtractDouble(body, "rtt", out stats.Rtt))
        {
            stats.Rtt = 0.0;
        }

        if (!TryExtractInt(body, "dropped_pkts", out stats.DroppedPackets))
        {
            stats.DroppedPackets = 0;
        }

        return true;
    }

    private void HandleOnline(BelaboxStats stats, int recoverChecks, string fallbackScene)
    {
        CPH.SetGlobalVar(VarFailCount, 0, true);

        if (!IsFallbackActuallyActive(fallbackScene))
        {
            CPH.SetGlobalVar(VarRecoverCount, 0, true);
            return;
        }

        int count = CPH.GetGlobalVar<int>(VarRecoverCount, true) + 1;
        CPH.SetGlobalVar(VarRecoverCount, count, true);

        CPH.LogInfo(
            "CometenIRL Watchdog: recovery " + count + "/" + recoverChecks
            + " bitrate=" + stats.Bitrate + " kbps."
        );

        if (count >= recoverChecks)
        {
            RestoreAfterRecovery(fallbackScene);
        }
    }

    private void HandleOffline(BelaboxStats stats, int failChecks, string fallbackScene)
    {
        CPH.SetGlobalVar(VarRecoverCount, 0, true);

        int count = CPH.GetGlobalVar<int>(VarFailCount, true) + 1;
        CPH.SetGlobalVar(VarFailCount, count, true);

        bool fallbackActive = IsFallbackActuallyActive(fallbackScene);

        if (count == 1 || count == failChecks)
        {
            CPH.LogWarn(
                "CometenIRL Watchdog: BELABOX ingest unhealthy "
                + count + "/" + failChecks
                + " connected=" + stats.Connected
                + " bitrate=" + stats.Bitrate + " kbps."
            );
        }

        if (fallbackActive || count < failChecks)
        {
            return;
        }

        ActivateFallback(
            fallbackScene,
            "ingest offline - connected=" + stats.Connected
            + ", bitrate=" + stats.Bitrate + " kbps"
        );
    }

    private void HandleQueryFailure(string error, int queryFailChecks, string fallbackScene)
    {
        SetStatus(false, 0, 0.0, 0, "stats-unavailable");
        CPH.SetGlobalVar(VarRecoverCount, 0, true);

        int count = CPH.GetGlobalVar<int>(VarQueryFailCount, true) + 1;
        CPH.SetGlobalVar(VarQueryFailCount, count, true);

        if (count == 1 || count == queryFailChecks)
        {
            CPH.LogWarn(
                "CometenIRL Watchdog: BELABOX stats unavailable "
                + count + "/" + queryFailChecks + " - " + error
            );
        }

        if (IsFallbackActuallyActive(fallbackScene) || count < queryFailChecks)
        {
            return;
        }

        ActivateFallback(fallbackScene, "BELABOX stats unavailable");
    }

    private void ActivateFallback(string fallbackScene, string reason)
    {
        string currentScene = CPH.ObsGetCurrentScene(ObsConnection) ?? string.Empty;

        if (string.Equals(currentScene, fallbackScene, StringComparison.Ordinal))
        {
            EnsureReturnScene();
            CPH.SetGlobalVar(VarFallbackActive, true, true);
            return;
        }

        if (!string.IsNullOrWhiteSpace(currentScene))
        {
            CPH.SetGlobalVar(VarReturnScene, currentScene, true);
        }
        else
        {
            EnsureReturnScene();
        }

        CPH.SetGlobalVar(VarFallbackActive, false, true);

        CPH.LogWarn(
            "CometenIRL Watchdog: SIGNAL LOST - " + reason
            + ". Scene '" + currentScene + "' -> '" + fallbackScene + "'."
        );

        bool switched = SwitchScene(fallbackScene, "fallback");
        CPH.SetGlobalVar(VarFallbackActive, switched, true);

        if (!switched)
        {
            CPH.LogWarn(
                "CometenIRL DIAG: fallback was not confirmed; next tick will retry."
            );
        }
    }

    private void RestoreAfterRecovery(string fallbackScene)
    {
        string currentScene = CPH.ObsGetCurrentScene(ObsConnection) ?? string.Empty;
        string returnScene = ResolveReturnScene();

        if (string.Equals(currentScene, fallbackScene, StringComparison.Ordinal))
        {
            CPH.LogInfo(
                "CometenIRL Watchdog: BELABOX stable again. Scene '"
                + fallbackScene + "' -> '" + returnScene + "'."
            );

            bool switched = SwitchScene(returnScene, "recovery");

            if (switched)
            {
                CPH.LogInfo(
                    "CometenIRL Watchdog: recovery confirmed on scene '"
                    + returnScene + "'."
                );
                ResetRuntime();
            }
            else
            {
                CPH.SetGlobalVar(VarFallbackActive, true, true);
                CPH.LogWarn(
                    "CometenIRL Watchdog: recovery scene change was not confirmed; "
                    + "will retry on next healthy check."
                );
            }

            return;
        }

        if (string.Equals(currentScene, returnScene, StringComparison.Ordinal))
        {
            CPH.LogInfo(
                "CometenIRL Watchdog: recovery already confirmed on scene '"
                + returnScene + "'."
            );
            ResetRuntime();
            return;
        }

        CPH.LogInfo(
            "CometenIRL Watchdog: recovery detected, but OBS scene was changed manually to '"
            + currentScene + "'. Auto-return skipped."
        );
        ResetRuntime();
    }

    private string ResolveReturnScene()
    {
        string stored = GetString(VarReturnScene, string.Empty);
        if (!string.IsNullOrWhiteSpace(stored))
        {
            return stored;
        }

        return GetString(VarDefaultReturnScene, DefaultReturnScene);
    }

    private void EnsureReturnScene()
    {
        string stored = GetString(VarReturnScene, string.Empty);
        if (!string.IsNullOrWhiteSpace(stored))
        {
            return;
        }

        CPH.SetGlobalVar(
            VarReturnScene,
            GetString(VarDefaultReturnScene, DefaultReturnScene),
            true
        );
    }

    private bool SwitchScene(string sceneName, string reason)
    {
        string before = CPH.ObsGetCurrentScene(ObsConnection) ?? string.Empty;

        CPH.LogWarn(
            "CometenIRL DIAG: switching scene reason=" + reason
            + " before='" + before + "' target='" + sceneName + "'."
        );

        try
        {
            CPH.ObsSetScene(sceneName, ObsConnection);
            Thread.Sleep(100);

            string after = CPH.ObsGetCurrentScene(ObsConnection) ?? string.Empty;
            bool confirmed = string.Equals(after, sceneName, StringComparison.Ordinal);

            CPH.LogWarn(
                "CometenIRL DIAG: ObsSetScene after='" + after
                + "' confirmed=" + confirmed + "."
            );

            return confirmed;
        }
        catch (Exception ex)
        {
            CPH.LogError("CometenIRL DIAG: scene switch FAILED: " + ex.Message);
            return false;
        }
    }

    private bool IsFallbackActuallyActive(string fallbackScene)
    {
        bool stored = CPH.GetGlobalVar<bool>(VarFallbackActive, true);
        string currentScene = CPH.ObsGetCurrentScene(ObsConnection) ?? string.Empty;
        bool actual = string.Equals(currentScene, fallbackScene, StringComparison.Ordinal);

        if (stored && !actual)
        {
            CPH.LogWarn(
                "CometenIRL DIAG: stale fallback flag detected. "
                + "Stored=True but OBS scene='" + currentScene
                + "'. Clearing flag and allowing retry."
            );
        }

        if (stored != actual)
        {
            CPH.SetGlobalVar(VarFallbackActive, actual, true);
        }

        return actual;
    }

    private void ResetRuntime()
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
        CPH.SetGlobalVar(VarDropped, dropped, true);
        CPH.SetGlobalVar(VarState, state, true);
    }

    private string GetString(string name, string fallback)
    {
        string value = (CPH.GetGlobalVar<string>(name, true) ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private bool GetBool(string name, bool fallback)
    {
        try
        {
            string text = (CPH.GetGlobalVar<string>(name, true) ?? string.Empty).Trim();
            bool parsed;

            if (bool.TryParse(text, out parsed))
            {
                return parsed;
            }

            if (text == "1")
            {
                return true;
            }

            if (text == "0")
            {
                return false;
            }
        }
        catch
        {
        }

        try
        {
            return CPH.GetGlobalVar<bool>(name, true);
        }
        catch
        {
            return fallback;
        }
    }

    private int GetPositiveInt(string name, int fallback)
    {
        int value = CPH.GetGlobalVar<int>(name, true);
        return value > 0 ? value : fallback;
    }

    private static bool TryExtractPublisherObject(string json, string streamId, out string body)
    {
        body = string.Empty;

        Match match = Regex.Match(
            json ?? string.Empty,
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
        Match m = Regex.Match(
            json ?? string.Empty,
            "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(true|false)",
            RegexOptions.IgnoreCase
        );

        if (!m.Success)
        {
            return false;
        }

        value = string.Equals(m.Groups[1].Value, "true", StringComparison.OrdinalIgnoreCase);
        return true;
    }

    private static bool TryExtractInt(string json, string key, out int value)
    {
        value = 0;
        Match m = Regex.Match(
            json ?? string.Empty,
            "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(-?[0-9]+)",
            RegexOptions.IgnoreCase
        );

        return m.Success
            && int.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryExtractDouble(string json, string key, out double value)
    {
        value = 0.0;
        Match m = Regex.Match(
            json ?? string.Empty,
            "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(-?[0-9]+(?:\\.[0-9]+)?)",
            RegexOptions.IgnoreCase
        );

        return m.Success
            && double.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private sealed class BelaboxStats
    {
        public bool PublisherFound;
        public bool Connected;
        public int Bitrate;
        public double Rtt;
        public int DroppedPackets;
    }
}
