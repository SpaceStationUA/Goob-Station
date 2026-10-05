# Pirate Publishing Deployment

Prepared for Alwyzon. Do not activate while players are online unless the operator explicitly authorizes cutover.

## Layout

- `/home/alwyzon/ss14-hosting/watchdog/bin`: official SS14.Watchdog v1.1 release, .NET 10.
- `/home/alwyzon/ss14-hosting/cdn/bin`: official `ghcr.io/space-wizards/robust.cdn:2` linux/amd64 payload, digest `sha256:2d030b6da829758b3b7de631faea61bbff6ea37edee6c87c81e190cd2d0db54d`.
- New client/server CDN: `http://31.14.17.169:27691/`, fork `pirate-ua`. Old Apache `/builds` and Robust.Cdn on `27690` stay unchanged for existing clients and replays.
- Watchdog API: `http://127.0.0.1:5000/`, instance `pirate`. Never expose this API publicly.
- Production units require `/home/alwyzon/ss14-hosting/ACTIVATED`; both units remain disabled until cutover.
- Tokens live under `secrets/`, not in Git. GitHub has `PUBLISH_TOKEN`, `PUBLISH_SSH_KEY`, `PUBLISH_SSH_KNOWN_HOSTS`. The `ss14-publish` SSH account is restricted to forwarding to `127.0.0.1:27691`; no shell or general forwarding.
- `PUBLISH_ENABLED=false` prevents scheduled publication. Manual Publish dispatch defaults to build-only and uploads `pirate-release` without notifying servers.
- Install the committed `run-game.sh` and `run-server.py` into `/home/alwyzon/ss14-hosting/` before enabling Watchdog. The production service runs those host paths, not files inside the Git checkout.

## Cutover, Only On Operator Authorization

1. Merge the publishing PR. Pull committed `ss14bot` changes with `git pull --ff-only`; leave `SERVER_CONSOLE_DRIVER=tmux` until the game changes over.
2. Create `ACTIVATED`, start **only** `pirate-cdn`, and verify `/control/status`. Enable `PUBLISH_ENABLED`, dispatch Publish from `master` with `publish=true`, then wait for the new version in `/fork/pirate-ua/manifest`. `NotifyWatchdogs` must still be empty. Do not launch the game against an empty manifest.
3. Back up `/home/alwyzon/config.toml`, root crontab and the current deployment scripts. Remove the root cron entry invoking `pirates_commands/restart.sh`; inspect root and alwyzon `atq` for pending updates. The old monitor and the new Watchdog must never run together.
4. At the agreed round boundary, run the existing `sudo pirates_commands/stop.sh` for a graceful stop. Confirm game-session and the old monitor have exited.
5. Copy the current `config.toml` into `watchdog/instances/pirate/config.toml`, **removing the entire `[build]` section** so stale numeric version/client URLs cannot override the CDN-injected `build.json`. Preserve all other settings. Symlink `watchdog/instances/pirate/data` to `/home/alwyzon/Pirate/data`; repair ownership for files written by the old root process so the alwyzon service can write them. Take a data backup first. Install the committed wrapper files as described above; the wrapper uses redirected pipes, not a PTY, to avoid the Robust console cursor-query hang while preserving the local command socket and old log path.
6. Start `pirate-watchdog`. Verify `/status` and `/info` on 1212: correct published SHA, fork `pirate-ua`, client URLs on 27691, expected database/player data and voice URLs. Enable both units only after this check.
7. Set `SERVER_CONSOLE_DRIVER=socket` in the bot `.env`, clear config cache and restart only its console process. Verify a harmless Discord `help` command and resulting output. Install `console.logrotate` as `/etc/logrotate.d/pirate-console`.
8. Add one `NotifyWatchdogs` entry to CDN config with URL `http://127.0.0.1:5000/`, instance `pirate`, and the private watchdog token. Restart **only** the CDN to load the notification config. Future Publish completions notify Watchdog; content waits for round end (or the server-empty restart delay) before shutdown, download and restart.

Current live state after cutover: `pirate-cdn.service` and `pirate-watchdog.service` are enabled and active, `ACTIVATED` exists, and the game is launched through redirected pipes (no PTY). Before any manual restart, check `/status` player count; restart is permitted at 0–10 players and must be deferred above 10 unless the operator explicitly accepts the disruption.

## Rollback

Disable `PUBLISH_ENABLED` and remove CDN notifications. Stop the new Watchdog before starting the old monitor. Restore the bot driver to `tmux`, restart its console, restore the saved cron and use the unchanged `pirates_commands/start.sh`. The original server package, config, old CDN databases and URLs remain in place; the live game data is shared, not duplicated. Watchdog also supports `/instances/pirate/revert` for a published previous version, defaulting to round-end application.

Sources: [Watchdog setup](https://docs.spacestation14.com/en/server-hosting/setting-up-ss14-watchdog.html), [Robust.Cdn setup](https://docs.spacestation14.com/en/server-hosting/setting-up-robust-cdn.html).
