// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Popups;
using Content.Shared._Pirate.CCVars;
using Content.Shared._Pirate.Knowledge;
using Content.Shared.Popups;
using Robust.Shared.Configuration;
using Robust.Shared.Timing;

namespace Content.Client._Pirate.Knowledge;

/// <summary>
/// Handles client-only skill feedback. All updates are driven by networked component state.
/// </summary>
public sealed class KnowledgeClientSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _configuration = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    public event Action? KnowledgeChanged;

    private bool _showPopups;
    private TimeSpan _nextPopup;
    private static readonly TimeSpan PopupCooldown = TimeSpan.FromSeconds(3);

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_configuration, KnowledgeCVars.SkillPopups, value => _showPopups = value, true);
        SubscribeLocalEvent<KnowledgeComponent, AfterAutoHandleStateEvent>(OnKnowledgeState);
        SubscribeAllEvent<SkillPopupEvent>(OnSkillPopup);
    }

    private void OnKnowledgeState(Entity<KnowledgeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        KnowledgeChanged?.Invoke();
    }

    private void OnSkillPopup(SkillPopupEvent args)
    {
        if (!_showPopups || _timing.CurTime < _nextPopup)
            return;

        _nextPopup = _timing.CurTime + PopupCooldown;
        _popup.PopupCursor(args.Popup, PopupType.Small);
    }
}
