using Content.Pirate.Shared.PAI;
using Content.Server.Medical;
using Content.Server.Medical.Components;
using Content.Shared.MedicalScanner;
using Content.Shared.Mobs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

namespace Content.Pirate.Server.PAI;

public sealed class PAIHealthSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PAIHealthScanEvent>(OnHealthScan);
    }

    private EntityUid? FindCarrier(EntityUid uid)
    {
        EntityUid? current = uid;
        while (_container.TryGetContainingContainer((current.Value, null, null), out var parentContainer))
        {
            current = parentContainer.Owner;
            if (HasComp<MobStateComponent>(current.Value))
                return current;
        }
        return null;
    }

    private void OnHealthScan(PAIHealthScanEvent args)
    {
        var uid = args.Performer;
        var carrier = FindCarrier(uid);
        if (carrier == null)
            return;

        var analyzer = EnsureComp<HealthAnalyzerComponent>(uid);
        var analyzerSystem = EntityManager.System<HealthAnalyzerSystem>();
        analyzerSystem.BeginAnalyzingEntity((uid, analyzer), carrier.Value);

        if (_ui.HasUi(uid, HealthAnalyzerUiKey.Key))
            _ui.OpenUi(uid, HealthAnalyzerUiKey.Key, uid);
    }
}
