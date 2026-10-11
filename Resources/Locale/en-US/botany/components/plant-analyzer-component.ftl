plant-analyzer-component-no-seed = no plant found
plant-analyzer-produce-plural = {MAKEPLURAL($thing)}

plant-analyzer-print = Print
plant-analyzer-printout-missing = N/A
plant-analyzer-printout = [color=#9FED58][head=2]Plant Analyzer Report[/head][/color]{$nl
    }──────────────────────────────{$nl
    }[bullet/] Species: {$seedName}{$nl
    }{$indent}[bullet/] Viable: {$viable ->
        [no][color=red]No[/color]
        [yes][color=green]Yes[/color]
        *[other]{LOC("plant-analyzer-printout-missing")}
    }{$nl
    }{$indent}[bullet/] Endurance: {$endurance}{$nl
    }{$indent}[bullet/] Lifespan: {$lifespan}{$nl
    }{$indent}[bullet/] Product: [color=#a4885c]{$produce}[/color]{$nl
    }{$indent}[bullet/] Kudzu: {$kudzu ->
        [no][color=green]No[/color]
        [yes][color=red]Yes[/color]
        *[other]{LOC("plant-analyzer-printout-missing")}
    }{$nl
    }[bullet/] Growth profile:{$nl
    }{$indent}[bullet/] Water: [color=cyan]{$water}[/color]{$nl
    }{$indent}[bullet/] Nutrition: [color=orange]{$nutrients}[/color]{$nl
    }{$indent}[bullet/] Toxins: [color=yellowgreen]{$toxins}[/color]{$nl
    }{$indent}[bullet/] Pests: [color=magenta]{$pests}[/color]{$nl
    }{$indent}[bullet/] Weeds: [color=red]{$weeds}[/color]{$nl
    }[bullet/] Environmental profile:{$nl
    }{$indent}[bullet/] Composition: [bold]{$gasesIn}[/bold]{$nl
    }{$indent}[bullet/] Pressure: [color=lightblue]{$kpa}kPa ± {$kpaTolerance}kPa[/color]{$nl
    }{$indent}[bullet/] Temperature: [color=lightsalmon]{$temp}°k ± {$tempTolerance}°k[/color]{$nl
    }{$indent}[bullet/] Light: [color=gray][bold]{$lightLevel} ± {$lightTolerance}[/bold][/color]{$nl
    }[bullet/] Flowers: {$yield ->
        [-1]{LOC("plant-analyzer-printout-missing")}
        [0][color=red]0[/color]
        *[other][color=lightgreen]{$yield} {$potency}[/color]
    }{$nl
    }[bullet/] Seeds: {$seeds ->
        [no][color=red]No[/color]
        [yes][color=green]Yes[/color]
        *[other]{LOC("plant-analyzer-printout-missing")}
    }{$nl
    }[bullet/] Chemicals: [color=gray][bold]{$chemicals}[/bold][/color]{$nl
    }[bullet/] Emissions: [bold]{$gasesOut}[/bold]
