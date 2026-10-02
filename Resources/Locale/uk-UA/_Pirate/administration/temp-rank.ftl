temp-rank-server-console = Консоль сервера
temp-rank-granted-announcement = {$admin} видав {$player} тимчасове звання «{$rank}» до кінця раунду.
temp-rank-granted-unlisted-announcement = {$admin} видав {$player} тимчасове звання «{$rank}» до кінця раунду. Гравця немає в списку допущених до цього звання.
temp-rank-revoked-announcement = {$admin} забрав у {$player} тимчасове звання «{$rank}».

cmd-temprank-desc = Видає гравцю наявне адмін-звання до кінця раунду. З консолі сервера (бот) гравець має бути в списку temprankallow.
cmd-temprank-not-eligible = {$player} не має допуску до звання «{$rank}». Додайте через temprankallow.
cmd-temprank-help = Використання: temprank <нік або user id> <назва звання>
cmd-temprank-arg-player = <нік або user id>
cmd-temprank-arg-rank = <назва звання>
cmd-temprank-player-not-found = Гравця «{$player}» не знайдено.
cmd-temprank-rank-not-found = Звання «{$rank}» не існує.
cmd-temprank-missing-flags = Ви не можете видати звання з правами, яких не маєте самі: {$flags}
cmd-temprank-success = {$player} отримав звання «{$rank}» до кінця раунду.

cmd-temprankremove-desc = Забирає у гравця тимчасове звання.
cmd-temprankremove-help = Використання: temprankremove <нік або user id>
cmd-temprankremove-none = У {$player} немає тимчасового звання.
cmd-temprankremove-success = Тимчасове звання {$player} забрано.

cmd-tempranks-desc = Показує всі тимчасові звання.
cmd-tempranks-help = Використання: tempranks
cmd-tempranks-empty = Тимчасових звань немає.
cmd-tempranks-entry = {$player}: «{$rank}» (видав {$admin}), до кінця раунду
cmd-tempranks-entry-next = {$player}: «{$rank}» (видав {$admin}), до кінця наступного раунду

cmd-temprankallow-desc = Дозволяє боту видавати гравцю це звання на раунд.
cmd-temprankallow-help = Використання: temprankallow <нік або user id> <назва звання>
cmd-temprankallow-exists = {$player} вже має допуск до звання «{$rank}».
cmd-temprankallow-success = Тепер бот може видавати {$player} звання «{$rank}» на раунд.

cmd-temprankdisallow-desc = Забирає у гравця допуск до тимчасового звання.
cmd-temprankdisallow-help = Використання: temprankdisallow <нік або user id> <назва звання>
cmd-temprankdisallow-none = {$player} не має допуску до звання «{$rank}».
cmd-temprankdisallow-success = {$player} більше не має допуску до звання «{$rank}». Звання, видане на поточний раунд, лишається до його кінця (temprankremove, щоб забрати зараз).

cmd-temprankallowed-desc = Показує, кому бот може видавати які звання.
cmd-temprankallowed-help = Використання: temprankallowed [нік або user id]
cmd-temprankallowed-empty = Список допущених порожній.
cmd-temprankallowed-entry = {$player}: {$ranks}
