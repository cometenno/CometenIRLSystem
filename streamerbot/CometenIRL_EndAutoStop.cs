using System;
using System.Threading;

// CometenIRLAlerts - ending auto-stop helper v1.1
// IMPORTANT: Put this action on a dedicated Streamer.bot queue, e.g. "IRL END".
// No chat command should point directly to this action.

public class CPHInline
{
    private const int ObsConnection = 0;
    private const int DefaultEndingSeconds = 25;
    private const string DefaultEndingScene = "IRL - ENDING";
    private const string DefaultNormalRewardGroup = "NORMAL";
    private const string DefaultIrlRewardGroup = "IRL";

    private const string VarLanguage = "CometenIRL_Language";
    private const string VarWatchdogArmed = "CometenIRL_WatchdogArmed";
    private const string VarIrlMode = "CometenIRL_IrlMode";
    private const string VarEndingScene = "CometenIRL_EndingScene";
    private const string VarEndingSeconds = "CometenIRL_EndingSeconds";
    private const string VarEndPending = "CometenIRL_EndPending";
    private const string VarEndSequence = "CometenIRL_EndSequence";
    private const string VarManageRewards = "CometenIRL_ManageRewards";
    private const string VarNormalRewardGroup = "CometenIRL_NormalRewardGroup";
    private const string VarIrlRewardGroup = "CometenIRL_IrlRewardGroup";

    public bool Execute()
    {
        if (!GetBool(VarEndPending, false)) return true;

        int sequence = GetInt(VarEndSequence, 0);
        int seconds = GetEndingSeconds();
        string endingScene = GetString(VarEndingScene, DefaultEndingScene);

        CPH.Wait(seconds * 1000);

        if (!GetBool(VarEndPending, false) || GetInt(VarEndSequence, 0) != sequence) return true;

        if (!CPH.ObsIsConnected(ObsConnection))
        {
            ClearIfCurrent(sequence);
            return false;
        }

        string currentScene = CPH.ObsGetCurrentScene(ObsConnection) ?? string.Empty;
        if (!string.Equals(currentScene, endingScene, StringComparison.Ordinal))
        {
            ClearIfCurrent(sequence);
            return true;
        }

        CPH.SetGlobalVar(VarWatchdogArmed, false, true);
        CPH.LogInfo(
            "CometenIRL EndAutoStop: ending conditions verified; stopping OBS for sequence "
            + sequence + "."
        );

        if (CPH.ObsIsStreaming(ObsConnection))
        {
            try { CPH.ObsStopStreaming(ObsConnection); }
            catch
            {
                ClearIfCurrent(sequence);
                SendChat(L("IRL: automatisk stopp feilet - OBS streamer fortsatt.",
                           "IRL: automatic stop failed - OBS is still streaming."));
                return false;
            }

            if (!WaitForStreaming(false, 5000))
            {
                ClearIfCurrent(sequence);
                SendChat(L("IRL: automatisk stopp ble sendt, men OBS rapporterer fortsatt live.",
                           "IRL: automatic stop was sent, but OBS still reports streaming."));
                return false;
            }
        }

        SetRewardsMode(false);
        CPH.SetGlobalVar(VarIrlMode, false, true);
        ClearIfCurrent(sequence);

        SendChat(L("IRL: Ending ferdig - streamen er stoppet automatisk.",
                   "IRL: Ending finished - stream stopped automatically."));
        return true;
    }

    private bool SetRewardsMode(bool irlMode)
    {
        if (!GetBool(VarManageRewards, false)) return true;

        string normalGroup = GetString(VarNormalRewardGroup, DefaultNormalRewardGroup);
        string irlGroup = GetString(VarIrlRewardGroup, DefaultIrlRewardGroup);

        try
        {
            if (irlMode)
            {
                CPH.TwitchRewardGroupDisable(normalGroup);
                CPH.TwitchRewardGroupEnable(irlGroup);
            }
            else
            {
                CPH.TwitchRewardGroupDisable(irlGroup);
                CPH.TwitchRewardGroupEnable(normalGroup);
            }
            return true;
        }
        catch { return false; }
    }

    private bool WaitForStreaming(bool expected, int timeoutMs)
    {
        int waited = 0;
        while (waited < timeoutMs)
        {
            if (CPH.ObsIsStreaming(ObsConnection) == expected) return true;
            Thread.Sleep(250);
            waited += 250;
        }
        return CPH.ObsIsStreaming(ObsConnection) == expected;
    }

    private void ClearIfCurrent(int sequence)
    {
        if (GetInt(VarEndSequence, 0) == sequence) CPH.SetGlobalVar(VarEndPending, false, true);
    }

    private int GetEndingSeconds()
    {
        int seconds = GetInt(VarEndingSeconds, DefaultEndingSeconds);

        // Streamer.bot may return 0 for a missing int global.
        // Treat missing/zero/negative as the documented default.
        if (seconds <= 0) seconds = DefaultEndingSeconds;

        if (seconds < 5) seconds = 5;
        if (seconds > 120) seconds = 120;
        return seconds;
    }

    private string GetLanguage()
    {
        string value = GetString(VarLanguage, "no").Trim().ToLowerInvariant();
        return value == "en" ? "en" : "no";
    }

    private string L(string norwegian, string english) => GetLanguage() == "en" ? english : norwegian;

    private string GetString(string name, string fallback)
    {
        try
        {
            string value = (CPH.GetGlobalVar<string>(name, true) ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
        catch { return fallback; }
    }

    private bool GetBool(string name, bool fallback)
    {
        try
        {
            string text = (CPH.GetGlobalVar<string>(name, true) ?? string.Empty).Trim();
            bool parsed;
            if (bool.TryParse(text, out parsed)) return parsed;
            if (text == "1") return true;
            if (text == "0") return false;
        }
        catch { }
        try { return CPH.GetGlobalVar<bool>(name, true); }
        catch { return fallback; }
    }

    private int GetInt(string name, int fallback)
    {
        try
        {
            string text = (CPH.GetGlobalVar<string>(name, true) ?? string.Empty).Trim();
            int parsed;
            if (int.TryParse(text, out parsed)) return parsed;
        }
        catch { }
        try { return CPH.GetGlobalVar<int>(name, true); }
        catch { return fallback; }
    }

    private void SendChat(string message)
    {
        if (!string.IsNullOrWhiteSpace(message)) CPH.SendMessage(message.Trim(), true, true);
    }
}
