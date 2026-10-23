// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Shared.GameStates;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Serialization;

using Content.Pirate.Common.Genetics.Speech;
using Content.Pirate.Shared.Genetics.Mutations;
using Content.Shared.Chat;

namespace Content.Pirate.Shared.Genetics.Speech;

public sealed class SpeechFontOverrideSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpeechFontOverrideComponent, SpeechFontOverrideEvent>(OnOverride);
        SubscribeLocalEvent<MutatableComponent, TransformSpeakerFontEvent>(OnFont);
    }

    private void OnOverride(Entity<SpeechFontOverrideComponent> ent, ref SpeechFontOverrideEvent args)
    {
        if (ent.Comp.SourceOnly && args.Source != ent.Owner)
            return;

        args.Font = ent.Comp.Font;
    }
    private void OnFont(EntityUid uid, MutatableComponent comp, TransformSpeakerFontEvent args)
    {
        var ev = new SpeechFontOverrideEvent(uid, args.FontId ?? string.Empty);
        RaiseLocalEvent(uid, ref ev);
        if (!string.IsNullOrEmpty(ev.Font))
            args.FontId = ev.Font;
    }
}
