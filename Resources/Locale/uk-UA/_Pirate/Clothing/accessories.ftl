pirate-clothing-accessory-verb-category = Зняти аксесуар
pirate-clothing-accessory-verb-detach = Зняти { $accessory }
pirate-clothing-accessory-attach-start = { CAPITALIZE(THE($user)) } починає прикріплювати { $accessory } до одягу { $target }.
pirate-clothing-accessory-not-allowed = { CAPITALIZE($accessory) } не можна прикріпити до цього одягу.
pirate-clothing-accessory-full = На цьому одязі більше немає місця для аксесуарів.
pirate-clothing-accessory-category-full = На цьому одязі більше немає місця для такого аксесуара.
pirate-clothing-accessory-duplicate = На цьому одязі вже є { $accessory }.
pirate-clothing-accessory-examine = Прикріплено: { $accessories }.
pirate-clothing-accessory-examine-wearer = На одязі прикріплено: { $accessories }.
pirate-clothing-accessory-examine-item = [color=white]{ $accessory }[/color]
# A line under a worn item in the character examine list, for each accessory attached to it.
pirate-clothing-accessory-examine-sublist = {"    "}◦ { $id ->
     [empty] {$item}
    *[other] [enttex id={$id} size={ $size }/]{$item}
}

pirate-armband-switch-arm = Перевдягнути на інше плече
