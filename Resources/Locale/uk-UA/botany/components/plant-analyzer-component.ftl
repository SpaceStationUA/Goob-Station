# Pirate: Omu plant analyzer with retained tray details and report printing.
plant-analyzer-interface-title = Аналізатор рослин
plant-analyzer-window-no-data = Рослину ще не проскановано.
plant-analyzer-window-scanned = Результат сканування:
plant-analyzer-window-out-of-range = Об'єкт поза зоною сканування.
plant-analyzer-unknown-plant = Невідома рослина
plant-analyzer-scanned-seed-name = Насіння: {$name}
plant-analyzer-toxin = Рівень токсинів
plant-analyzer-weeds = Бур'яни
plant-analyzer-pests = Шкідники
plant-analyzer-warnings-none-message = ✓ Проблем не виявлено
plant-analyzer-potency = Потужність
plant-analyzer-yield = Урожайність
plant-analyzer-resistances = Стійкість

plant-analyzer-traits = Ознаки
plant-analyzer-traits-none = Ознак не виявлено
plant-analyzer-trait-seedless = Без насіння
plant-analyzer-trait-seedless-desc = Не утворює насіння.
plant-analyzer-trait-non-viable = Нежиттєздатна
plant-analyzer-trait-non-viable-desc = Постійно втрачає здоров'я та зрештою загине.
plant-analyzer-trait-ligneous = Здерев'яніла
plant-analyzer-trait-ligneous-desc = Міцні дерев'янисті тканини зміцнюють рослину.
plant-analyzer-trait-kudzu = Кудзу
plant-analyzer-trait-kudzu-desc = Містить гени інвазивного кудзу та може перетворитися на нього.
plant-analyzer-trait-slippery = Слизька
plant-analyzer-trait-slippery-desc = Вкрита слизькою оболонкою.
plant-analyzer-trait-sentient = Розумна
plant-analyzer-trait-sentient-desc = Виявляє ознаки свідомості.
plant-analyzer-trait-scream = Кричить
plant-analyzer-trait-scream-desc = Видає моторошні крики.
plant-analyzer-trait-auto-harvest = Самостійне скидання врожаю
plant-analyzer-trait-auto-harvest-desc = Автоматично скидає плоди після дозрівання.

plant-analyzer-total-reagents = Усього реагентів
plant-analyzer-base-reagents = Природні реагенти
plant-analyzer-additional-reagents = Додані реагенти
plant-analyzer-consumed-gases = Споживані гази
plant-analyzer-exuded-gases = Виділювані гази
plant-analyzer-no-base-reagents = Природних реагентів немає
plant-analyzer-no-additional-reagents = Доданих реагентів немає
plant-analyzer-no-consumed-gases = Газів не споживає
plant-analyzer-no-exuded-gases = Газів не виділяє
plant-analyzer-resist-pest = До шкідників
plant-analyzer-resist-toxin = До токсинів
plant-analyzer-resist-weed = До бур'янів
plant-analyzer-species-mutation = Можливі мутації виду
plant-analyzer-maturation = Дозрівання
plant-analyzer-production = Інтервал урожаю
plant-analyzer-health = Здоров'я
plant-analyzer-water = Вода
plant-analyzer-nutrition = Поживність
plant-analyzer-tab-condition = Стан
plant-analyzer-tab-genetics = Гени
plant-analyzer-tab-traits = Ознаки
plant-analyzer-tab-chemistry = Хімія
plant-analyzer-risk-meters = Показники ризику
plant-analyzer-warnings = Попередження

plant-analyzer-warning-low-water = Замало води
plant-analyzer-warning-high-toxin = Високий рівень токсинів
plant-analyzer-warning-pest = Виявлено шкідників
plant-analyzer-warning-weeds = Виявлено бур'яни
plant-analyzer-warning-improper-temperature = Температура поза допустимим діапазоном
plant-analyzer-warning-improper-pressure = Тиск поза допустимим діапазоном
plant-analyzer-warning-dead = Рослина загинула
plant-analyzer-warning-low-nutrition = Замало поживних речовин
plant-analyzer-warning-low-health = Здоров'я на критично низькому рівні
plant-analyzer-warning-missing-gas = В атмосфері бракує необхідних газів
plant-analyzer-ready-to-harvest = Урожай готовий до збирання
plant-analyzer-mutating = Рослина мутує

plant-analyzer-environment = Вимоги до середовища
plant-analyzer-ideal-temperature = Температура
plant-analyzer-ideal-pressure = Тиск
plant-analyzer-ideal-light = Освітлення
plant-analyzer-age = Вік
plant-analyzer-lifespan = Тривалість життя
plant-analyzer-endurance = Витривалість
plant-analyzer-water-consumption = Споживання води
plant-analyzer-nutrient-consumption = Споживання поживних речовин
plant-analyzer-products = Продукти врожаю
plant-analyzer-products-none = Плодів не утворює
plant-analyzer-produce-plural = {MAKEPLURAL($thing)}
plant-analyzer-soil-reagents = Реагенти в ґрунті
plant-analyzer-soil-empty = Непоглинених реагентів немає
plant-analyzer-unit-moles = моль
plant-analyzer-na = Н/Д

plant-analyzer-component-no-seed = рослина не знайдена
plant-analyzer-print = Друк
plant-analyzer-printout-missing = Н/Д
plant-analyzer-printout = [color=#9FED58][head=2]Звіт аналізатора рослин[/head][/color]{$nl
    }──────────────────────────────{$nl
    }[bullet/] Вид: {$seedName}{$nl
    }{$indent}[bullet/] Життєздатність: {$viable ->
        [no][color=red]Ні[/color]
        [yes][color=green]Так[/color]
        *[other]{LOC("plant-analyzer-printout-missing")}
    }{$nl
    }{$indent}[bullet/] Витривалість: {$endurance}{$nl
    }{$indent}[bullet/] Тривалість життя: {$lifespan}{$nl
    }{$indent}[bullet/] Продукт: [color=#a4885c]{$produce}[/color]{$nl
    }{$indent}[bullet/] Кудзу: {$kudzu ->
        [no][color=green]Ні[/color]
        [yes][color=red]Так[/color]
        *[other]{LOC("plant-analyzer-printout-missing")}
    }{$nl
    }[bullet/] Профіль росту:{$nl
    }{$indent}[bullet/] Вода: [color=cyan]{$water}[/color]{$nl
    }{$indent}[bullet/] Поживність: [color=orange]{$nutrients}[/color]{$nl
    }{$indent}[bullet/] Токсини: [color=yellowgreen]{$toxins}[/color]{$nl
    }{$indent}[bullet/] Шкідники: [color=magenta]{$pests}[/color]{$nl
    }{$indent}[bullet/] Бур'яни: [color=red]{$weeds}[/color]{$nl
    }[bullet/] Екологічний профіль:{$nl
    }{$indent}[bullet/] Склад: [bold]{$gasesIn}[/bold]{$nl
    }{$indent}[bullet/] Тиск: [color=lightblue]{$kpa}кПа ± {$kpaTolerance}кПа[/color]{$nl
    }{$indent}[bullet/] Температура: [color=lightsalmon]{$temp}°k ± {$tempTolerance}°k[/color]{$nl
    }{$indent}[bullet/] Освітлення: [color=gray][bold]{$lightLevel} ± {$lightTolerance}[/bold][/color]{$nl
    }[bullet/] Урожай: {$yield ->
        [-1]{LOC("plant-analyzer-printout-missing")}
        [0][color=red]0[/color]
        *[other][color=lightgreen]{$yield}[/color]
    }{$nl
    }[bullet/] Потужність: {$potency}{$nl
    }[bullet/] Насіння: {$seeds ->
        [no][color=red]Ні[/color]
        [yes][color=green]Так[/color]
        *[other]{LOC("plant-analyzer-printout-missing")}
    }{$nl
    }[bullet/] Хімікати: [color=gray][bold]{$chemicals}[/bold][/color]{$nl
    }[bullet/] Викиди: [bold]{$gasesOut}[/bold]

plant-analyzer-printer-not-ready = Принтер ще не готовий.
