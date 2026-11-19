pirate-clothing-accessory-verb-category = Detach accessory
pirate-clothing-accessory-verb-detach = Detach {THE($accessory)}
pirate-clothing-accessory-attach-start = {CAPITALIZE(THE($user))} starts to attach {THE($accessory)} to the clothes of {THE($target)}.
pirate-clothing-accessory-not-allowed = You cannot attach {THE($accessory)} to {THE($holder)}.
pirate-clothing-accessory-full = {CAPITALIZE(THE($holder))} has no more space for accessories.
pirate-clothing-accessory-category-full = {CAPITALIZE(THE($holder))} has no more space for this type of accessory.
pirate-clothing-accessory-duplicate = {CAPITALIZE(THE($holder))} already has {THE($accessory)}.
pirate-clothing-accessory-examine = It has {$accessories} attached.
pirate-clothing-accessory-examine-wearer = {CAPITALIZE(POSS-ADJ($wearer))} {$holder} has {$accessories} attached.
pirate-clothing-accessory-examine-item = [color=white]{$accessory}[/color]
# A line under a worn item in the character examine list, for each accessory attached to it.
pirate-clothing-accessory-examine-sublist = {"    "}◦ { $id ->
     [empty] {$item}
    *[other] [enttex id={$id} size={ $size }/]{$item}
}
