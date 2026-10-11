# Оновлення контенту для «Секрет Сіль 1.4»: кандидати, не рішення про баланс

**Дата порівняння:** 11 жовтня 2026 року.
**Джерела:** актуальний `master` `SpaceStationUA/Goob-Station` та `feature/secret-salt-1.4` форка `The-Devils-Luck/Goob-Station`.
**Статус:** **АУДИТ, ЖОДНИХ ЗМІН БАЛАНСУ ЧИ ІГРОВИХ ПРАВИЛ.** Результати тільки за переліченими файлами та точковими перевірками залежностей; **не** повний аудит усіх прототипів репозиторію.

## 1. Власні таблиці Salt — незмінні

[`secret_salt.yml`](https://github.com/SpaceStationUA/Goob-Station/blob/master/Resources/Prototypes/_Pirate/GameRules/secret_salt.yml) у двох репозиторіях має однаковий Git blob SHA `3b1a598333962a936092cfea41350ac5edf21a08`.

- `SecretSaltPrimary` має свій окремий набір ваг.
- `SecretSaltRoundstart` має окремий набір ваг для додаткових раундстартових антагоністів.
- `SecretSaltTable` посилається на спільні `SpaceTrafficControlTable` та `BasicGameRulesTable`. Зміни спільних event-таблиць і прототипів потенційно впливають на Salt **після** синхронізації відповідних файлів; точну доступність кожної події та її запуск потрібно перевірити.
- Початковий хаос 11–22 на гравця, інтервал подій 180–450 с, коефіцієнти й чинні ваги **не змінювати автоматично**.

## 2. Кандидати на нових антагоністів

| Правило | Є у `SecretSaltPrimary` | Стан у нашій гілці | SpaceStationUA `SecretPlusPrimary` | Мінімальний онлайн у перевіреному правилі | Попередня оцінка |
|---|---|---|---:|---:|---|
| **`Honkops`** | Ні | Прототип `Honkops` вже існує у нашому [`roundstart.yml`](https://github.com/The-Devils-Luck/Goob-Station/blob/feature/secret-salt-1.4/Resources/Prototypes/_Goobstation/GameRules/roundstart.yml) | `0.01` | 20 | **Перший кандидат**, потребує погодження ролі й ваги |
| **`Xenoborgs`** | Ні | Власного прототипу `Xenoborgs` у нашому [`roundstart.yml`](https://github.com/The-Devils-Luck/Goob-Station/blob/feature/secret-salt-1.4/Resources/Prototypes/_Goobstation/GameRules/roundstart.yml) **немає** | `0.05` | 30 | Потрібні перенесення прототипу, ревізія залежностей і окремі тести |
| **`MalfAiMidround`** | Не застосовується — це подія посеред раунду | Відсутній каталог `Resources/Prototypes/_Pirate/MalfAI`, а також запис у нашій спільній таблиці | Не вагове раундстартове правило; upstream додав умовну подію | Не підтверджено; тільки за `MalfAiAvailableCondition` | Складніший перенос підсистеми та умов |

**Джерела:** [новий `Xenoborgs` у upstream `roundstart.yml`](https://github.com/SpaceStationUA/Goob-Station/blob/master/Resources/Prototypes/_Goobstation/GameRules/roundstart.yml), [upstream `SecretPlusPrimary`](https://github.com/SpaceStationUA/Goob-Station/blob/master/Resources/Prototypes/_Goobstation/GameRules/secretplus.yml), [upstream `MalfAi`/`MalfAiMidround`](https://github.com/SpaceStationUA/Goob-Station/blob/master/Resources/Prototypes/_Pirate/MalfAI/infrastructure.yml).

Важливо: вага `0.01`/`0.05` у `SecretPlusPrimary` **не є автоматично рекомендацією чи прямим відсотком появи в Salt**. Під час відбору працюють бюджет хаосу, мінімальний онлайн, виключення несумісних правил, перевірка шансів і фактичне призначення ролей. Для `Honkops` можливе порівняння з іншими раундстартовими антагоністами перед ухваленням рішення.

## 3. Спільні події посеред раунду — що відрізняється

У [upstream `Resources/Prototypes/_Goobstation/GameRules/midround.yml`](https://github.com/SpaceStationUA/Goob-Station/blob/master/Resources/Prototypes/_Goobstation/GameRules/midround.yml) **прототипи тих самих п'яти подій лишилися, але їхні ваги нижчі**, ніж у [нашому файлі](https://github.com/The-Devils-Luck/Goob-Station/blob/feature/secret-salt-1.4/Resources/Prototypes/_Goobstation/GameRules/midround.yml):

| Подія | Наша вага | SpaceStationUA | Оцінка |
|---|---:|---:|---|
| `TunnelClownMidround` | 4 | 1.5 | Помітне зменшення |
| `SingulothKnightsMidround` | 2.5 | 1 | Помітне зменшення |
| `MimeAssassinMidround` | 4 | 1.2 | Суттєве зменшення |
| `DarkPriestMidround` | 3 | 0.05 | Дуже суттєве зменшення |
| `VoxRaidersMidround` | 2.5 | 0.9 | Помітне зменшення |

Застереження: це **ваги прототипів**, не виміряні частоти і не самостійна шкала ймовірності появи. Зміна спільного `midround.yml` впливає не тільки на Salt. Перш ніж переносити, потрібно прийняти окреме рішення про загальносерверне оновлення або спроєктувати Salt-специфічну альтернативу, якщо вона справді потрібна.

## 4. Відмінності у спільній таблиці подій

Порівняно з [нашим `Resources/Prototypes/GameRules/events.yml`](https://github.com/The-Devils-Luck/Goob-Station/blob/feature/secret-salt-1.4/Resources/Prototypes/GameRules/events.yml), upstream [`events.yml`](https://github.com/SpaceStationUA/Goob-Station/blob/master/Resources/Prototypes/GameRules/events.yml):

1. Додав `MalfAiMidround` у `ModerateAntagEventsTable` із обов'язковою умовою `!type:MalfAiAvailableCondition`, тобто потрібен придатний станційний ШІ.
2. Прибрав `ListeningPost` із `BasicAntagEventsTable` (у форку запис є).
3. Закоментував `SyndicateDeadDrop` з поясненням, що його замінив постійний `SyndicateDropDispatcher`. Чи наявний потрібний dispatcher у нашому форку, **НЕПЕРЕВІРЕНО** — сліпо видаляти запис не можна.

Додатково в upstream [`game_presets.yml`](https://github.com/SpaceStationUA/Goob-Station/blob/master/Resources/Prototypes/_Pirate/game_presets.yml) є приховані пресети `MalfAi` та `SecretMalfAi`, яких немає у форку. **Сам факт нового пресету не означає, що його треба переносити в Salt.**

## 5. Рішення, які потрібно погодити перед кодом

**Пропозиція пріоритетів:**

1. **`Honkops` — локальний кандидат №1.** Правило вже існує у форку, тому для інтеграції в Salt не обов'язково переносити всю нову підсистему. Але додавання в `SecretSaltPrimary` змінює баланс: узгодити конкретну вагу й перевірити мінімальний онлайн, бюджет хаосу, фактичні видані ролі та несумісності.
2. **`Xenoborgs` — окремий етап перенесення контенту.** Не додавати одну стрічку `Xenoborgs: 0.05` без прототипів, карт/спавнерів та перевірки серверного коду. Лише після тестів вирішувати, чи потрібні вони в Salt.
3. **`MalfAiMidround` — окреме оновлення залежностей і поведінки.** Спершу з'ясувати, чи підсистема MalfAI взагалі потрібна Salt і який онлайн/стан ШІ допускає.
4. **Ваги п'яти midround-подій** — **не переносити разом з антами**: це окремий баланс, часто для інших режимів також. Порівняти майбутню статистику Salt та визначити бажаний рівень хаосу.

**До згоди автора:** жодних змін `secret_salt.yml`, `roundstart.yml`, `midround.yml`, глобальних event-таблиць, коду ролей привидів чи `master`. Баланс може й буде переглядатися для затверджених механік, але зміни робити окремими комітами з тестами.

**Стан тестування:** чинна гілка пройшла [GitHub Actions №38139888171](https://github.com/The-Devils-Luck/Goob-Station/actions/runs/38139888171) (21/21 PASS) **до цього документа**; живі SS14-раунди та Discord відсутні, статус **BLOCKED**. Цей аудит не змінює код і не запускає нове CI.
