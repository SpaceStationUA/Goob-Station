vampires-title = Вампіри

vampire-blooddrink-empty = Це тіло позбавлене крові
vampire-blooddrink-rotted = Їхнє тіло гниє, а кров зіпсована
vampire-blooddrink-zombie = Їхня кров зіпсована смертю

vampire-startlight-burning = Ти відчуваєш, як твоя шкіра горить у світлі тисячі сонць

vampire-not-enough-blood = У тебе недостатньо крові
vampire-cuffed = Тобі потрібні вільні руки!
vampire-stunned = Ти не можеш достатньо сконцентруватися!
vampire-muffled = Твій рот закритий
vampire-full-stomach = Ти роздутий від крові

vampire-deathsembrace-bind = Відчувається як вдома

vampire-ingest-holyblood = Твій рот пече!

vampire-cloak-enable = Ти огортаєш свою форму тінями
vampire-cloak-disable = Ти відпускаєш свою хватку на тінях

vampire-bloodsteal-other = Ти відчуваєш, як кров виривається з твого тіла!
vampire-bloodsteal-no-victims = Ти намагаєшся вкрасти кров, але навколо немає жертв, твої сили розсіюються в повітрі!
vampire-hypnotise-other = {CAPITALIZE(THE($user))} пильно дивиться в очі {MAKEPLURAL(THE($target))}!
vampire-unnaturalstrength = Верхні м'язи {CAPITALIZE(THE($user))} збільшуються, роблячи його сильнішим!
vampire-supernaturalstrength = Верхні м'язи {CAPITALIZE(THE($user))} наливаються силою, роблячи його надзвичайно сильним!

store-currency-display-blood-essence = Кров'яна Есенція
store-category-vampirepowers = Сили
store-category-vampirepassives = Пасиви

# Сили

# Пасиви
vampire-passive-unholystrength = Нечестива Сила
vampire-passive-unholystrength-description = Наповни м'язи верхньої частини тіла есенцією, що дасть тобі кігті та збільшену силу. Ефект: 10 рубаючих за удар

vampire-passive-supernaturalstrength = Надприродна Сила
vampire-passive-supernaturalstrength-description = Ще більше збільш силу м'язів верхньої частини тіла, жодна перешкода не встоїть перед тобою. Ефект: 15 рубаючих за удар, здатність виламувати двері руками.

vampire-passive-deathsembrace = Обійми Смерті
vampire-passive-deathsembrace-description = Прийми смерть, і вона обмине тебе. Ефект: зцілення в труні, автоматичне повернення в труну після смерті за 100 одиниць кров'яної есенції.

# Меню мутацій

vampire-mutation-menu-ui-window-name = Меню мутацій

vampire-mutation-none-info = Нічого не вибрано

vampire-mutation-hemomancer-info = Гемомант

    Зосереджується на магії крові та маніпулюванні кров'ю навколо себе.

    Здібності:

    - Крик
    - Кровокрад

vampire-mutation-umbrae-info = Тінь

    Зосереджується на темряві, скритності, мобільності.

    Здібності:

    - Нульовий Погляд
    - Плащ Темряви

vampire-mutation-gargantua-info = Гаргантюа

    Зосереджується на ближньому бою та стійкості.

    Здібності:

    - Нечестива Сила
    - Надприродна Сила

vampire-mutation-bestia-info = Бестія

    Зосереджується на перетворенні та збиранні трофеїв.

    Здібності:

    - Форма Кажана
    - Форма Миші

vampire-mutation-sire-info = Прабатько

    Зосереджується на створенні та підтримці інших вампірів. Вартість зміни цієї мутації: 500 есенції

    Здібності:

    - Породити
    - Темний Дар

## Цілі

objective-condition-drain-title = Випити { $count } крові.
objective-condition-drain-description = Я повинен випити { $count } крові. Це необхідно для мого виживання та подальшої еволюції.
ent-VampireSurviveObjective = Вижити
    .desc = Я маю вижити, чого б це не коштувало.
ent-VampireEscapeObjective = Полетіти зі станції живим і вільним.
    .desc = Я повинен покинути станцію на рятувальному шатлі. Вільним.

## Сповіщення

alerts-vampire-blood-name = Кількість Кров'яної Есенції
alerts-vampire-blood-desc = Кількість вампірської кров'яної есенції.
alerts-vampire-stellar-weakness-name = Зоряна Слабкість
alerts-vampire-stellar-weakness-desc = Тебе обпікає світло сонця, а точніше - кількох мільярдів зірок, яким ти піддаєшся за межами станції.


## Пресет

vampire-roundend-name = Вампір
objective-issuer-vampire = [color=red]Жага крові[/color]
roundend-prepend-vampire-drained-named = [color=white]{ $name }[/color] випив загалом [color=red]{ $number }[/color] крові.
roundend-prepend-vampire-drained = Хтось випив загалом [color=red]{ $number }[/color] крові.
vampire-gamemode-title = Вампіри
vampire-gamemode-description = Кровожерливі вампіри проникли на станцію, щоб пити кров!
vampire-role-greeting = Ти - вампір, який проник на станцію під виглядом співробітника!
        Твої завдання перераховані в меню персонажа.
        Пий кров і розвивайся, щоб виконати їх!
vampire-role-greeting-short = Ти - вампір, який проник на станцію під виглядом співробітника!
roles-antag-vamire-name = Вампір

## Дії

ent-ActionVampireOpenMutationsMenu = Меню мутацій
    .desc = Відкриває меню з мутаціями вампіра.
ent-ActionVampireToggleFangs = Перемкнути Ікла
    .desc = Витягнути або втягнути ікла. Ходіння з витягнутими іклами може розкрити твою справжню природу.
ent-ActionVampireGlare = Погляд
    .desc = Випустити сліпучий спалах з очей, оглушаючи незахищеного смертного на 10 секунд. Вартість активації: 20 есенції. Перезарядка: 60 секунд
ent-ActionVampireHypnotise = Загіпнотизувати
    .desc = Пильно подивитися в очі смертному, змушуючи його миттєво заснути на 60 секунд. Вартість активації: 20 есенції. Перезарядка: 5 хвилин
ent-ActionVampireScreech = Крик
    .desc = Видати пронизливий крик, оглушаючи незахищених смертних і розбиваючи крихкі предмети поблизу. Вартість активації: 20 есенції. Затримка активації: 5 секунд. Перезарядка: 5 хвилин
ent-ActionVampireBloodSteal = Крадіжка Крові
    .desc = Вирвати кров з усіх тіл поблизу - живих чи мертвих. Вартість активації: 20 есенції. Перезарядка: 60 секунд
ent-ActionVampireBatform = Форма Кажана
    .desc = Прийняти форму кажана. Швидкий, важко влучити, любить фрукти. Вартість активації: 20 есенції. Перезарядка: 30 секунд
ent-ActionVampireMouseform = Форма Миші
    .desc = Прийняти форму миші. Швидка, маленька, невразлива до дверей. Вартість активації: 20 есенції. Перезарядка: 30 секунд
ent-ActionVampireCloakOfDarkness = Плащ Темряви
    .desc = Сховатися від очей смертних, стаючи невидимим у нерухомому стані. Крові для активації: 330 есенції, Вартість активації: 30 есенції. Підтримка: 1 есенція/секунду. Перезарядка: 10 секунд

ent-ActionVampireSire = Породити
    .desc = Перетворити смертного на вампіра. Не працює на тих, хто має майндшилд. Вартість активації: 200 есенції.

vampire-sire-mindshield-block = Ціль опирається вашому впливу (майндшилд).
vampire-sire-already = Вони вже вампір.
vampire-sire-success = Ви породили {THE($target)}.
vampire-sire-turned = Ви відчуваєте, як у вас прокидається темна сила...

vampire-sire-channel = Ти почав(ла) ритуал породження.
vampire-sire-channel-target = Хтось намагається перетворити тебе на вампіра...

ent-ActionVampireDarkGift = Темний Дар
    .desc = Зцілює або підіймає вампіра, якого ти перетворив. Вартість активації: 100 есенції.

vampire-darkgift-used-user = Ти наділяєш {THE($target)} Темним Даром.
vampire-darkgift-used-target = Ти відчуваєш, як темні сили зцілюють тебе.

vampire-darkgift-channel = Ти почав(ла) дарувати Темний Дар {THE($target)}.
vampire-darkgift-channel-target = Ти відчуваєш, як темна енергія огортає тебе...
