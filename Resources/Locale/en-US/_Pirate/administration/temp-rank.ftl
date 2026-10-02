temp-rank-server-console = Server console
temp-rank-granted-announcement = {$admin} gave {$player} the temporary rank "{$rank}" until the end of the round.
temp-rank-granted-unlisted-announcement = {$admin} gave {$player} the temporary rank "{$rank}" until the end of the round. The player is not on the list for this rank.
temp-rank-revoked-announcement = {$admin} removed the temporary rank "{$rank}" from {$player}.

cmd-temprankadd-desc = Gives a player an existing admin rank until the end of the round. From the server console (the bot) the player must be on the rank whitelist (rankwhitelistadd).
cmd-temprankadd-not-eligible = {$player} is not allowed to get the rank "{$rank}". Add them with rankwhitelistadd.
cmd-temprankadd-help = Usage: temprankadd <username or user id> <rank name>
cmd-temprank-arg-player = <username or user id>
cmd-temprank-arg-rank = <rank name>
cmd-temprank-player-not-found = Player "{$player}" not found.
cmd-temprank-rank-not-found = Rank "{$rank}" does not exist.
cmd-temprank-missing-flags = You can't give a rank with permissions you don't have yourself: {$flags}
cmd-temprankadd-success = {$player} has the rank "{$rank}" until the end of the round.

cmd-temprankremove-desc = Removes a player's temporary rank.
cmd-temprankremove-help = Usage: temprankremove <username or user id>
cmd-temprankremove-none = {$player} has no temporary rank.
cmd-temprankremove-success = Removed the temporary rank of {$player}.

cmd-tempranklist-desc = Lists all temporary ranks.
cmd-tempranklist-help = Usage: tempranklist
cmd-tempranklist-empty = There are no temporary ranks.
cmd-tempranklist-entry = {$player}: "{$rank}" (by {$admin}), until the end of the round
cmd-tempranklist-entry-next = {$player}: "{$rank}" (by {$admin}), until the end of the next round

cmd-rankwhitelistadd-desc = Lets the bot give a player this rank for a round.
cmd-rankwhitelistadd-help = Usage: rankwhitelistadd <username or user id> <rank name>
cmd-rankwhitelistadd-exists = {$player} is already allowed to get the rank "{$rank}".
cmd-rankwhitelistadd-success = The bot can now give {$player} the rank "{$rank}" for a round.

cmd-rankwhitelistremove-desc = Removes a player from a rank's whitelist: the bot can no longer give it to them.
cmd-rankwhitelistremove-help = Usage: rankwhitelistremove <username or user id> <rank name>
cmd-rankwhitelistremove-none = {$player} is not allowed to get the rank "{$rank}".
cmd-rankwhitelistremove-success = {$player} can no longer get the rank "{$rank}". A rank already given for this round stays until it ends (temprankremove to take it now).

cmd-rankwhitelistget-desc = Lists who the bot may give which ranks.
cmd-rankwhitelistget-help = Usage: rankwhitelistget [username or user id]
cmd-rankwhitelistget-empty = Nobody is on the list.
cmd-rankwhitelistget-entry = {$player}: {$ranks}
