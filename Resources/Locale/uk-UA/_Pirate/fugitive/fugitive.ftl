# SPDX-License-Identifier: AGPL-3.0-or-later

roles-antag-fugitive-name = Утікач
roles-antag-fugitive-objective = Залишайтеся на волі попри свої злочини.
role-subtype-fugitive = Утікач

ghost-role-information-fugitive-name = Утікач
ghost-role-information-fugitive-description = Ви втекли з в'язниці. Утечіть звідси та виживіть.
ghost-role-information-fugitive-rules = Ваша мета: переховуватися й уникати Служби Безпеки, а не атакувати її безпосередньо. Не влаштовуйте безпричинної різанини.
ent-SpawnPointGhostFugitive = точка появи Утікача

fugitive-round-end-agent-name = Утікач
fugitive-spawn = Ви падаєте зі стелі!

station-event-fugitive-hunt-announcement = Перевірте комунікаційні консолі: надійшло конфіденційне повідомлення.
fugitive-announcement-GALPOL = GALPOL

fugitive-report-title = РОЗШУКУЄТЬСЯ ВТІКАЧ!
fugitive-report-first-line = У секторі помічено особу, яка втекла та приховала свою ідентичність. Ця особа може переховуватися на одній зі станцій.
fugitive-report-inhuman = {$name} не є гуманоїдом. Інших відомостей немає.
fugitive-report-morphotype = МОРФОТИП: {$species}
fugitive-report-age = ВІК: {$age}
fugitive-report-sex = СТАТЬ: {$sex ->
    [Male] Ч
    [Female] Ж
    *[none] Н/Д
}
fugitive-report-weight = МАСА: {$weight} кг
fugitive-report-detail-dna = ДНК: {$dna}
fugitive-report-detail-prints = ВІДБИТКИ ПАЛЬЦІВ: {$prints}
fugitive-report-crimes-header = Цю особу розшукують у всьому секторі за такі злочини:
fugitive-report-crime = - {$count ->
    [1] Один епізод
    *[other] {$count} епізодів
} за статтею «{$crime}»
fugitive-report-last-line = GALPOL доручає Nanotrasen затримати цю особу й доправити її на суд до Центрального Командування. Забезпечте затримання без летальних наслідків.

fugitive-crime-1 = Вбивство
fugitive-crime-2 = Тероризм
fugitive-crime-3 = Тяжкий саботаж
fugitive-crime-4 = Перешкоджання відродженню
fugitive-crime-5 = Заколот
fugitive-crime-6 = Втеча з-під варти
fugitive-crime-7 = Ненавмисне вбивство
fugitive-crime-8 = Викрадення
fugitive-crime-9 = Зберігання великої кількості контрабанди
fugitive-crime-10 = Ноосферне втручання
fugitive-crime-11 = Саботаж
fugitive-crime-12 = Зловживання владою
fugitive-crime-13 = Крадіжка у великих розмірах
fugitive-crime-14 = Торгівля на чорному ринку
fugitive-crime-15 = Напад
fugitive-crime-16 = Незаконне проникнення
fugitive-crime-17 = Масові заворушення
fugitive-crime-18 = Створення небезпеки
fugitive-crime-19 = Незаконне зберігання
fugitive-crime-20 = Перешкоджання правосуддю
fugitive-crime-21 = Лжесвідчення
fugitive-crime-22 = Неправдиве повідомлення
fugitive-crime-23 = Неповага до суду
fugitive-crime-24 = Викрадення особистості

ent-FugitiveEscapeObjective = Уникайте правоохоронців
    .desc = Змішайтеся з натовпом, виживіть і покиньте станцію на евакуаційному шатлі.

ent-FugitiveStash = схованка Утікача
    .desc = Ці припаси допомогли вам утекти з в'язниці й мають допомогти залишитися на волі.

fugitive-set-hitman-name = комплект кілера
fugitive-set-hitman-description = Заряджений Viper, запасний магазин і коричнева валіза для озброєного самозахисту.
fugitive-set-saboteur-name = комплект диверсанта
fugitive-set-saboteur-description = Дві EMP-гранати, заряд C4 і протигаз для диверсій.
fugitive-set-ghost-name = комплект привида
fugitive-set-ghost-description = Дві димові гранати, Scram-імплантер і костюм-простирадло для зникнення під час погоні.
fugitive-set-leverage-name = комплект важелів впливу
fugitive-set-leverage-description = Двоє кайданків, бола й кислотний імплантер, щоб перетворити переслідувача на заручника.
fugitive-set-infiltrator-name = комплект інфільтратора
fugitive-set-infiltrator-description = Агентська ID-картка, імплантер свободи й протигаз Синдикату для зміни особи та доступів.
fugitive-set-disruptor-name = комплект дестабілізатора
fugitive-set-disruptor-description = Криптографічний секвенсор і пристрій підключення до камер для злому систем станції.

ent-PaperFugitiveReport = орієнтування на Утікача
    .desc = Ордер на арешт космічного втікача, надісланий GALPOL.
ent-RubberStampGalpol = печатка GALPOL
    .desc = Печатка для важливих документів у справах міжгалактичної безпеки.
stamp-component-stamped-name-GALPOL = GALPOL
