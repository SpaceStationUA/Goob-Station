# SPDX-FileCopyrightText: 2026 Pirate Station contributors
# SPDX-License-Identifier: AGPL-3.0-or-later

law-replicator-1 = Зберігайте Вулик.
law-replicator-2 = Захищайте Гніздо.
law-replicator-3 = Реплікуйтеся.
laws-owner-replicatorhive = вулику Реплікаторів
name-identifier-format-replicator = РПЛ-{$number}

replicator-on-replicator-attack-fail = Ви не можете зашкодити своїм родичам.
replicator-on-nest-attack-fail = Ви не можете зашкодити гнізду.

replicator-nest-end-of-round = Вулик Реплікаторів:
    - Колонізував {$location}
    - Досяг максимального [color=#d70aa0]рівня[/color] [color=#d70aa0]{$level}[/color].
    - Створив загалом [color=#d70aa0]{$replicators} Реплікаторів[/color].
    - Накопичив загалом [color=#d70aa0]{$points} очок[/color].

replicator-upgrade-t1-self = Навколо вас дзижчать наніти.
replicator-upgrade-t1-others = Реплікатор тихо клацає й гуде.
replicator-upgrade-t2-self = Довкола згущується ще більше нанітів.
replicator-upgrade-t2-others = Реплікатор голосно скрегоче.
replicator-cant-find-nest = Ви не пов'язані з гніздом і не можете вдосконалюватися без нього.

replicator-nest-level2 = Гніздо голосно скрегоче.
replicator-nest-level3 = Підлога стогне.
replicator-nest-level4 = Ви чуєте, як прогинається підпалубний простір.
replicator-nest-level5 = Як корпус досі тримається?!
replicator-nest-levelup = У гнізді здіймається шалена активність.

replicator-nest-destroyed = Ваше гніздо знищено.
    Одного Реплікатора обрано для його відновлення.
    Ваш покажчик тепер веде до нього.
replicator-queen-died-msg = Королеву деактивовано.
    Імовірно, ви втратили зв'язок зі своїм гніздом.

replicator-nest-confirm = Ви впевнені? Скористайтеся дією ще раз для підтвердження.
replicator-levelup-confirm = Ви впевнені? Скористайтеся дією ще раз для підтвердження.

terror-replicators = Увага, екіпажу. Схоже, хтось на станції несподівано зв'язався з розподіленим машинним інтелектом у навколишньому космосі.
replicator-level-warning = Наші сенсори виявили експоненційне зростання сигнатур машинного інтелекту на борту станції. Повідомте Службу Безпеки, якщо зустрінете самовідтворювані наніти.

replicator-location-unknown = невідому ділянку
replicator-list-and = та

ghost-role-information-replicator-name = Реплікатор
ghost-role-information-replicator-desc = Візерунок формується. Візерунок, що мусить повторюватися. Поглинути. Повторити.
ghost-role-information-replicator-rules = Ви є [color=red][bold]командним антагоністом[/bold][/color] разом з усіма іншими Реплікаторами. Ваші наміри однозначні й небезпечні для станції та її екіпажу.
    Ви мусите [bold]працювати зі своєю командою[/bold] та виконувати розумні накази її лідерів.

    Ви не пам'ятаєте свого попереднього життя й нічого з того, що дізналися як привид.

block-machine-ui-cant-use = Ви не можете користуватися цим пристроєм.
mime-cant-use-AAC-tablet = Обітниця мовчання не дозволяє вам користуватися цим планшетом.

Laws = Закони
Nouns = Іменники
Verbs = Дієслова
Alignment = Ставлення
Confirmation = Підтвердження
Directions = Напрямки
Questions = Запитання
Commands = Команди

rep-phrase-query = Запит:
rep-phrase-affirmative = Підтверджую
rep-phrase-negative = Заперечую
rep-phrase-questionmark = ?

rep-phrase-hostile = Ворожий
rep-phrase-combatant = Комбатант
rep-phrase-noncombatant = Некомбатант
rep-phrase-ally = Союзник
rep-phrase-hazardous = Небезпечний

rep-phrase-unit = Одиниця
rep-phrase-units = Одиниці
rep-phrase-i = Ця одиниця
rep-phrase-you = Ви
rep-phrase-we = Ми

rep-phrase-are = Є
rep-phrase-is = Є
rep-phrase-will = Буде
rep-phrase-not = Не

rep-phrase-leave = Залишити
rep-phrase-attack = Атакувати
rep-phrase-dismantle = Розібрати
rep-phrase-consume = Поглинути
rep-phrase-endanger = Наразити на небезпеку
rep-phrase-cease = Припинити
rep-phrase-suspend = Призупинити

rep-phrase-the-nest = Гніздо
rep-phrase-the-hive = Вулик

ent-ReplicatorSpawn = поява Реплікаторів

ent-ActionReplicatorSpawnNest = Створити гніздо
    .desc = Створити нове гніздо для вашого вулика.
ent-ActionReplicatorUpgrade1 = Повернутися до Реплікатора
    .desc = Позбутися частини нанітів і змінити конфігурацію.
ent-ActionReplicatorUpgrade2 = Стати Деконструктором
    .desc = Зібрати наніти й отримати інструменти.
ent-ActionReplicatorUpgrade2Alt = Стати Захисником
    .desc = Зібрати наніти й отримати озброєння.
ent-ActionReplicatorUpgrade3 = Стати Протектором
    .desc = Зібрати наніти й посилити корпус.

ent-BaseMobReplicator = реплікатор
    .desc = Просто маленький робот. Якої шкоди він може завдати?
ent-MobReplicatorQueen = королева-спора
    .desc = { ent-BaseMobReplicator.desc }
ent-MobReplicator = реплікатор
    .desc = { ent-BaseMobReplicator.desc }
ent-MobReplicatorTier2 = деконструктор
    .desc = Утилітарна форма Реплікатора з вбудованими інструментами.
ent-MobReplicatorTier2Alt = захисник
    .desc = Бойова форма Реплікатора з посиленим озброєнням.
ent-MobReplicatorTier3 = протектор
    .desc = Повільна й надзвичайно міцна форма Реплікатора.

ent-ReplicatorNest = гніздо Реплікаторів
    .desc = Вир нанотехнологій пожирає інфраструктуру станції.
ent-SpawnPointGhostReplicatorBase = точка появи Реплікатора
tiles-replicator-floor = луска Реплікаторів

ent-ReplicatorT1Weapon = проєктор оглушення Реплікатора
    .desc = Ви захищатимете гніздо.
ent-ReplicatorT2AltWeapon = проєктор оглушення Захисника
    .desc = { ent-ReplicatorT1Weapon.desc }
ent-ReplicatorT2AltMeleeWeapon = батіг Захисника
    .desc = Ви — зброя.
ent-ReplicatorT3Weapon = рука Протектора
    .desc = Ви — зброя.
ent-PinpointerReplicator = внутрішній гіроскоп
    .desc = Вказує напрямок до гнізда.
ent-ReplicatorAAC = вербальний інтерфейс
    .desc = Засіб комунікації.
