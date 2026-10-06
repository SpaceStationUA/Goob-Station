using System.Text.RegularExpressions;
using Content.Server._Pirate.Speech.Components;
using Content.Shared.Speech;
using Robust.Shared.Random;

namespace Content.Server._Pirate.Speech.Systems;

public sealed class RhotacismAccentSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly Regex Rs = new("[\\u0440\\u0420]+", RegexOptions.Compiled);
    private static readonly Regex Ls = new("[\\u043B\\u041B](?:['\\u2019\\u02BC])?", RegexOptions.Compiled);

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RhotacismAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, RhotacismAccentComponent component, AccentGetEvent args)
    {
        var message = Rs.Replace(args.Message, match => new string(match.Value[0], _random.Next(1, 4)));
        message = Ls.Replace(message, match => match.Length > 1 || match.Value[0] == '\u041B' ? "\u0420" : "\u0440");
        args.Message = message;
    }
}

