pirate-blade-server-rack-window-title = Стійка blade-серверів
pirate-blade-server-rack-window-footer-flavor = ПРОШИВКА ПРИСТРОЮ (C) 2125 NANOSOFT
pirate-blade-server-rack-slot-status = Слот {$index}: {$content}
pirate-blade-server-rack-slot-entity-unknown = невідомо
pirate-blade-server-rack-slot-empty = порожньо
pirate-blade-server-rack-slot-eject = Вийняти
pirate-blade-server-rack-slot-insert = Вставити
pirate-blade-server-rack-slot-power-toggle = Перемкнути живлення
pirate-blade-server-rack-slot-locked-fail = Замкнено!
pirate-blade-server-rack-slot-whitelist-fail = Це не підходить!
pirate-blade-server-rack-examine-empty = Всередині [color=#1f8ab2]немає blade-серверів[/color].
pirate-blade-server-rack-examine-single = Всередині лише {$slot}.
pirate-blade-server-rack-examine-multiple-start = Всередині:
pirate-blade-server-rack-examine-multiple-slot-line = - {$slot}
pirate-blade-server-rack-examine-slot = [color=#1f8ab2]{ CAPITALIZE($name) }[/color] у слоті {$index}
pirate-blade-server-rack-examine-distant = Всередині [color=#1f8ab2]{ $numBlades ->
        [one] {$numBlades} blade-сервер
        [few] {$numBlades} blade-сервери
        [many] {$numBlades} blade-серверів
        *[other] {$numBlades} blade-серверів
    }[/color], але з цієї відстані не розібрати, { $numBlades ->
        [one] що саме це за сервер
        [few] що саме це за сервери
        [many] що саме це за сервери
        *[other] що саме це за сервери
    }.
pirate-blade-server-frame-incompatible-board = Ця плата несумісна з рамою...
pirate-blade-server-board-compatible-hint = Її можна використати для створення [color=#1f8ab2]blade-сервера[/color].
ent-UnfinishedBladeServerFrame = рама blade-сервера
    .desc = Рама blade-сервера в процесі складання. Потребує додаткових деталей.
ent-BladeServerFrame = рама blade-сервера
    .desc = Рама blade-сервера, готова до встановлення плати.
ent-ResearchAndDevelopmentBladeServer = blade-сервер досліджень і розробок
    .desc = Містить колективні знання науковців станції. Його знищення відкине їх у кам'яний вік. Ви ж цього не хочете?
ent-CrewMonitoringBladeServer = blade-сервер моніторингу екіпажу
    .desc = Приймає та передає стан усіх активних сенсорів костюмів на станції.
ent-BladeServerRackMachineCircuitboard = плата стійки blade-серверів
    .desc = Машинна друкована плата для стійки blade-серверів.
ent-BladeServerRack = стійка blade-серверів
    .desc = Витончена сталева рама для кількох blade-серверів. Компактна функціональність зі стильним виглядом.