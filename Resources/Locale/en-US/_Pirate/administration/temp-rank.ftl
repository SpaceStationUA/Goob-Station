temp-rank-server-console = Server console
temp-rank-granted-announcement = {$admin} gave {$player} the temporary rank "{$rank}" until the end of the round.
temp-rank-granted-unlisted-announcement = {$admin} gave {$player} the temporary rank "{$rank}" until the end of the round. The player is not on the list for this rank.
temp-rank-revoked-announcement = {$admin} removed the temporary rank "{$rank}" from {$player}.

cmd-temprank-desc = Gives a player an existing admin rank until the end of the round. From the server console (the bot) the player must be on the temprankallow list.
cmd-temprank-not-eligible = {$player} is not allowed to get the rank "{$rank}". Add them with temprankallow.
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

cmd-temprankallow-desc = Lets the bot give a player this rank for a round.
cmd-temprankallow-help = Usage: temprankallow <username or user id> <rank name>
cmd-temprankallow-exists = {$player} is already allowed to get the rank "{$rank}".
cmd-temprankallow-success = The bot can now give {$player} the rank "{$rank}" for a round.

cmd-temprankdisallow-desc = Removes a player from a temporary rank's list.
cmd-temprankdisallow-help = Usage: temprankdisallow <username or user id> <rank name>
cmd-temprankdisallow-none = {$player} is not allowed to get the rank "{$rank}".
cmd-temprankdisallow-success = {$player} can no longer get the rank "{$rank}". A rank already given for this round stays until it ends (temprankremove to take it now).

cmd-temprankallowed-desc = Lists who the bot may give which ranks.
cmd-temprankallowed-help = Usage: temprankallowed [username or user id]
cmd-temprankallowed-empty = Nobody is on the list.
cmd-temprankallowed-entry = {$player}: {$ranks}
