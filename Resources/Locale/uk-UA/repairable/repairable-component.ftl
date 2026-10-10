### Interaction Messages

# Shown when repairing something
comp-repairable-repair = Ви завершуєте ремонт {PROPER($target) ->
  [true] {""}
  *[false] {""}
    }{$target} за допомогою {PROPER($tool) ->
  [true] {""}
  *[false] {""}
    }{$tool}
