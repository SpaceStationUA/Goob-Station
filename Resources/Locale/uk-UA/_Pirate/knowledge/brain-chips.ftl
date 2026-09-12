### Мозкові чипи: інтерфейс, взаємодія та огляд.

verb-categories-organ-chips = Чипи

organ-chip-verb-none = Чипів не встановлено ({ $organ })
organ-chip-verb-remove-known = Вийняти { $chip }
organ-chip-verb-remove-unknown = Вийняти чип { $index } ({ $organ })

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
organ-chip-not-removable = Цей чип вмонтовано намертво.
organ-chip-no-self-removal = Ви не дотягнетеся до цього чипа самотужки. Потрібна чужа рука.
organ-chip-need-hard-grab = Спершу потрібен жорсткий захват!
organ-chip-out-of-reach = Ви більше не дотягуєтеся до місця операції.
organ-chip-lost-chip = Чипа більше немає у ваших руках.

knowledge-grant-on-wear-examine = Поки встановлено, змінює навички:
knowledge-grant-on-wear-examine-positive = - [color=green]+{ $level }[/color] [bold]{ $skill }[/bold]
knowledge-grant-on-wear-examine-negative = - [color=red]{ $level }[/color] [bold]{ $skill }[/bold]

### Чипи

ent-BaseBrainChip = мозковий чип
    .desc = Стерилізований мікрочип, що взаємодіє напряму з мозковою тканиною.
ent-BaseSkillChipMRAM = ПЗП-чип
    .desc = Стерилізований мікрочип, що взаємодіє напряму з мозковою тканиною. Цей підкидає спогади про навички, яких ви ніколи не вчили.
ent-BaseSkillChipAPTR = ГАРТ-чип
    .desc = Стерилізований мікрочип, що взаємодіє напряму з мозковою тканиною. Цей дає рефлекси та вроджене розуміння зброї.
ent-BaseSkillChipPSON = ОТРУ-чип
    .desc = Стерилізований мікрочип, що взаємодіє напряму з мозковою тканиною. Цей притлумлює вроджені навички та переписує особистість.
ent-BaseSkillChipHPYS = СОМА-чип
    .desc = Стерилізований мікрочип, що взаємодіє напряму з мозковою тканиною. Цей зміщує гормональний баланс, покращуючи фізичні дані.

# APTR (ГАРТ)

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

# MRAM (ПЗП)

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
ent-SkillChipCombatEducation = ПЗП-чип (бойова освіта клона)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipEchelonEducation = ПЗП-чип (командна освіта клона)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipMagLit = ПЗП-чип (спогади культиста)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipJanitor = ПЗП-чип (спогади прибиральника)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipClown = ПЗП-чип (спогади клоуна)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipDoctor = ПЗП-чип (спогади лікаря)
    .desc = Стерилізований мікрочип, що взаємодіє напряму з мозковою тканиною. Медична підготовка йде в комплекті з придушувачем агресивних рефлексів.
ent-SkillChipChemist = ПЗП-чип (спогади хіміка)
    .desc = { ent-SkillChipDoctor.desc }
ent-SkillChipSurgeon = ПЗП-чип (спогади хірурга)
    .desc = { ent-SkillChipDoctor.desc }
ent-SkillChipCMO = ПЗП-чип (спогади головного хірурга)
    .desc = { ent-BaseSkillChipMRAM.desc }

# PSON (ОТРУ)

ent-SkillChipMagicalDampener = ОТРУ-чип (випалення магічного хисту)
    .desc = { ent-BaseSkillChipPSON.desc }
ent-SkillChipCombatDampener = ОТРУ-чип (випалення бойового хисту)
    .desc = { ent-BaseSkillChipPSON.desc }
ent-SkillChipMindPurge = ОТРУ-чип (випалення розуму)
    .desc = { ent-BaseSkillChipPSON.desc }
ent-SkillChipTiderDampener = ОТРУ-чип (нейродемпфер)
    .desc = Стерилізований мікрочип, що взаємодіє напряму з мозковою тканиною. Цей збиває запал. Щоб вийняти його, знадобиться друг.

# HPYS (СОМА)

ent-SkillChipThrowing = СОМА-чип (координація рук і очей)
    .desc = { ent-BaseSkillChipHPYS.desc }
ent-SkillChipThrowingTampered = СОМА-чип (розігнана координація рук і очей)
    .desc = { ent-BaseSkillChipHPYS.desc }

# Центком

ent-SkillChipDeathSquad = ОТРУ-чип (перезапис: ескадрон смерті)
    .desc = { ent-BaseSkillChipPSON.desc }
ent-SkillChipERT = ОТРУ-чип (перезапис: ГШР)
    .desc = { ent-BaseSkillChipPSON.desc }

# Антагоністи

ent-SkillChipNukie = ПЗП-чип (горлекс)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipSyndieSoldierTeamLeader = ПЗП-чип (командир синдикату)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipSyndieSoldier = ПЗП-чип (боєць синдикату)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipFieldMedicine = ПЗП-чип (польова медицина)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipFreelancer = ОТРУ-чип (перезапис: фрілансер)
    .desc = { ent-BaseSkillChipPSON.desc }
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
