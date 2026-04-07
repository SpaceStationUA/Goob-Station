# templates
# service
ntr-document-service-starting-text1 = [color=#009100]█▄ █ ▀█▀    [head=3]Документ NanoTrasen[/head]
    █ ▀█     █        Кому: Відділ обслуговування
                           Від: ЦентКом
                           Видано: {$date}
    ──────────────────────────────────────────[/color]
ntr-document-security-starting-text1 = [head=3]Документ NanoTrasen[/head]                               [color=#990909]█▄ █ ▀█▀
    Кому: Відділ безпеки                                       █ ▀█     █
    Від: ЦентКом
    Видано: {$date}
    ──────────────────────────────────────────[/color]
ntr-document-cargo-starting-text1 = [head=3]  NanoTrasen[/head]        [color=#d48311]█▄ █ ▀█▀ [/color][bold]      Кому: Вантажний відділ[/bold][head=3]
       Документ[/head]           [color=#d48311]█ ▀█     █       [/color] [bold]   Від: ЦентКом[/bold]
    ──────────────────────────────────────────
                                        Видано: {$date}
ntr-document-medical-starting-text1 = [color=#118fd4]░             █▄ █ ▀█▀    [head=3]Документ NanoTrasen[/head]                 ░
    █             █ ▀█     █        Кому: Медичний відділ                         █
    ░                                    Від: ЦентКом                                     ░
                                         Видано: {$date}
    ──────────────────────────────────────────[/color]
ntr-document-engineering-starting-text1 = [color=#a15000]█▄ █ ▀█▀    [head=3]Документ NanoTrasen[/head]
    █ ▀█     █        Кому: Інженерний відділ
                           Від: ЦентКом
                           Видано: {$date}
    ──────────────────────────────────────────[/color]
ntr-document-science-starting-text1 = [color=#94196f]░             █▄ █ ▀█▀    [head=3]Документ NanoTrasen[/head]                 ░
    █             █ ▀█     █        Кому: Науковий відділ                         █
    ░                                    Від: ЦентКом                                     ░
                                         Видано: {$date}
    ──────────────────────────────────────────[/color]
ntr-document-service-document-text = {$start}
    Корпорація хоче, щоб ви знали, що ви не {$text1} {$text2}
    Корпорація була б задоволена, якби ви {$text3}
    Печатки нижче підтверджують, що {$text4}

ntr-document-security-document-text = {$start}
    Корпорація хоче, щоб ви перевірили деякі речі перед тим, як поставити печатку на цей документ, переконайтеся, що {$text1} {$text2}
    {$text3}
    {$text4}

ntr-document-cargo-document-text = {$start}
    {$text1}
    {$text2}
    Ставлячи тут печатку, ви {$text3}

ntr-document-medical-document-text = {$start}
    {$text1} {$text2}
    {$text3}
    Ставлячи тут печатку, ви {$text4}

ntr-document-engineering-document-text = {$start}
    {$text1} {$text2}
    {$text3}
    Ставлячи тут печатку, ви {$text4}

ntr-document-science-document-text = {$start}
    Ми уважно стежили за дослідницьким відділом. {$text1} {$text2}
    через все вищесказане, ми хочемо, щоб ви забезпечили {$text3}
    печатки нижче підтверджують {$text4}
