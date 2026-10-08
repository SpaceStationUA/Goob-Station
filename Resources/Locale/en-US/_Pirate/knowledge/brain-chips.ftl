
verb-categories-organ-chips = Chips

organ-chip-verb-none = No { $organ } chips installed
organ-chip-verb-remove-known = Remove { $chip }
organ-chip-verb-remove-unknown = Remove { $organ } chip { $index }
organ-chip-unchipped = No neural augmentation for brain chips.

organ-chip-insert-start-self = You start slotting a chip into your { $organ }!
organ-chip-insert-start-other = You start slotting a chip into { CAPITALIZE($target) }'s { $organ }!
organ-chip-insert-start-victim = { CAPITALIZE($user) } starts slotting a chip into your { $organ }!
organ-chip-insert-start-loose = You start slotting a chip into the { $organ }.
organ-chip-insert-success = You slot the chip into the { $organ }.

organ-chip-remove-start-self = You start prying a chip out of your { $organ }!
organ-chip-remove-start-other = You start prying a chip out of { CAPITALIZE($target) }'s { $organ }!
organ-chip-remove-start-victim = { CAPITALIZE($user) } starts prying a chip out of your { $organ }!
organ-chip-remove-start-loose = You start prying a chip out of the { $organ }.
organ-chip-remove-success = You pry the chip out of the { $organ }.

organ-chip-not-a-chip = That is not a chip.
organ-chip-incompatible = { CAPITALIZE($chip) } does not fit a { $organ }.
organ-chip-no-room = That { $organ } has no room for another chip.
organ-chip-duplicate = An identical chip is already installed.
organ-chip-same-family = { CAPITALIZE($chip) } conflicts with { $installed }. Only one chip from each family can be installed.
organ-chip-installed-unknown = { $organ } chip { $index }
organ-chip-family-examine = Chip family: [bold]{ $family }[/bold]. Only one chip from this family can be installed.
hormone-chip-examine = While installed:
hormone-chip-examine-crit-up = - [color=green]+{ $amount }[/color] damage before going critical
hormone-chip-examine-crit-down = - [color=red]-{ $amount }[/color] damage before going critical
hormone-chip-examine-stamina-up = - [color=green]+{ $amount }%[/color] stamina before collapsing
hormone-chip-examine-stamina-down = - [color=red]-{ $amount }%[/color] stamina before collapsing
hormone-chip-examine-heat-up = - [color=green]+{ $amount }%[/color] heat tolerance
hormone-chip-examine-heat-down = - [color=red]-{ $amount }%[/color] heat tolerance
hormone-chip-examine-cold-up = - [color=green]+{ $amount }%[/color] cold tolerance
hormone-chip-examine-cold-down = - [color=red]-{ $amount }%[/color] cold tolerance
hormone-chip-examine-light-step = - [color=green]silent footsteps[/color]
psionic-amplifier-chip-examine = While installed:
psionic-amplifier-chip-examine-cooldown = - [color=green]{ $percent }% shorter[/color] psionic power cooldowns
psionic-amplifier-chip-examine-dispel = - Dispel [color=red]deals shock damage to the wearer[/color]
psionic-amplifier-chip-examine-overload = - [color=red]anti-psionic weapons always knock powers offline[/color] for { $seconds } seconds
organ-chip-not-removable = That chip is fused in place.
organ-chip-no-self-removal = You cannot reach that chip yourself. Someone else will have to pull it.
organ-chip-need-hard-grab = You need a hard grab on them first!
organ-chip-out-of-reach = You cannot reach the operation site any more.
organ-chip-lost-chip = You are no longer holding the chip.

knowledge-grant-on-wear-examine = Skill changes while installed:
knowledge-grant-on-wear-examine-positive = - [color=green]+{ $level }[/color] [bold]{ $skill }[/bold]
knowledge-grant-on-wear-examine-negative = - [color=red]{ $level }[/color] [bold]{ $skill }[/bold]


organ-chip-family-overwrite = faction skill pack
organ-chip-family-armorsmithing = armoursmith memories
organ-chip-family-weaponsmithing = weaponsmith memories
organ-chip-family-blacksmith = blacksmith memories
organ-chip-family-woodworker = woodworker memories
organ-chip-family-gunsmith = gunsmith memories
organ-chip-family-mechanic = mechanic memories
organ-chip-family-electronics = electrician memories
organ-chip-family-tailor = tailor memories
organ-chip-family-database = blueprint database
organ-chip-family-database-syndicate = Syndicate blueprint database
organ-chip-family-sidearms = sidearms training
organ-chip-family-education = clone education
organ-chip-family-throwing = hand-eye coordination
organ-chip-family-endorphin = pain tolerance
organ-chip-family-adrenal = stamina
organ-chip-family-thyroid = temperature tolerance


ent-BaseBrainChip = brain chip
    .desc = A sterilised microchip assembly that interfaces directly with brain tissue.
ent-BaseSkillChipMRAM = MRAM-chip
    .desc = A neural chip loaded with someone else's training and work experience.
ent-BaseSkillChipAPTR = APTR-chip
    .desc = A neural chip loaded with weapon drills and combat reflexes.
ent-BaseSkillChipPSON = PSON-chip
    .desc = A neural chip that alters brain activity. Models include skill dampeners and psionic amplifiers.
ent-BaseSkillChipHPYS = HPYS-chip
    .desc = A neural chip that adjusts motor control and hormone regulation.


ent-SkillChipHeavy = APTR-chip (heavy weapon training)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipLaser = APTR-chip (laser weapon training)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipMarksmanship = APTR-chip (marksmanship)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipMining = APTR-chip (mining tool training)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipTool = APTR-chip (combat tool training)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipPistol = APTR-chip (pistol training)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipRifle = APTR-chip (longarms training)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipSMG = APTR-chip (SMG training)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipShotgun = APTR-chip (shotgun training)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipSniper = APTR-chip (sniper training)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipEnergy = APTR-chip (energy weapon training)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipBludgeon = APTR-chip (bludgeon training)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipCloseQuarters = APTR-chip (close quarters training)
ent-SkillChipShortBlade = APTR-chip (short blade training)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipLongBlade = APTR-chip (long blade training)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipNonLethal = APTR-chip (non-lethal training)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipPolearm = APTR-chip (polearm training)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipUnarmed = APTR-chip (unarmed training)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipShield = APTR-chip (shield training)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipCombatHOS = MRAM-chip (extended combat training)
    .desc = { ent-BaseSkillChipMRAM.desc }


ent-SkillChipArmorsmithing = MRAM-chip (memories of an armoursmith)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipArmorsmithing2 = MRAM-chip (memories of a master armoursmith)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipWeaponsmithing = MRAM-chip (memories of a weaponsmith)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipWeaponsmithing2 = MRAM-chip (memories of a master weaponsmith)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipBlacksmith = MRAM-chip (memories of a blacksmith)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipBlacksmith2 = MRAM-chip (memories of a master blacksmith)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipWoodworker = MRAM-chip (memories of a woodworker)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipWoodworker2 = MRAM-chip (memories of a master woodworker)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipGunsmith = MRAM-chip (memories of a gunsmith)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipGunsmith2 = MRAM-chip (memories of a master gunsmith)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipMechanic = MRAM-chip (memories of a mechanic)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipMechanic2 = MRAM-chip (memories of a master mechanic)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipElectronics = MRAM-chip (memories of an electrician)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipElectronics2 = MRAM-chip (memories of a master electrician)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipTailor = MRAM-chip (memories of a tailor)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipTailor2 = MRAM-chip (memories of a master tailor)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipStoner = MRAM-chip (memories of a stoner)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipDatabase = MRAM-chip (crafting database)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipEducation = MRAM-chip (standard clone education)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipEducationPilot = MRAM-chip (standard clone education, pilot)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipCombatEducation = MRAM-chip (combat clone education)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipCombatEducationPilot = MRAM-chip (combat clone education, pilot)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipEchelonEducation = MRAM-chip (echelon clone education)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipEchelonEducationPilot = MRAM-chip (echelon clone education, pilot)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipMagLit = MRAM-chip (memories of a cultist)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipJanitor = MRAM-chip (memories of a janitor)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipClown = MRAM-chip (memories of a clown)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipDoctor = MRAM-chip (memories of a doctor)
    .desc = A sterilised microchip assembly that interfaces directly with brain tissue. The medical training comes bundled with a suppressor for violent reflexes.
ent-SkillChipService = MRAM-chip (service staff)
ent-SkillChipChemist = MRAM-chip (memories of a chemist)
    .desc = { ent-SkillChipDoctor.desc }
ent-SkillChipSurgeon = MRAM-chip (memories of a surgeon)
    .desc = { ent-SkillChipDoctor.desc }
ent-SkillChipCMO = MRAM-chip (memories of a surgeon general)
    .desc = { ent-BaseSkillChipMRAM.desc }


ent-SkillChipMagicalDampener = PSON-chip (magical literacy dampener)
    .desc = Suppresses magical literacy while installed. Someone else must remove it.
ent-SkillChipCombatDampener = PSON-chip (combat dampener)
    .desc = Suppresses combat skills while installed. Someone else must remove it.
ent-SkillChipMindPurge = PSON-chip (combat dampener)
    .desc = { ent-SkillChipCombatDampener.desc }
ent-SkillChipTiderDampener = PSON-chip (neural dampener)
    .desc = Reduces melee and shooting skills while installed. Someone else must remove it.
ent-SkillChipPsionicAmplifier = PSON-chip (psionic amplifier)
    .desc = Halves the cooldowns of your psionic powers. Dispel causes shock damage, and anti-psionic weapons temporarily disable your powers.


ent-SkillChipThrowing = HPYS-chip (hand-eye coordination)
    .desc = Improves throwing technique. Thrown objects travel faster and hit harder.
ent-SkillChipThrowingTampered = HPYS-chip (overclocked hand-eye coordination)
    .desc = An overclocked coordination chip that sharpens control of your throwing motion.
ent-SkillChipEndorphin = HPYS-chip (pain tolerance)
    .desc = Regulates endorphin release so you can take more damage before going critical. It does not heal injuries.
ent-SkillChipAdrenal = HPYS-chip (stamina)
    .desc = Regulates adrenaline release so you can withstand more stamina damage before collapsing.
ent-SkillChipThyroid = HPYS-chip (temperature tolerance)
    .desc = Adjusts thyroid activity so your body can tolerate more heat and cold before taking damage.
ent-SkillChipProprioception = HPYS-chip (silent step)
    .desc = Adjusts your gait to silence your footsteps.


ent-SkillChipDeathSquad = MRAM-chip (death squad)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipERT = MRAM-chip (ERT)
    .desc = { ent-BaseSkillChipMRAM.desc }


ent-SkillChipNukie = MRAM-chip (gorlex)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipSyndieSoldierTeamLeader = MRAM-chip (syndicate leader)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipSyndieSoldier = MRAM-chip (syndicate soldier)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipFieldMedicine = MRAM-chip (field medicine)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipFreelancer = MRAM-chip (freelancer)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipSidearms = APTR-chip (sidearms)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipSidearmsAdvanced = APTR-chip (advanced sidearms)
    .desc = { ent-BaseSkillChipAPTR.desc }
ent-SkillChipDatabaseBasic = MRAM-chip (basic blueprint database)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipSyndieMarshal = MRAM-chip (syndicate marshal)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipSyndieVisitor = MRAM-chip (syndicate visitor)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipPirateCaptainScooner = MRAM-chip (pirate captain)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipPirateScooner = MRAM-chip (pirate)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipBlackmarketeer = MRAM-chip (blackmarketeer)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipCossack = MRAM-chip (cossack)
    .desc = { ent-BaseSkillChipMRAM.desc }

# Syndicate variants use the original names and descriptions.
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
