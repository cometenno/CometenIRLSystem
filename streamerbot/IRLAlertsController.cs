using System;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;

// CometenIRLAlerts - main Streamer.bot IRL controller
//
// BELABOX watchdog v2
// -------------------
// OBS Media Source state is not reliable when an SRT feed disappears because
// OBS may keep reporting PLAYING while the last frame is frozen.
//
// This controller therefore uses the IRL Alerts heartbeat from ROCK 5B+:
//   ROCK 5B+ -> heartbeat.php -> relay/database -> receiver_status.php
//   Streamer.bot -> receiver_status.php -> OBS scene failover
//
// Default OBS setup:
//   IRL scene:        BELABOX SRT
//   Fallback scene:   IRL - SIGNAL MISTET
//
// Existing Streamer.bot globals used:
//   CometenIRL_RelayUrl
//   CometenIRL_SenderToken
//
// Optional globals:
//   CometenIRL_FallbackScene          string, default "IRL - SIGNAL MISTET"
//   CometenIRL_HeartbeatReceiverId    string, default "belabox"
//   CometenIRL_HeartbeatFailChecks    int, default 1
//   CometenIRL_HeartbeatRecoverChecks int, default 3
//
// The relay itself marks the receiver offline after 5 seconds without a
// heartbeat. The 1-second Streamer.bot timer can therefore use one confirmed
// offline response without reacting to a one-second network hiccup.

public class CPHInline
{
    private static readonly HttpClient Http = CreateHttpClient();

    private const int ObsConnection = 0;
    private const string DefaultFallbackScene = "IRL - SIGNAL MISTET";
    private const string DefaultReceiverId = "belabox";
    private const int DefaultFailChecks = 1;
    private const int DefaultRecoverChecks = 3;

    private const string VarRelayUrl = "CometenIRL_RelayUrl";
    private const string VarSenderToken = "CometenIRL_SenderToken";
    private const string VarFallbackScene = "CometenIRL_FallbackScene";
    private const string VarReceiverId = "CometenIRL_HeartbeatReceiverId";
    private const string VarFailChecks = "CometenIRL_HeartbeatFailChecks";
    private const string VarRecoverChecks = "CometenIRL_HeartbeatRecoverChecks";

    private const string VarFailCount = "CometenIRL_HeartbeatFailCount";
    private const string VarRecoverCount = "CometenIRL_HeartbeatRecoverCount";
    private const string VarFallbackActive = "CometenIRL_SrtFallbackActive";
    private const string VarReturnScene = "CometenIRL_SrtReturnScene";
    private const string VarLastHeartbeatState = "CometenIRL_LastHeartbeatState";
    private const string VarLastHeartbeatAge = "CometenIRL_LastHeartbeatAgeSeconds";
    private const string VarQueryFailCount = "CometenIRL_HeartbeatQueryFailCount";

    public bool Execute()
    {
        bool obsConnected = CPH.ObsIsConnected(ObsConnection);
        bool obsStreaming = CPH.ObsIsStreaming(ObsConnection);

        if (!obsConnected)
        {
            CPH.LogWarn("CometenIRL Watchdog: OBS is not connected.");
            return true;
        }

        // Never force IRL scene changes while OBS is not actually streaming.
        if (!obsStreaming)
        {
            ResetRuntimeState();
            return true;
        }

        string relayUrl = (CPH.GetGlobalVar<string>(VarRelayUrl, true) ?? string.Empty).Trim();
        string senderToken = (CPH.GetGlobalVar<string>(VarSenderToken, true) ?? string.Empty).Trim();
        string fallbackScene = (CPH.GetGlobalVar<string>(VarFallbackScene, true) ?? string.Empty).Trim();
        string receiverId = (CPH.GetGlobalVar<string>(VarReceiverId, true) ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(fallbackScene))
        {
            fallbackScene = DefaultFallbackScene;
        }

        if (string.IsNullOrWhiteSpace(receiverId))
        {
            receiverId = DefaultReceiverId;
        }

        if (string.IsNullOrWhiteSpace(relayUrl) || string.IsNullOrWhiteSpace(senderToken))
        {
            CPH.LogError("CometenIRL Watchdog: Missing CometenIRL_RelayUrl or CometenIRL_SenderToken.");
            return true;
        }

        bool online;
        double? ageSeconds;
        string rawStatus;

        if (!TryGetHeartbeatStatus(relayUrl, senderToken, receiverId, out online, out ageSeconds, out rawStatus))
        {
            HandleStatusQueryFailure();
            return true;
        }

        HandleStatusQueryRecovered();

        string state = online ? "online" : "offline";
        string previousState = (CPH.GetGlobalVar<string>(VarLastHeartbeatState, true) ?? string.Empty).Trim();

        CPH.SetGlobalVar(VarLastHeartbeatState, state, true);
        CPH.SetGlobalVar(VarLastHeartbeatAge, ageSeconds.HasValue ? ageSeconds.Value : -1.0, true);

        if (!string.Equals(previousState, state, StringComparison.OrdinalIgnoreCase))
        {
            CPH.LogInfo(
                "CometenIRL Watchdog: BELABOX heartbeat " + state
                + FormatAge(ageSeconds)
                + "."
            );
        }

        int failChecks = CPH.GetGlobalVar<int>(VarFailChecks, true);
        int recoverChecks = CPH.GetGlobalVar<int>(VarRecoverChecks, true);

        if (failChecks <= 0)
        {
            failChecks = DefaultFailChecks;
        }

        if (recoverChecks <= 0)
        {
            recoverChecks = DefaultRecoverChecks;
        }

        int failCount = CPH.GetGlobalVar<int>(VarFailCount, true);
        int recoverCount = CPH.GetGlobalVar<int>(VarRecoverCount, true);
        bool fallbackActive = CPH.GetGlobalVar<bool>(VarFallbackActive, true);

        if (online)
        {
            failCount = 0;

            if (!fallbackActive)
            {
                SaveCounters(0, 0);
                return true;
            }

            recoverCount++;
            SaveCounters(failCount, recoverCount);

            CPH.LogInfo(
                "CometenIRL Watchdog: BELABOX recovery "
                + recoverCount + "/" + recoverChecks
                + FormatAge(ageSeconds)
                + "."
            );

            if (recoverCount >= recoverChecks)
            {
                RestoreAfterRecovery(fallbackScene);
            }

            return true;
        }

        recoverCount = 0;
        failCount++;
        SaveCounters(failCount, recoverCount);

        if (fallbackActive || failCount < failChecks)
        {
            return true;
        }

        ActivateFallback(fallbackScene, ageSeconds);
        return true;
    }

    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new HttpClient();
        client.Timeout = TimeSpan.FromSeconds(2.5);
        return client;
    }

    private bool TryGetHeartbeatStatus(
        string relayUrl,
        string senderToken,
        string receiverId,
        out bool online,
        out double? ageSeconds,
        out string rawStatus)
    {
        online = false;
        ageSeconds = null;
        rawStatus = string.Empty;

        string endpoint = relayUrl.TrimEnd('/')
            + "/receiver_status.php?receiver_id="
            + Uri.EscapeDataString(receiverId);

        try
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint))
            {
                request.Headers.TryAddWithoutValidation("X-Cometen-Token", senderToken);
                request.Headers.TryAddWithoutValidation("Cache-Control", "no-cache");

                using (HttpResponseMessage response = Http.SendAsync(request).GetAwaiter().GetResult())
                {
                    rawStatus = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                    if (!response.IsSuccessStatusCode)
                    {
                        CPH.LogError(
                            "CometenIRL Watchdog: receiver_status.php returned HTTP "
                            + (int)response.StatusCode + ": " + rawStatus
                        );
                        return false;
                    }
                }
            }

            bool ok;
            if (!TryExtractJsonBool(rawStatus, "ok", out ok) || !ok)
            {
                CPH.LogError("CometenIRL Watchdog: invalid receiver status response: " + rawStatus);
                return false;
            }

            if (!TryExtractJsonBool(rawStatus, "online", out online))
            {
                CPH.LogError("CometenIRL Watchdog: receiver status has no online field: " + rawStatus);
                return false;
            }

            ageSeconds = ExtractJsonNullableDouble(rawStatus, "age_seconds");
            return true;
        }
        catch (Exception exception)
        {
            CPH.LogError("CometenIRL Watchdog: heartbeat status request failed: " + exception.Message);
            return false;
        }
    }

    private void HandleStatusQueryFailure()
    {
        int count = CPH.GetGlobalVar<int>(VarQueryFailCount, true) + 1;
        CPH.SetGlobalVar(VarQueryFailCount, count, true);

        // A relay/webhotel failure is not automatically treated as a BELABOX
        // failure. We only fail over on a successful status response saying that
        // the BELABOX heartbeat itself is stale.
        if (count == 1 || count == 5 || count % 30 == 0)
        {
            CPH.LogWarn(
                "CometenIRL Watchdog: heartbeat status unavailable (attempt "
                + count + "). No automatic scene change on relay errors."
            );
        }
    }

    private void HandleStatusQueryRecovered()
    {
        int count = CPH.GetGlobalVar<int>(VarQueryFailCount, true);
        if (count > 0)
        {
            CPH.LogInfo("CometenIRL Watchdog: heartbeat status connection restored.");
        }
        CPH.SetGlobalVar(VarQueryFailCount, 0, true);
    }

    private void ActivateFallback(string fallbackScene, double? ageSeconds)
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
            "CometenIRL Watchdog: BELABOX HEARTBEAT LOST"
            + FormatAge(ageSeconds)
            + ". Scene '" + currentScene
            + "' -> '" + fallbackScene + "'."
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
                "CometenIRL Watchdog: BELABOX stable again. Scene '"
                + fallbackScene + "' -> '" + returnScene + "'."
            );
            CPH.ObsSetScene(returnScene, ObsConnection);
        }
        else if (!string.Equals(currentScene, fallbackScene, StringComparison.Ordinal))
        {
            CPH.LogInfo(
                "CometenIRL Watchdog: BELABOX stable again, but scene was changed manually to '"
                + currentScene + "'. Auto-return skipped."
            );
        }
        else
        {
            CPH.LogWarn("CometenIRL Watchdog: BELABOX stable again, but no return scene was stored.");
        }

        ResetRuntimeState();
    }

    private void SaveCounters(int failCount, int recoverCount)
    {
        CPH.SetGlobalVar(VarFailCount, failCount, true);
        CPH.SetGlobalVar(VarRecoverCount, recoverCount, true);
    }

    private void ResetRuntimeState()
    {
        CPH.SetGlobalVar(VarFailCount, 0, true);
        CPH.SetGlobalVar(VarRecoverCount, 0, true);
        CPH.SetGlobalVar(VarFallbackActive, false, true);
        CPH.SetGlobalVar(VarReturnScene, string.Empty, true);
    }

    private static string FormatAge(double? ageSeconds)
    {
        if (!ageSeconds.HasValue || ageSeconds.Value < 0)
        {
            return string.Empty;
        }

        return " (age "
            + ageSeconds.Value.ToString("0.0", CultureInfo.InvariantCulture)
            + "s)";
    }

    private static bool TryExtractJsonBool(string json, string key, out bool value)
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

    private static double? ExtractJsonNullableDouble(string json, string key)
    {
        Match match = Regex.Match(
            json ?? string.Empty,
            "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*(null|-?[0-9]+(?:\\.[0-9]+)?)",
            RegexOptions.IgnoreCase
        );

        if (!match.Success || string.Equals(match.Groups[1].Value, "null", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        double parsed;
        if (double.TryParse(
            match.Groups[1].Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out parsed))
        {
            return parsed;
        }

        return null;
    }
}
