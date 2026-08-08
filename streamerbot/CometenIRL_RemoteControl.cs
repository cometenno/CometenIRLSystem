using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

public class CPHInline
{
    private static readonly HttpClient Http = new HttpClient();

    public bool Execute()
    {
        string relayBaseUrl = CPH.GetGlobalVar<string>("CometenIRL_RelayUrl", true);
        string senderToken = CPH.GetGlobalVar<string>("CometenIRL_SenderToken", true);

        if (string.IsNullOrWhiteSpace(relayBaseUrl) || string.IsNullOrWhiteSpace(senderToken))
        {
            CPH.LogError("CometenIRL Remote: Missing CometenIRL_RelayUrl or CometenIRL_SenderToken.");
            return false;
        }

        string action = FirstArg("controlAction", "remoteAction").Trim().ToLowerInvariant();
        int value = FirstIntArg("controlValue", "remoteValue", "amount");

        if (string.IsNullOrWhiteSpace(action))
        {
            // For Streamer.bot command triggers, input0 is normally the first value after the command.
            // Prefer it over rawInput so "!volum 30" reliably resolves to "30".
            string input = FirstArg("input0", "rawInput", "command", "message");
            if (!TryParseCommand(input, out action, out value))
            {
                CPH.LogError("CometenIRL Remote: Could not parse remote command: " + input);
                return false;
            }
        }

        if (!ValidateAction(action, ref value))
        {
            CPH.LogError("CometenIRL Remote: Invalid action/value: " + action + " / " + value);
            return false;
        }

        string userName = FirstArg("user", "userName", "displayName");
        string eventId = "ctl-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
            + "-" + Guid.NewGuid().ToString("N").Substring(0, 12);

        string json = "{"
            + "\"id\":\"" + JsonEscape(eventId) + "\","
            + "\"type\":\"control\","
            + "\"user\":\"" + JsonEscape(userName) + "\","
            + "\"amount\":" + value.ToString(CultureInfo.InvariantCulture) + ","
            + "\"message\":\"" + JsonEscape(action) + "\","
            + "\"sound\":\"test.wav\","
            + "\"priority\":100"
            + "}";

        string endpoint = relayBaseUrl.TrimEnd('/') + "/push.php";

        try
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, endpoint))
            {
                request.Headers.TryAddWithoutValidation("X-Cometen-Token", senderToken.Trim());
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = Http.SendAsync(request).GetAwaiter().GetResult())
                {
                    string responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode)
                    {
                        CPH.LogError("CometenIRL Remote: Relay returned HTTP " + (int)response.StatusCode + ": " + responseBody);
                        return false;
                    }

                    CPH.LogInfo("CometenIRL Remote: Sent " + action + (action == "volume_set" ? " " + value + "%" : "") + ".");
                    return true;
                }
            }
        }
        catch (Exception exception)
        {
            CPH.LogError("CometenIRL Remote: Send failed: " + exception.Message);
            return false;
        }
    }

    private bool TryParseCommand(string input, out string action, out int value)
    {
        action = string.Empty;
        value = 0;
        string text = (input ?? string.Empty).Trim().ToLowerInvariant();

        if (text == "!volup" || text == "volup")
        {
            action = "volume_up";
            return true;
        }

        if (text == "!voldown" || text == "voldown")
        {
            action = "volume_down";
            return true;
        }

        if (text == "!mute" || text == "mute")
        {
            action = "mute";
            return true;
        }

        if (text == "!unmute" || text == "unmute")
        {
            action = "unmute";
            return true;
        }

        // Accept: 30, !vol30, !vol 30, !volum30 and !volum 30.
        Match match = Regex.Match(text, @"^!?vol(?:um)?\s*(\d{1,3})$");
        if (!match.Success)
        {
            match = Regex.Match(text, @"^(\d{1,3})$");
        }

        if (match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        {
            action = "volume_set";
            return value >= 0 && value <= 100;
        }

        return false;
    }

    private bool ValidateAction(string action, ref int value)
    {
        switch (action)
        {
            case "volume_set":
                return value >= 0 && value <= 100;
            case "volume_up":
            case "volume_down":
            case "mute":
            case "unmute":
                value = 0;
                return true;
            default:
                return false;
        }
    }

    private string FirstArg(params string[] names)
    {
        foreach (string name in names)
        {
            object value;
            if (args.TryGetValue(name, out value) && value != null)
            {
                string text = value.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text.Trim();
                }
            }
        }

        return string.Empty;
    }

    private int FirstIntArg(params string[] names)
    {
        string value = FirstArg(names);
        int parsed;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed) ? parsed : 0;
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
                        builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
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
}
