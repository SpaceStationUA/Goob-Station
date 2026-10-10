using Content.Server._Goobstation.Wizard.Components;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Pirate.Server.MakeATraitor
{
    public sealed class MakeATraitorSystem : EntitySystem
    {
        public enum TraitorType
        {
            Traitor = 0,
            Thief = 1,
            Revolutionary = 2,
            Changeling = 3,
            Heretic = 4,
            Blob = 5,
            Wizard = 6,
            // Pirate start
            Vampire = 7,
            // Pirate end
        }

        private static readonly EntProtoId DefaultTraitorRule = "Traitor";

        private static readonly EntProtoId DefaultRevsRule = "Revolutionary";

        private static readonly EntProtoId DefaultThiefRule = "Thief";

        private static readonly EntProtoId DefaultChangelingRule = "Changeling";

        private static readonly EntProtoId DefaultHereticRule = "Heretic";

        // Pirate start
        private static readonly EntProtoId DefaultVampireRule = "Vampire";
        // Pirate end

        [Dependency] private readonly AntagSelectionSystem _antag = default!;

        public void MakeTraitor(TraitorType traitorType, EntityUid entity)
        {
            if (!TryComp<ActorComponent>(entity, out var actor))
                return;

            var player = actor.PlayerSession;

            switch (traitorType)
            {
                case TraitorType.Traitor:
                    MakeTraitor(player);
                    break;
                case TraitorType.Thief:
                    MakeThief(player);
                    break;
                case TraitorType.Revolutionary:
                    MakeRevolutionary(player);
                    break;
                case TraitorType.Changeling:
                    MakeChangeling(player);
                    break;
                case TraitorType.Heretic:
                    MakeHeretic(player);
                    break;
                case TraitorType.Blob:
                    MakeBlob(entity);
                    break;
                case TraitorType.Wizard:
                    MakeWizard(player);
                    break;
                // Pirate start
                case TraitorType.Vampire:
                    MakeVampire(player);
                    break;
                // Pirate end
                default:
                    return;
            }
        }

        private void MakeWizard(ICommonSession? target)
        {
            _antag.ForceMakeAntag<WizardRuleComponent>(target, "Wizard");
        }

        private void MakeBlob(EntityUid target)
        {
            EnsureComp<Content.Goobstation.Common.Blob.BlobCarrierComponent>(target).HasMind =
                HasComp<ActorComponent>(target);
        }

        private void MakeTraitor(ICommonSession? target)
        {
            _antag.ForceMakeAntag<TraitorRuleComponent>(target, DefaultTraitorRule);
        }

        private void MakeThief(ICommonSession? target)
        {
            _antag.ForceMakeAntag<ThiefRuleComponent>(target, DefaultThiefRule);
        }

        private void MakeRevolutionary(ICommonSession? target)
        {
            _antag.ForceMakeAntag<RevolutionaryRuleComponent>(target, DefaultRevsRule);
        }

        private void MakeChangeling(ICommonSession? target)
        {
            _antag.ForceMakeAntag<Content.Goobstation.Server.Changeling.GameTicking.Rules.ChangelingRuleComponent>(
                target,
                DefaultChangelingRule);
        }

        // Pirate start
        private void MakeVampire(ICommonSession? target)
        {
            _antag.ForceMakeAntag<Content.Pirate.Server.GameTicking.Rules.Components.VampireRuleComponent>(
                target,
                DefaultVampireRule);
        }
        // Pirate end

        private void MakeHeretic(ICommonSession? target)
        {
            _antag.ForceMakeAntag<HereticRuleComponent>(target, DefaultHereticRule);
        }
    }
}
