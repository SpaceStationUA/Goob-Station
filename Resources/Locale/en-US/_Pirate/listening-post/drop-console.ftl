syndicate-drop-console-window-title = dead drop dispatch
syndicate-drop-console-no-station = No station acquired

syndicate-drop-console-mode-header = Dispatch mode
syndicate-drop-console-mode-auto = Automatic
syndicate-drop-console-mode-manual = Manual
syndicate-drop-console-mode-auto-desc = Every charge is spent the moment it is ready, on a random site
syndicate-drop-console-mode-manual-desc = Charges are banked until you send one, and land on the drop site below.

syndicate-drop-console-next-header = Next charge
syndicate-drop-console-countdown-unknown = --:--

# Matches the handheld GPS readout, so operators can call sites out to agents directly.
syndicate-drop-console-coordinates = ({$x}, {$y})

syndicate-drop-console-target-header = Drop site
syndicate-drop-console-target-none = Random
syndicate-drop-console-target-clear = Clear drop site

syndicate-drop-console-charges-header = Charges ({$count}/{$max})
syndicate-drop-console-launch = Send

syndicate-drop-console-pod-header = Launch pad
syndicate-drop-console-pod-send = SEND FROM PAD
syndicate-drop-console-pod-warning = Notice: every launch transmits from this facility. Repeated use will let the station triangulate our position.
syndicate-drop-console-pod-unlinked = No pad linked.
syndicate-drop-console-pod-ready = Signal cold.
syndicate-drop-console-pod-traceable = Traceable: {$time}

syndicate-drop-console-history-header = Dispatch log ({$count})
syndicate-drop-console-history-empty = No drops yet.
syndicate-drop-console-history-entry = [{$time}] {$price} TC {$coordinates} {$mode}
syndicate-drop-console-history-entry-pod = [{$time}] {$coordinates} {$mode}
syndicate-drop-console-mode-badge-automatic = A
syndicate-drop-console-mode-badge-manual = M
syndicate-drop-console-mode-badge-pod = POD

syndicate-drop-console-intercept-announcement = Attention. Nanotrasen Communications Intelligence has logged a hostile bluespace transmission directed at this station and fixed its source. A detailed report has been sent to command fax terminals.
syndicate-drop-console-intercept-fax-title = Intercept report
syndicate-drop-console-intercept-unknown-source = an untracked position
syndicate-drop-console-intercept-fax-body =
    {"["}head=2]NANOTRASEN COMMUNICATIONS INTELLIGENCE[/head]
    {"["}bold]INTERCEPT REPORT - PRIORITY RED[/bold]

    Listening stations have logged a series of bluespace transmissions directed at your station and fixed their source: [bold]{$location}[/bold], approximate coordinates [bold]{$coordinates}[/bold].

    Signature analysis establishes an operational launch platform at the indicated position, from which cargo is being transferred aboard your station on a regular basis. The source is classified as hostile to Nanotrasen assets.

    {"["}bold]Instructions:[/bold]
    - Treat any cargo reaching the station outside the supply schedule as hostile.
    - Direct Security to sweep the station for foreign objects.
    - Command is to brief all personnel operating in the vicinity of the indicated coordinates: the area is to be treated as hostile.
    - Approach to the indicated coordinates without express authorisation from Central Command is prohibited.

syndicate-drop-console-radio-announcement = A dead drop has been bluespaced near {$location}, coordinates {$coordinates}. Collect these tools at your convenience.

syndicate-drop-console-footer-left = Cybersun Industries Logistics
syndicate-drop-console-footer-right = Bluespace Dispatcher v3.1
