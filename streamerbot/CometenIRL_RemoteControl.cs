using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

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
            SendChat("IRL: relay-oppsettet mangler.");
            return false;
        }

        string action = FirstArg("controlAction", "remoteAction").Trim().ToLowerInvariant();
        int value = FirstIntArg("controlValue", "remoteValue", "amount");

        if (string.IsNullOrWhiteSpace(action))
        {
            string input = FirstArg("input0", "rawInput", "command", "message");
            if (!TryParseCommand(input, out action, out value))
            {
                CPH.LogError("CometenIRL Remote: Could not parse remote command: " + input);
                SendChat("IRL: ugyldig remote-kommando.");
                return false;
            }
        }

        if (!ValidateAction(action, ref value))
        {
            CPH.LogError("CometenIRL Remote: Invalid action/value: " + action + " / " + value);
            SendChat("IRL: ugyldig kommando eller verdi.");
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
                        SendChat("IRL: relay avviste kommandoen.");
                        return false;
                    }
                }
            }

            CPH.LogInfo("CometenIRL Remote: Sent " + action + (action == "volume_set" ? " " + value + "%" : "") + ". Waiting for BELABOX confirmation.");

            bool resultOk;
            string resultMessage;
            if (WaitForResult(relayBaseUrl, senderToken, eventId, out resultOk, out resultMessage))
            {
                if (string.IsNullOrWhiteSpace(resultMessage))
                {
                    resultMessage = resultOk ? "IRL: kommando utført." : "IRL: kommando feilet.";
                }

                CPH.LogInfo("CometenIRL Remote: BELABOX result: " + resultMessage);
                SendChat(resultMessage);
                return resultOk;
            }

            CPH.LogError("CometenIRL Remote: No BELABOX confirmation received for " + eventId + ".");
            SendChat("IRL: ingen bekreftelse fra BELABOX.");
            return false;
        }
        catch (Exception exception)
        {
            CPH.LogError("CometenIRL Remote: Send failed: " + exception.Message);
            SendChat("IRL: kunne ikke kontakte relay/BELABOX.");
            return false;
        }
    }

    private bool WaitForResult(string relayBaseUrl, string senderToken, string eventId, out bool resultOk, out string resultMessage)
    {
        resultOk = false;
        resultMessage = string.Empty;
        string endpoint = relayBaseUrl.TrimEnd('/') + "/control_result.php?id=" + Uri.EscapeDataString(eventId);

        for (int attempt = 0; attempt < 20; attempt++)
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, endpoint))
            {
                request.Headers.TryAddWithoutValidation("X-Cometen-Token", senderToken.Trim());

                using (HttpResponseMessage response = Http.SendAsync(request).GetAwaiter().GetResult())
                {
                    string body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    if (!response.IsSuccessStatusCode)
                    {
                        CPH.LogError("CometenIRL Remote: Result endpoint HTTP " + (int)response.StatusCode + ": " + body);
                        return false;
                    }

                    if (body.IndexOf("\"ready\":true", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        resultOk = body.IndexOf("\"result_ok\":true", StringComparison.OrdinalIgnoreCase) >= 0;
                        resultMessage = ExtractJsonString(body, "message");
                        return true;
                    }
                }
            }

            Thread.Sleep(250);
        }

        return false;
    }

    private void SendChat(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        CPH.SendMessage(message.Trim(), true, true);
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

        if (text == "!irlstatus" || text == "irlstatus" || text == "status")
        {
            action = "status";
            return true;
        }

        if (text == "!alerttest" || text == "alerttest")
        {
            action = "alert_test";
            return true;
        }

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
            case "status":
            case "alert_test":
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

    private static string ExtractJsonString(string json, string key)
    {
        Match match = Regex.Match(
            json ?? string.Empty,
            "\\\"" + Regex.Escape(key) + "\\\"\\s*:\\s*\\\"((?:\\\\.|[^\\\"\\\\])*)\\\"",
            RegexOptions.IgnoreCase
        );

        if (!match.Success)
        {
            return string.Empty;
        }

        return JsonUnescape(match.Groups[1].Value);
    }

    private static string JsonUnescape(string value)
    {
        if (value == null)
        {
            return string.Empty;
        }

        return value
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\t", "\t")
            .Replace("\\\"", "\"")
            .Replace("\\\\", "\\");
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
