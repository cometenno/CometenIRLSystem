using System;
using System.Threading;

// CometenIRLAlerts - local OBS/admin control v1.2
// Local Streamer.bot/OBS control. Does not use the BELABOX relay.
// Recommended command permissions: Broadcaster only.
// Language is persistent in CometenIRL_Language and never changes automatically.

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
    private const string EndAutoStopAction = "CometenIRL_EndAutoStop";
    private const int DefaultEndingSeconds = 25;

    private const string VarWatchdogArmed = "CometenIRL_WatchdogArmed";
    private const string VarIrlMode = "CometenIRL_IrlMode";
    private const string VarLanguage = "CometenIRL_Language";
    private const string VarStartingSoonScene = "CometenIRL_StartingSoonScene";
    private const string VarSrtScene = "CometenIRL_DefaultReturnScene";
    private const string VarFallbackScene = "CometenIRL_FallbackScene";
    private const string VarBrbScene = "CometenIRL_BrbScene";
    private const string VarEndingScene = "CometenIRL_EndingScene";
    private const string VarEndingSeconds = "CometenIRL_EndingSeconds";
    private const string VarEndPending = "CometenIRL_EndPending";
    private const string VarEndSequence = "CometenIRL_EndSequence";
    private const string VarManageRewards = "CometenIRL_ManageRewards";
    private const string VarNormalRewardGroup = "CometenIRL_NormalRewardGroup";
    private const string VarIrlRewardGroup = "CometenIRL_IrlRewardGroup";

    public bool Execute()
    {
        if (!CPH.ObsIsConnected(ObsConnection))
        {
            SendChat(L(
                "IRL: OBS er ikke koblet til Streamer.bot.",
                "IRL: OBS is not connected to Streamer.bot."
            ));
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
            SendChat(L("IRL: ukjent admin-kommando.", "IRL: unknown admin command."));
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
            case "language": return SetLanguage(parameter);
            default:
                SendChat(L("IRL: ukjent admin-kommando.", "IRL: unknown admin command."));
                return false;
        }
    }

    private bool StartIrl()
    {
        CancelPendingEnd();
        SetArmed(false);
        CPH.SetGlobalVar(VarIrlMode, true, true);

        string scene = GetString(VarStartingSoonScene, DefaultStartingSoonScene);
        if (!SwitchScene(scene))
        {
            SendChat(L(
                "IRL: kunne ikke bytte til Starting Soon - streamen ble ikke startet.",
                "IRL: could not switch to Starting Soon - stream was not started."
            ));
            return false;
        }

        bool rewardsOk = SetRewardsMode(true, false);
        if (!rewardsOk)
        {
            CPH.LogWarn("CometenIRL Admin: automatic IRL reward mode failed; continuing with OBS start.");
        }

        if (CPH.ObsIsStreaming(ObsConnection))
        {
            SendChat(L(
                "IRL: Starting Soon aktiv - streamen var allerede live. Bruk !irlgo når du er klar.",
                "IRL: Starting Soon active - stream was already live. Use !irlgo when ready."
            ));
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
            SendChat(L(
                "IRL: OBS klarte ikke å starte streamen.",
                "IRL: OBS could not start the stream."
            ));
            return false;
        }

        if (!WaitForStreaming(true, 4000))
        {
            SetRewardsMode(false, false);
            CPH.SetGlobalVar(VarIrlMode, false, true);
            SendChat(L(
                "IRL: startkommando sendt, men OBS rapporterer ikke live.",
                "IRL: start command sent, but OBS does not report streaming."
            ));
            return false;
        }

        SendChat(L(
            "IRL: Starting Soon - stream startet. Bruk !irlgo når du er klar.",
            "IRL: Starting Soon - stream started. Use !irlgo when ready."
        ));
        return true;
    }

    private bool GoLiveScene()
    {
        CancelPendingEnd();
        string scene = GetString(VarSrtScene, DefaultSrtScene);
        if (!SwitchScene(scene))
        {
            SendChat(L(
                "IRL: kunne ikke bytte til BELABOX SRT - watchdog ble ikke aktivert.",
                "IRL: could not switch to BELABOX SRT - watchdog was not armed."
            ));
            return false;
        }

        SetArmed(true);
        SendChat(L(
            "IRL: BELABOX SRT - watchdog aktiv.",
            "IRL: BELABOX SRT - watchdog armed."
        ));
        return true;
    }

    private bool GoBrb()
    {
        CancelPendingEnd();
        SetArmed(false);
        string scene = GetString(VarBrbScene, DefaultBrbScene);
        if (!SwitchScene(scene))
        {
            SendChat(L("IRL: kunne ikke bytte til BRB.", "IRL: could not switch to BRB."));
            return false;
        }

        SendChat(L("IRL: BRB - watchdog pauset.", "IRL: BRB - watchdog paused."));
        return true;
    }

    private bool BackToSrt()
    {
        CancelPendingEnd();
        string scene = GetString(VarSrtScene, DefaultSrtScene);
        if (!SwitchScene(scene))
        {
            SendChat(L(
                "IRL: kunne ikke gå tilbake til BELABOX SRT - watchdog ble ikke aktivert.",
                "IRL: could not return to BELABOX SRT - watchdog was not armed."
            ));
            return false;
        }

        SetArmed(true);
        SendChat(L(
            "IRL: tilbake på BELABOX SRT - watchdog aktiv.",
            "IRL: back on BELABOX SRT - watchdog armed."
        ));
        return true;
    }

    private bool GoEnding()
    {
        CancelPendingEnd();
        SetArmed(false);

        string scene = GetString(VarEndingScene, DefaultEndingScene);
        if (!SwitchScene(scene))
        {
            SendChat(L("IRL: kunne ikke bytte til Ending.", "IRL: could not switch to Ending."));
            return false;
        }

        if (!CPH.ObsIsStreaming(ObsConnection))
        {
            CPH.SetGlobalVar(VarIrlMode, false, true);
            SendChat(L(
                "IRL: Ending-scenen er aktiv, men OBS streamer ikke.",
                "IRL: Ending scene is active, but OBS is not streaming."
            ));
            return true;
        }

        int seconds = GetEndingSeconds();
        int sequence = GetInt(VarEndSequence, 0) + 1;
        CPH.SetGlobalVar(VarEndSequence, sequence, true);
        CPH.SetGlobalVar(VarEndPending, true, true);

        if (!CPH.ActionExists(EndAutoStopAction))
        {
            CPH.SetGlobalVar(VarEndPending, false, true);
            SendChat(L(
                "IRL: Ending er aktiv, men auto-stopp action mangler - streamen fortsetter.",
                "IRL: Ending is active, but the auto-stop action is missing - stream will continue."
            ));
            return false;
        }

        if (!CPH.RunAction(EndAutoStopAction, false))
        {
            CPH.SetGlobalVar(VarEndPending, false, true);
            SendChat(L(
                "IRL: Ending er aktiv, men auto-stopp kunne ikke startes.",
                "IRL: Ending is active, but auto-stop could not be started."
            ));
            return false;
        }

        SendChat(L(
            "IRL: Ending - streamen stopper automatisk om " + seconds + " sek.",
            "IRL: Ending - stream will stop automatically in " + seconds + " sec."
        ));
        return true;
    }

    private bool StopIrl()
    {
        CancelPendingEnd();
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
                SendChat(L(
                    "IRL: OBS klarte ikke å stoppe streamen.",
                    "IRL: OBS could not stop the stream."
                ));
                return false;
            }

            if (!WaitForStreaming(false, 4000))
            {
                SendChat(L(
                    "IRL: stoppkommando sendt, men OBS rapporterer fortsatt live.",
                    "IRL: stop command sent, but OBS still reports streaming."
                ));
                return false;
            }
        }

        bool rewardsOk = SetRewardsMode(false, false);
        if (!rewardsOk)
        {
            CPH.LogWarn("CometenIRL Admin: automatic normal reward mode failed after OBS stop.");
        }

        CPH.SetGlobalVar(VarIrlMode, false, true);
        SendChat(L(
            "IRL: stream stoppet - watchdog av.",
            "IRL: stream stopped - watchdog disarmed."
        ));
        return true;
    }

    private bool SetSceneAlias(string alias)
    {
        string key = (alias ?? string.Empty).Trim().ToLowerInvariant();

        if (key == "end" || key == "ending")
        {
            return GoEnding();
        }

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

            case "signal":
            case "lost":
                scene = GetString(VarFallbackScene, DefaultFallbackScene);
                armAfter = false;
                break;

            default:
                SendChat(L(
                    "IRL: ukjent scene-alias. Bruk soon, srt, brb, end eller signal.",
                    "IRL: unknown scene alias. Use soon, srt, brb, end or signal."
                ));
                return false;
        }

        CancelPendingEnd();
        SetArmed(false);

        if (!SwitchScene(scene))
        {
            SendChat(L("IRL: scenebytte feilet.", "IRL: scene switch failed."));
            return false;
        }

        if (armAfter)
        {
            SetArmed(true);
        }

        SendChat(L(
            "IRL: scene -> " + scene + (armAfter ? " | watchdog aktiv" : " | watchdog pauset"),
            "IRL: scene -> " + scene + (armAfter ? " | watchdog armed" : " | watchdog paused")
        ));
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
            SendChat(L(
                "IRL: bruk !irlpoints on eller !irlpoints off.",
                "IRL: use !irlpoints on or !irlpoints off."
            ));
            return false;
        }

        if (!SetRewardsMode(irlMode, true))
        {
            SendChat(L(
                "IRL: Channel Points-gruppene kunne ikke oppdateres.",
                "IRL: Channel Points groups could not be updated."
            ));
            return false;
        }

        SendChat(irlMode
            ? L("IRL: Channel Points satt til IRL-modus.", "IRL: Channel Points set to IRL mode.")
            : L("IRL: Channel Points satt tilbake til normal modus.", "IRL: Channel Points restored to normal mode."));
        return true;
    }

    private bool SetLanguage(string value)
    {
        string key = FirstWord(value).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(key))
        {
            string current = GetLanguage();
            SendChat(current == "en"
                ? "IRL: language = English (EN)."
                : "IRL: språk = Norsk (NO).");
            return true;
        }

        string language;
        if (key == "no" || key == "nb" || key == "norsk" || key == "norwegian")
        {
            language = "no";
        }
        else if (key == "en" || key == "eng" || key == "english")
        {
            language = "en";
        }
        else
        {
            SendChat(L(
                "IRL: ugyldig språk. Bruk !irllang no eller !irllang en.",
                "IRL: invalid language. Use !irllang no or !irllang en."
            ));
            return false;
        }

        CPH.SetGlobalVar(VarLanguage, language, true);
        SendChat(language == "en"
            ? "IRL: language set to English (EN). It stays English until you change it manually."
            : "IRL: språk satt til Norsk (NO). Det forblir norsk til du endrer det manuelt.");
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

    private void CancelPendingEnd()
    {
        bool pending = GetBool(VarEndPending, false);
        if (!pending)
        {
            return;
        }

        int sequence = GetInt(VarEndSequence, 0) + 1;
        CPH.SetGlobalVar(VarEndSequence, sequence, true);
        CPH.SetGlobalVar(VarEndPending, false, true);
        CPH.LogInfo("CometenIRL Admin: pending ending auto-stop cancelled.");
    }

    private int GetEndingSeconds()
    {
        int seconds = GetInt(VarEndingSeconds, DefaultEndingSeconds);
        if (seconds < 5) seconds = 5;
        if (seconds > 120) seconds = 120;
        return seconds;
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
            case "irllang": action = "language"; parameter = FirstWord(rawInput); return true;
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

    private string GetLanguage()
    {
        string value = GetString(VarLanguage, "no").Trim().ToLowerInvariant();
        return value == "en" ? "en" : "no";
    }

    private string L(string norwegian, string english)
    {
        return GetLanguage() == "en" ? english : norwegian;
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

    private int GetInt(string name, int fallback)
    {
        try
        {
            string text = (CPH.GetGlobalVar<string>(name, true) ?? string.Empty).Trim();
            int parsed;
            if (int.TryParse(text, out parsed)) return parsed;
        }
        catch
        {
        }

        try
        {
            return CPH.GetGlobalVar<int>(name, true);
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
