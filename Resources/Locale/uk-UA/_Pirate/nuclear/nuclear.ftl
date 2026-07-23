### Загальне

nuclear-reactor-window-title = Ядерний реактор
gas-turbine-window-title = Газова турбіна
nuclear-machine-invalid-anchoring = Неможливо закріпити машину в цьому положенні!
nuclear-machine-ui-emergency-shutdown = Аварійна зупинка

### Ядерна центрифуга

nuclear-centrifuge-insert-item = { CAPITALIZE(THE($user)) } вставляє { THE($item) } у { THE($machine) }.
nuclear-centrifuge-wrong-item = { CAPITALIZE(THE($item)) } сюди не поміщається.
nuclear-centrifuge-unfit-item = { CAPITALIZE(THE($item)) } ще не готовий до перероблення.

### Реактор: сповіщення та повідомлення

reactor-smoke-start = {$owner} починає диміти!
reactor-smoke-stop = {$owner} припиняє диміти.
reactor-fire-start = {$owner} починає горіти!
reactor-fire-stop = {$owner} припиняє горіти.

reactor-unanchor-melted = Неможливо відкріпити ядерний реактор: він розплавився і спікся з корпусом!
reactor-unanchor-warning = Неможливо відкріпити ядерний реактор, доки він не порожній або гарячіший за 80 C!

reactor-smoke-start-message = УВАГА: {$owner} досяг небезпечної температури {$temperature} K. Негайно втруться, щоб запобігти розплавленню.
reactor-smoke-stop-message = {$owner} охолов нижче небезпечної температури. Гарного дня.
reactor-fire-start-message = УВАГА: {$owner} досяг КРИТИЧНОЇ температури {$temperature} K. РОЗПЛАВЛЕННЯ НЕМИНУЧЕ.
reactor-fire-stop-message = {$owner} охолов нижче критичної температури. Розплавлення відвернено.

reactor-temperature-dangerous-message = {$owner} має небезпечну температуру: {$temperature} K.
reactor-temperature-critical-message = {$owner} має критичну температуру: {$temperature} K.
reactor-temperature-cooling-message = {$owner} охолоджується: {$temperature} K.

reactor-melting-announcement = Ядерний реактор на станції починає розплавлятися. Рекомендується евакуювати прилеглу зону.
reactor-melting-announcement-sender = Ядерна аварійна система

reactor-meltdown-announcement = Ядерний реактор на станції зазнав катастрофічного перевантаження. Можливі радіоактивні уламки, випадіння опадів та пожежі теплоносія. Наполегливо рекомендується негайно евакуювати прилеглу зону.
reactor-meltdown-announcement-sender = Розплавлення реактора

### Інтерфейс реактора

comp-nuclear-reactor-ui-locked = Заблоковано
comp-nuclear-reactor-ui-insert-button = Вставити
comp-nuclear-reactor-ui-remove-button = Вийняти
comp-nuclear-reactor-ui-eject-button = Викинути
comp-nuclear-reactor-ui-overload = ПЕРЕВАНТАЖЕННЯ
comp-nuclear-reactor-ui-empty = порожньо
comp-nuclear-reactor-ui-rod = Стрижень
comp-nuclear-reactor-ui-fuel-level = Рівень палива: {$level}%

comp-nuclear-reactor-ui-view-change = Змінити режим
comp-nuclear-reactor-ui-view-temp = Температура
comp-nuclear-reactor-ui-view-neutron = Нейтрони
comp-nuclear-reactor-ui-view-target = Цільові значення
comp-nuclear-reactor-ui-view-fuel = Паливо

comp-nuclear-reactor-ui-status-panel = Стан реактора
comp-nuclear-reactor-ui-reactor-temp = Температура
comp-nuclear-reactor-ui-reactor-rads = Радіація
comp-nuclear-reactor-ui-reactor-therm = Теплова потужність
comp-nuclear-reactor-ui-reactor-control = Керувальні стрижні
comp-nuclear-reactor-ui-therm-format = { POWERWATTS($power) }

comp-nuclear-reactor-ui-footer-left = Небезпека: висока радіація.
comp-nuclear-reactor-ui-footer-right = 0.8 РЕД. 3

### Компоненти реактора

reactor-part-nrad-0 = Він ледь помітно світиться синім.
reactor-part-nrad-1 = Він трохи світиться синім.
reactor-part-nrad-2 = Він світиться синім.
reactor-part-nrad-3 = Він яскраво світиться синім.
reactor-part-nrad-4 = Він сліпуче світиться синім.
reactor-part-nrad-5 = Його синє сяйво неможливо витримати.

reactor-part-rad-0 = Він має слабку радіоактивність.
reactor-part-rad-1 = Він помірно радіоактивний.
reactor-part-rad-2 = Він радіоактивний.
reactor-part-rad-3 = Він дуже радіоактивний.
reactor-part-rad-4 = Він надзвичайно радіоактивний.
reactor-part-rad-5 = Його радіоактивність виходить за межі можливого.

reactor-part-hot = [color=yellow]Він гарячий на дотик.[/color]
reactor-part-burning = [color=red]Повітря навколо нього спотворюється від жару.[/color]

### Газова турбіна

gas-turbine-examine-stator-null = Схоже, у ній відсутній статор.
gas-turbine-examine-stator = У ній встановлено статор.
gas-turbine-examine-blade-null = Схоже, у ній відсутні турбінні лопаті.
gas-turbine-examine-blade = У ній встановлено турбінні лопаті.
gas-turbine-examine-speed-stopped = Лопаті не обертаються.
gas-turbine-examine-speed-slow = Лопаті повільно обертаються.
gas-turbine-examine-speed-normal = Лопаті обертаються.
gas-turbine-examine-speed-fast = Лопаті швидко обертаються.
gas-turbine-examine-speed-dangerous = [color=red]Лопаті обертаються неконтрольовано![/color]

turbine-damaged-0 = Вона у доброму стані.
turbine-damaged-1 = Турбіна трохи пошкоджена.
turbine-damaged-2 = [color=yellow]Турбіна сильно пошкоджена.[/color]
turbine-damaged-3 = [color=orange]Вона критично пошкоджена![/color]
turbine-ruined = [color=red]Вона повністю зламана![/color]

turbine-overheat = {$owner} відкриває аварійний клапан скидання перегрітого газу!
turbine-explode = {$owner} розриває на частини!
turbine-spark = {$owner} починає іскрити!
turbine-spark-stop = {$owner} припиняє іскрити.
turbine-smoke = {$owner} починає диміти!
turbine-smoke-stop = {$owner} припиняє диміти.

gas-turbine-repair-fail-blade = Спершу потрібно замінити турбінні лопаті.
gas-turbine-repair-fail-stator = Спершу потрібно замінити статор.
turbine-repair-ruined = Ви ремонтуєте корпус {$target} за допомогою {$tool}.
turbine-repair = Ви усуваєте частину пошкоджень {$target} за допомогою {$tool}.
turbine-no-damage = {$target} не має пошкоджень, які можна усунути за допомогою {$tool}.

turbine-unanchor-warning = Неможливо відкріпити газову турбіну, доки вона обертається!
gas-turbine-eject-fail-speed = Неможливо вийняти деталі турбіни, доки вона обертається!
gas-turbine-insert-fail-speed = Неможливо вставити деталі турбіни, доки вона обертається!

### Інтерфейс турбіни

comp-turbine-ui-tab-main = Керування
comp-turbine-ui-tab-parts = Деталі
comp-turbine-ui-rpm = ОБ/ХВ
comp-turbine-ui-overspeed = ПЕРЕВИЩЕННЯ ОБЕРТІВ
comp-turbine-ui-overtemp = ПЕРЕГРІВ
comp-turbine-ui-stalling = ЗУПИНКА
comp-turbine-ui-undertemp = НИЗЬКА ТЕМПЕРАТУРА
comp-turbine-ui-flow-rate = Швидкість потоку
comp-turbine-ui-stator-load = Навантаження статора
comp-turbine-ui-blade = Турбінні лопаті
comp-turbine-ui-blade-integrity = Цілісність
comp-turbine-ui-blade-stress = Напруження
comp-turbine-ui-stator = Статор турбіни
comp-turbine-ui-stator-potential = Потенціал
comp-turbine-ui-stator-supply = Віддача
comp-turbine-ui-power = { POWERWATTS($power) }
comp-turbine-ui-locked-message = Керування заблоковано.
comp-turbine-ui-footer-left = Небезпека: механізми, що швидко рухаються.
comp-turbine-ui-footer-right = 2.0 РЕД. 1

### Порти автоматики

signal-port-name-nuclear-reactor-data-receiver = Монітор реактора
signal-port-description-nuclear-reactor-data-receiver = Отримує дані про ядерний реактор.
signal-port-name-nuclear-reactor-receiver-insert = Ввести керувальні стрижні
signal-port-description-nuclear-reactor-receiver-insert = Збільшує рівень введення керувальних стрижнів.
signal-port-name-nuclear-reactor-receiver-retract = Вивести керувальні стрижні
signal-port-description-nuclear-reactor-receiver-retract = Зменшує рівень введення керувальних стрижнів.
signal-port-name-gas-turbine-data-receiver = Монітор турбіни
signal-port-description-gas-turbine-data-receiver = Отримує дані про газову турбіну.
signal-port-name-gas-turbine-receiver-increase = Збільшити навантаження
signal-port-description-gas-turbine-receiver-increase = Збільшує навантаження статора турбіни.
signal-port-name-gas-turbine-receiver-decrease = Зменшити навантаження
signal-port-description-gas-turbine-receiver-decrease = Зменшує навантаження статора турбіни.

signal-port-name-nuclear-reactor-data-sender = Ядерний реактор
signal-port-description-nuclear-reactor-data-sender = Надсилає дані про ядерний реактор на монітор.
signal-port-name-gas-turbine-speed-high = Висока швидкість
signal-port-description-gas-turbine-speed-high = Подає ВИСОКИЙ сигнал, коли швидкість перевищує оптимальну.
signal-port-name-gas-turbine-speed-low = Низька швидкість
signal-port-description-gas-turbine-speed-low = Подає ВИСОКИЙ сигнал, коли швидкість нижча за оптимальну.
signal-port-name-gas-turbine-data-sender = Газова турбіна
signal-port-description-gas-turbine-data-sender = Надсилає дані про газову турбіну на монітор.

### Дослідження, матеріали та довідник

materials-cerenkite = церенкіт
materials-plutonium = плутоній
materials-bohrum = борум
stack-plutonium = плутоній
research-technology-nuclear-power = Ядерна енергетика
research-technology-nuclear-recycling = Перероблення ядерних відходів
guide-entry-nuclear-reactor = Ядерний реактор
guide-entry-nuclear-materials = Властивості матеріалів

### Сутності

ent-HeavyFlatpackBase = важкий флетпак
    .desc = Великий флетпак для зберігання підозріло великої машини.
ent-NuclearFabricatorMachineCircuitboard = машинна плата ядерного фабрикатора
    .desc = Машинна друкована плата для ядерного фабрикатора.
ent-NuclearCentrifugeMachineCircuitboard = машинна плата ядерної центрифуги
    .desc = Машинна друкована плата для ядерної центрифуги.
ent-NuclearReactorMonitorComputerCircuitboard = комп'ютерна плата монітора ядерного реактора
    .desc = Комп'ютерна друкована плата монітора ядерного реактора.
ent-GasTurbineMonitorComputerCircuitboard = комп'ютерна плата монітора газової турбіни
    .desc = Комп'ютерна друкована плата монітора газової турбіни.

ent-NuclearReactorFlatpack = флетпак ядерного реактора
    .desc = Флетпак для побудови ядерного реактора. Компоненти продаються окремо.
ent-GasTurbineFlatpack = флетпак газової турбіни
    .desc = Флетпак для побудови газової турбіни.
ent-NuclearReactorSmallFlatpack = флетпак малого ядерного реактора
    .desc = Флетпак для побудови малого ядерного реактора. Компоненти продаються окремо.
ent-GasTurbineSmallFlatpack = флетпак малої газової турбіни
    .desc = Флетпак для побудови малої газової турбіни.

ent-RadiationBlockingProjector = проєктор радіаційного бар'єра
    .desc = Дає змогу ненадовго стримати смертельну радіацію від деламінації або розплавлення реактора.
ent-HolosignRadiationBlocking = голографічний радіаційний бар'єр
    .desc = Бар'єр із твердого світла, що блокує світло й радіацію, але не перешкоджає руху.

ent-IngotPlutonium = плутонієвий зливок
ent-IngotPlutonium1 = плутонієвий зливок

ent-NuclearReactorMonitor = монітор ядерного реактора
    .desc = Пристрій, що відстежує стан під'єднаного ядерного реактора.
ent-GasTurbineMonitor = монітор газової турбіни
    .desc = Пристрій, що відстежує стан під'єднаної газової турбіни.
ent-NuclearCentrifuge = ядерна центрифуга
    .desc = Велика машина для відокремлення радіоактивних ізотопів із відпрацьованого палива.
ent-NuclearFabricator = ядерний фабрикатор
    .desc = Виготовляє компоненти ядерного реактора й газової турбіни.

ent-BaseReactorFuelRod = паливний стрижень
    .desc = Паливний стрижень для ядерного реактора.
ent-CerenkiteReactorFuelRod = церенкітовий паливний стрижень
ent-UraniumReactorFuelRod = урановий паливний стрижень
ent-PlutoniumReactorFuelRod = плутонієвий паливний стрижень
ent-BananiumReactorFuelRod = бананієвий паливний стрижень
    .desc = Гудковий паливний стрижень для ядерного реактора.
ent-PlasmaReactorFuelRod = плазмовий паливний стрижень
ent-UraniumGlassReactorFuelRod = ураново-скляний паливний стрижень
ent-MeatReactorFuelRod = м'ясний паливний стрижень
    .desc = Паливний стрижень... зачекайте, він живий?

ent-BaseReactorControlRod = керувальний стрижень
    .desc = Збірка керувального стрижня для ядерного реактора.
ent-BohrumReactorControlRod = борумовий керувальний стрижень
ent-SteelReactorControlRod = сталевий керувальний стрижень
ent-GoldReactorControlRod = золотий керувальний стрижень
ent-SilverReactorControlRod = срібний керувальний стрижень
ent-BrassReactorControlRod = латунний керувальний стрижень
ent-PlasteelReactorControlRod = пласталевий керувальний стрижень
ent-GlassReactorControlRod = скляний керувальний стрижень
ent-PlasmaGlassReactorControlRod = плазмово-скляний керувальний стрижень
ent-DiamondReactorControlRod = діамантовий керувальний стрижень

ent-BaseReactorGasChannel = газоканальний стрижень
    .desc = Газовий канал для ядерного реактора.
ent-SteelReactorGasChannel = сталевий газоканальний стрижень
ent-GoldReactorGasChannel = золотий газоканальний стрижень
ent-SilverReactorGasChannel = срібний газоканальний стрижень
ent-BrassReactorGasChannel = латунний газоканальний стрижень
ent-PlasteelReactorGasChannel = пласталевий газоканальний стрижень
ent-GlassReactorGasChannel = скляний газоканальний стрижень
ent-PlasmaGlassReactorGasChannel = плазмово-скляний газоканальний стрижень
ent-DiamondReactorGasChannel = діамантовий газоканальний стрижень

ent-BaseReactorHeatExchanger = теплообмінний стрижень
    .desc = Теплообмінник для ядерного реактора.
ent-SteelReactorHeatExchanger = сталевий теплообмінний стрижень
ent-GoldReactorHeatExchanger = золотий теплообмінний стрижень
ent-SilverReactorHeatExchanger = срібний теплообмінний стрижень
ent-BrassReactorHeatExchanger = латунний теплообмінний стрижень
ent-PlasteelReactorHeatExchanger = пласталевий теплообмінний стрижень
ent-GlassReactorHeatExchanger = скляний теплообмінний стрижень
ent-PlasmaGlassReactorHeatExchanger = плазмово-скляний теплообмінний стрижень
ent-DiamondReactorHeatExchanger = діамантовий теплообмінний стрижень

ent-BaseGasTurbineBlade = лопаті газової турбіни
    .desc = Змінні лопаті для газової турбіни.
ent-SteelGasTurbineBlade = сталеві лопаті газової турбіни
ent-BrassGasTurbineBlade = латунні лопаті газової турбіни
ent-DiamondGasTurbineBlade = діамантові лопаті газової турбіни
ent-GoldGasTurbineBlade = золоті лопаті газової турбіни
ent-PlasteelGasTurbineBlade = пласталеві лопаті газової турбіни
ent-BaseGasTurbineStator = статор газової турбіни
    .desc = Змінний статор для газової турбіни.
ent-SteelGasTurbineStator = сталевий статор газової турбіни
ent-SilverGasTurbineStator = срібний статор газової турбіни
ent-GoldGasTurbineStator = золотий статор газової турбіни

ent-BaseNuclearReactor = ядерний реактор
    .desc = Корпус ядерного реактора з комірками для паливних стрижнів та інших компонентів. Стривайте, хіба один із таких не вибухнув?
ent-NuclearReactorCrew = ядерний реактор
ent-NuclearReactorNormal = ядерний реактор
ent-NuclearReactorEmpty = ядерний реактор
ent-NuclearReactorRandom = ядерний реактор
ent-NuclearReactorMeltdown = ядерний реактор
ent-NuclearReactorMelted = розплавлений ядерний реактор
    .desc = Зруйнований корпус ядерного реактора, що досі світиться від жару й радіації.
ent-NuclearReactorSmall = малий ядерний реактор
ent-NuclearReactorSmallRandom = малий ядерний реактор
ent-NuclearReactorSmallMelted = розплавлений малий ядерний реактор
    .desc = Зруйнований корпус ядерного реактора, що досі світиться від жару й радіації.
ent-NuclearReactorSalvage = ядерний реактор
ent-NuclearReactorNormalSalvage = ядерний реактор
ent-NuclearReactorEmptySalvage = ядерний реактор
ent-NuclearReactorRandomSalvage = ядерний реактор
ent-NuclearReactorMeltedSalvage = старий розплавлений ядерний реактор
    .desc = Корпус ядерного реактора, що давно розплавився. Він досі світиться від залишкового жару й радіації.
ent-NuclearReactorSmallSalvage = малий ядерний реактор
ent-NuclearReactorSmallRandomSalvage = малий ядерний реактор
ent-NuclearReactorSmallMeltedSalvage = старий розплавлений малий ядерний реактор
    .desc = Корпус ядерного реактора, що давно розплавився. Він досі світиться від залишкового жару й радіації.
ent-NuclearDebrisChunk = ядерні уламки
    .desc = Ви не бачите графіту на підлозі. Ви в шоці. Зверніться до медичного відділу.

ent-Turbine = газова турбіна
    .desc = Велика турбіна, що виробляє електроенергію з гарячого газу.
ent-TurbineSmall = мала газова турбіна
    .desc = Мала турбіна, що виробляє електроенергію з гарячого газу.
ent-TurbineBladeShrapnel = уламок турбінної лопаті

ent-CrateCerenkiteFuelRod = ящик церенкітових паливних стрижнів
    .desc = Містить три церенкітові паливні стрижні для ядерного реактора.
ent-CrateBohrumControlRod = ящик борумових керувальних стрижнів
    .desc = Містить два борумові керувальні стрижні для ядерного реактора.
ent-CrateSteelGasChannel = ящик сталевих газоканальних стрижнів
    .desc = Містить чотири сталеві газоканальні стрижні для ядерного реактора.
ent-CrateSteelHeatExhanger = ящик сталевих теплообмінних стрижнів
    .desc = Містить чотири сталеві теплообмінні стрижні для ядерного реактора.

### Ролі привидів і цілі

ghost-role-information-plutonium-rod-name = Плутонієвий паливний стрижень
ghost-role-information-plutonium-rod-description = Не хоче перетворитися на електроенергію.
ghost-role-information-plutonium-rod-rules = Ви — [color={role-type-free-agent-color}][bold]{ghost-role-information-plutonium-rod-name}[/bold][/color]. Ваша єдина мета — не потрапити до ядерного реактора чи центрифуги, адже там ви [color=red]помрете[/color].
    Ви не пам'ятаєте свого попереднього життя й нічого з того, що дізналися як привид.
    Ви [color=red]не[/color] пам'ятаєте загальних знань про гру: як готувати, користуватися предметами тощо.
    Вам категорично [color=red]заборонено[/color] згадувати ім'я, зовнішність або будь-які інші подробиці свого попереднього персонажа.

steal-target-groups-reactorfuelrod = паливний стрижень реактора
