role-subtype-Chimera = Хімера
roles-antag-letoferol-sleeper-name = Сплячий агент летоферолу
roles-antag-Chimera-objective = Поширюйте інфекцію та перетворіть усі живі істоти.
leto-role-greeting = Ви знали, на що погоджувалися. Скористайтеся виданою пігулкою та пляшечкою на власний розсуд. Заразьте всіх.
letoinfected = Агент летоферолу

admin-verb-make-chimera = Перетворює ціль на початкового агента хімер.
admin-verb-text-make-chimera = Зробити агентом хімер

chimera-critical-announcement = За даними датчиків костюмів, кількість незараженого персоналу критично мала. Усьому вцілілому екіпажу наказано активувати ядерну боєголовку.

ghost-role-information-chimera-description = Поширюйте інфекцію хімер серед усіх живих істот на борту! Намагайтеся підтримувати життя союзників. Пам'ятайте: ви не бездумний зомбі!
ghost-role-information-chimera-name = Хімера
ghost-role-information-chimera-rules = Ви — розумна хімера. Ваша мета — поширювати інфекцію. Масові руйнування не є вашою ціллю. Користуватися зброєю можна, проте звичайна зброя не заражає!

collective-mind-letoferol = Летоферол
chat-radio-letoferol = Колективний розум хімер
metabolizer-type-chimera = Хімера
guide-entry-letoferol = Летоферол і хімери

reagent-name-rhymatine = риматин
reagent-desc-rhymatine = Незвичайний препарат для лікування клітинних пошкоджень, який натомість завдає шкоди холодом та електричним струмом.
reagent-name-mesotaxinide = мезотаксинід
reagent-desc-mesotaxinide = Потужний препарат проти токсинів. Розчиняє форми життя на основі летоферолу та здатен усунути мутацію хімери.
reagent-name-mesophenerol = мезофенерол
reagent-desc-mesophenerol = Дешевша альтернатива мезотаксиніду, яка усуває мутацію хімери та виводить летоферол з організму.
reagent-name-letoferol = летоферол
reagent-desc-letoferol = Суворо заборонена біологічна зброя, розроблена як удосконалена форма ромеролу. Перетворює носія на дещо більше, ніж бездумного зомбі. Її використання вважається порушенням прав розумних істот.
reagent-name-nat-letoferol = природний летоферол
reagent-desc-nat-letoferol = Органічна форма летоферолу, яку виробляють хімери. Для зараження потрібна більша доза, ніж у синтетичного летоферолу. Мутацію можна усунути мезотаксинідом або мезофенеролом.
reagent-name-protoletoferol = протолетоферол
reagent-desc-protoletoferol = Летоферол, знешкоджений електролізом. Використовується як прекурсор для препаратів проти інфекції.

reagent-effect-guidebook-revert-polymorph =
    { $chance ->
        [1] Повертає
        *[other] повертає
    } метаболізатора з форми { $entityname }

ent-Letoferol = Спалах інфекції хімер
ent-PillLetoferol = пігулка
    .desc = { ent-Pill.desc }
    .suffix = Летоферол
ent-SyringeLetoferol = шприц
    .desc = { ent-PrefilledSyringe.desc }
    .suffix = Летоферол
ent-BottleLetoferol = пляшечка летоферолу
    .desc = { ent-BaseChemistryBottleFilled.desc }
    .suffix = Летоферол
ent-MindRoleChimera = Роль хімери
ent-MindRoleChimeraInitial = Початковий агент хімер
ent-MindRoleGhostRoleChimeraOmu = Роль привида (хімера)
ent-roles-antag-Chimera-objective = Поширити інфекцію
    .desc = Заразьте всі живі істоти.

ent-ChimeraFleshKudzu = біомаса вулика хімер
    .desc = Скупчення біомаси хімер, що стрімко розростається. НАВІЩО ВИ ЗУПИНИЛИСЯ, ЩОБ ЙОГО РОЗГЛЯДАТИ?!
ent-MonoBaseMobLetoferol = хімера
    .desc = Жахливо мутована істота. Вже не розібрати, чим вона була раніше.
ent-MonoBaseMobLetoferolHumanoid = хімера
    .desc = Колись розумна істота, вкрита гнилою плоттю та кістками. У її очах досі жевріє щось людське, наче колишній власник цього тіла ще при свідомості.
ent-MobLetoferolHorror = хімера
    .desc = { ent-MonoBaseMobLetoferolHumanoid.desc }
ent-MobLetoferolHorrorGhostrole = хімера
    .desc = { ent-MonoBaseMobLetoferolHumanoid.desc }
    .suffix = Роль привида
ent-MobLetoferolBeast = звір-хімера
    .desc = { ent-MonoBaseMobLetoferol.desc }
ent-DartAberrantFleshHorrorSpikeGuidebook = кістяний шип
ent-DartAberrantFleshHorrorSpike = кістяний шип
ent-LetoferolSpikeGun = кістяний шип із летоферолом
    .desc = Випускає короткий залп кістяних шипів.
ent-LetoferolHorrorSpike = кістяний шип із летоферолом
ent-LetoferolHorrorSpikeSpread = кістяні шипи з летоферолом
ent-ActionLetoferolSpike = [color=yellow]Кістяні шипи[/color]
    .desc = Випустіть короткий залп кістяних шипів, вкритих [color=yellow]летоферолом[/color].

ent-BaseChimeraOrgan = орган хімери
ent-OrganChimeraStomach = шлунок хімери
    .desc = Дивний орган, що забезпечує обмін речовин хімери.
ent-OrganChimeraBrain = мозок хімери
    .desc = Осередок росту хімери.
ent-OrganChimeraLiver = шипова залоза хімери
    .desc = Заміщує печінку й вирощує кістяні шипи.
ent-OrganChimeraHeart = серце хімери
ent-OrganChimeraLungs = феромонний орган хімери
    .desc = Поширює феромони для спілкування з іншими хімерами через колективний розум. Також виконує роль легень.

ent-PartSkeletonChimeraBase = частина ендоскелета хімери
ent-TorsoSkeletonChimera = тулуб ендоскелета хімери
ent-GroinSkeletonChimera = таз ендоскелета хімери
ent-HeadSkeletonChimera = череп ендоскелета хімери
    .desc = На жаль, бідний Йоріку...
ent-LeftArmSkeletonChimera = ліва рука ендоскелета хімери
ent-RightArmSkeletonChimera = права рука ендоскелета хімери
ent-LeftHandSkeletonChimera = ліва кисть ендоскелета хімери
ent-RightHandSkeletonChimera = права кисть ендоскелета хімери
ent-LeftLegSkeletonChimera = ліва нога ендоскелета хімери
ent-RightLegSkeletonChimera = права нога ендоскелета хімери
ent-LeftFootSkeletonChimera = ліва стопа ендоскелета хімери
ent-RightFootSkeletonChimera = права стопа ендоскелета хімери
