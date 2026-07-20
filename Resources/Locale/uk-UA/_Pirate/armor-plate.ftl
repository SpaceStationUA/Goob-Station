ent-ArmorPlateBlunt_Slash = ударно-різальна бронеплита
    .desc = Бронемодуль із додаткових пласталевих пластин та ударопоглинального пластику. Захищає від ударів і порізів, але вразливий до опіків.
ent-ArmorPlatePierce = протикульова бронеплита
    .desc = Бронемодуль із міцних пластитанових пластин. Захищає від куль та колючих атак, але вразливий до опіків.
ent-ArmorPlateHeat = теплозахисна бронеплита
    .desc = Бронемодуль з іридієвим екрануванням і системами розсіювання тепла, що послаблює лазерні атаки. Вразливий до колючих атак.
ent-ArmorPlateSpeed = полегшена бронеплита
    .desc = Бронемодуль із полегшених латунних пластин. Збільшує швидкість, але робить власника вразливішим до більшості видів шкоди.

armor-plate-break = Бронеплита «{$plateName}» розкололася!
armor-plate-examine-with-plate = Встановлено [color=yellow]{$plateName}[/color]. Міцність: [color={$durabilityColor}]{$percent}%[/color]
armor-plate-examine-with-plate-simple = Встановлено [color=yellow]{$plateName}[/color].
armor-plate-examine-no-plate = Бронеплиту не встановлено.
armor-plate-examine-no-storage = Відсік для бронеплити відсутній.

armor-plate-examinable-verb-text = Властивості бронеплити
armor-plate-examinable-verb-message = Оглянути захисні властивості та міцність бронеплити.

armor-plate-attributes-examine = Ця бронеплита:
armor-plate-initial-durability = Розрахована на [color=yellow]{ $durability }[/color] стандартних одиниць шкоди.
armor-plate-item-durability = Міцність: [color={$durabilityColor}]{$percent}%[/color]

armor-plate-gait-speed = швидкість
armor-plate-gait-walk = швидкість ходьби
armor-plate-gait-sprint = швидкість бігу

armor-plate-speed-display =
    { $deltasign ->
        [-1] Збільшує {$gait} на [color=yellow]{$speedPercent}%[/color].
         [0] Не впливає на {$gait}.
         [1] Зменшує {$gait} на [color=yellow]{$speedPercent}%[/color].
        *[other] Має некоректне значення швидкості.
    }

armor-plate-ratios-display =
    { $deltasign ->
        [-1] [color=cyan]Поглинає[/color] [color=yellow]{$ratioPercent}%[/color] шкоди типу «[color=yellow]{$dmgType}[/color]» і втрачає міцність із множником [color=yellow]x{$multiplier}[/color].
         [0] Не реагує на шкоду типу «{$dmgType}».
         [1] [color=fuchsia]Підсилює[/color] шкоду типу «[color=yellow]{$dmgType}[/color]» на [color=yellow]{$ratioPercent}%[/color] і втрачає міцність від доданої шкоди з множником [color=yellow]x{$multiplier}[/color].
        *[other] Має некоректне значення поглинання для типу «{$dmgType}».
    }

armor-plate-stamina-value = Перетворює [color=yellow]{$multiplier}%[/color] поглинутої шкоди на шкоду витривалості.

research-technology-armor-plates-t1 = Композитні бронеплити
research-technology-armor-plates-t2 = Просунуті бронеплити
materials-plastitanium = пластитан
