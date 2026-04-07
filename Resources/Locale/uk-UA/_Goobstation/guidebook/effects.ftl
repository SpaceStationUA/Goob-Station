entity-effect-guidebook-modify-disgust = { $chance ->
        [1] { $deltasign ->
                [1] Збільшує
                *[-1] Зменшує
            }
        *[other]
            { $deltasign ->
                [1] збільшення
                *[-1] зменшення
            }
    } рівень огиди на { $amount }
