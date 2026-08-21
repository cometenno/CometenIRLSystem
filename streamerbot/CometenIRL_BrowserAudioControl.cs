using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

// CometenIRLAlerts - Browser Audio chat control v0.1
// Command: !irlaudio (Starts With)
// Broadcaster/mod only. Browser Source URLs are deleted from Twitch after capture.

public class CPHInline
{
    private static readonly HttpClient Http = new HttpClient();

    public bool Execute()
    {
        string input = BuildInput();
        string action, payload;
        if (!TryParse(input, out action, out payload))
        {
            CPH.SendMessage("IRL Audio: ugyldig kommando.", true, true);
            return false;
        }

        // Delete URL-bearing add command immediately, even before role validation.
        if (action == "browser_audio_add")
            DeleteSourceMessage();

        if (!AdminAllowed())
        {
            CPH.SendMessage("IRL Audio: krever broadcaster/mod.", true, true);
            return false;
        }

        string relay = GetGlobal("CometenIRL_RelayUrl");
        string token = GetGlobal("CometenIRL_SenderToken");
        if (string.IsNullOrWhiteSpace(relay) || string.IsNullOrWhiteSpace(token))
        {
            CPH.SendMessage("IRL Audio: relay-oppsettet mangler.", true, true);
            return false;
        }

        string id = "ctl-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
            + "-" + Guid.NewGuid().ToString("N").Substring(0, 12);
        string message = string.IsNullOrWhiteSpace(payload) ? action : action + " " + payload;
        string user = FirstArg("userName", "user", "displayName");

        string json = "{"
            + "\"id\":\"" + JsonEscape(id) + "\","
            + "\"type\":\"control\","
            + "\"user\":\"" + JsonEscape(user) + "\","
            + "\"amount\":0,"
            + "\"message\":\"" + JsonEscape(message) + "\","
            + "\"sound\":\"test.wav\","
            + "\"priority\":100}"
            ;

        try
        {
            using (HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Post, relay.TrimEnd('/') + "/push.php"))
            {
                req.Headers.TryAddWithoutValidation("X-Cometen-Token", token.Trim());
                req.Content = new StringContent(json, Encoding.UTF8, "application/json");
                using (HttpResponseMessage res = Http.SendAsync(req).GetAwaiter().GetResult())
                {
                    if (!res.IsSuccessStatusCode)
                    {
                        CPH.LogError("IRL Browser Audio: relay rejected command, HTTP " + (int)res.StatusCode);
                        CPH.SendMessage("IRL Audio: relay avviste kommandoen.", true, true);
                        return false;
                    }
                }
            }

            bool ok;
            string result;
            if (!WaitForResult(relay, token, id, out ok, out result))
            {
                CPH.SendMessage("IRL Audio: ingen bekreftelse fra BELABOX.", true, true);
                return false;
            }

            CPH.SendMessage(string.IsNullOrWhiteSpace(result)
                ? (ok ? "IRL Audio: kommando utført." : "IRL Audio: kommando feilet.")
                : result.Trim(), true, true);
            return ok;
        }
        catch (Exception ex)
        {
            CPH.LogError("IRL Browser Audio control failed: " + ex.Message);
            CPH.SendMessage("IRL Audio: kunne ikke kontakte relay/BELABOX.", true, true);
            return false;
        }
    }

    private string BuildInput()
    {
        string message = FirstArg("message");
        if (!string.IsNullOrWhiteSpace(message)) return message.Trim();

        string command = FirstArg("command", "commandName").Trim();
        string raw = FirstArg("rawInput").Trim();
        if (string.IsNullOrWhiteSpace(raw)) raw = FirstArg("input0").Trim();

        string clean = command.TrimStart('!').ToLowerInvariant();
        if (clean == "irlaudio")
            return "!irlaudio" + (string.IsNullOrWhiteSpace(raw) ? "" : " " + raw);
        return string.IsNullOrWhiteSpace(raw) ? command : raw;
    }

    private bool TryParse(string input, out string action, out string payload)
    {
        action = "";
        payload = "";
        string text = Regex.Replace((input ?? "").Trim(), @"^!?irlaudio\b", "", RegexOptions.IgnoreCase).Trim();
        if (text == "") { action = "browser_audio_status"; return true; }

        string[] first = text.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
        string verb = first[0].ToLowerInvariant();
        string rest = first.Length > 1 ? first[1].Trim() : "";

        if (verb == "on") { action = "browser_audio_master_on"; return true; }
        if (verb == "off") { action = "browser_audio_master_off"; return true; }
        if (verb == "status" || verb == "list") { action = "browser_audio_status"; return true; }
        if (verb == "restart") { action = "browser_audio_restart"; return true; }

        if (verb == "add")
        {
            string[] p = rest.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length != 2) return false;
            string name = p[0].ToLowerInvariant();
            string url = p[1].Trim();
            if (!ValidName(name) || !ValidUrl(url)) return false;
            payload = name + " " + url;
            if (("browser_audio_add " + payload).Length > 250) return false;
            action = "browser_audio_add";
            return true;
        }

        if (verb == "remove" || verb == "delete" || verb == "del")
        {
            string name = rest.ToLowerInvariant();
            if (!ValidName(name)) return false;
            action = "browser_audio_remove";
            payload = name;
            return true;
        }

        if (!ValidName(verb) || rest == "") return false;
        string sourceVerb = rest.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)[0].ToLowerInvariant();
        payload = verb;
        if (sourceVerb == "on") action = "browser_audio_source_on";
        else if (sourceVerb == "off") action = "browser_audio_source_off";
        else if (sourceVerb == "restart") action = "browser_audio_source_restart";
        else if (sourceVerb == "status") action = "browser_audio_source_status";
        else return false;
        return true;
    }

    private bool ValidName(string value)
    {
        return Regex.IsMatch((value ?? "").Trim(), @"^[a-z0-9][a-z0-9_-]{0,31}$", RegexOptions.IgnoreCase);
    }

    private bool ValidUrl(string value)
    {
        string url = (value ?? "").Trim();
        if (url.Length < 8 || url.Length > 190) return false;
        Uri parsed;
        if (!Uri.TryCreate(url, UriKind.Absolute, out parsed)) return false;
        return parsed.Scheme == Uri.UriSchemeHttps || parsed.Scheme == Uri.UriSchemeHttp;
    }

    private bool AdminAllowed()
    {
        if (string.IsNullOrWhiteSpace(FirstArg("msgId", "messageId"))) return true;
        if (BoolArg("isBroadcaster", "broadcaster")) return true;
        if (BoolArg("isModerator", "isMod", "moderator")) return true;
        string role = FirstArg("role").ToLowerInvariant();
        return role.Contains("broadcaster") || role.Contains("moderator") || role == "mod";
    }

    private void DeleteSourceMessage()
    {
        string msgId = FirstArg("msgId", "messageId");
        if (string.IsNullOrWhiteSpace(msgId)) return;
        try
        {
            if (CPH.TwitchDeleteChatMessage(msgId, true)) return;
        }
        catch { }
        try
        {
            if (CPH.TwitchDeleteChatMessage(msgId, false)) return;
        }
        catch { }
        CPH.LogWarn("IRL Browser Audio: could not delete URL command from Twitch chat.");
    }

    private bool WaitForResult(string relay, string token, string id, out bool ok, out string message)
    {
        ok = false;
        message = "";
        string endpoint = relay.TrimEnd('/') + "/control_result.php?id=" + Uri.EscapeDataString(id);
        for (int i = 0; i < 20; i++)
        {
            using (HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, endpoint))
            {
                req.Headers.TryAddWithoutValidation("X-Cometen-Token", token.Trim());
                using (HttpResponseMessage res = Http.SendAsync(req).GetAwaiter().GetResult())
                {
                    string body = res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    if (!res.IsSuccessStatusCode) return false;
                    if (body.IndexOf("\"ready\":true", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        ok = body.IndexOf("\"result_ok\":true", StringComparison.OrdinalIgnoreCase) >= 0;
                        message = ExtractJsonString(body, "message");
                        return true;
                    }
                }
            }
            Thread.Sleep(250);
        }
        return false;
    }

    private string GetGlobal(string name)
    {
        try { return (CPH.GetGlobalVar<string>(name, true) ?? "").Trim(); }
        catch { return ""; }
    }

    private bool BoolArg(params string[] names)
    {
        foreach (string name in names)
        {
            object value;
            if (args.TryGetValue(name, out value) && value != null)
            {
                bool b;
                if (bool.TryParse(value.ToString(), out b)) return b;
                string s = value.ToString().Trim().ToLowerInvariant();
                if (s == "1" || s == "yes" || s == "on") return true;
            }
        }
        return false;
    }

    private string FirstArg(params string[] names)
    {
        foreach (string name in names)
        {
            object value;
            if (args.TryGetValue(name, out value) && value != null && !string.IsNullOrWhiteSpace(value.ToString()))
                return value.ToString().Trim();
        }
        return "";
    }

    private static string ExtractJsonString(string json, string key)
    {
        Match m = Regex.Match(json ?? "", "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*\\\"((?:\\\\.|[^\\\"\\\\])*)\\\"", RegexOptions.IgnoreCase);
        if (!m.Success) return "";
        return m.Groups[1].Value.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\t", "\t").Replace("\\\"", "\"").Replace("\\\\", "\\");
    }

    private static string JsonEscape(string value)
    {
        if (value == null) return "";
        StringBuilder b = new StringBuilder(value.Length + 16);
        foreach (char c in value)
        {
            switch (c)
            {
                case '\\': b.Append("\\\\"); break;
                case '"': b.Append("\\\""); break;
                case '\n': b.Append("\\n"); break;
                case '\r': b.Append("\\r"); break;
                case '\t': b.Append("\\t"); break;
                default:
                    if (c < 32) b.Append("\\u" + ((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else b.Append(c);
                    break;
            }
        }
        return b.ToString();
    }
}
