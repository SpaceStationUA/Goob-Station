# SPDX-License-Identifier: AGPL-3.0-or-later

roles-antag-fugitive-name = Fugitive
roles-antag-fugitive-objective = Stay on the run for your crimes.
role-subtype-fugitive = Fugitive

ghost-role-information-fugitive-name = Fugitive
ghost-role-information-fugitive-description = You are an escaped prisoner. Make it out alive.
ghost-role-information-fugitive-rules = You are a light solo antagonist. Focus on laying low and escaping rather than directly engaging Security. Do not murderbone.
ent-SpawnPointGhostFugitive = fugitive spawn point

fugitive-round-end-agent-name = Fugitive
fugitive-spawn = You fall from the ceiling!

station-event-fugitive-hunt-announcement = Please check communications consoles for a sensitive message.
fugitive-announcement-GALPOL = GALPOL

fugitive-report-title = WANTED FUGITIVE!
fugitive-report-first-line = An escaped fugitive has been spotted in the sector and disguised their identity. They may be a stowaway on a station somewhere.
fugitive-report-inhuman = {CAPITALIZE(THE($name))} {CONJUGATE-BE($name)} inhuman. We have no further details.
fugitive-report-morphotype = MORPHOTYPE: {$species}
fugitive-report-age = AGE: {$age}
fugitive-report-sex = SEX: {$sex ->
    [Male] M
    [Female] F
    *[none] N/A
}
fugitive-report-weight = WEIGHT: {$weight} kg
fugitive-report-detail-dna = DNA: {$dna}
fugitive-report-detail-prints = FINGERPRINT: {$prints}
fugitive-report-crimes-header = The above individual is wanted across the sector for the following:
fugitive-report-crime = - {$count ->
    [1] One count
    *[other] {$count} counts
} of {$crime}
fugitive-report-last-line = GALPOL is entrusting Nanotrasen with securing this individual and conducting a trial at Central Command. Please ensure they are kept alive and brought to Central Command.

fugitive-crime-1 = Murder
fugitive-crime-2 = Terrorism
fugitive-crime-3 = Grand Sabotage
fugitive-crime-4 = Prevention of Revival
fugitive-crime-5 = Sedition
fugitive-crime-6 = Breach of Custody
fugitive-crime-7 = Manslaughter
fugitive-crime-8 = Kidnapping
fugitive-crime-9 = Grand Possession
fugitive-crime-10 = Noöspheric Tampering
fugitive-crime-11 = Sabotage
fugitive-crime-12 = Abuse of Power
fugitive-crime-13 = Grand Larceny
fugitive-crime-14 = Black Marketeering
fugitive-crime-15 = Assault
fugitive-crime-16 = Breaking and Entering
fugitive-crime-17 = Rioting
fugitive-crime-18 = Endangerment
fugitive-crime-19 = Possession
fugitive-crime-20 = Obstruction of Justice
fugitive-crime-21 = Perjury
fugitive-crime-22 = False Report
fugitive-crime-23 = Contempt of Court
fugitive-crime-24 = Identity Theft

ent-FugitiveEscapeObjective = Evade law enforcement
    .desc = You will never atone for your crimes. Blend into the crowd and escape on the evacuation shuttle.

ent-FugitiveStash = fugitive's stash
    .desc = These supplies got you out of jail and hopefully they will keep you out of it.

fugitive-set-hitman-name = hitman's kit
fugitive-set-hitman-description = A loaded Viper, a spare magazine, and a brown briefcase for armed self-defense.
fugitive-set-saboteur-name = saboteur's kit
fugitive-set-saboteur-description = Two EMP grenades, a brick of C4, and a gas mask for sabotage.
fugitive-set-ghost-name = ghost's kit
fugitive-set-ghost-description = Two smoke grenades, a Scram implanter, and a ghost sheet for disappearing during a chase.
fugitive-set-leverage-name = leverage kit
fugitive-set-leverage-description = Handcuffs, a bola, and a death acidifier implanter for turning a pursuer into leverage.
fugitive-set-infiltrator-name = infiltrator's kit
fugitive-set-infiltrator-description = An Agent ID, a freedom implanter, and a Syndicate gas mask for changing identity and access.
fugitive-set-disruptor-name = disruptor's kit
fugitive-set-disruptor-description = A cryptographic sequencer and a camera bug for compromising station systems.

ent-PaperFugitiveReport = fugitive report
    .desc = An arrest warrant for a space fugitive sent from GALPOL.
ent-RubberStampGalpol = GALPOL rubber stamp
    .desc = A rubber stamp for important documents concerning intergalactic security affairs.
stamp-component-stamped-name-GALPOL = GALPOL
