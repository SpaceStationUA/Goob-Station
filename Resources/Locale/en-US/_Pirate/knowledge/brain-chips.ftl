### Brain chip UI, interaction and examine text.

verb-categories-organ-chips = Chips

organ-chip-verb-none = No { $organ } chips installed
organ-chip-verb-remove-known = Remove { $chip }
organ-chip-verb-remove-unknown = Remove { $organ } chip { $index }

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
organ-chip-not-removable = That chip is fused in place.
organ-chip-no-self-removal = You cannot reach that chip yourself. Someone else will have to pull it.
organ-chip-need-hard-grab = You need a hard grab on them first!
organ-chip-out-of-reach = You cannot reach the operation site any more.
organ-chip-lost-chip = You are no longer holding the chip.

knowledge-grant-on-wear-examine = It offsets these skills while installed:
knowledge-grant-on-wear-examine-positive = - [color=green]+{ $level }[/color] [bold]{ $skill }[/bold]
knowledge-grant-on-wear-examine-negative = - [color=red]{ $level }[/color] [bold]{ $skill }[/bold]

### Chips

ent-BaseBrainChip = brain chip
    .desc = A sterilised microchip assembly that interfaces directly with brain tissue.
ent-BaseSkillChipMRAM = MRAM-chip
    .desc = A sterilised microchip assembly that interfaces directly with brain tissue. This one rewires it to supply memories of skills you never learned.
ent-BaseSkillChipAPTR = APTR-chip
    .desc = A sterilised microchip assembly that interfaces directly with brain tissue. This one supplies reflexes and intrinsic knowledge of weaponry.
ent-BaseSkillChipPSON = PSON-chip
    .desc = A sterilised microchip assembly that interfaces directly with brain tissue. This one weakens innate skills and overwrites personalities.
ent-BaseSkillChipHPYS = HPYS-chip
    .desc = A sterilised microchip assembly that interfaces directly with brain tissue. This one shifts hormone balances to enhance physical traits.

# APTR

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

# MRAM

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
ent-SkillChipCombatEducation = MRAM-chip (combat clone education)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipEchelonEducation = MRAM-chip (echelon clone education)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipMagLit = MRAM-chip (memories of a cultist)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipJanitor = MRAM-chip (memories of a janitor)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipClown = MRAM-chip (memories of a clown)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipDoctor = MRAM-chip (memories of a doctor)
    .desc = A sterilised microchip assembly that interfaces directly with brain tissue. The medical training comes bundled with a suppressor for violent reflexes.
ent-SkillChipChemist = MRAM-chip (memories of a chemist)
    .desc = { ent-SkillChipDoctor.desc }
ent-SkillChipSurgeon = MRAM-chip (memories of a surgeon)
    .desc = { ent-SkillChipDoctor.desc }
ent-SkillChipCMO = MRAM-chip (memories of a surgeon general)
    .desc = { ent-BaseSkillChipMRAM.desc }

# PSON

ent-SkillChipMagicalDampener = PSON-chip (purge magical talent)
    .desc = { ent-BaseSkillChipPSON.desc }
ent-SkillChipCombatDampener = PSON-chip (purge combat talent)
    .desc = { ent-BaseSkillChipPSON.desc }
ent-SkillChipMindPurge = PSON-chip (purge mind)
    .desc = { ent-BaseSkillChipPSON.desc }
ent-SkillChipTiderDampener = PSON-chip (neural dampener)
    .desc = A sterilised microchip assembly that interfaces directly with brain tissue. This one takes the edge off. You will need a friend to get it back out.

# HPYS

ent-SkillChipThrowing = HPYS-chip (hand-eye coordination)
    .desc = { ent-BaseSkillChipHPYS.desc }
ent-SkillChipThrowingTampered = HPYS-chip (tampered hand-eye coordination)
    .desc = { ent-BaseSkillChipHPYS.desc }

# Central Command

ent-SkillChipDeathSquad = PSON-chip (deathsquad overwrite)
    .desc = { ent-BaseSkillChipPSON.desc }
ent-SkillChipERT = PSON-chip (ERT overwrite)
    .desc = { ent-BaseSkillChipPSON.desc }

# Antagonists

ent-SkillChipNukie = MRAM-chip (gorlex)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipSyndieSoldierTeamLeader = MRAM-chip (syndicate leader)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipSyndieSoldier = MRAM-chip (syndicate soldier)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipFieldMedicine = MRAM-chip (field medicine)
    .desc = { ent-BaseSkillChipMRAM.desc }
ent-SkillChipFreelancer = PSON-chip (overwrite: freelancer)
    .desc = { ent-BaseSkillChipPSON.desc }
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
