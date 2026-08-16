using System;
using System.Threading;

// CometenIRLAlerts - local OBS/admin control v1.1
// This action is intentionally local to Streamer.bot/OBS. It does not use the BELABOX relay.
// Recommended command permissions: Broadcaster only.

public class CPHInline
{
    private const int ObsConnection = 0;

    private const string DefaultStartingSoonScene = "IRL - STARTING SOON";
    private const string DefaultSrtScene = "BELABOX SRT";
    private const string DefaultFallbackScene = "IRL - SIGNAL MISTET";
    private const string DefaultBrbScene = "IRL - BRB";
    private const string DefaultEndingScene = "IRL - ENDING";
    private const string DefaultNormalRewardGroup = "NORMAL";
    private const string DefaultIrlRewardGroup = "IRL";

    private const string VarWatchdogArmed = "CometenIRL_WatchdogArmed";
    private const string VarIrlMode = "CometenIRL_IrlMode";
    private const string VarStartingSoonScene = "CometenIRL_StartingSoonScene";
    private const string VarSrtScene = "CometenIRL_DefaultReturnScene";
    private const string VarFallbackScene = "CometenIRL_FallbackScene";
    private const string VarBrbScene = "CometenIRL_BrbScene";
    private const string VarEndingScene = "CometenIRL_EndingScene";
    private const string VarManageRewards = "CometenIRL_ManageRewards";
    private const string VarNormalRewardGroup = "CometenIRL_NormalRewardGroup";
    private const string VarIrlRewardGroup = "CometenIRL_IrlRewardGroup";

    public bool Execute()
    {
        if (!CPH.ObsIsConnected(ObsConnection))
        {
            SendChat("IRL: OBS er ikke koblet til Streamer.bot.");
            CPH.LogError("CometenIRL Admin: OBS connection 0 is not connected.");
            return false;
        }

        string command = FirstArg("command").Trim().ToLowerInvariant();
        string rawInput = FirstArg("rawInput", "message").Trim();
        string explicitAction = FirstArg("adminAction").Trim().ToLowerInvariant();

        string action;
        string parameter;
        if (!ResolveAction(explicitAction, command, rawInput, out action, out parameter))
        {
            SendChat("IRL: ukjent admin-kommando.");
            return false;
        }

        switch (action)
        {
            case "start": return StartIrl();
            case "go": return GoLiveScene();
            case "brb": return GoBrb();
            case "back": return BackToSrt();
            case "end": return GoEnding();
            case "stop": return StopIrl();
            case "scene": return SetSceneAlias(parameter);
            case "points": return SetPointsMode(parameter);
            default:
                SendChat("IRL: ukjent admin-kommando.");
                return false;
        }
    }

    private bool StartIrl()
    {
        SetArmed(false);
        CPH.SetGlobalVar(VarIrlMode, true, true);

        string scene = GetString(VarStartingSoonScene, DefaultStartingSoonScene);
        if (!SwitchScene(scene))
        {
            SendChat("IRL: kunne ikke bytte til Starting Soon - streamen ble ikke startet.");
            return false;
        }

        bool rewardsOk = SetRewardsMode(true, false);
        if (!rewardsOk)
        {
            CPH.LogWarn("CometenIRL Admin: automatic IRL reward mode failed; continuing with OBS start.");
        }

        if (CPH.ObsIsStreaming(ObsConnection))
        {
            SendChat("IRL: Starting Soon aktiv - streamen var allerede live. Bruk !irlgo når du er klar.");
            return true;
        }

        try
        {
            CPH.ObsStartStreaming(ObsConnection);
        }
        catch (Exception ex)
        {
            CPH.LogError("CometenIRL Admin: ObsStartStreaming failed: " + ex.Message);
            SetRewardsMode(false, false);
            CPH.SetGlobalVar(VarIrlMode, false, true);
            SendChat("IRL: OBS klarte ikke å starte streamen.");
            return false;
        }

        if (!WaitForStreaming(true, 4000))
        {
            SetRewardsMode(false, false);
            CPH.SetGlobalVar(VarIrlMode, false, true);
            SendChat("IRL: startkommando sendt, men OBS rapporterer ikke live.");
            return false;
        }

        SendChat("IRL: Starting Soon - stream startet. Bruk !irlgo når du er klar.");
        return true;
    }

    private bool GoLiveScene()
    {
        string scene = GetString(VarSrtScene, DefaultSrtScene);
        if (!SwitchScene(scene))
        {
            SendChat("IRL: kunne ikke bytte til BELABOX SRT - watchdog ble ikke aktivert.");
            return false;
        }

        SetArmed(true);
        SendChat("IRL: BELABOX SRT - watchdog aktiv.");
        return true;
    }

    private bool GoBrb()
    {
        SetArmed(false);
        string scene = GetString(VarBrbScene, DefaultBrbScene);
        if (!SwitchScene(scene))
        {
            SendChat("IRL: kunne ikke bytte til BRB.");
            return false;
        }

        SendChat("IRL: BRB - watchdog pauset.");
        return true;
    }

    private bool BackToSrt()
    {
        string scene = GetString(VarSrtScene, DefaultSrtScene);
        if (!SwitchScene(scene))
        {
            SendChat("IRL: kunne ikke gå tilbake til BELABOX SRT - watchdog ble ikke aktivert.");
            return false;
        }

        SetArmed(true);
        SendChat("IRL: tilbake på BELABOX SRT - watchdog aktiv.");
        return true;
    }

    private bool GoEnding()
    {
        SetArmed(false);
        string scene = GetString(VarEndingScene, DefaultEndingScene);
        if (!SwitchScene(scene))
        {
            SendChat("IRL: kunne ikke bytte til Ending.");
            return false;
        }

        SendChat("IRL: Ending - watchdog pauset. Bruk !irlstop når streamen skal stoppes.");
        return true;
    }

    private bool StopIrl()
    {
        SetArmed(false);

        if (CPH.ObsIsStreaming(ObsConnection))
        {
            try
            {
                CPH.ObsStopStreaming(ObsConnection);
            }
            catch (Exception ex)
            {
                CPH.LogError("CometenIRL Admin: ObsStopStreaming failed: " + ex.Message);
                SendChat("IRL: OBS klarte ikke å stoppe streamen.");
                return false;
            }

            if (!WaitForStreaming(false, 4000))
            {
                SendChat("IRL: stoppkommando sendt, men OBS rapporterer fortsatt live.");
                return false;
            }
        }

        bool rewardsOk = SetRewardsMode(false, false);
        if (!rewardsOk)
        {
            CPH.LogWarn("CometenIRL Admin: automatic normal reward mode failed after OBS stop.");
        }

        CPH.SetGlobalVar(VarIrlMode, false, true);
        SendChat("IRL: stream stoppet - watchdog av.");
        return true;
    }

    private bool SetSceneAlias(string alias)
    {
        string key = (alias ?? string.Empty).Trim().ToLowerInvariant();
        string scene;
        bool armAfter;

        switch (key)
        {
            case "soon":
            case "starting":
            case "start":
                scene = GetString(VarStartingSoonScene, DefaultStartingSoonScene);
                armAfter = false;
                break;

            case "srt":
            case "live":
            case "go":
                scene = GetString(VarSrtScene, DefaultSrtScene);
                armAfter = true;
                break;

            case "brb":
            case "pause":
                scene = GetString(VarBrbScene, DefaultBrbScene);
                armAfter = false;
                break;

            case "end":
            case "ending":
                scene = GetString(VarEndingScene, DefaultEndingScene);
                armAfter = false;
                break;

            case "signal":
            case "lost":
                scene = GetString(VarFallbackScene, DefaultFallbackScene);
                armAfter = false;
                break;

            default:
                SendChat("IRL: ukjent scene-alias. Bruk soon, srt, brb, end eller signal.");
                return false;
        }

        SetArmed(false);

        if (!SwitchScene(scene))
        {
            SendChat("IRL: scenebytte feilet.");
            return false;
        }

        if (armAfter)
        {
            SetArmed(true);
        }

        SendChat("IRL: scene -> " + scene + (armAfter ? " | watchdog aktiv" : " | watchdog pauset"));
        return true;
    }

    private bool SetPointsMode(string mode)
    {
        string key = (mode ?? string.Empty).Trim().ToLowerInvariant();
        bool irlMode;

        if (key == "on" || key == "irl")
        {
            irlMode = true;
        }
        else if (key == "off" || key == "normal")
        {
            irlMode = false;
        }
        else
        {
            SendChat("IRL: bruk !irlpoints on eller !irlpoints off.");
            return false;
        }

        if (!SetRewardsMode(irlMode, true))
        {
            SendChat("IRL: Channel Points-gruppene kunne ikke oppdateres.");
            return false;
        }

        SendChat(irlMode
            ? "IRL: Channel Points satt til IRL-modus."
            : "IRL: Channel Points satt tilbake til normal modus.");
        return true;
    }

    private bool SetRewardsMode(bool irlMode, bool force)
    {
        bool automaticEnabled = GetBool(VarManageRewards, false);
        if (!force && !automaticEnabled)
        {
            CPH.LogInfo("CometenIRL Admin: automatic reward switching is disabled by " + VarManageRewards + ".");
            return true;
        }

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

            CPH.LogInfo(
                "CometenIRL Admin: reward mode=" + (irlMode ? "IRL" : "NORMAL")
                + " normalGroup='" + normalGroup + "' irlGroup='" + irlGroup + "'."
            );
            return true;
        }
        catch (Exception ex)
        {
            CPH.LogError("CometenIRL Admin: reward group update failed: " + ex.Message);
            return false;
        }
    }

    private bool SwitchScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            return false;
        }

        string before = CPH.ObsGetCurrentScene(ObsConnection) ?? string.Empty;
        if (string.Equals(before, sceneName, StringComparison.Ordinal))
        {
            return true;
        }

        try
        {
            CPH.ObsSetScene(sceneName, ObsConnection);

            string after = before;
            int waited = 0;
            const int timeoutMs = 1500;
            const int pollMs = 100;

            while (waited < timeoutMs)
            {
                Thread.Sleep(pollMs);
                waited += pollMs;
                after = CPH.ObsGetCurrentScene(ObsConnection) ?? string.Empty;

                if (string.Equals(after, sceneName, StringComparison.Ordinal))
                {
                    CPH.LogInfo(
                        "CometenIRL Admin: scene '" + before + "' -> '" + sceneName
                        + "' confirmed=true after " + waited + "ms."
                    );
                    return true;
                }
            }

            CPH.LogWarn(
                "CometenIRL Admin: scene command sent for '" + sceneName
                + "', but confirmation timed out after " + timeoutMs
                + "ms. OBS reports scene='" + after + "'."
            );
            return false;
        }
        catch (Exception ex)
        {
            CPH.LogError("CometenIRL Admin: scene switch failed: " + ex.Message);
            return false;
        }
    }

    private bool WaitForStreaming(bool expected, int timeoutMs)
    {
        int waited = 0;
        while (waited < timeoutMs)
        {
            if (CPH.ObsIsStreaming(ObsConnection) == expected)
            {
                return true;
            }

            Thread.Sleep(250);
            waited += 250;
        }

        return CPH.ObsIsStreaming(ObsConnection) == expected;
    }

    private void SetArmed(bool armed)
    {
        CPH.SetGlobalVar(VarWatchdogArmed, armed, true);
        CPH.LogInfo("CometenIRL Admin: watchdog armed=" + armed + ".");
    }

    private bool ResolveAction(
        string explicitAction,
        string command,
        string rawInput,
        out string action,
        out string parameter)
    {
        action = string.Empty;
        parameter = string.Empty;

        if (!string.IsNullOrWhiteSpace(explicitAction))
        {
            action = explicitAction;
            parameter = rawInput;
            return true;
        }

        string cmd = (command ?? string.Empty).Trim().ToLowerInvariant();
        if (cmd.StartsWith("!"))
        {
            cmd = cmd.Substring(1);
        }

        switch (cmd)
        {
            case "irlstart": action = "start"; return true;
            case "irlgo": action = "go"; return true;
            case "irlbrb": action = "brb"; return true;
            case "irlback": action = "back"; return true;
            case "irlend": action = "end"; return true;
            case "irlstop": action = "stop"; return true;
            case "irlscene": action = "scene"; parameter = FirstWord(rawInput); return true;
            case "irlpoints": action = "points"; parameter = FirstWord(rawInput); return true;
        }

        string full = (rawInput ?? string.Empty).Trim();
        if (full.StartsWith("!"))
        {
            int space = full.IndexOf(' ');
            string first = space >= 0 ? full.Substring(0, space) : full;
            string rest = space >= 0 ? full.Substring(space + 1).Trim() : string.Empty;
            return ResolveAction(string.Empty, first, rest, out action, out parameter);
        }

        return false;
    }

    private string FirstArg(params string[] names)
    {
        foreach (string name in names)
        {
            string value;
            if (CPH.TryGetArg(name, out value) && !string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private string GetString(string name, string fallback)
    {
        try
        {
            string value = (CPH.GetGlobalVar<string>(name, true) ?? string.Empty).Trim();
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
        catch
        {
            return fallback;
        }
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

    private static string FirstWord(string value)
    {
        string text = (value ?? string.Empty).Trim();
        int space = text.IndexOf(' ');
        return space >= 0 ? text.Substring(0, space) : text;
    }

    private void SendChat(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            CPH.SendMessage(message.Trim(), true, true);
        }
    }
}
