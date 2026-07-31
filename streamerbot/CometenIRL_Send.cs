using System;
using System.Globalization;
using System.Net.Http;
using System.Text;

public class CPHInline
{
    private static readonly HttpClient Http = new HttpClient();

    public bool Execute()
    {
        string relayBaseUrl = CPH.GetGlobalVar<string>("CometenIRL_RelayUrl", true);
        string senderToken = CPH.GetGlobalVar<string>("CometenIRL_SenderToken", true);

        if (string.IsNullOrWhiteSpace(relayBaseUrl) || string.IsNullOrWhiteSpace(senderToken))
        {
            CPH.LogError("CometenIRL: Missing global variables CometenIRL_RelayUrl or CometenIRL_SenderToken.");
            return false;
        }

        string eventType = FirstArg("eventType", "type").ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(eventType))
        {
            eventType = "test";
        }

        string userName = FirstArg("user", "userName", "displayName", "targetUser");
        int amount = FirstIntArg("amount", "viewerCount", "viewers", "bits", "cumulativeMonths");
        int priority = FirstIntArg("priority");
        string message = FirstArg("message", "text");
        string sound = FirstArg("sound");

        if (string.IsNullOrWhiteSpace(sound))
        {
            sound = eventType + ".wav";
        }

        if (priority == 0)
        {
            priority = DefaultPriority(eventType);
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            message = DefaultMessage(eventType, userName, amount);
        }

        string eventId = "evt-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)
            + "-" + Guid.NewGuid().ToString("N").Substring(0, 12);

        string json = "{"
            + "\"id\":\"" + JsonEscape(eventId) + "\","
            + "\"type\":\"" + JsonEscape(eventType) + "\","
            + "\"user\":\"" + JsonEscape(userName) + "\","
            + "\"amount\":" + amount.ToString(CultureInfo.InvariantCulture) + ","
            + "\"message\":\"" + JsonEscape(message) + "\","
            + "\"sound\":\"" + JsonEscape(sound) + "\","
            + "\"priority\":" + priority.ToString(CultureInfo.InvariantCulture)
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
                        CPH.LogError("CometenIRL: Relay returned HTTP " + (int)response.StatusCode + ": " + responseBody);
                        return false;
                    }

                    CPH.LogInfo("CometenIRL: Sent " + eventType + " event " + eventId + ".");
                    return true;
                }
            }
        }
        catch (Exception exception)
        {
            CPH.LogError("CometenIRL: Send failed: " + exception.Message);
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

    private static int DefaultPriority(string eventType)
    {
        switch (eventType)
        {
            case "system":
            case "moderator":
                return 90;
            case "raid":
                return 70;
            case "giftsub":
            case "sub":
            case "resub":
                return 50;
            case "bits":
                return 40;
            case "follow":
                return 20;
            default:
                return 10;
        }
    }

    private static string DefaultMessage(string eventType, string userName, int amount)
    {
        switch (eventType)
        {
            case "follow":
                return string.IsNullOrWhiteSpace(userName) ? "Ny følger" : "Ny følger: " + userName;
            case "sub":
                return string.IsNullOrWhiteSpace(userName) ? "Ny sub" : "Ny sub: " + userName;
            case "resub":
                return string.IsNullOrWhiteSpace(userName) ? "Resub" : "Resub: " + userName;
            case "giftsub":
                return amount > 0 ? "Gift subs: " + amount : "Gift sub";
            case "raid":
                return amount > 0 ? "Raid med " + amount + " seere" : "Ny raid";
            case "bits":
                return amount > 0 ? amount + " bits" : "Nye bits";
            case "moderator":
                return "Viktig beskjed fra moderator";
            case "system":
                return "Teknisk varsel";
            default:
                return "Cometen IRL test";
        }
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
