// SPDX-FileCopyrightText: 2026 Pirate Development Team
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Pirate.Shared.WebUi;
using Content.Shared._Pirate.WebUi;
using Content.Pirate.Server.Radio;
using Content.Shared.PDA;
using Robust.Shared.GameObjects;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Log;
using Robust.Shared.Prototypes;
using System;
using System.Collections.Generic;

namespace Content.Pirate.Server.WebUi;

/// <summary>
///     Device-level WebUI theme service. The PDA Settings tab button shows
///     the theme picker page; switching validates server-side, applies to
///     the PDA's PirateWebUiThemeComponent (per-device override of the
///     prototype default) and lets open radio pages re-push their catalogs
///     so every CEF app follows.
/// </summary>
public sealed class PirateWebUiSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<PirateThemeListRequestEvent>(OnListRequest);
        SubscribeNetworkEvent<PirateThemeSetEvent>(OnSet);
        SubscribeLocalEvent<PdaComponent, PdaShowThemeMessage>(OnShowTheme);

    }

    /// <summary>Settings tab button: reply with the picker's initial state.</summary>
    private void OnShowTheme(EntityUid uid, PdaComponent pda, PdaShowThemeMessage msg)
    {
        Logger.DebugS("webui.theme", $"settings button on {GetNetEntity(uid)}");
        if (!_entMan.TryGetComponent<ActorComponent>(msg.Actor, out var actor))
            return;
        var channel = actor.PlayerSession.Channel;
        RaiseNetworkEvent(new PirateThemeStateEvent
        {
            Pda = GetNetEntity(uid),
            Current = PirateWebThemeResolver.ThemeOf(_entMan, _prototypes, uid),
            Allowed = PirateWebThemeResolver.AllowedThemes(_entMan, _prototypes, uid),
        }, channel);
    }

    private void OnListRequest(PirateThemeListRequestEvent msg, EntitySessionEventArgs args)
    {
        if (!TryGetEntity(msg.Pda, out var pda) || !Exists(pda.Value))
        {
            Logger.DebugS("webui.theme", $"list request for {msg.Pda}: target missing");
            return;
        }

        Logger.DebugS("webui.theme", $"list request {msg.Pda} -> current/allowed push");
        RaiseNetworkEvent(new PirateThemeStateEvent
        {
            Pda = msg.Pda,
            Current = PirateWebThemeResolver.ThemeOf(_entMan, _prototypes, pda.Value),
            Allowed = PirateWebThemeResolver.AllowedThemes(_entMan, _prototypes, pda.Value),
        }, args.SenderSession.Channel);
    }

    /// <summary>Picker click: validate against the allowed list, apply, refresh radio pages.</summary>
    private void OnSet(PirateThemeSetEvent msg, EntitySessionEventArgs args)
    {
        if (!TryGetEntity(msg.Pda, out var pda) || !Exists(pda.Value))
            return;
        if (string.IsNullOrEmpty(msg.ThemeId))
            return;
        if (!PirateWebThemeResolver.AllowedThemes(_entMan, _prototypes, pda.Value).Contains(msg.ThemeId))
        {
            Logger.DebugS("webui.theme", $"theme set {msg.ThemeId} rejected for {msg.Pda}");
            return;
        }

        var theme = EnsureComp<PirateWebUiThemeComponent>(pda.Value);
        theme.WebThemeId = msg.ThemeId;
        Dirty(pda.Value, theme);
        Logger.DebugS("webui.theme", $"theme set {msg.ThemeId} on {msg.Pda}");

        // The picker refreshes instantly (no state event was re-raised on
        // apply, which is why switched cards never showed the new current).
        RaiseNetworkEvent(new PirateThemeStateEvent
        {
            Pda = msg.Pda,
            Current = msg.ThemeId,
            Allowed = PirateWebThemeResolver.AllowedThemes(_entMan, _prototypes, pda.Value),
        }, args.SenderSession.Channel);

        EntitySystem.Get<PirateRadioSystem>().RepushCatalogsForPda(pda.Value, args.SenderSession.Channel);
    }
}

/// <summary>
///     Pure helpers resolving (and gating) WebUI themes for a device.
///     Shared truth for this system and the radio system.
/// </summary>
public static class PirateWebThemeResolver
{
    public static string ThemeOf(IEntityManager entMan, IPrototypeManager protos, EntityUid marker)
    {
        // Walk marker -> parent (cartridge -> PDA); default NT.
        var uid = (EntityUid?)marker;
        for (var i = 0; i < 3 && uid != null; i++)
        {
            if (entMan.TryGetComponent<PirateWebUiThemeComponent>(uid, out var comp))
                return comp.WebThemeId;
            if (!entMan.TryGetComponent<TransformComponent>(uid, out var t) || !t.ParentUid.IsValid())
                break;
            uid = t.ParentUid;
        }
        return "PirateNtWeb";
    }

    public static List<string> AllowedThemes(IEntityManager entMan, IPrototypeManager protos, EntityUid marker)
    {
        // The gate follows the DEVICE'S prototype, not the live override:
        // a syndi-line PDA switched to NT must be able to switch back.
        var cls = BaseClass(entMan, protos, marker);
        var list = new List<string>();
        foreach (var proto in protos.EnumeratePrototypes<PirateWebThemePrototype>())
        {
            if (proto.Class == "nt" || (cls == "syndi" && proto.Class == "syndi"))
                list.Add(proto.ID);
        }
        return list;
    }

    /// <summary>The prototype's own theme (before any live override).
    /// The composition flattened into the resolved prototype, so a direct
    /// prototype lookup is enough.</summary>
    private static string BaseClass(IEntityManager entMan, IPrototypeManager protos, EntityUid uid)
    {
        var proto = entMan.GetComponent<MetaDataComponent>(uid).EntityPrototype;
        if (proto != null &&
            proto.TryGetComponent("PirateWebUiTheme", out PirateWebUiThemeComponent? baseComp))
        {
            return protos.TryIndex<PirateWebThemePrototype>(baseComp.WebThemeId, out var p) ? p.Class : "nt";
        }
        return "nt";
    }
}
