// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Events; // Pirate: mappable codeword paper
using Content.Server.GameTicking.Rules;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.Traitor.Components;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using System.Linq;
using Content.Server.Codewords;
using Content.Shared.Paper;

namespace Content.Server.Traitor.Systems;

public sealed class TraitorCodePaperSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly CodewordSystem _codewordSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TraitorCodePaperComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting, after: [typeof(CodewordSystem)]); // Pirate: mappable codeword paper
    }

    #region Pirate: mappable codeword paper
    /// <summary>
    /// Grids that load during <c>LoadMaps</c> (centcomm, Lavaland and its ruins) map-init before
    /// <see cref="CodewordSystem"/> has created the codeword manager, so any paper mapped onto them
    /// fills itself in with the "no codewords" fallback. Fill them in again once codewords exist.
    /// </summary>
    private void OnRoundStarting(RoundStartingEvent ev)
    {
        var query = EntityQueryEnumerator<TraitorCodePaperComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            SetupPaper(uid, component);
        }
    }
    #endregion

    private void OnMapInit(EntityUid uid, TraitorCodePaperComponent component, MapInitEvent args)
    {
        SetupPaper(uid, component);
    }

    private void SetupPaper(EntityUid uid, TraitorCodePaperComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (TryComp(uid, out PaperComponent? paperComp))
        {
            if (TryGetTraitorCode(out var paperContent, component))
            {
                _paper.SetContent((uid, paperComp), paperContent);
            }
        }
    }

    private bool TryGetTraitorCode([NotNullWhen(true)] out string? traitorCode, TraitorCodePaperComponent component)
    {
        traitorCode = null;

        var codesMessage = new FormattedMessage();
        var codeList = _codewordSystem.GetCodewords(component.CodewordFaction).ToList();

        if (codeList.Count == 0)
        {
            if (component.FakeCodewords)
                codeList = _codewordSystem.GenerateCodewords(component.CodewordGenerator).ToList();
            else
                codeList = [Loc.GetString("traitor-codes-none")];
        }

        _random.Shuffle(codeList);

        int i = 0;
        foreach (var code in codeList)
        {
            i++;
            if (i > component.CodewordAmount && !component.CodewordShowAll)
                break;

            codesMessage.PushNewline();
            codesMessage.AddMarkupOrThrow(code);
        }

        if (!codesMessage.IsEmpty)
        {
            if (i == 1)
                traitorCode = Loc.GetString("traitor-codes-message-singular") + codesMessage;
            else
                traitorCode = Loc.GetString("traitor-codes-message-plural") + codesMessage;
        }
        return !codesMessage.IsEmpty;
    }
}