temp-rank-server-console = Консоль сервера
temp-rank-granted-announcement = {$admin} видав {$player} тимчасове звання «{$rank}» до кінця раунду.
temp-rank-granted-unlisted-announcement = {$admin} видав {$player} тимчасове звання «{$rank}» до кінця раунду. Гравця немає в списку допущених до цього звання.
temp-rank-revoked-announcement = {$admin} забрав у {$player} тимчасове звання «{$rank}».

cmd-temprankadd-desc = Видає гравцю наявне адмін-звання до кінця раунду. З консолі сервера (бот) гравець має бути у вайтлисті звання (rankwhitelistadd).
cmd-temprankadd-not-eligible = {$player} не має допуску до звання «{$rank}». Додайте через rankwhitelistadd.
cmd-temprankadd-help = Використання: temprankadd <нік або user id> <назва звання>
cmd-temprank-arg-player = <нік або user id>
cmd-temprank-arg-rank = <назва звання>
cmd-temprank-player-not-found = Гравця «{$player}» не знайдено.
cmd-temprank-rank-not-found = Звання «{$rank}» не існує.
cmd-temprank-missing-flags = Ви не можете видати звання з правами, яких не маєте самі: {$flags}
cmd-temprankadd-success = {$player} отримав звання «{$rank}» до кінця раунду.

cmd-temprankremove-desc = Забирає у гравця тимчасове звання.
cmd-temprankremove-help = Використання: temprankremove <нік або user id>
cmd-temprankremove-none = У {$player} немає тимчасового звання.
cmd-temprankremove-success = Тимчасове звання {$player} забрано.

cmd-tempranklist-desc = Показує всі тимчасові звання.
cmd-tempranklist-help = Використання: tempranklist
cmd-tempranklist-empty = Тимчасових звань немає.
cmd-tempranklist-entry = {$player}: «{$rank}» (видав {$admin}), до кінця раунду
cmd-tempranklist-entry-next = {$player}: «{$rank}» (видав {$admin}), до кінця наступного раунду

cmd-rankwhitelistadd-desc = Дозволяє боту видавати гравцю це звання на раунд.
cmd-rankwhitelistadd-help = Використання: rankwhitelistadd <нік або user id> <назва звання>
cmd-rankwhitelistadd-exists = {$player} вже має допуск до звання «{$rank}».
cmd-rankwhitelistadd-success = Тепер бот може видавати {$player} звання «{$rank}» на раунд.

cmd-rankwhitelistremove-desc = Прибирає гравця з вайтлисту звання: бот більше не зможе його видати.
cmd-rankwhitelistremove-help = Використання: rankwhitelistremove <нік або user id> <назва звання>
cmd-rankwhitelistremove-none = {$player} не має допуску до звання «{$rank}».
cmd-rankwhitelistremove-success = {$player} більше не має допуску до звання «{$rank}». Звання, видане на поточний раунд, лишається до його кінця (temprankremove, щоб забрати зараз).

cmd-rankwhitelistget-desc = Показує, кому бот може видавати які звання.
cmd-rankwhitelistget-help = Використання: rankwhitelistget [нік або user id]
cmd-rankwhitelistget-empty = Список допущених порожній.
cmd-rankwhitelistget-entry = {$player}: {$ranks}
