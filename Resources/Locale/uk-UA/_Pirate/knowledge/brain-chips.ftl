
verb-categories-organ-chips = Чипи

organ-chip-verb-none = Чипів не встановлено ({ $organ })
organ-chip-verb-remove-known = Вийняти { $chip }
organ-chip-verb-remove-unknown = Вийняти чип { $index } ({ $organ })
organ-chip-unchipped = Не має аугментації для встановлення мозкових чипів.

organ-chip-insert-start-self = Ви починаєте встановлювати чип у свій { $organ }!
organ-chip-insert-start-other = Ви починаєте встановлювати чип у { $organ } ({ CAPITALIZE($target) })!
organ-chip-insert-start-victim = { CAPITALIZE($user) } починає встановлювати чип у ваш { $organ }!
organ-chip-insert-start-loose = Ви починаєте встановлювати чип у { $organ }.
organ-chip-insert-success = Ви встановлюєте чип у { $organ }.

organ-chip-remove-start-self = Ви починаєте виколупувати чип зі свого { $organ }!
organ-chip-remove-start-other = Ви починаєте виколупувати чип із { $organ } ({ CAPITALIZE($target) })!
organ-chip-remove-start-victim = { CAPITALIZE($user) } починає виколупувати чип із вашого { $organ }!
organ-chip-remove-start-loose = Ви починаєте виколупувати чип із { $organ }.
organ-chip-remove-success = Ви виймаєте чип із { $organ }.

organ-chip-not-a-chip = Це не чип.
organ-chip-incompatible = { CAPITALIZE($chip) } не підходить до органа { $organ }.
organ-chip-no-room = У { $organ } більше немає місця для чипів.
organ-chip-duplicate = Такий самий чип уже встановлено.
organ-chip-same-family = { CAPITALIZE($chip) } конфліктує з: { $installed }. Можна встановити лише один чип із кожного сімейства.
organ-chip-installed-unknown = чип { $index } ({ $organ })
organ-chip-family-examine = Сімейство: [bold]{ $family }[/bold]. Можна встановити лише один чип із цього сімейства.
hormone-chip-examine = Поки чип встановлено:
hormone-chip-examine-crit-up = - [color=green]+{ $amount }[/color] шкоди до критичного стану
hormone-chip-examine-crit-down = - [color=red]-{ $amount }[/color] шкоди до критичного стану
hormone-chip-examine-stamina-up = - [color=green]+{ $amount }%[/color] витривалості до знесилення
hormone-chip-examine-stamina-down = - [color=red]-{ $amount }%[/color] витривалості до знесилення
hormone-chip-examine-heat-up = - [color=green]+{ $amount }%[/color] стійкості до спеки
hormone-chip-examine-heat-down = - [color=red]-{ $amount }%[/color] стійкості до спеки
hormone-chip-examine-cold-up = - [color=green]+{ $amount }%[/color] стійкості до холоду
hormone-chip-examine-cold-down = - [color=red]-{ $amount }%[/color] стійкості до холоду
hormone-chip-examine-light-step = - [color=green]тихі кроки[/color]
psionic-amplifier-chip-examine = Поки чип встановлено:
psionic-amplifier-chip-examine-cooldown = - Час відновлення псіонічних сил [color=green]менший на { $percent }%[/color]
psionic-amplifier-chip-examine-dispel = - Розвіювання [color=red]завдає носію електричної шкоди[/color]
psionic-amplifier-chip-examine-overload = - [color=red]антипсіонічна зброя завжди вимикає сили[/color] на { $seconds } с
organ-chip-not-removable = Цей чип вмонтовано намертво.
organ-chip-no-self-removal = Ви не дотягнетеся до цього чипа самотужки. Потрібна чужа рука.
organ-chip-need-hard-grab = Спершу потрібен жорсткий захват!
organ-chip-out-of-reach = Ви більше не дотягуєтеся до місця операції.
organ-chip-lost-chip = Чипа більше немає у ваших руках.

knowledge-grant-on-wear-examine = Поки встановлено, змінює навички:
knowledge-grant-on-wear-examine-positive = - [color=green]+{ $level }[/color] [bold]{ $skill }[/bold]
knowledge-grant-on-wear-examine-negative = - [color=red]{ $level }[/color] [bold]{ $skill }[/bold]


organ-chip-family-overwrite = фракційний пакет навичок
organ-chip-family-armorsmithing = спогади бронника
organ-chip-family-weaponsmithing = спогади зброяра
organ-chip-family-blacksmith = спогади коваля
organ-chip-family-woodworker = спогади столяра
organ-chip-family-gunsmith = спогади рушничного майстра
organ-chip-family-mechanic = спогади механіка
organ-chip-family-electronics = спогади електрика
organ-chip-family-tailor = спогади кравця
organ-chip-family-database = база креслень
organ-chip-family-database-syndicate = база креслень Синдикату
organ-chip-family-sidearms = особиста зброя
organ-chip-family-education = освіта клона
organ-chip-family-throwing = координація рук і очей
organ-chip-family-endorphin = больовий поріг
organ-chip-family-adrenal = витривалість
organ-chip-family-thyroid = терморегуляція


ent-BaseBrainChip = мозковий чип
    .desc = Стерилізований мікрочип, що взаємодіє напряму з мозковою тканиною.
ent-BaseSkillChipMRAM = ПЗП-чип
    .desc = Нейрочип із записами чужих знань і професійного досвіду.
ent-BaseSkillChipAPTR = ГАРТ-чип
    .desc = Нейрочип із записами бойових рефлексів і навичок поводження зі зброєю.
ent-BaseSkillChipPSON = ОТРУ-чип
    .desc = Нейрочип, що змінює активність мозку. Ця серія містить пригнічувачі навичок і псіонічні підсилювачі.
ent-BaseSkillChipHPYS = СОМА-чип
    .desc = Нейрочип, що коригує рухи та гормональну регуляцію.


ent-SkillChipHeavy = ГАРТ-чип (важка зброя)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipLaser = ГАРТ-чип (лазерна зброя)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipMarksmanship = ГАРТ-чип (влучність)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipMining = ГАРТ-чип (шахтарський інструмент)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipTool = ГАРТ-чип (бойовий інструмент)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipPistol = ГАРТ-чип (пістолети)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipRifle = ГАРТ-чип (довгоствольна зброя)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipSMG = ГАРТ-чип (пістолети-кулемети)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipShotgun = ГАРТ-чип (дробовики)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipSniper = ГАРТ-чип (снайперська зброя)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipEnergy = ГАРТ-чип (енергетична зброя)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipBludgeon = ГАРТ-чип (дрючки)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipCloseQuarters = ГАРТ-чип (ближній бій)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipShortBlade = ГАРТ-чип (короткі клинки)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipLongBlade = ГАРТ-чип (довгі клинки)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipNonLethal = ГАРТ-чип (нелетальна зброя)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipPolearm = ГАРТ-чип (древкова зброя)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipUnarmed = ГАРТ-чип (рукопашний бій)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipShield = ГАРТ-чип (щити)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipCombatHOS = ПЗП-чип (розширена бойова підготовка)
    .desc = { ent-BaseSkillChipMRAM.desc }


ent-SkillChipArmorsmithing = ПЗП-чип (спогади бронника)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipArmorsmithing2 = ПЗП-чип (спогади майстра-бронника)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipWeaponsmithing = ПЗП-чип (спогади зброяра)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipWeaponsmithing2 = ПЗП-чип (спогади майстра-зброяра)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipBlacksmith = ПЗП-чип (спогади коваля)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipBlacksmith2 = ПЗП-чип (спогади майстра-коваля)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipWoodworker = ПЗП-чип (спогади столяра)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipWoodworker2 = ПЗП-чип (спогади майстра-столяра)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipGunsmith = ПЗП-чип (спогади рушничного майстра)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipGunsmith2 = ПЗП-чип (спогади старшого рушничного майстра)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipMechanic = ПЗП-чип (спогади механіка)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipMechanic2 = ПЗП-чип (спогади майстра-механіка)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipElectronics = ПЗП-чип (спогади електрика)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipElectronics2 = ПЗП-чип (спогади майстра-електрика)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipTailor = ПЗП-чип (спогади кравця)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipTailor2 = ПЗП-чип (спогади майстра-кравця)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipStoner = ПЗП-чип (спогади торчка)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipDatabase = ПЗП-чип (база креслень)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipEducation = ПЗП-чип (стандартна освіта клона)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipEducationPilot = ПЗП-чип (стандартна освіта клона, пілот)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipCombatEducation = ПЗП-чип (бойова освіта клона)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipCombatEducationPilot = ПЗП-чип (бойова освіта клона, пілот)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipEchelonEducation = ПЗП-чип (командна освіта клона)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipEchelonEducationPilot = ПЗП-чип (командна освіта клона, пілот)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipMagLit = ПЗП-чип (спогади культиста)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipJanitor = ПЗП-чип (спогади прибиральника)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipClown = ПЗП-чип (спогади клоуна)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipDoctor = ПЗП-чип (спогади лікаря)
    .desc = Стерилізований мікрочип, що взаємодіє напряму з мозковою тканиною. Медична підготовка йде в комплекті з придушувачем агресивних рефлексів.
ent-SkillChipService = ПЗП-чип (сервісний персонал)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipChemist = ПЗП-чип (спогади хіміка)
    .desc = { ent-SkillChipDoctor.desc }
ent-SkillChipSurgeon = ПЗП-чип (спогади хірурга)
    .desc = { ent-SkillChipDoctor.desc }
ent-SkillChipCMO = ПЗП-чип (спогади головного хірурга)
    .desc = { ent-BaseSkillChipMRAM.desc }


ent-SkillChipMagicalDampener = ОТРУ-чип (пригнічення магічної грамотності)
    .desc = Пригнічує магічну грамотність, поки встановлений. Вийняти його може лише хтось інший.
ent-SkillChipCombatDampener = ОТРУ-чип (пригнічення бойових навичок)
    .desc = Пригнічує бойові навички, поки встановлений. Вийняти його може лише хтось інший.
ent-SkillChipMindPurge = ОТРУ-чип (пригнічення бойових навичок)
    .desc = { ent-SkillChipCombatDampener.desc }
ent-SkillChipTiderDampener = ОТРУ-чип (нейродемпфер)
    .desc = Знижує навички ближнього бою та стрільби, поки встановлений. Вийняти його може лише хтось інший.
ent-SkillChipPsionicAmplifier = ОТРУ-чип (псіонічний підсилювач)
    .desc = Удвічі скорочує час відновлення псіонічних сил. Розвіювання завдає носію електричної шкоди, а антипсіонічна зброя тимчасово вимикає його сили.


ent-SkillChipThrowing = СОМА-чип (координація рук і очей)
    .desc = Покращує техніку кидка. Кинуті предмети летять швидше та б'ють сильніше.
ent-SkillChipThrowingTampered = СОМА-чип (розігнана координація рук і очей)
    .desc = Розігнаний чип координації, що покращує контроль рухів під час кидка.
ent-SkillChipEndorphin = СОМА-чип (больовий поріг)
    .desc = Регулює виділення ендорфінів, щоб ви могли витримати більше шкоди до критичного стану. Травм не лікує.
ent-SkillChipAdrenal = СОМА-чип (витривалість)
    .desc = Регулює виділення адреналіну, щоб ви могли витримати більше шкоди витривалості до знесилення.
ent-SkillChipThyroid = СОМА-чип (терморегуляція)
    .desc = Коригує роботу щитоподібної залози, щоб тіло витримувало вищі та нижчі температури без шкоди.
ent-SkillChipProprioception = СОМА-чип (тихий крок)
    .desc = Коригує ходу, щоб ваші кроки не видавали звуку.


ent-SkillChipDeathSquad = ПЗП-чип (ескадрон смерті)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipERT = ПЗП-чип (ГШР)
    .desc = { ent-BaseSkillChipMRAM.desc }


ent-SkillChipNukie = ПЗП-чип (горлекс)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipSyndieSoldierTeamLeader = ПЗП-чип (командир синдикату)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipSyndieSoldier = ПЗП-чип (боєць синдикату)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipFieldMedicine = ПЗП-чип (польова медицина)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipFreelancer = ПЗП-чип (фрілансер)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipSidearms = ГАРТ-чип (особиста зброя)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipSidearmsAdvanced = ГАРТ-чип (особиста зброя: просунутий)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipDatabaseBasic = ПЗП-чип (елементарна база креслень)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipSyndieMarshal = ПЗП-чип (маршал синдикату)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipSyndieVisitor = ПЗП-чип (гість синдикату)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipPirateCaptainScooner = ПЗП-чип (піратський капітан)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipPirateScooner = ПЗП-чип (пірат)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipBlackmarketeer = ПЗП-чип (чорний ділок)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipCossack = ПЗП-чип (козак)
    .desc = { ent-BaseSkillChipMRAM.desc }

# Брендовані копії Синдикату мають ті самі назви й описи, що й оригінали.
ent-SkillChipUnarmedSyndicate = { ent-SkillChipUnarmed }
    .desc = { ent-SkillChipUnarmed.desc }
ent-SkillChipBludgeonSyndicate = { ent-SkillChipBludgeon }
    .desc = { ent-SkillChipBludgeon.desc }
ent-SkillChipShortBladeSyndicate = { ent-SkillChipShortBlade }
    .desc = { ent-SkillChipShortBlade.desc }
ent-SkillChipLongBladeSyndicate = { ent-SkillChipLongBlade }
    .desc = { ent-SkillChipLongBlade.desc }
ent-SkillChipPolearmSyndicate = { ent-SkillChipPolearm }
    .desc = { ent-SkillChipPolearm.desc }
ent-SkillChipNonLethalSyndicate = { ent-SkillChipNonLethal }
    .desc = { ent-SkillChipNonLethal.desc }
ent-SkillChipToolSyndicate = { ent-SkillChipTool }
    .desc = { ent-SkillChipTool.desc }
ent-SkillChipEnergySyndicate = { ent-SkillChipEnergy }
    .desc = { ent-SkillChipEnergy.desc }
ent-SkillChipSMGSyndicate = { ent-SkillChipSMG }
    .desc = { ent-SkillChipSMG.desc }
ent-SkillChipPistolSyndicate = { ent-SkillChipPistol }
    .desc = { ent-SkillChipPistol.desc }
ent-SkillChipRifleSyndicate = { ent-SkillChipRifle }
    .desc = { ent-SkillChipRifle.desc }
ent-SkillChipShotgunSyndicate = { ent-SkillChipShotgun }
    .desc = { ent-SkillChipShotgun.desc }
ent-SkillChipSniperSyndicate = { ent-SkillChipSniper }
    .desc = { ent-SkillChipSniper.desc }
ent-SkillChipLaserSyndicate = { ent-SkillChipLaser }
    .desc = { ent-SkillChipLaser.desc }
ent-SkillChipHeavySyndicate = { ent-SkillChipHeavy }
    .desc = { ent-SkillChipHeavy.desc }
ent-SkillChipFieldMedicineSyndicate = { ent-SkillChipFieldMedicine }
    .desc = { ent-SkillChipFieldMedicine.desc }
ent-SkillChipDatabaseSyndicate = { ent-SkillChipDatabase }
    .desc = { ent-SkillChipDatabase.desc }
