# Twitch Chat URL Guard

The URL Guard is a Streamer.bot moderation/helper action used to keep sensitive URLs out of public Twitch chat while still allowing Streamer.bot to process URL-bearing commands.

## Intended policy

Normal URLs:

- broadcaster - allowed
- moderators - allowed
- VIPs - allowed
- other users - deleted, with an optional short explanation

URL-bearing commands:

- Streamer.bot receives the command event first
- the original Twitch message is deleted afterward
- the command action continues using the already-captured arguments

This allows workflows such as:

```text
!sr <url>
!irlaudio add <name> <url>
```

without leaving the URL visible in public chat.

## Where it runs

Streamer.bot on the streaming PC.

Use **one** global URL Guard action connected to:

```text
Twitch -> Chat -> Chat Message
```

Do not run multiple guards in parallel, because the same chat event could be deleted/handled twice.

## Recommended CometenWebAdmin organization

The URL Guard belongs logically under CometenWebAdmin/global chat moderation rather than under the BELABOX receiver, because it protects all Twitch chat URL workflows, including IRL Browser Audio and the SR system.

Suggested actions:

```text
CWA - URL Guard
CWA - URL Guard Status
CWA - URL Guard Save Settings
CWA - Set URL Guard Globals
CWA - URL Guard Reset Session
```

Only the runtime guard action needs the Twitch Chat Message trigger. The other actions can be called from the WebAdmin panel.

## Recommended configurable settings

The WebAdmin panel can expose persisted settings for:

- URL Guard enabled/disabled
- allow broadcaster URLs
- allow moderator URLs
- allow VIP URLs
- delete URLs from other users
- delete URL-bearing commands after capture
- announce blocked ordinary URLs
- editable blocked-message text
- additional allowed usernames
- blocked URL count for the current session

## URL-bearing command behavior

Example:

```text
!irlaudio add blerp https://PRIVATE_BROWSER_SOURCE_URL
```

Expected sequence:

```text
Twitch sends chat event
-> Streamer.bot receives command + msgId + URL
-> Browser Audio action parses/captures URL
-> URL Guard / command action deletes original Twitch message
-> control event is sent to BELABOX
-> BELABOX stores URL locally
-> chat receives a short confirmation without the URL
```

The deletion happens after the event is already available to Streamer.bot, so deleting the Twitch message does not remove the command arguments from the currently executing action.

## Browser Audio safety

`CometenIRL_BrowserAudioControl.cs` also attempts to delete its own URL-bearing add command immediately after capture. This provides a direct safety layer for private Browser Source URLs.

A global URL Guard is still useful because it protects other URL workflows such as SR requests and ordinary viewer links.

## Permissions

The bot/broadcaster account used to delete messages must have sufficient Twitch moderation permissions.

The code should use the Twitch `msgId` from the chat event rather than trying to match/delete messages by text.

## Privacy rule

Never log or echo a private Browser Source URL just to confirm that a command was received.

Good:

```text
IRL Audio: blerp added
```

Bad:

```text
IRL Audio: added https://private-token-url/...
```

## Testing

Recommended test order:

1. post a normal URL as broadcaster -> remains visible
2. post a normal URL as moderator -> remains visible
3. post a normal URL as VIP -> remains visible if VIPs are enabled
4. post a normal URL as ordinary viewer -> message is deleted + optional short notice
5. run `!sr <url>` -> command processes while URL message is deleted
6. run `!irlaudio add <name> <url>` -> Browser Audio source is added while URL message is deleted
7. confirm neither guard nor command logs expose the URL

## Verification status

The `!irlaudio add` URL-bearing command deletion has been observed working on the live setup.

The broader configurable WebAdmin URL Guard should be separately tested after the final WebAdmin actions/panel are installed.

## Related documentation

- [Browser Audio](BROWSER_AUDIO.md)
- [Commands](COMMANDS.md)
- [Streamer.bot setup](streamerbot-setup.md)
