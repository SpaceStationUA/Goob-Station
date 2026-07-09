vampires-title = Вампіри

## Alerts
alerts-vampire-blood-name = Випита кров
alerts-vampire-blood-desc = Показує, скільки крові ти випив. Витягни ікла й клацни по цілі, щоб пити кров.
alerts-vampire-fed-name = Ситість кров'ю
alerts-vampire-fed-desc = Твоя поточна вампірська ситість. Пий кров, щоб не голодувати.
alerts-vampire-blood-swell-name = Криваве набухання
alerts-vampire-blood-swell-desc = М'язи наливаються нечестивою силою.
alerts-vampire-blood-rush-name = Кривавий ривок
alerts-vampire-blood-rush-desc = Надприродна швидкість проходить крізь твоє тіло.

## Roles and round state
roles-antag-vamire-name = Вампір
roles-antag-vampire-description = Харчуйся екіпажем. Витягуй ікла та пий їхню кров.
roles-antag-vampire-thrall-name = Вампірський раб
roles-antag-vampire-thrall-description = Служи своєму господарю і виконуй його накази.
vampire-roundend-name = вампір
vampire-role-greeting = Ти вампір!
    Жага крові змушує тебе харчуватися членами екіпажу. Використовуй здібності, щоб вижити й посилитися.
    Ікла дозволяють висмоктувати кров з гуманоїдів. Кров лікує тебе та відкриває нові здібності.
    Знайди, чого досягти під час цієї зміни.
objective-issuer-vampire = [color=crimson]Вампір[/color]
roundend-prepend-vampire-drained-low = Вампіри майже не харчувалися цієї зміни, випивши лише { $blood } крові.
roundend-prepend-vampire-drained-medium = Вампіри непогано попоїли, випивши { $blood } крові.
roundend-prepend-vampire-drained-high = Вампіри влаштували кривавий бенкет, випивши { $blood } крові!
roundend-prepend-vampire-drained-critical = Вампіри зірвалися у криваве шаленство, випивши неймовірні { $blood } крові!
roundend-prepend-vampire-drained = Жоден вампір не зміг випити суттєву кількість крові цього раунду.
roundend-prepend-vampire-drained-named = { $name } був найкровожерливішим вампіром, випивши загалом { $number } крові.

## Objectives
objective-condition-drain-title = Випити { $count } крові
objective-condition-drain-description = Випий { $count } крові з членів екіпажу, використовуючи свої ікла.
objective-vampire-thrall-obey-master-title = Слухатися свого господаря, { $targetName }.
ent-VampireSurviveObjective = Вижити
    .desc = Я маю вижити, чого б це не коштувало.
ent-VampireEscapeObjective = Полетіти до Центкому живим і не скутим.
    .desc = Мені треба втекти на евакуаційному шатлі. Без кайданів.
ent-VampireKillRandomPersonObjective = Усунути ціль
    .desc = Зроби це як завгодно, але ціль не має дістатися Центкому.
ent-VampireDrainObjective = Випити кров
    .desc = Я повинен наситити свою вампірську сутність кров'ю екіпажу.
ent-VampireThrallObeyMasterObjective = Слухатися господаря
    .desc = Ти поневолений. Виконуй накази свого господаря.

## Base vampire
vampire-drink-start = Ти впиваєшся іклами в { CAPITALIZE(THE($target)) }.
vampire-not-enough-blood = Недостатньо крові.
vampire-mouth-covered = Твій рот закритий!
vampire-drink-invalid-target = Ти не можеш пити кров вампірів або їхніх рабів.
vampire-target-protected-by-faith = Ця людина захищена своєю вірою!
vampire-drink-target-empty = У цій істоті не залишилося крові!
vampire-drink-target-maxed = Ти вже випив { $amount } крові з цієї цілі.
vampire-drink-target-hard-max = Ти випив максимум крові з цієї цілі ({ $amount }).
vampire-full-power-achieved = Твоя вампірська сутність досягає повної сили!
vampire-drink-target-not-viable = У цієї істоти немає серця, що б'ється!
vampire-drink-target-rot = Сутність цієї істоти зіпсована!
vampire-sleep-shielded = Імплант не дає приспати цю істоту!
vampire-sleep-protected = Потрібен кращий зоровий контакт...
vampire-space-burn-warning = Жорстке світло космосу обпалює твою неживу плоть!
vampire-holy-place-burn = Священна земля обпалює твою нечестиву плоть!

## Class selection
action-vampire-class-select = Обрати клас вампіра
action-vampire-class-select-desc = Обери свій вампірський підклас.
vampire-class-hemomancer-tooltip = Гемомант
    Зосереджується на магії крові та маніпуляції кров'ю навколо себе.
vampire-class-umbrae-tooltip = Умбра
    Зосереджується на темряві, засідках зі скритності та мобільності.
vampire-class-gargantua-tooltip = Гаргантюа
    Зосереджується на витривалості та ближньому бою.
vampire-class-dantalion-tooltip = Данталіон
    Зосереджується на поневоленні та ілюзіях.

## Entity names
ent-ActionVampireToggleFangs = Перемкнути ікла
    .desc = Витягнути або втягнути ікла, щоб пити кров жертв.
ent-ActionVampireGlare = Погляд
    .desc = Приголомшити найближчі цілі поглядом і виснажити їхню витривалість.
ent-ActionVampireSleep = Сон
    .desc = Приспати ціль на короткий час.
ent-ActionVampireRejuvenateI = Омолодження
    .desc = Негайно прибрати стани оглушення й збиття з ніг та скинути втому.
ent-ActionVampireRejuvenateII = Покращене омолодження
    .desc = Очистити частину отрут, прибрати стани контролю та поступово зцілитися.
ent-ActionClassSelectId = Обрати клас вампіра
    .desc = Обрати свій вампірський підклас.
ent-ActionVampireHemomancerClaws = Вампірські кігті
    .desc = Створити криваві кігті, що висмоктують кров під час ударів.
ent-ActionVampireSanguinePool = Кривава калюжа
    .desc = Перетворитися на рухому калюжу крові.
ent-ActionVampireHemomancerTendrils = Криваві щупальця
    .desc = Викликати щупальця крові на вибраній ділянці.
ent-ActionVampireBloodBarrier = Кривавий бар'єр
    .desc = Створити криваві бар'єри, крізь які ти можеш проходити.
ent-ActionVampirePredatorSense = Чуття хижака
    .desc = Вистежити здобич, якій ніде сховатися.
ent-ActionVampireBloodEruption = Криваве виверження
    .desc = Підійняти шипи крові навколо себе.
ent-ActionVampireBloodBringersRite = Обряд носія крові
    .desc = Увімкнути ауру, що висмоктує кров поруч і лікує тебе.
ent-ActionVampireCloakOfDarkness = Плащ темряви
    .desc = Сховатися в тінях.
ent-ActionVampireShadowSnare = Тіньова пастка
    .desc = Поставити крихку тіньову пастку.
ent-ActionVampireShadowAnchor = Тіньовий якір
    .desc = Закріпити точку в тінях і повернутися до неї.
ent-ActionVampireShadowBoxing = Тіньовий бій
    .desc = Нацькувати тіньових кажанів на ціль.
ent-ActionVampireDarkPassage = Темний прохід
    .desc = Телепортуватися крізь тіні.
ent-ActionVampireExtinguish = Загасити світло
    .desc = Знищити ввімкнені лампи поруч.
ent-ActionVampireEternalDarkness = Вічна темрява
    .desc = Огорнути місцевість темрявою і холодом.
ent-ActionVampireEnthrall = Поневолити
    .desc = Зламати волю гуманоїда й прив'язати його до себе.
ent-ActionVampirePacify = Утихомирити
    .desc = Затопити свідомість жертви спокоєм, тимчасово позбавивши волі до бою.
ent-ActionVampireSubspaceSwap = Підпросторовий обмін
    .desc = Помінятися місцями з живою ціллю.
ent-ActionVampireDecoy = Приманка
    .desc = Лишити крихкого двійника і ненадовго зникнути.
ent-ActionVampireRallyThralls = Зібрати рабів
    .desc = Наказати рабам поруч прийти до тями.
ent-ActionVampireBloodBond = Кривавий зв'язок
    .desc = Зв'язати себе з рабами кривавими путами, ділячи вхідну шкоду.
ent-ActionVampireMassHysteria = Масова істерія
    .desc = Наповнити жахом усіх нерабів поруч.
ent-ActionVampireBloodSwell = Криваве набухання
    .desc = Налити тіло кров'ю, зменшуючи отриману шкоду.
ent-ActionVampireBloodRush = Кривавий ривок
    .desc = Тимчасово прискорити рух.
ent-ActionVampireSeismicStomp = Сейсмічний тупіт
    .desc = Вдарити по землі, збиваючи істот навколо.
ent-ActionVampireOverwhelmingForce = Непереборна сила
    .desc = Силою розчиняти двері й ігнорувати поштовхи.
ent-ActionVampireDemonicGrasp = Демонічна хватка
    .desc = Запустити демонічну руку, що знерухомлює ціль.
ent-ActionVampireCharge = Ривок
    .desc = Пронестися вперед з руйнівною силою.
ent-VampiricClawsItem = вампірські кігті
    .desc = Кігті, викувані з крові, висмоктують життєву силу під час ударів.
ent-MobVampireSanguinePool = кривава калюжа
    .desc = Розумна калюжа вампірської крові.

## Hemomancer
action-vampire-hemomancer-tendrils-wrong-place = Тут не можна це використати.
action-vampire-blood-barrier-wrong-place = Тут не можна поставити бар'єр.
action-vampire-sanguine-pool-already-in = Ти вже у формі кривавої калюжі!
action-vampire-sanguine-pool-invalid-tile = Тут не можна стати кривавою калюжею.
action-vampire-sanguine-pool-enter = Ти перетворюєшся на калюжу крові!
action-vampire-sanguine-pool-exit = Ти знову збираєшся з крові!
action-vampire-blood-eruption-activated = Ти змушуєш кров вибухнути шипами навколо себе!
action-vampire-blood-eruption-no-blood = Поблизу немає крові на підлозі, яку можна підняти шипами.
action-vampire-blood-eruption-no-targets = Поблизу немає жертв біля крові, яку можна підняти шипами.
action-vampire-blood-bringers-rite-not-enough-power = Бракує повної вампірської сили: потрібно понад 1000 загальної крові та 8 унікальних жертв.
action-vampire-blood-brighters-rite-not-enough-blood = Недостатньо крові, щоб активувати обряд носія крові.
action-vampire-blood-bringers-rite-start = Обряд носія крові активовано!
action-vampire-blood-bringers-rite-stop = Обряд носія крові вимкнено.
action-vampire-blood-bringers-rite-stop-blood = Обряд носія крові згас: бракує крові.
vampire-locate-result = Твої чуття ведуть до { $target }: { $location }.
vampire-locate-not-same-sector = Ця людина не у твоєму секторі.
vampire-locate-unknown = Невідома місцевість
vampire-locate-no-targets = У цьому секторі не відчувається інших гуманоїдів із розумом.
predator-sense-title = Чуття хижака
vampire-locate-search-placeholder = Пошук...
vampiric-claws-remove-popup = Ти змушуєш кігті зникнути.

## Umbrae
action-vampire-cloak-of-darkness-start = Ти зливаєшся з тінями!
action-vampire-cloak-of-darkness-stop = Ти виходиш із тіней.
action-vampire-shadow-snare-placed = Ти ставиш тіньову пастку.
action-vampire-shadow-snare-wrong-place = Тут не можна поставити пастку.
action-vampire-shadow-snare-scatter = Ти розсіюєш тіньову пастку.
vampire-shadow-snare-oldest-removed = Твоя стара тіньова пастка розсіюється.
ent-shadow-snare-ensnare = тіньова пастка
action-vampire-shadow-anchor-returned = Ти повертаєшся до тіньового якоря.
action-vampire-shadow-anchor-installed = Ти закріплюєш місце в тінях.
action-vampire-shadow-boxing-start = Ти починаєш тіньовий бій.
action-vampire-shadow-boxing-stop = Тіньовий бій зупинено.
action-vampire-shadow-boxing-ends = Тіньовий бій завершується.
action-vampire-dark-passage-wrong-place = Темрява тут непроникна...
action-vampire-dark-passage-activated = Ти прослизаєш крізь темряву...
action-vampire-extinguish-activated = Ти поглинаєш світло навколо... ({ $count })
action-vampire-eternal-darkness-not-enough-blood = У тебе закінчилася кров для підтримання вічної темряви.
action-vampire-eternal-darkness-start = Ти викликаєш вічну темряву...
action-vampire-eternal-darkness-stop = Вічна темрява розсіюється...
vampire-umbrae-full-power-fov = Тіні коряться твоїй волі. Тепер ти бачиш крізь стіни!

## Dantalion
vampire-enthrall-start = Ти проникаєш у розум { CAPITALIZE(THE($target)) }...
vampire-enthrall-success = { CAPITALIZE(THE($target)) } схиляється перед тобою і стає твоїм рабом.
vampire-enthrall-target = Твій розум придушено вампірським пануванням!
vampire-enthrall-limit = Ти не можеш контролювати більше рабів.
vampire-enthrall-invalid = Цю ціль не можна поневолити.
vampire-thrall-released = Вампірська влада над твоїм розумом згасає.
vampire-pacify-invalid = Цю ціль не можна утихомирити.
vampire-pacify-success = { CAPITALIZE(THE($target)) } піддається твоєму всепоглинальному спокою.
vampire-pacify-target = Нищівний спокій топить твою волю до бою!
vampire-subspace-swap-thrall = Ти не можеш мінятися місцями зі своїми рабами.
vampire-subspace-swap-dead = Цей розум уже поза твоєю досяжністю.
vampire-subspace-swap-failed = Підпросторовий розрив марно згасає.
vampire-subspace-swap-success = Простір викривлюється, і ти міняєшся місцями з { CAPITALIZE(THE($target)) }!
vampire-subspace-swap-target = Реальність викривлюється, і тебе вириває в нове місце!
vampire-rally-thralls-success = { $count ->
    [one] Твій поклик повертає раба до тями!
    *[other] Твій поклик повертає до тями рабів: { $count }!
}
vampire-rally-thralls-none = Жоден твій раб не може відповісти на поклик.
vampire-thrall-holy-water-freed = Свята вода очищує твій розум від влади вампіра!
vampire-blood-bond-start = Потоки крові зшивають тебе з рабами.
vampire-blood-bond-stop = Ти послаблюєш кривавий зв'язок.
vampire-blood-bond-no-thralls = У тебе немає поневолених слуг для зв'язку.
vampire-blood-bond-stop-blood = Зв'язок рветься: тобі бракує крові для підтримки.
action-vampire-not-enough-power = Твоєї сили недостатньо: потрібно понад 1000 загальної крові та 8 унікальних жертв.
Vamp-converted-title = Поневолено!
Vamp-converted-text = Тебе поневолено!
    Вірно слухайся свого господаря.
Vamp-converted-confirm = Зрозуміло

## Gargantua
vampire-blood-swell-start = Твої м'язи набухають нечестивою силою.
vampire-blood-swell-end = Кривава лють спадає.
vampire-blood-rush-start = Кров рине крізь твої кінцівки!
vampire-blood-rush-end = Надприродна швидкість зникає.
vampire-seismic-stomp-activate = Земля здригається під твоєю люттю!
vampire-overwhelming-force-start = Твоя присутність стає непохитною.
vampire-overwhelming-force-stop = Ти послаблюєш залізну хватку.
vampire-overwhelming-force-too-heavy = Цей об'єкт занадто важкий, щоб його зрушити!
vampire-overwhelming-force-door-pried = Ти силою розчиняєш двері.
vampire-demonic-grasp-hit = Демонічна рука хапає тебе!
vampire-demonic-grasp-pull = Рука тягне ціль до вампіра!
vampire-legs-ensnared = Твої ноги скувала демонічна хватка!
vampire-charge-start = Ти мчиш уперед з нестримною силою!
vampire-charge-impact = Ти врізаєшся в { CAPITALIZE(THE($target)) } з руйнівною силою!
vampire-blood-swell-cancel-shoot = Твої пальці не влазять у спускову скобу!

## Language
language-chat-confirmation = Повідомлення буде надіслано мовою { $lang }.
language-Dantalion-name = Рабський зв'язок
chat-language-Dantalion-name = Рабський зв'язок
language-Dantalion-description = Безмовний зв'язок між Данталіоном і його поневоленими.
collective-mind-dantalion = Рабський зв'язок
chat-speech-verb-hivemind-1 = думає
chat-speech-verb-hivemind-2 = розмірковує
chat-speech-verb-hivemind-3 = замислюється
chat-speech-verb-hivemind-4 = уявляє
chat-speech-verb-hivemind-5 = візуалізує
chat-speech-verb-hivemind-6 = бачить подумки
chat-speech-verb-hivemind-7 = марить