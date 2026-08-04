using Content.Server.Chat.Managers;
using Content.Server._DV.Psionics.UI;
using Content.Server.EUI;
using Content.Shared._DV.Psionics.Components;
using Content.Shared._DV.Psionics.Events;
using Content.Shared._DV.Psionics.Systems;
using Content.Shared.Chat;
using Content.Shared.GameTicking;
using Robust.Server.Player;

namespace Content.Server._DV.Psionics.Systems;

public sealed partial class PsionicSystem : SharedPsionicSystem
{
    [Dependency] private readonly EuiManager _euiManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PotentialPsionicComponent, PlayerSpawnCompleteEvent>(OnPlayerSpawnComplete);
        SubscribeLocalEvent<PsionicPowerGainedEvent>(OnPsionicPowerGained);
        SubscribeLocalEvent<PotentialPsionicComponent, ComponentRemove>(OnPotentialRemoved);

        InitializeItems();
    }

    /// <summary>
    /// When an entity loses its psionic potential, all of its removable psionic powers
    /// self-delete (unremovable, innate powers stay).
    /// </summary>
    private void OnPotentialRemoved(EntityUid uid, PotentialPsionicComponent component, ComponentRemove args)
    {
        // Skip when the entity itself is being deleted.
        if (MetaData(uid).EntityLifeStage >= EntityLifeStage.Terminating)
            return;

        RemovePsionicPowers(uid);
    }

    private void OnPlayerSpawnComplete(Entity<PotentialPsionicComponent> potPsionic, ref PlayerSpawnCompleteEvent args)
    {
        if (RollChance(potPsionic))
            _euiManager.OpenEui(new AcceptPsionicsEui(potPsionic, this), args.Player);
    }

    /// <summary>
    /// Shows the power-gain feedback as a private, chat-only message to the player
    /// who gained the power. No world popup is shown.
    /// </summary>
    private void OnPsionicPowerGained(PsionicPowerGainedEvent ev)
    {
        if (!_playerManager.TryGetSessionByEntity(ev.User, out var session))
            return;

        _chatManager.ChatMessageToOne(ChatChannel.Emotes, ev.Feedback, ev.Feedback, ev.User, false, session.Channel);
    }
}
