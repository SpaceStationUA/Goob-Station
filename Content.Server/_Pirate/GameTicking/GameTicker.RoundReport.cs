// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Linq;
using System.Text.Json;
using Content.Server._Pirate.Photo;
using Content.Shared.GameTicking;
using Robust.Shared.Utility;

namespace Content.Server.GameTicking;

public sealed partial class GameTicker
{
    // The bot creates this directory on instances that publish round reports.
    private static readonly ResPath PirateRoundReportDirectory = new("/round-reports");
    private int _lastExportedPirateRound;

    private void WritePirateRoundReport(RoundEndMessageEvent scoreboard)
    {
        if (RunLevel != GameRunLevel.PostRound || scoreboard.RoundId <= 0 || scoreboard.RoundId == _lastExportedPirateRound)
            return;

        try
        {
            var data = _resourceManager.UserData;
            if (!data.IsDir(PirateRoundReportDirectory))
                return;

            var path = PirateRoundReportDirectory / $"{scoreboard.RoundId}.json";
            if (!data.Exists(path))
            {
                var pending = PirateRoundReportDirectory / $"{scoreboard.RoundId}.tmp";
                var report = new
                {
                    version = 1,
                    roundId = scoreboard.RoundId,
                    start = new DateTimeOffset(_roundStartDateTime).ToUnixTimeSeconds(),
                    end = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    seconds = (long) scoreboard.RoundDuration.TotalSeconds,
                    map = _gameMapManager.GetSelectedMap()?.MapName,
                    gamemode = scoreboard.GamemodeTitle,
                    roundEndText = scoreboard.RoundEndText,
                    roundEndPlayers = scoreboard.AllPlayersEndInfo.Select(player => new
                    {
                        playerGuid = player.PlayerGuid?.UserId,
                        playerOOCName = player.PlayerOOCName,
                        playerICName = player.PlayerICName,
                        observer = player.Observer,
                        jobPrototypes = player.JobPrototypes,
                        antagPrototypes = player.AntagPrototypes,
                    }),
                    pirateShiftPhotos = EntityManager.System<PhotoSystem>().GetDiscordPhotos()
                        .Select(photo => new { author = photo.Author, caption = photo.Caption, png = photo.Png }),
                };
                using (var stream = data.Open(pending, FileMode.Create, FileAccess.Write, FileShare.None))
                    JsonSerializer.Serialize(stream, report);

                data.Rename(pending, path);
            }

            _lastExportedPirateRound = scoreboard.RoundId;
        }
        catch (Exception ex)
        {
            Log.Error($"Could not save the Discord scoreboard for round {scoreboard.RoundId}: {ex}");
        }
    }
}
