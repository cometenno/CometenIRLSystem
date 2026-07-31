using System;

public class CPHInline
{
    public bool Execute()
    {
        string alertType = FirstArg("alertType", "alert", "eventType", "type");
        string eventType = NormalizeAlertType(alertType);

        if (string.IsNullOrWhiteSpace(eventType))
        {
            CPH.LogWarn("CometenIRL: CWA alert type is unsupported: " + alertType);
            return true;
        }

        args["eventType"] = eventType;

        string userName = FirstArg("userName", "user", "displayName", "username");
        if (!string.IsNullOrWhiteSpace(userName))
        {
            args["userName"] = userName;
        }

        string amount = FirstArg("amount", "count", "viewers", "viewerCount", "bits", "months");
        if (!string.IsNullOrWhiteSpace(amount))
        {
            args["amount"] = amount;
        }

        string message = FirstArg("message", "text", "msg");
        if (!string.IsNullOrWhiteSpace(message))
        {
            args["message"] = message;
        }

        args["sound"] = SoundFor(eventType);

        CPH.RunAction("Cometen IRL Notifications - Send", false);
        return true;
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

    private static string NormalizeAlertType(string value)
    {
        string key = (value ?? string.Empty)
            .Trim()
            .ToLowerInvariant()
            .Replace("-", "_")
            .Replace(" ", "_");

        switch (key)
        {
            case "follow":
            case "follower":
                return "follow";
            case "sub":
            case "subscription":
                return "sub";
            case "resub":
            case "re_sub":
            case "resubscription":
                return "resub";
            case "gifted":
            case "giftsub":
            case "gift_sub":
            case "gifted_sub":
                return "gifted";
            case "giftbomb":
            case "gift_bomb":
            case "community_gift":
            case "communitygift":
                return "giftbomb";
            case "bits":
            case "cheer":
                return "bits";
            case "donation":
            case "charity":
                return "donation";
            case "raid":
                return "raid";
            case "yt_sub":
            case "youtube_sub":
            case "youtubesub":
                return "youtubesub";
            default:
                return string.Empty;
        }
    }

    private static string SoundFor(string eventType)
    {
        switch (eventType)
        {
            case "follow": return "follow.wav";
            case "sub": return "sub.wav";
            case "resub": return "resub.wav";
            case "gifted": return "gifted.wav";
            case "giftbomb": return "giftbomb.wav";
            case "bits": return "bits.wav";
            case "donation": return "donation.wav";
            case "raid": return "raid.wav";
            case "youtubesub": return "sub.wav";
            default: return "test.wav";
        }
    }
}
