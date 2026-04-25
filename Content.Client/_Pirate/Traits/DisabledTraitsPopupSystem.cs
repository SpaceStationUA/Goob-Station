using Content.Client._Pirate.Traits.UI;
using Content.Shared.Traits;
using Content.Shared._Pirate.CCVars;
using Content.Shared._Pirate.Traits;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.Traits;

/// <summary>
/// Client system that shows a popup when traits are disabled due to unmet conditions.
/// </summary>
public sealed class DisabledTraitsPopupSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private DisabledTraitsPopup? _window;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<DisabledTraitsEvent>(OnDisabledTraits);
    }

    private void OnDisabledTraits(DisabledTraitsEvent ev)
    {
        if (_cfg.GetCVar(PirateVars.SkipDisabledTraitsPopup))
            return;

        if (ev.DisabledTraits.Count == 0)
            return;

        OpenDisabledTraitsPopup(ev.DisabledTraits);
    }

    private void OpenDisabledTraitsPopup(Dictionary<ProtoId<TraitPrototype>, List<string>> disabledTraits)
    {
        if (_window != null)
        {
            _window.Close();
            _window = null;
        }

        _window = new DisabledTraitsPopup(disabledTraits);
        _window.OpenCentered();
        _window.OnClose += () => _window = null;
    }
}
