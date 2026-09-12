using Content.Pirate.Shared.Traits.Trainability;
using Content.Shared.Alert;
using Content.Shared.Popups;
using Robust.Shared.IoC;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Pirate.Server.Traits.Trainability
{
    public sealed partial class TrainabilitySystem : EntitySystem
    {
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly AlertsSystem _alertsSystem = default!;
        [Dependency] private readonly SharedPopupSystem _popup = default!;

        public override void Initialize()
        {
            base.Initialize();

            InitializeCombat();
            InitializePhysicalTraining();
            InitializeNutrition();
            InitializeLifecycle();
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var query = EntityQueryEnumerator<TrainabilityComponent>();

            while (query.MoveNext(out var uid, out var comp))
            {
                UpdateSprintProgress(frameTime, uid, comp);
                HandleRecovery(uid, comp);
            }
        }
    }
}
