using System;
using System.Text;
using System.Text.RegularExpressions;

// CometenIRLAlerts - main Streamer.bot IRL controller
//
// Initial controller module:
//   - Watches the BELABOX SRT media input while OBS is live
//   - Ignores short signal glitches
//   - Switches to a fallback scene after sustained signal loss
//   - Remembers the scene that was active before failover
//   - Restores that scene after the SRT input has been stable again
//   - Does not force a restore if the operator manually changed scene
//   - Writes temporary diagnostic lines to the Streamer.bot log
//
// Default OBS names:
//   SRT Media Source: BELABOX SRT
//   Fallback scene:   IRL - SIGNAL MISTET
//
// Optional Streamer.bot global overrides:
//   CometenIRL_SrtInputName      string
//   CometenIRL_FallbackScene     string
//   CometenIRL_SrtFailChecks     int     Defaults to 3
//   CometenIRL_SrtRecoverChecks  int     Defaults to 5
//
// Run this action from a repeating Streamer.bot Timed Action every 1 second.

public class CPHInline
{
    private const int ObsConnection = 0;

    private const string DefaultSrtInputName = "BELABOX SRT";
    private const string DefaultFallbackScene = "IRL - SIGNAL MISTET";
    private const int DefaultFailChecks = 3;
    private const int DefaultRecoverChecks = 5;

    private const string VarSrtInputName = "CometenIRL_SrtInputName";
    private const string VarFallbackScene = "CometenIRL_FallbackScene";
    private const string VarFailChecks = "CometenIRL_SrtFailChecks";
    private const string VarRecoverChecks = "CometenIRL_SrtRecoverChecks";

    private const string VarFailCount = "CometenIRL_SrtFailCount";
    private const string VarRecoverCount = "CometenIRL_SrtRecoverCount";
    private const string VarFallbackActive = "CometenIRL_SrtFallbackActive";
    private const string VarReturnScene = "CometenIRL_SrtReturnScene";
    private const string VarLastMediaState = "CometenIRL_SrtLastMediaState";

    public bool Execute()
    {
        bool obsConnected = CPH.ObsIsConnected(ObsConnection);
        bool obsStreaming = CPH.ObsIsStreaming(ObsConnection);

        CPH.LogInfo("CometenIRL TEST: tick - OBS connected="
            + obsConnected
            + " streaming="
            + obsStreaming);

        string inputName = (CPH.GetGlobalVar<string>(VarSrtInputName, true) ?? string.Empty).Trim();
        string fallbackScene = (CPH.GetGlobalVar<string>(VarFallbackScene, true) ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(inputName))
        {
            inputName = DefaultSrtInputName;
        }

        if (string.IsNullOrWhiteSpace(fallbackScene))
        {
            fallbackScene = DefaultFallbackScene;
        }

        // The watchdog is intentionally inactive whenever OBS is not actually live.
        if (!obsStreaming)
        {
            ResetRuntimeState();
            return true;
        }

        string mediaState;
        if (!TryGetMediaState(inputName, out mediaState))
        {
            // A malformed/missing OBS response is treated as a configuration or
            // connection problem, not immediately as a real SRT outage. This
            // prevents an incorrect source name from forcing the fallback scene.
            return true;
        }

        CPH.SetGlobalVar(VarLastMediaState, mediaState, true);
        CPH.LogInfo("CometenIRL TEST: " + inputName + " mediaState=" + mediaState);

        int failChecks = CPH.GetGlobalVar<int>(VarFailChecks, true);
        int recoverChecks = CPH.GetGlobalVar<int>(VarRecoverChecks, true);
        if (failChecks <= 0) failChecks = DefaultFailChecks;
        if (recoverChecks <= 0) recoverChecks = DefaultRecoverChecks;

        int failCount = CPH.GetGlobalVar<int>(VarFailCount, true);
        int recoverCount = CPH.GetGlobalVar<int>(VarRecoverCount, true);
        bool fallbackActive = CPH.GetGlobalVar<bool>(VarFallbackActive, true);

        bool signalOk = string.Equals(
            mediaState,
            "OBS_MEDIA_STATE_PLAYING",
            StringComparison.OrdinalIgnoreCase);

        if (signalOk)
        {
            failCount = 0;

            if (!fallbackActive)
            {
                SaveCounters(0, 0);
                return true;
            }

            recoverCount++;
            SaveCounters(failCount, recoverCount);

            CPH.LogInfo("CometenIRL Controller: SRT recovered "
                + recoverCount + "/" + recoverChecks
                + " (" + mediaState + ").");

            if (recoverCount >= recoverChecks)
            {
                RestoreAfterRecovery(fallbackScene);
            }

            return true;
        }

        // Any valid OBS media state other than PLAYING counts toward signal loss.
        // Examples include BUFFERING, OPENING, STOPPED and ERROR.
        recoverCount = 0;
        failCount++;
        SaveCounters(failCount, recoverCount);

        CPH.LogWarn("CometenIRL Controller: SRT not healthy "
            + failCount + "/" + failChecks
            + " (" + mediaState + ").");

        if (fallbackActive || failCount < failChecks)
        {
            return true;
        }

        ActivateFallback(fallbackScene, mediaState);
        return true;
    }

    private bool TryGetMediaState(string inputName, out string mediaState)
    {
        mediaState = string.Empty;

        try
        {
            string requestData = "{\"inputName\":\"" + JsonEscape(inputName) + "\"}";
            string response = CPH.ObsSendRaw("GetMediaInputStatus", requestData, ObsConnection);

            mediaState = ExtractJsonString(response, "mediaState");
            if (string.IsNullOrWhiteSpace(mediaState))
            {
                CPH.LogError("CometenIRL Controller: OBS returned no mediaState for input '"
                    + inputName + "'. Raw response: " + (response ?? "<null>"));
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError("CometenIRL Controller: GetMediaInputStatus failed for '"
                + inputName + "': " + ex.Message);
            return false;
        }
    }

    private void ActivateFallback(string fallbackScene, string mediaState)
    {
        string currentScene = CPH.ObsGetCurrentScene(ObsConnection) ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(currentScene)
            && !string.Equals(currentScene, fallbackScene, StringComparison.Ordinal))
        {
            CPH.SetGlobalVar(VarReturnScene, currentScene, true);
        }

        CPH.SetGlobalVar(VarFallbackActive, true, true);
        CPH.SetGlobalVar(VarRecoverCount, 0, true);

        CPH.LogWarn("CometenIRL Controller: SRT SIGNAL LOST ("
            + mediaState + "). Scene '" + currentScene
            + "' -> '" + fallbackScene + "'.");

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
            CPH.LogInfo("CometenIRL Controller: SRT stable again. Scene '"
                + fallbackScene + "' -> '" + returnScene + "'.");
            CPH.ObsSetScene(returnScene, ObsConnection);
        }
        else if (!string.Equals(currentScene, fallbackScene, StringComparison.Ordinal))
        {
            CPH.LogInfo("CometenIRL Controller: SRT stable again, but scene was changed manually to '"
                + currentScene + "'. Auto-return skipped.");
        }
        else
        {
            CPH.LogWarn("CometenIRL Controller: SRT stable again, but no return scene was stored.");
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
        CPH.SetGlobalVar(VarLastMediaState, string.Empty, true);
    }

    private static string ExtractJsonString(string json, string key)
    {
        if (string.IsNullOrWhiteSpace(json) || string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        Match match = Regex.Match(
            json,
            "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*\\\"((?:\\\\.|[^\\\"\\\\])*)\\\"",
            RegexOptions.IgnoreCase);

        return match.Success ? JsonUnescape(match.Groups[1].Value) : string.Empty;
    }

    private static string JsonEscape(string value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        StringBuilder builder = new StringBuilder(value.Length + 16);
        foreach (char character in value)
        {
            switch (character)
            {
                case '\\': builder.Append("\\\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (character < 32)
                    {
                        builder.Append("\\u");
                        builder.Append(((int)character).ToString("x4"));
                    }
                    else
                    {
                        builder.Append(character);
                    }
                    break;
            }
        }

        return builder.ToString();
    }

    private static string JsonUnescape(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        return Regex.Unescape(value);
    }
}
