## Names of Psionic Powers
psionic-power-name-dispel = Dispel
psionic-power-name-eruption = Psionic Eruption
psionic-power-name-mass-sleep = Mass Sleep
psionic-power-name-mindswap = Mind Swap
psionic-power-name-mindswap-return = Mind Swap Return
psionic-power-name-metapsionic = Metapsionic Pulse
psionic-power-name-noospheric-zap = Noospheric Zap
psionic-power-name-psychokinetic = Psychokinetic Scream
psionic-power-name-psionic-regeneration = Psionic Regeneration
psionic-power-name-pyrokinesis = Pyrokinesis
psionic-power-name-telegnosis = Telegnosis
# Pirate: Ported from upstream PR #34 (SpaceStationUA/Goob-Station)
psionic-power-name-anoigo = Anoigo
psionic-power-name-healing-word = Healing Word
psionic-power-name-revivify = Revivify
psionic-power-name-darkswap = DarkSwap
psionic-power-name-assay = Assay
psionic-power-name-shadeskip = Shadeskip

## Psionic Rolling & Mindbreaking Messages
psionic-roll-failed = The noöspheric influence leaves no mark on your mind...
psionic-partly-mindbroken = The psionic influence on your mind weakens..
psionic-mindbroken = Your mind retreats from abstraction to reality.

## Base Psionic messages
psionic-cannot-use-psionics = Your psionic energy can't escape your mind!
psionic-shielded-from-attempt = A psionic influence faltered against your shield!
psionic-cannot-target-shielded = They remain steadfast against your psionic grasp!
psionic-equipped-shielded-in-doafter = The insulative gear broke your concentration..
psionic-dispelled = Someone dispelled your psionic concentration!

## Specific Psionic messages
# Metapsionic Pulse
psionic-power-metapsionic-success = You detect psychic presence there.
psionic-power-metapsionic-failure = You don't detect any psychic presence there.
psionic-power-metapsionic-power-detected = You detect that {$power} was used nearby.

# Mindswap
psionic-power-mindswap-target-mindshielded = Your mindshield.. surprisingly shielded your mind from an psionic influence.
psionic-power-mindswap-own-mindshield = Your mindshield.. stops your mind from leaving your body.
psionic-power-mindswap-original-lost = The psionic tether to your original body was severed!

# Noospheric Zap
psionic-power-noospheric-zap-user = Lightning shoots out of {THE($user)}'s fingertips!
psionic-power-noospheric-zap-battery = {CAPITALIZE(THE($battery))}'s charge ramps up!

# Psionic Eruption
psionic-eruption-begin = {CAPITALIZE(THE($user))} is being consumed by a psionic energy!
psionic-eruption-annoy-minimal = You feel a pressure building up in your mind.
psionic-eruption-annoy-low = Your head aches from the psionic energy.
psionic-eruption-annoy-moderate = You feel a strong pressure in your mind. Make it stop!
psionic-eruption-annoy-high = Your head is pounding from the psionic energy. You need to release it!
psionic-eruption-annoy-dangerous = Your head is about to explode from the psionic energy!
psionic-eruption-annoy-critical = Make it stop! Make it stop! Make it stop!

eruption-warning-window-title = Your Brain isn't Ready!
eruption-warning-window-prompt-text-part = You feel a strong pressure building up in your mind
                                            and you need to release it before it overwhelms you.
                                            When you are ready, you can unleash a psionic eruption.
                                            Doing so will cause a massive psionic discharge, obliterating you entirely.
                                            You should probably try to find a fix to that...
                                            Do not detonately randomly, ensure proper buildup.
                                            Do you understand?
eruption-warning-window-acknowledge-button = I Understand

## Psionic Gamerule Messages
gamerule-noospheric-zap-seize = An external eruption overwhelms your mind!
gamerule-noospheric-zap-seize-potential-regained = Your mind restructures.. it demands knowledge...

psionic-nosebleed-message = Your nose starts gushing blood!

mass-mind-swap-event-announcement = Warning: abnormal glimmer discharge detected. Mass consciousness transfer event imminent, T-{$time} seconds. Please equip psionically-insulating headwear immediately.
mass-mind-swap-event-sender = Sophic Grammateus

minor-mass-mind-swap-event-announcement = Warning: abnormal glimmer discharge detected. Minor Mass Consciousness transfer event imminent, T-{$time} seconds. Please equip psionically-insulating headwear immedieately.
minor-mass-mind-swap-event-sender = Sophic Grammateus

psionic-power-mass-sleep-warning = Your eyelids begin to droop...

# Healing Word / Revivify (Pirate: words the caster is forced to speak when the action succeeds)
healing-word-begin = A word that brings warmth to all who hear it.
healing-word-target = You begin channeling healing energy into {THE($target)}.
revivify-begin = Pleasant words that warm the soul.
revivify-target = You begin channeling revivification into {THE($target)}.

# Anoigo
airlock-blocked-anoigo-fail = The secured wiring panel blocks your psionic influence!

# Assay
assay-begin = You begin assaying {THE($entity)}...
assay-self = You cannot focus enough to assay yourself!
no-powers = {CAPITALIZE(THE($entity))} has no psionic powers.
assay-body = Psionic powers detected on {$entity}:

# Assay response component (attached to the target to rewrite/append the scan result)
assay-response-refuse = I aynt tallin' ya!
assay-response-why-scan = Why are you scanning me?

# Shadeskip
entity-anomaly-no-grid = You must be on a solid surface to use this ability!

telegnosis-power-ssd = { CAPITALIZE(POSS-ADJ($ent)) } eyes are unfocused and darting around, as if trying to see something that isn't there.

glimmer-restyle-event = You feel like something changed about your looks...

## Power-gain feedback (Pirate: shown as a chat-only message when the power is first gained)
# Dispel
dispel-power-initialization-feedback = The forces of fate mean nothing to me. I feel I can reach out and grasp the threads around me, imposing reality upon others.
dispel-power-metapsionic-feedback = {CAPITALIZE($entity)} stands as a mighty bulwark against the currents of fate

# Mass Sleep
mass-sleep-power-initialization-feedback = Reaching out to the minds around me, I found the words that can send others into the realm of dreams.
mass-sleep-power-metapsionic-feedback = {CAPITALIZE($entity)} bears the indelible mark of a dream-thief.

# Mind Swap
mind-swap-power-initialization-feedback = I feel the bond between soul and body weaken at my whim, my vessel able to be traded for another.
mind-swap-power-metapsionic-feedback = {CAPITALIZE($entity)} lacks a firm bond with their vessel, as if their spirit were plastic.
mind-swap-return-power-initialization-feedback = I feel a silver thread binding my mind to a stranger's body. With a single thought, I can return home.
mind-swap-return-power-metapsionic-feedback = A silver thread stretches from {CAPITALIZE($entity)} to another body.

# Noospheric Zap
noospheric-zap-power-initialization-feedback = In a single transcendent moment, I find myself in a universe paved with silicon tiles.
    I wander this place for days, desperately seeking any form of life, but no one greets me.
    Just before thirst begins to torment me, a silver man finds me. He plunges his hand into my body, and I wake up screaming.
noospheric-zap-power-metapsionic-feedback = I look into the heart of {CAPITALIZE($entity)}, and there, among the flesh, a microscopic sliver of a creature of pure energy buzzes.
    It glares at me, its silvery eyes full of hatred for carbon flesh.

# Pyrokinesis
pyrokinesis-power-initialization-feedback = A bright flash of light and heat, and for a moment I feel every inch of my flesh turning to steam.
    But death does not come for me, though I catch myself praying that it would. The afterlife is at once agonizingly hot and cold to the bone.
    For weeks I desperately believe Gehenna exists, starving, weeping, screaming, and the pain never stops. At last, a man in white, with the face of a horrible fly,
    beckons to me, offering his services. As I reach out to shake his hand, the vision fades, and I find myself standing in the primordial matter.
    Now I know His name: the Mystery of Fire. Merely thinking of it, I feel the warmth of that place seep into my hands.
pyrokinesis-power-metapsionic-feedback = The Mystery of Fire dwells within {CAPITALIZE($entity)}

# Metapsionic Pulse
metapsionic-power-initialization-feedback = The world around me awakens in the light of dreams. For a transcendent moment, I see all that is, all that will ever be.
    I sway, my lips parched not from thirst, but from the desire to drink from the cup of knowledge. I. Must. Find. Him.
metapsionic-power-metapsionic-feedback = {CAPITALIZE($entity)} stares back at you

# Psionic Regeneration
psionic-regeneration-power-initialization-feedback = I look within myself, finding the wellspring of life.
psionic-regeneration-power-metapsionic-feedback = {CAPITALIZE($entity)} has an unyielding will to live

# Telegnosis
telegnosis-power-initialization-feedback = Taking my next step, I notice I am no longer in the material world. My feet tread a bridge of rainbow light.
    But strangely, as I look left and right, I first see pink within pink, and to my right, blue within blue.
    And as my mind falters, dissatisfied from seeing colors that do not exist, a creature I can only describe as
    a dragon with peacock wings swoops in and devours my flesh in a single bite. I wake in an instant, in a world utterly devoid
    of true, real colors.
telegnosis-power-metapsionic-feedback = The soul of {CAPITALIZE($entity)} wanders bridges woven from the light of dreams

# Healing Word
healing-word-power-initialization-feedback = In the beginning of time, a word was spoken that brought life into the Spheres.
    Though the knowledge of this secret weighs upon my mind, it is now known to me.
    I need only speak it.
healing-word-power-metapsionic-feedback = {CAPITALIZE($entity)} carries the Lesser Mystery of Life.

# Revivify
revivify-power-initialization-feedback = For a moment, my soul travels through time and space to the beginning of it all, and there I hear it.
    The Mystery of Life in all its fullness. I feel my entire existence burning from within, simply by knowing it.
    Power flows through me like a mighty river, begging to be released with a single spoken word.
revivify-power-metapsionic-feedback = {CAPITALIZE($entity)} carries the Great Mystery of Life.

# Shadeskip
shadeskip-power-initialization-feedback = I stand on cold earth, beneath a sky devoid of starlight. The cold is the void at the end of time.
    I gaze at a pale blue horizon and see a great eye at the center of it all, black and empty as the deepest reaches of space.
    My soul begins to wither beneath its gaze, and I beg it to look away. The eye laughs; it demands I serve it or die.
    Knowing I have no choice, I swear myself to it, and suddenly I am back in the material world. The eye still watches from behind me.
shadeskip-power-metapsionic-feedback = {CAPITALIZE($entity)} has been claimed by the Lords of the End of Time.

# DarkSwap
darkswap-power-initialization-feedback = For a moment, I feel I can tear through boundaries. Slowly sinking into shadow and darkness, ready to journey to the darkest places...
darkswap-power-metapsionic-feedback = {CAPITALIZE($entity)} exists between shadow and matter.

# Assay
assay-power-initialization-feedback = I sink once more into the realm of dreams, drinking deeper from the cup of knowledge. The noösphere's touch upon others becomes known to me; I can turn my will upon them, laying bare their inner nature.
assay-power-metapsionic-feedback = {CAPITALIZE($entity)} bears the spark of divine judgment, having drunk deeply from the cup of knowledge.

# Anoigo
anoigo-power-initialization-feedback = Knowledge came to me in a bottle along the shores of Entropy.
    The Keepers of riches and secrets can be persuaded if one speaks their tongue.
anoigo-power-metapsionic-feedback = {CAPITALIZE($entity)} speaks the language of the Keeper.

# Psionic Eruption
psionic-eruption-power-initialization-feedback = Pressure builds behind my eyes, a star about to burst. The noösphere floods me, demanding release.
psionic-eruption-power-metapsionic-feedback = {CAPITALIZE($entity)} is a storm about to break.

# Psychokinetic Scream
psychokinetic-scream-power-initialization-feedback = A wordless scream grows in my throat, older than language. When I release it, the light around me will shatter.
psychokinetic-scream-power-metapsionic-feedback = {CAPITALIZE($entity)} has a voice that shatters light itself.

# Mantis pendulum
mantis-pendulum-hot-message = The pendulum grows uncomfortably hot in your hand.
