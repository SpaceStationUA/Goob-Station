// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.ServerCurrency;
using Content.Goobstation.Shared.ServerCurrency.UI;
using Content.Server.Administration.Notes;
using Content.Server.EUI;
using Content.Shared.Eui;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Server.ServerCurrency.UI
{
    public sealed partial class CurrencyEui : BaseEui
    {
        [Dependency] private IAdminNotesManager _notesMan = default!;
        [Dependency] private IPrototypeManager _protoMan = default!;
        public CurrencyEui()
        {
            IoCManager.InjectDependencies(this);
        }

        public override void Opened()
        {
            StateDirty();
        }

        public override EuiStateBase GetNewState()
        {
            return new CurrencyEuiState();
        }


        public override void HandleMessage(EuiMessageBase msg)
        {
            base.HandleMessage(msg);
            switch (msg)
            {
                case CurrencyEuiMsg.Buy Buy:

                    BuyToken(Buy.TokenId, Player);
                    StateDirty();
                    break; //grrr fix formatting
            }
        }

        private async void BuyToken(ProtoId<TokenListingPrototype> tokenId, ICommonSession playerName)
        {
            if (!_protoMan.TryIndex<TokenListingPrototype>(tokenId, out var token))
                return;

            // Pirate: commit the token note and the conditional debit in the same transaction.
            await _notesMan.BuyCurrencyToken(playerName, Loc.GetString(token.AdminNote), token.Price);
        }
    }
}
