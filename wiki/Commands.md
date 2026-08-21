# Commands

Full maintained reference:

https://github.com/la1ona/CometenIRLSystem/blob/main/docs/COMMANDS.md

## Status / speaker

```text
!irlstatus
!alerttest
!volum 0-100
!vol 0-100
!vol75
!volum75
!volup
!voldown
!mute
!unmute
```

## OBS IRL admin

Broadcaster only is recommended:

```text
!irlstart
!irlgo
!irlbrb
!irlback
!irlend
!irlstop
!irlscene <alias>
!irlpoints on|off
!irllang no|en
```

Scene aliases:

```text
soon / starting / start
srt / live / go
brb / pause
end / ending
signal / lost
```

## Browser Audio

Broadcaster + moderators:

```text
!irlaudio status
!irlaudio on
!irlaudio off
!irlaudio restart
!irlaudio add <name> <url>
!irlaudio remove <name>
!irlaudio <name> status
!irlaudio <name> on
!irlaudio <name> off
!irlaudio <name> restart
```

Aliases for remove:

```text
delete
del
```

Private URL-bearing add messages should be deleted from Twitch chat after Streamer.bot captures them.
