# Перевірки технічних виправлень «Секрет Сіль 1.4»

> **Новий тест (11.10.2026):** попередні інтеграційні тести та компіляція в [№38124715402](https://github.com/The-Devils-Luck/Goob-Station/actions/runs/38124715402) — **PASS**. Тепер [тест `TestSummaryOnlyGeneratedAtRoundEnd`](https://github.com/The-Devils-Luck/Goob-Station/blob/feature/secret-salt-1.4/Content.IntegrationTests/Tests/GameRules/SecretSaltTelemetryTest.cs) перевіряє звичайний старт і завершення раунду: `TelemetryLastRoundSummary == null` до завершення; після `EndRound` — наявні `ROUND SUMMARY`, `roundstartAttempts`, `roundstartAdded` та `roundstartSkips`. Це ще **не** відправлення в Discord. [Нова перевірка №38125207666](https://github.com/The-Devils-Luck/Goob-Station/actions/runs/38125207666) — результат очікується.

> **Підтверджений CI-результат 11.10.2026:** [№38124715402](https://github.com/The-Devils-Luck/Goob-Station/actions/runs/38124715402) — **SUCCESS**: компіляція, власний `SecretSaltTelemetryTest` та штатний `SecretStartsTest` пройшли. Це **не** є повноцінним тестуванням завершення раунду, ротації чи Discord.

> **Інтеграційний тест — перша спроба FAILED:** [№38124255947](https://github.com/The-Devils-Luck/Goob-Station/actions/runs/38124255947), компіляція PASS, штатний `SecretStartsTest` PASS, власний `SecretSaltTelemetryTest` FAIL. Точна причина за журналом: `Log.Error("Tried running roundstart event BloodBrothers, but chaos score was null")` у `SecretSaltPlusSystem.TrySpawnRoundstartAntags`. Роль пропускається через `nullChaosScore`, але інтеграційний тест вважає журнал `ERROR` винятком. [Коміт `8374e34`](https://github.com/The-Devils-Luck/Goob-Station/commit/8374e34b38b0c7be3c44037e2a3143c83cf8838a) залишає структурований запис пропуску, прибирає дубльований `ERROR`; шанси й логіка вибору не змінюються. [Повторний запуск №38124715402](https://github.com/The-Devils-Luck/Goob-Station/actions/runs/38124715402) — **SUCCESS**, `SecretSaltTelemetryTest` та `SecretStartsTest` пройшли.

> **Оновлення тестування 11.10.2026:** [збірка №38123892196](https://github.com/The-Devils-Luck/Goob-Station/actions/runs/38123892196) — **SUCCESS** для лічильників телеметрії. Додано [`SecretSaltTelemetryTest.cs`](https://github.com/The-Devils-Luck/Goob-Station/blob/feature/secret-salt-1.4/Content.IntegrationTests/Tests/GameRules/SecretSaltTelemetryTest.cs), а до [`secret-salt-build.yml`](https://github.com/The-Devils-Luck/Goob-Station/blob/feature/secret-salt-1.4/.github/workflows/secret-salt-build.yml) додано виконання **двох** інтеграційних тестів: `SecretSaltTelemetryTest` і `SecretStartsTest`. [CI №38124255947](https://github.com/The-Devils-Luck/Goob-Station/actions/runs/38124255947) **очікує результату**. Тест перевіряє, що `roundstartAttempts = roundstartAdded + відхилені випадкові кандидати` (за винятком причин без вибору), `roundstartAdded = TelemetryRules.Count` до старту, а `TelemetryStartedRules` відстежує саме перехід правил у активний/завершений стан. **Не доводить** коректність `ROUND SUMMARY` після завершення повноцінного раунду, тож Т9–Т12 лишаються не завершеними.

> **Статус CI на 11.10.2026:** [№38123571609](https://github.com/The-Devils-Luck/Goob-Station/actions/runs/38123571609) — **SUCCESS (компіляція C#)**. Нові лічильники [f851f9c](https://github.com/The-Devils-Luck/Goob-Station/commit/f851f9caa952bfb94260bf08930bd06f4a473795) перевіряє [№38123892196](https://github.com/The-Devils-Luck/Goob-Station/actions/runs/38123892196). Ці запуски **не** замінюють ігрових тестів.

**Нові невиконані сценарії:** Т13 — звірити `roundstartAttempts` із кількістю випадкових кандидатів, а `roundstartAdded` із реально доданими стартовими правилами; Т14 — звірити `roundstartSkips` за всіма причинами; Т15 — перевірити очищення лічильників між раундами.

**Статус на 11.10.2026:** [перша компіляція №38123571609](https://github.com/The-Devils-Luck/Goob-Station/actions/runs/38123571609) **успішна**. Нові лічильники перевіряються у [збірці №38123892196](https://github.com/The-Devils-Luck/Goob-Station/actions/runs/38123892196). Тестів у грі **не виконано**.

Кодові коміти: [`278b61f` — захисти](https://github.com/The-Devils-Luck/Goob-Station/commit/278b61f37ffd0561e9cb63f703d50e0baf9abd65), [`1f6ea69` — поля телеметрії](https://github.com/The-Devils-Luck/Goob-Station/commit/1f6ea6901f01956eda807b07a518d1f68b9756d5), [`e03a49f` — звіт](https://github.com/The-Devils-Luck/Goob-Station/commit/e03a49fc3a3f6448eaee96eb8fd883c3a60c97b).

Змінені ігрові файли: `Content.Goobstation.Server/StationEvents/SecretPlus/SecretSaltPlusSystem.cs` і `SecretSaltPlusComponent.cs`. **У компоненті додано лише несеріалізовані структури телеметрії; вихідні параметри балансу не змінювалися**.

## Вже перевірено статично

- [x] Змінено лише два ігрові файли: `SecretSaltPlusSystem.cs` і `SecretSaltPlusComponent.cs`. У другому додано виключно поля збору статистики.
- [x] `Resources/Prototypes/_Pirate/GameRules/secret_salt.yml` у робочій гілці **ідентичний** `master` за blob SHA `3b1a598333962a936092cfea41350ac5edf21a08`.
- [x] Числові поля `SecretSaltPlusComponent.cs` не змінено; до компонента додано два поля для обліку доданих і запущених правил. Його blob SHA **відрізняється** від `master`; незмінність числових полів перевірено шляхом порівняння змін.
- [x] У системі не змінено формули розрахунку бюджету або константи часу, не змінено послідовність видалення несумісних кандидатів.
- [x] Додано перевірку відсутності додатних ваг перед випадковим добором антагоністів.
- [x] Додано `CancelPresetOnTooFewPlayers = false` лише для доданих генератором, але ще не запущених правил (`doStart == false`), як у актуальному `SecretPlusSystem.cs`.
- [x] Якщо `GetChaosScore` після додавання правила повернув `null`, правило завершується через `EndGameRule`, а в журналах лишається причина. Раунд не має падати через `!.Value`.

**Примітка:** це перевірка змін у вихідному коді, **не** доказ коректного виконання на сервері.

## Перевірити після компіляції у тестовому середовищі

| № | Сценарій | Очікуваний результат | Статус |
|---|---|---|---|
| Т1 | Звичайний запуск «Секрет Сіль» із достатнім онлайном | Такий самий вибір за тією самою послідовністю випадкових чисел; не змінені формули/ваги | Не виконано |
| Т2 | Вибір правила з `MinPlayers` вищим за фактичний онлайн | Лог `ROUNDSTART SKIP ... reason=minPlayers`; немає падіння | Не виконано |
| Т3 | Після вибору правила онлайн стає нижчим, ніж `MinPlayers`, перед `RoundStartAttemptEvent` | Не скасований **весь** пресет через автоматично додане правило; невідповідне правило може бути завершено окремо | Не виконано |
| Т4 | Доступний список ваг порожній або всі ваги недодатні | `reason=noPositiveWeights`, вихід без винятку | Не виконано |
| Т5 | Після додавання правила розрахунок хаосу повернув `null` | `reason=nullChaosScoreAfterAdd`, `EndGameRule`, без зміни бюджету на невідомий бал | Не виконано |
| Т6 | Набір ваг нормальний, значення хаосу коректне | Немає додаткових пропусків; правила запускаються штатно | Не виконано |
| Т7 | Звичайний `SecretPlus`, `SecretTP` та інші пресети | Відсутні побічні зміни від патча в `SecretSaltPlusSystem` | Не виконано |
| Т8 | Зіставлення YAML, числових полів компонента і ваг із `master` | Значення балансу незмінні; компонент відрізняється тільки новими полями телеметрії | Статична перевірка виконана |
| Т9 | Генератор додав правило, яке пізніше стартувало | В адміністративному журналі послідовно `ROUNDSTART ADDED` / `RULE STARTED`; у `ROUND SUMMARY` коректні лічильники | Не виконано |
| Т10 | Правило додано, але завершено до активації | `rulesAdded` збільшено, `rulesStarted` не збільшено | Не виконано |
| Т11 | Антагоністів призначено системою `AntagSelectionSystem` | Підсумок `AssignedMinds` відповідає призначеним mind; ролі іншого походження не помилково видаються за повний облік усіх антагоністів | Не виконано |
| Т12 | Завершення раунду та наступний раунд | `RULE SUMMARY` / `ROUND SUMMARY` з'являються після завершення раунду, а дані не переходять між раундами | Не виконано |

| Т16 | Реальний запуск і завершення раунду `SecretSalt` | Після `EndRound` заповнено `TelemetryLastRoundSummary`, а до завершення воно `null` | Не виконано; CI №38125207666 |

## Що ще потрібно зробити

- [x] Успішно скомпілювати код захистів і початкової телеметрії: [збірка №38123571609](https://github.com/The-Devils-Luck/Goob-Station/actions/runs/38123571609).
- [ ] Перевірити збірку нових лічильників [№38123892196](https://github.com/The-Devils-Luck/Goob-Station/actions/runs/38123892196) та виконати штатні автоматизовані/ігрові тести. **Окремий `secret-salt-build.yml` тепер запускає перевірку компіляції на push у робочу гілку; [перший запуск №38123571609](https://github.com/The-Devils-Luck/Goob-Station/actions/runs/38123571609). Звичайний `build-test-debug.yml` працює на `master/staging/stable` або PR до них.**
- [ ] За можливості зробити автоматизовані тести для переходів правил та нестачі гравців.
- [ ] Перевірити, що завершення доданого, але ще не запущеного правила не створює побічних ефектів.
- [ ] Перевірити реальні діагностичні повідомлення, зокрема `ROUNDSTART ADDED` (це **додавання** правила, ще не видача антагоністичних ролей).
- [ ] Зберегти результати перевірок і посилання на тести в [журналі](PROJECT.md).

**Далі за [планом](PLAN.md):** доповнення телеметрії: розрізнити спробу, додавання, запуск правила й успішне призначення ролей. Не змінювати механізм вибору й баланс.
