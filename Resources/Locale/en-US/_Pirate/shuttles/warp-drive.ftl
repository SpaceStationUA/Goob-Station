# SPDX-License-Identifier: AGPL-3.0-or-later

warp-drive-window-state = Field:
warp-drive-window-state-idle = Idle
warp-drive-window-state-spooling = Spooling up
warp-drive-window-state-engaged = Field up
warp-drive-window-state-in-transit = In transit

warp-drive-window-power = Power draw:
warp-drive-window-power-value = {$draw} / {$max} W
warp-drive-window-power-none = No supply

warp-drive-window-range = Range:
warp-drive-window-range-value = {$range} m

warp-drive-window-spool = Spool:
warp-drive-window-spool-value = {$pct} %

warp-drive-window-heat = Core heat:
warp-drive-window-heat-value = {$pct} %

warp-drive-window-stability = Stability:
warp-drive-window-stability-value = {$time}

warp-drive-window-cooldown = Cooldown:
warp-drive-window-cooldown-value = {$time}

warp-drive-window-none = N/A

warp-drive-window-spool-up = Spool up
warp-drive-window-shut-down = Shut down
warp-drive-window-cooling = Cooling down
warp-drive-window-in-transit = Jump in progress

# Examine lines. Added only when examined up close, and deliberately more specific than the
# bluespace drives get - this is what lets a player tell the two apart without opening a window.
warp-drive-examine-ready = [i]The core is cold. The field is offline.[/i]
warp-drive-examine-unpowered = [i]No supply. It draws far too much for anything but HV.[/i]
warp-drive-examine-cooling = [i]The core is still hot. It will not spool for a while yet.[/i]
warp-drive-examine-spooling = [i]The field is building. It is drawing heavily.[/i]
warp-drive-examine-engaged = [i]The field is stable, and the core is climbing toward its limit.[/i]
warp-drive-examine-in-transit = [i]The field is open. The ship is in transit.[/i]