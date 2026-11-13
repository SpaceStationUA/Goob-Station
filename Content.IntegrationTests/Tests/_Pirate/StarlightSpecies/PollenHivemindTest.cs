using Content.Server.Store.Systems;
using Content.Shared._Starlight.CollectiveMind;
using Content.Shared.Store;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Pirate.StarlightSpecies;

[TestFixture]
public sealed class PollenHivemindTest
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task PurchaseImmediatelyRegistersSporeRecipientAndPreservesExistingChannels(bool existingChannel)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var buyer = em.Spawn("MobNeoDiona", MapCoordinates.Nullspace);
            if (existingChannel)
            {
                var previous = em.EnsureComponent<CollectiveMindComponent>(buyer);
                previous.Channels.Add("CorticalBorer");
                previous.DefaultChannel = "CorticalBorer";
                em.System<CollectiveMindUpdateSystem>().UpdateCollectiveMind(buyer, previous);
            }

            var listing = server.ProtoMan.Index<ListingPrototype>("PollenTreeMushroomT2hivemind");
            var purchased = new StoreBuyFinishedEvent(buyer, new ListingDataWithCostModifiers(listing));
            em.EventBus.RaiseLocalEvent(buyer, ref purchased, broadcast: true);

            var collective = em.GetComponent<CollectiveMindComponent>(buyer);
            Assert.That(collective.Minds.ContainsKey("Spore"), Is.True,
                "Incoming chat selects recipients from Minds before the buyer has sent any IC message.");
            Assert.That(collective.DefaultChannel?.Id, Is.EqualTo(existingChannel ? "CorticalBorer" : "Spore"));
            if (existingChannel)
                Assert.That(collective.Minds.ContainsKey("CorticalBorer"), Is.True);

            var membership = collective.Minds["Spore"];
            em.EventBus.RaiseLocalEvent(buyer, ref purchased, broadcast: true);
            Assert.That(collective.Minds["Spore"], Is.EqualTo(membership), "Repeated notifications must retain the membership ID.");
            em.DeleteEntity(buyer);
        });
        await pair.CleanReturnAsync();
    }
}
