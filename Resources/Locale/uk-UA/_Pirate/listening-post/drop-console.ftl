syndicate-drop-console-window-title = пульт скидань
syndicate-drop-console-no-station = Станцію не захоплено в приціл

syndicate-drop-console-mode-header = Режим відправлення
syndicate-drop-console-mode-auto = Автоматичний
syndicate-drop-console-mode-manual = Ручний
syndicate-drop-console-mode-auto-desc = Кожен заряд витрачається одразу, щойно буде готовий, у випадковому місці
syndicate-drop-console-mode-manual-desc = Заряди накопичуються, доки ви не відправите один, і падають у вибраному місці скидання.

syndicate-drop-console-next-header = Наступний заряд
syndicate-drop-console-countdown-unknown = --:--

# Збігається з показами ручного GPS, щоб оператор міг диктувати координати агентам.
syndicate-drop-console-coordinates = ({$x}, {$y})

syndicate-drop-console-target-header = Місце скидання
syndicate-drop-console-target-none = Випадкове
syndicate-drop-console-target-clear = Скинути місце

syndicate-drop-console-charges-header = Заряди ({$count}/{$max})
syndicate-drop-console-launch = Відправити

syndicate-drop-console-pod-header = Пускова платформа
syndicate-drop-console-pod-send = ВІДПРАВИТИ З ПЛАТФОРМИ
syndicate-drop-console-pod-warning = Увага: кожен запуск передається з цього об'єкта. Повторне використання дозволить станції визначити наше розташування.
syndicate-drop-console-pod-unlinked = Платформу не підключено.
syndicate-drop-console-pod-ready = Сигнал охолов.
syndicate-drop-console-pod-traceable = Слід активний: {$time}

syndicate-drop-console-history-header = Історія відправлень ({$count})
syndicate-drop-console-history-empty = Відправлень ще не було.
syndicate-drop-console-history-entry = [{$time}] {$price} ТК {$coordinates} {$mode}
syndicate-drop-console-history-entry-pod = [{$time}] {$coordinates} {$mode}
syndicate-drop-console-mode-badge-automatic = А
syndicate-drop-console-mode-badge-manual = Р
syndicate-drop-console-mode-badge-pod = ПОД

syndicate-drop-console-intercept-announcement = Увага. Служба радіоперехоплення Nanotrasen зафіксувала ворожу блюспейс-передачу в бік цієї станції та встановила її джерело. Детальний звіт надіслано на командні факс-термінали.
syndicate-drop-console-intercept-fax-title = Звіт про перехоплення
syndicate-drop-console-intercept-unknown-source = невідстежена позиція
syndicate-drop-console-intercept-fax-body =
    {"["}head=2]СЛУЖБА РАДІОПЕРЕХОПЛЕННЯ NANOTRASEN[/head]
    {"["}bold]ЗВІТ ПРО ПЕРЕХОПЛЕННЯ - ПРІОРИТЕТ ЧЕРВОНИЙ[/bold]

    Станції стеження зафіксували серію блюспейс-передач у бік вашої станції та встановили їхнє джерело: [bold]{$location}[/bold], орієнтовні координати [bold]{$coordinates}[/bold].

    За результатами аналізу сигнатури встановлено, що в зазначеній точці розгорнуто діючу пускову установку, з якої на борт вашої станції на регулярній основі здійснюється перекидання вантажу. Джерело кваліфіковано як вороже до активів Nanotrasen.

    {"["}bold]Вказівки:[/bold]
    - Будь-який вантаж, що опинився на станції поза графіком постачання, вважати ворожим.
    - Доручити Службі безпеки обшук приміщень на предмет сторонніх предметів.
    - Командному складу довести до відома весь персонал, що працює поблизу зазначених координат: район вважати ворожим.
    - Наближення до зазначених координат без окремого дозволу Центрального командування заборонено.

syndicate-drop-console-radio-announcement = Тайник із нашими припасами телепортовано поблизу {$location}, координати {$coordinates}. Заберіть ці інструменти за зручності. Хай живе Синдикат!

syndicate-drop-console-footer-left = Логістика Cybersun Industries
syndicate-drop-console-footer-right = Блюспейс-диспетчер v3.1

ent-ComputerSyndicateDropConsole = пульт скидань
    .desc = Далекодійний блюспейс-диспетчер. Сам збирає посилки з припасами за власним розкладом і закидає їх на станцію, обрану Верховним командуванням.
ent-SyndicateDropConsoleCircuitboard = плата пульта скидань
    .desc = Друкована плата для пульта скидань.
ent-SyndicateDropDispatcher = диспетчер тайників
    .desc = Невидимий вузол, що веде розклад скидань для всіх консолей.
