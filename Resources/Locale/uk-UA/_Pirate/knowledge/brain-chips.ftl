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
ent-BaseSkillChipMRAM = MRAM-чип
    .desc = Стерилізований мікрочип, що взаємодіє напряму з мозковою тканиною. Цей підкидає спогади про навички, яких ви ніколи не вчили.
ent-BaseSkillChipAPTR = APTR-чип
    .desc = Стерилізований мікрочип, що взаємодіє напряму з мозковою тканиною. Цей дає рефлекси та вроджене розуміння зброї.
ent-BaseSkillChipPSON = PSON-чип
    .desc = Стерилізований мікрочип, що взаємодіє напряму з мозковою тканиною. Цей притлумлює вроджені навички та переписує особистість.
ent-BaseSkillChipHPYS = HPYS-чип
    .desc = Стерилізований мікрочип, що взаємодіє напряму з мозковою тканиною. Цей зміщує гормональний баланс, покращуючи фізичні дані.

# APTR

ent-SkillChipHeavy = APTR-чип (важка зброя)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipLaser = APTR-чип (лазерна зброя)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipMarksmanship = APTR-чип (влучність)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipMining = APTR-чип (шахтарський інструмент)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipTool = APTR-чип (бойовий інструмент)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipPistol = APTR-чип (пістолети)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipRifle = APTR-чип (довгоствольна зброя)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipSMG = APTR-чип (пістолети-кулемети)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipShotgun = APTR-чип (дробовики)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipSniper = APTR-чип (снайперська зброя)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipEnergy = APTR-чип (енергетична зброя)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipBludgeon = APTR-чип (дрючки)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipShortBlade = APTR-чип (короткі клинки)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipLongBlade = APTR-чип (довгі клинки)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipNonLethal = APTR-чип (нелетальна зброя)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipPolearm = APTR-чип (древкова зброя)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipUnarmed = APTR-чип (рукопашний бій)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipShield = APTR-чип (щити)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipCombatHOS = MRAM-чип (розширена бойова підготовка)
    .desc = { ent-BaseSkillChipMRAM.desc }

# MRAM

ent-SkillChipArmorsmithing = MRAM-чип (спогади бронника)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipArmorsmithing2 = MRAM-чип (спогади майстра-бронника)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipWeaponsmithing = MRAM-чип (спогади зброяра)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipWeaponsmithing2 = MRAM-чип (спогади майстра-зброяра)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipBlacksmith = MRAM-чип (спогади коваля)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipBlacksmith2 = MRAM-чип (спогади майстра-коваля)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipWoodworker = MRAM-чип (спогади столяра)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipWoodworker2 = MRAM-чип (спогади майстра-столяра)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipGunsmith = MRAM-чип (спогади рушничного майстра)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipGunsmith2 = MRAM-чип (спогади старшого рушничного майстра)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipMechanic = MRAM-чип (спогади механіка)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipMechanic2 = MRAM-чип (спогади майстра-механіка)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipElectronics = MRAM-чип (спогади електрика)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipElectronics2 = MRAM-чип (спогади майстра-електрика)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipTailor = MRAM-чип (спогади кравця)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipTailor2 = MRAM-чип (спогади майстра-кравця)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipStoner = MRAM-чип (спогади торчка)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipDatabase = MRAM-чип (база креслень)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipEducation = MRAM-чип (стандартна освіта клона)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipCombatEducation = MRAM-чип (бойова освіта клона)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipEchelonEducation = MRAM-чип (командна освіта клона)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipMagLit = MRAM-чип (спогади культиста)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipJanitor = MRAM-чип (спогади прибиральника)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipClown = MRAM-чип (спогади клоуна)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipDoctor = MRAM-чип (спогади лікаря)
    .desc = Стерилізований мікрочип, що взаємодіє напряму з мозковою тканиною. Медична підготовка йде в комплекті з придушувачем агресивних рефлексів.
ent-SkillChipChemist = MRAM-чип (спогади хіміка)
    .desc = { ent-SkillChipDoctor.desc }
ent-SkillChipSurgeon = MRAM-чип (спогади хірурга)
    .desc = { ent-SkillChipDoctor.desc }
ent-SkillChipCMO = MRAM-чип (спогади головного хірурга)
    .desc = { ent-BaseSkillChipMRAM.desc }

# PSON

ent-SkillChipMagicalDampener = PSON-чип (випалення магічного хисту)
    .desc = { ent-BaseSkillChipPSON.desc }
ent-SkillChipCombatDampener = PSON-чип (випалення бойового хисту)
    .desc = { ent-BaseSkillChipPSON.desc }
ent-SkillChipMindPurge = PSON-чип (випалення розуму)
    .desc = { ent-BaseSkillChipPSON.desc }
ent-SkillChipTiderDampener = PSON-чип (нейродемпфер)
    .desc = Стерилізований мікрочип, що взаємодіє напряму з мозковою тканиною. Цей збиває запал. Щоб вийняти його, знадобиться друг.

# HPYS

ent-SkillChipThrowing = HPYS-чип (координація рук і очей)
    .desc = { ent-BaseSkillChipHPYS.desc }
ent-SkillChipThrowingTampered = HPYS-чип (підкручена координація рук і очей)
    .desc = { ent-BaseSkillChipHPYS.desc }

# Центком

ent-SkillChipDeathSquad = PSON-чип (перезапис: ескадрон смерті)
    .desc = { ent-BaseSkillChipPSON.desc }
ent-SkillChipERT = PSON-чип (перезапис: ЗШР)
    .desc = { ent-BaseSkillChipPSON.desc }

# Антагоністи

ent-SkillChipNukie = MRAM-чип (горлекс)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipSyndieSoldierTeamLeader = MRAM-чип (командир синдикату)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipSyndieSoldier = MRAM-чип (боєць синдикату)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipSyndieVisitor = MRAM-чип (гість синдикату)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipPirateCaptainScooner = MRAM-чип (піратський капітан)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipPirateScooner = MRAM-чип (пірат)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipBlackmarketeer = MRAM-чип (чорний ділок)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipCossack = MRAM-чип (козак)
    .desc = { ent-BaseSkillChipMRAM.desc }
