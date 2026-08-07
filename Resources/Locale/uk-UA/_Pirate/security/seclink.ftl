# SPDX-License-Identifier: AGPL-3.0-only

# SecLink store
store-preset-name-seclink = SecLink
store-category-secbaton = Кийки
store-category-secdisabler = Дизейблери
store-category-secutility = Спорядження

store-currency-display-batontoken = жетони кийка
store-currency-display-disablertoken = жетони дизейблера
store-currency-display-utilitytoken = жетони спорядження

stack-baton-token = жетон кийка
stack-disabler-token = жетон дизейблера
stack-utility-token = жетон спорядження

# Baton listings
seclink-stun-baton-name = Електрокийок
seclink-stun-baton-desc = Стандартний електрокийок. Найкращий друг офіцера СБ.
seclink-fun-baton-name = Веселий кийок
seclink-fun-baton-desc = Випромінює досі небачену енергію, здатну розважити навіть найпохмурішого злочинця. Кажуть, його живить «найсмішніша річ, яку ми будь-коли бачили», але корпус герметично запаяний.
seclink-stun-bat-name = Електробита
seclink-stun-bat-desc = Ідеально збалансована, майстерно виготовлена, з прогумованим руків'ям і бездоганним матовим покриттям. Справжній шедевр технології оглушення.
seclink-stun-sabre-name = Електрошабля
seclink-stun-sabre-desc = Результат кількох місяців розробки нового електрокийка. Керівник НДВ назвав її катаною, хоча це безперечно шабля.

# Disabler listings
seclink-disabler-name = Пістолет-дизейблер
seclink-disabler-desc = Стандартний дизейблер. Звичайний друг офіцера СБ.
seclink-funny-disabler-name = Дезорієнтатор
seclink-funny-disabler-desc = Проєктує досі небачену енергію, яку описують як «саму сутність гумору». На питання про наслідки винахідник відповів: «Воно ж працює? Відчепіться».
seclink-pocket-disabler-name = Кишеньковий дизейблер
seclink-pocket-disabler-desc = Компактний варіант дизейблера, що міняє місткість батареї на портативність. Не менш дієвий за стандартний, якщо не промахуватися.
seclink-auto-disabler-name = Автоматичний дизейблер
seclink-auto-disabler-desc = Не такий потужний, як дизейблер-ПП, але залишається надійним засобом затримання на середній дистанції.
seclink-stun-projector-name = Дизейблер «Магнум»
seclink-stun-projector-desc = Дизейблер, який жертвує ефективністю заради максимальної сили оглушення.

# Utility listings
seclink-spaceblade-name = Космолезо: обмежена серія СБ
seclink-spaceblade-desc = Спеціальна дзиґа на замовлення сил правопорядку Центрального Командування, оснащена запатентованою технологією «Оглуши й закуй».
seclink-crowbar-name = P.R.I.bar
seclink-crowbar-desc = Складаний кишеньковий лом, створений для розтискання шлюзів. Запитайте трьох людей, що означає ця абревіатура, й почуєте три різні відповіді.
seclink-citationator-name = Цитатор
seclink-citationator-desc = Пневматичний пусковий пристрій для точного вручення штрафів і попереджень.
seclink-shield-name = Компактний щит
seclink-shield-desc = Простий металевий щит, який складається для зберігання. Не надто міцний, зате легко ремонтується.
seclink-nvgoggles-name = Списані окуляри нічного бачення
seclink-nvgoggles-desc = Окуляри нічного бачення, яким місце в музеї. Вони досі працюють, хоча якість зображення залишає бажати кращого.

# Tokens and radio
ent-BatonToken = жетон кийка
    .desc = Жетон для придбання електрокийка через рацію SecLink. Він не має лимонного смаку.
    .suffix = 3 BT
ent-BatonToken1 = { ent-BatonToken }
    .desc = { ent-BatonToken.desc }
    .suffix = 1 BT
ent-BatonToken5 = { ent-BatonToken }
    .desc = { ent-BatonToken.desc }
    .suffix = 5 BT

ent-DisablerToken = жетон дизейблера
    .desc = Жетон для придбання дизейблера через рацію SecLink. Центральне Командування нагадує, що він неїстівний і не має смаку блакитної малини.
    .suffix = 3 DT
ent-DisablerToken1 = { ent-DisablerToken }
    .desc = { ent-DisablerToken.desc }
    .suffix = 1 DT
ent-DisablerToken5 = { ent-DisablerToken }
    .desc = { ent-DisablerToken.desc }
    .suffix = 5 DT

ent-UtilityToken = жетон спорядження
    .desc = Жетон для придбання допоміжного спорядження через рацію SecLink. Після низки «випадкових» госпіталізацій НДВ надало йому штучного виноградного смаку, щоб відбити бажання його їсти.
    .suffix = 3 UT
ent-UtilityToken1 = { ent-UtilityToken }
    .desc = { ent-UtilityToken.desc }
    .suffix = 1 UT
ent-UtilityToken5 = { ent-UtilityToken }
    .desc = { ent-UtilityToken.desc }
    .suffix = 5 UT

ent-BaseSeclinkRadio = рація SecLink
    .desc = Усередині встановлено мініатюрний телепортатор, що обмінює жетони SecLink на корисне спорядження. Пристрій виглядає підозріло знайомим, але це напевно випадковість.
    .suffix = По одному жетону кожного типу

# Batons
ent-StunbatonSeclink = електрокийок SecLink
    .desc = Стандартний електрокийок для знешкодження порушників.
ent-Funbaton = веселий кийок
    .desc = Електрифікований засіб, здатний безпечно розважити будь-якого злочинця. Не забудьте про перемикач живлення.
ent-Stunbat = електробита
    .desc = Електрокийок для спортивного офіцера СБ. Використання без живлення гарантовано принесе штрафний удар.
ent-Stunsabre = електрошабля
    .desc = Сучасний електрокийок, натхненний зброєю підпільних убивць. Без живлення це лише палиця.

# Disablers and bolts
ent-BaseBulletDisablerSeclink = заряд дизейблера SecLink
ent-BulletDisablerSeclink = заряд дизейблера SecLink
ent-BulletAutoDisabler = заряд автоматичного дизейблера
ent-BulletMagnumDisabler = посилений заряд дизейблера

ent-WeaponDisablerSeclink = дизейблер SecLink
    .desc = Зброя самооборони, що виснажує органічні цілі, доки вони не знепритомніють.
ent-WeaponFunnyDisabler = дезорієнтатор
    .desc = Зброя самооборони, що розважає органічні цілі, доки вони не знепритомніють.
ent-WeaponPocketDisabler = кишеньковий дизейблер
    .desc = Компактна версія культового дизейблера, яка не поступається йому силою пострілу.
ent-WeaponAutoDisabler = автоматичний дизейблер
    .desc = Дизейблер для тривалого автоматичного вогню ціною меншої сили кожного пострілу.
ent-WeaponStunProjector = дизейблер «Магнум»
    .desc = Дизейблер, розрахований на поодинокі постріли підвищеної потужності.

# Utility items
ent-SpaceBladeSec = космолезо служби безпеки
    .desc = Особлива дзиґа з технологією оглушення, сиреною та гострою любов'ю до щиколоток.
    .suffix = Служба безпеки
ent-Pribar = P.R.I.bar
    .desc = Спеціалізований складаний лом для розтискання протипожежних шлюзів. Уміщується в кишені.
ent-FineTicket = штрафна квитанція
    .desc = Штрафна квитанція з космоклеєм. Після приклеювання її можна зняти лише знищивши.
ent-TicketPad = блокнот штрафів
    .desc = Невелика папка із самоклейними штрафними квитанціями.
ent-LauncherCitationator = Цитатор
    .desc = Вручає штрафи з особливою упередженістю.
ent-CompactShield = компактний щит
    .desc = Складаний металевий щит. Не найміцніший, зате його легко полагодити зварюванням.
ent-BrokenSecShield = зламаний щит
    .desc = Компактний щит, який украй потребує зварювання.
ent-ClothingEyesSurplusNVGoggles = списані окуляри нічного бачення
    .desc = Громіздка стара модель, яка технічно ще працює. Місця для звичайних лінз усередині немає.

flavor-complex-secticket = як тухлі яйця

code-violation =
    {"[head=2]ШТРАФНА КВИТАНЦІЯ[/head]"}
    {"[bold]Порушник:[/bold]"}
    {"[bold]Порушення:[/bold]"}
    {"[bold]Сума штрафу:[/bold]"}
    {"[bold]Офіцер:[/bold]"}
