// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using Content.Client._Pirate.Traits.UI;
using Content.Shared.Preferences;
using Content.Shared.Traits;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Pirate.Traits;

[TestFixture]
public sealed class TraitSelectionPersistenceTest
{
    [TestCase("Tajaran", "TraitNightVision")]
    [TestCase("IPC", "Vampirism")]
    public async Task LoadingProfileDoesNotUsePreviousSpeciesConditions(
        string previousSpecies,
        string traitId)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;

        await client.WaitAssertion(() =>
        {
            var traitsTab = new TraitsTab();
            var previousProfile = HumanoidCharacterProfile.DefaultWithSpecies(previousSpecies);
            var selectedTrait = new ProtoId<TraitPrototype>(traitId);
            var nextProfile = HumanoidCharacterProfile.DefaultWithSpecies("Human")
                .WithTraitPreferences([selectedTrait]);

            traitsTab.UpdateConditions(previousProfile);

            HashSet<ProtoId<TraitPrototype>> changedTraits = null;
            traitsTab.OnTraitsChanged += traits => changedTraits = new HashSet<ProtoId<TraitPrototype>>(traits);

            traitsTab.SetSelectedTraits(nextProfile.TraitPreferences);
            traitsTab.UpdateConditions(nextProfile);

            Assert.That(changedTraits,
                Is.Null,
                $"Loading {traitId} must not clear it using conditions from {previousSpecies}");
        });

        await pair.CleanReturnAsync();
    }
}
