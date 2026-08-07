# SPDX-License-Identifier: AGPL-3.0-or-later

grapple-start = Твої {LOC($part)} впиваються в {$victim}!
grapple-start-victim = {THE($grappler)} впивається в тебе {LOC($part)}!
grapple-start-escaping = {THE($victim)} намагається звільнитися!
grapple-start-escaping-victim = Ти починаєш звільнятися від {LOC($part)}!
grapple-manual-release = Ти обережно відпускаєш {$victim} зі своїх {LOC($part)}.
grapple-manual-release-victim = {THE($grappler)} відпускає тебе зі своїх {LOC($part)}.
grapple-finished-escaping = {$victim} звільняється з твоїх {LOC($part)}!
grapple-finished-escaping-victim = Ти звільняєшся з {LOC($part)}!

grapple-part-hands = рук
grapple-part-jaws = щелеп
grapple-part-claws = пазурів

alerts-grappled-name = [color=yellow]Схоплено[/color]
alerts-grappled-desc = Тебе [color=yellow]схопили[/color]: ти не можеш рухатися, а твої руки можуть бути зв'язані.

specialized-clothing-default-failure = Тобі це не підходить.

ghost-role-information-secdog-name = Розумний службовий пес
ghost-role-information-secdog-description = Допомагай службі безпеки, кусаючи порушників і гавкаючи на все, що рухається.
ghost-role-information-laika-name = Лайка
ghost-role-information-laika-description = Службова собака безпеки. Випрошуй почухування за вухом в офіцерів та хапай небезпечних порушників за п'яти.

role-name-laika = Лайка
role-description-laika = Гав! Гав! Гарр!
role-name-k9-officer = Офіцер K9
role-description-k9-officer = Допомагай команді безпеки гострим нюхом і зубами.
RoleLaika = Лайка
RoleK9Officer = Офіцер K9

names-secdogs-dataset-1 = Білка
names-secdogs-dataset-2 = Стрілка
names-secdogs-dataset-3 = Пчілка
names-secdogs-dataset-4 = Мушка
names-secdogs-dataset-5 = Чорнушка
names-secdogs-dataset-6 = Зірочка
names-secdogs-dataset-7 = Вітерець
names-secdogs-dataset-8 = Вуглик

lathe-category-hardsuits = Скафандри
research-technology-security-eva = Скафандри служби безпеки

ent-MobSecDogBase = службовий пес
    .desc = Кумедно, але ця свиня насправді собака.
ent-MobSecDog = службовий пес
    .desc = Кумедно, але ця свиня насправді собака.
ent-MobSecDogLaika = Лайка
    .desc = Як і її тезка, Лайка — собака невизначеної породи, щойно з вулиць і готова до сутички.

ent-ClothingOuterHardsuitCombatK9 = бойовий скафандр K9
    .desc = Бойовий скафандр для собак, створений для захисту від будь-яких ворогів у середовищі з низьким тиском. Має позначки служби безпеки станції.
ent-ClothingHeadHelmetHardsuitCombatK9 = шолом бойового скафандра K9
    .desc = Шолом для бойового скафандра службового пса.
ent-ClothingOuterHardsuitCombatRiotK9 = протиударний скафандр K9
    .desc = Собачий бойовий скафандр для контролю натовпу та протидії озброєним противникам у середовищі з низьким тиском.
ent-ClothingOuterHardsuitCombatRiotK9Helmet = шолом протиударного скафандра K9
    .desc = Шолом для протиударного скафандра службового пса.
ent-ClothingOuterArmorDuraVestK9 = дюратканинний жилет K9
    .desc = Щільний і міцний бронежилет для собак, укріплений дюратканиною для захисту від гострих предметів і ударів.

ent-K9OfficerIDCard = ID-картка офіцера K9
    .desc = Картка, що підтверджує службовий статус пса служби безпеки.
ent-K9IDCardImplant = ID-імплант K9
    .desc = Підшкірний імплант із попередньо завантаженою ID-карткою.
ent-CrateNPCSecDog = вантажний ящик зі службовим псом
    .desc = Містить одного безпородного собаку, навченого для роботи в службі безпеки.
ent-LivestockSecDog = { ent-CrateNPCSecDog }
    .desc = { ent-CrateNPCSecDog.desc }
ent-SpawnMobSecDogLaika = спавнер Лайки
    .desc = Місце для появи Лайки.
ent-SpawnMobSecLaikaOrShiva = спавнер службової тварини
    .desc = Випадково створює Лайку або Шиву.
