temp-rank-server-console = Server console
temp-rank-granted-announcement = {$admin} gave {$player} the temporary rank "{$rank}" until the end of the round.
temp-rank-revoked-announcement = {$admin} removed the temporary rank "{$rank}" from {$player}.

cmd-temprank-desc = Gives a player an existing admin rank until the end of the round. Nothing is written to the database.
cmd-temprank-help = Usage: temprank <username or user id> <rank name>
cmd-temprank-arg-player = <username or user id>
cmd-temprank-arg-rank = <rank name>
cmd-temprank-player-not-found = Player "{$player}" not found.
cmd-temprank-rank-not-found = Rank "{$rank}" does not exist.
cmd-temprank-missing-flags = You can't give a rank with permissions you don't have yourself: {$flags}
cmd-temprank-success = {$player} has the rank "{$rank}" until the end of the round.

cmd-temprankremove-desc = Removes a player's temporary rank.
cmd-temprankremove-help = Usage: temprankremove <username or user id>
cmd-temprankremove-none = {$player} has no temporary rank.
cmd-temprankremove-success = Removed the temporary rank of {$player}.

cmd-tempranks-desc = Lists all temporary ranks.
cmd-tempranks-help = Usage: tempranks
cmd-tempranks-empty = There are no temporary ranks.
cmd-tempranks-entry = {$player}: "{$rank}" (by {$admin}), until the end of the round
cmd-tempranks-entry-next = {$player}: "{$rank}" (by {$admin}), until the end of the next round
