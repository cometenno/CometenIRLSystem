using System;
using System.Text.RegularExpressions;

// Cometen Chat URL Guard v0.1
// Attach to Twitch -> Chat -> Chat Message.
// - URL commands are deleted after Streamer.bot receives the event.
// - Ordinary URLs are allowed only for broadcaster, moderators and VIPs.
// - Never logs the URL itself.

public class CPHInline
{
    private static readonly Regex UrlRegex = new Regex(
        @"(?ix)(?:https?://|www\.)\S+|\b[a-z0-9](?:[a-z0-9-]{0,62}\.)+[a-z]{2,24}(?:[/?#]\S*)?",
        RegexOptions.Compiled | RegexOptions.CultureInvariant
    );

    public bool Execute()
    {
        string message = FirstArg("message", "rawInput").Trim();
        if (string.IsNullOrWhiteSpace(message) || !UrlRegex.IsMatch(message))
            return true;

        string msgId = FirstArg("msgId", "messageId").Trim();
        string user = FirstArg("userName", "user", "displayName").Trim();
        bool isCommand = message.TrimStart().StartsWith("!", StringComparison.Ordinal);

        // URL-bearing commands are always removed once the chat event is captured.
        // The command action itself owns any success/error confirmation.
        if (isCommand)
        {
            if (!DeleteMessage(msgId))
                CPH.LogWarn("Cometen URL Guard: could not delete URL command message (missing/invalid msgId or insufficient Twitch permissions).");
            return true;
        }

        if (AllowedToPostUrls())
            return true;

        bool deleted = DeleteMessage(msgId);
        if (!deleted)
        {
            CPH.LogWarn("Cometen URL Guard: could not delete URL message (missing/invalid msgId or insufficient Twitch permissions).");
            return true;
        }

        if (!string.IsNullOrWhiteSpace(user))
            CPH.SendMessage("@" + user + " lenker er kun tillatt for VIP/mods.", true, true);
        else
            CPH.SendMessage("Lenker er kun tillatt for VIP/mods.", true, true);

        return true;
    }

    private bool AllowedToPostUrls()
    {
        if (BoolArg("isBroadcaster", "broadcaster")) return true;
        if (BoolArg("isModerator", "isMod", "moderator")) return true;
        if (BoolArg("isVip", "isVIP", "vip")) return true;

        string role = FirstArg("role").Trim().ToLowerInvariant();
        return role.Contains("broadcaster") || role.Contains("moderator") || role == "mod" || role.Contains("vip");
    }

    private bool DeleteMessage(string msgId)
    {
        if (string.IsNullOrWhiteSpace(msgId))
            return false;

        try
        {
            if (CPH.TwitchDeleteChatMessage(msgId, true))
                return true;
        }
        catch { }

        try
        {
            return CPH.TwitchDeleteChatMessage(msgId, false);
        }
        catch
        {
            return false;
        }
    }

    private bool BoolArg(params string[] names)
    {
        foreach (string name in names)
        {
            object value;
            if (args.TryGetValue(name, out value) && value != null)
            {
                bool parsed;
                if (bool.TryParse(value.ToString(), out parsed))
                    return parsed;

                string text = value.ToString().Trim().ToLowerInvariant();
                if (text == "1" || text == "yes" || text == "on") return true;
            }
        }
        return false;
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
                    return text.Trim();
            }
        }
        return string.Empty;
    }
}
