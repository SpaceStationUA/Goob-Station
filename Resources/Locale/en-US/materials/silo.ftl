ore-silo-ui-title = Material Silo
ore-silo-ui-label-clients = Machines
ore-silo-ui-label-mats = Materials
ore-silo-ui-itemlist-entry = {$linked ->
    [true] {"[Linked] "}
    *[False] {""}
} {$name} ({$beacon}) {$inRange ->
    [true] {""}
    *[false] (Out of Range)
}

# Pirate: multiz - cross-z silo link failures
ore-silo-ui-link-failed-unavailable = That machine can no longer be linked.
ore-silo-ui-link-failed-unpowered = The silo has no power.
ore-silo-ui-link-failed-different-grid = That machine is on a separate structure.
ore-silo-ui-link-failed-out-of-range = That machine is outside the silo's range.
