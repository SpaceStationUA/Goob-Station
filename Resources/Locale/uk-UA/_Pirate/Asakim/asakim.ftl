roles-antag-asakim-name = Асаким
roles-antag-asakim-objective = Виконуйте стародавні директиви корабельного ШІ.
role-subtype-asakim = Асаким
species-name-asakim = Асаким

ghost-role-information-asakim-name = Воїн Асаким
ghost-role-information-asakim-description = Генетично вдосконалена біозброя, що опинилася в цьому секторі після збою кріостазу.
ghost-role-information-asakim-rules =
    Ви [color=yellow][bold]вільний агент[/bold][/color]. Можете виконувати видані цілі або самостійно обрати свій шлях.
    Ви [color=red]нічого не знаєте[/color] (щонайбільше найпростіші речі) [color=red]про цей сектор космосу, його мешканців, угруповання чи культуру[/color]. Ви розумієте їхню мову, але не можете нормально нею говорити.
    Ви можете вбивати визначені цілі, а також захищати себе та своє майно. [color=green]Цивільні й невинні не є вашими ворогами[/color] та можуть стати корисними союзниками, якщо ви подолаєте мовний бар’єр. Спробуйте знайти AAC-планшет. Бийтеся з честю.

asakim-role-briefing =
    Ви Асаким — генетично створена зброя з далекої частини космосу.
    Ви прокинулися після тривалого кріосну в незнайомому секторі та незнайомій епосі.
    Ваш корабель передав нові накази безпосередньо у ваш мозок...

objective-issuer-asakim = [color=#5085fa]Автоматизована система оборони[/color]
objective-condition-asakim-terminate-title = >ЛІКВІДУВАТИ {$targetName}, {CAPITALIZE($job)}.
objective-condition-asakim-terminate-reroll-message = <ПОМИЛКА>: ВИЯВЛЕНО ЦИКЛ ЗВОРОТНОГО ЗВ’ЯЗКУ. НОВИЙ СУБ’ЄКТ ДЛЯ ЛІКВІДАЦІЇ: {$targetName}, {CAPITALIZE($job)}.

damage-on-unequip-examine = [color=red]Ви відчуваєте, що знімати це — погана ідея...[/color]
damage-on-unequip-begin = {$item} починає скрипіти й стогнати...
damage-on-unequip-finish = {$item} здирається з тіла {$wearer}!

ent-MindRoleAsakim = роль Асакима
ent-MobAsakimGhostrole = воїн Асаким
    .desc = Генетично вдосконалена рептилоїдна біозброя.
    .suffix = Асаким
ent-MobAsakimDummy =
    .desc = Манекен Асакима для меню створення персонажа.
ent-OrganAsakimEyes = очі Асакима

ent-PartAsakim = частина тіла Асакима
ent-ChestAsakim = груди Асакима
ent-GroinAsakim = пах Асакима
ent-HeadAsakim = голова Асакима
ent-LeftArmAsakim = ліва рука Асакима
ent-RightArmAsakim = права рука Асакима
ent-LeftHandAsakim = ліва кисть Асакима
ent-RightHandAsakim = права кисть Асакима
ent-LeftLegAsakim = ліва нога Асакима
ent-RightLegAsakim = права нога Асакима
ent-LeftFootAsakim = ліва стопа Асакима
ent-RightFootAsakim = права стопа Асакима

ent-ClothingHelmetHardsuitAsakim = шолом бойової системи
    .desc = Частина вдосконаленої бойової системи.
ent-ClothingOuterHardsuitAsakim = бойова система
    .desc = Вдосконалена бойова система. Надзвичайно рідкісна й майже чужинська порівняно із сучасними скафандрами технологія. Після вдягання броня з’єднується з тілом носія, і зняти її можна лише за зовнішньої допомоги.
    .suffix = Незнімна
ent-ClothingOuterHardsuitAsakimUnremoveable = { ent-ClothingOuterHardsuitAsakim }
    .desc = Вдосконалена бойова система. Надзвичайно рідкісна й майже чужинська порівняно із сучасними скафандрами технологія. Цей екземпляр повністю з’єднався з воїном, тому його зняття триватиме дуже довго.
    .suffix = Зняття 120 секунд
ent-ClothingShoesBootsMagAsakim = бойові магнітні черевики
    .desc = Найсучасніші магнітні черевики для використання з удосконаленими бойовими системами.
ent-ClothingBackpackAdvancedTactical = удосконалений тактичний рюкзак
    .desc = Місткий рюкзак із великою кількістю кишень.
ent-ClothingUniformJumpsuitAsakim = бойовий комбінезон
    .desc = Зручний і гнучкий комбінезон для носіння під скафандром.

ent-WeaponRifleAsakimAutopulser = плазмовий автомат
    .desc = Вдосконалена ручна плазмова гвинтівка.
ent-HandheldAutopulserProjectile = плазмовий заряд
ent-HandheldAutopulserDisablerProjectile = паралізувальний плазмовий заряд
ent-WeaponHFKatana = високочастотний клинок
    .desc = Надзвичайно потужний клинок, що вібрує з високою частотою.

ent-AsakimKillObjective = >ЛІКВІДУВАТИ
    .desc = Життєві показники суб’єкта МАЮТЬ досягти нуля. Один раз.
ent-AsakimTheftObjective = >КОНФІСКУВАТИ
    .desc = Записи вказують, що в цьому секторі можуть бути цінні предмети. Конфіскуйте їх для подальшого дослідження.
ent-AsakimInfiltrateObjective = >ПРОНИКНУТИ
    .desc = У місцевому радіоефірі згадуються «зони підвищеної безпеки». Дослідіть такі місця, якщо вони справді існують.
ent-AsakimInterrogateObjective = >ДОПИТАТИ
    .desc = УВАГА: виявлено часовий розрив у [ПОМИЛКА] років. Записи можуть бути застарілими. Опитайте місцеве населення.
ent-AsakimUpgradeObjective = >ВДОСКОНАЛИТИСЯ
    .desc = Попереднє сканування сектора виявило можливі цінні кібернетичні технології. Вдоскональте своє тіло.
ent-AsakimAllyObjective = >ОБ’ЄДНАТИСЯ
    .desc = Попередні записи вказують на можливих осіб, що становлять інтерес у цьому секторі. Знайдіть або здобудьте союзників серед місцевого населення.
