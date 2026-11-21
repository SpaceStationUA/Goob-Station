// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Pirate.Server.Pacification.Managers;

namespace Content.IntegrationTests.Tests._Pirate.Newbies;

[TestFixture]
[NonParallelizable]
public sealed class ManualPacificationPardonTest
{
    [Test]
    public void ReissuingInvalidatesAnEarlierPardon()
    {
        var target = Guid.NewGuid();
        var manager = new PacifyManager();
        try
        {
            manager.AddToPacify(target.ToString(), "pardon-test", 1);
            var previous = manager.ReviewActive().Single(record => record.Target == target);
            manager.AddToPacify(target.ToString(), "pardon-test", 1);
            var current = manager.ReviewActive().Single(record => record.Target == target);

            Assert.That(current.Version, Is.Not.EqualTo(previous.Version));
            Assert.That(manager.PardonAtVersion(target, previous.Version), Is.EqualTo("changed"));
            Assert.That(manager.IsPacified(target.ToString()), Is.True);
            Assert.That(manager.PardonAtVersion(target, current.Version), Is.EqualTo("applied"));
            Assert.That(manager.PardonAtVersion(target, current.Version), Is.EqualTo("inactive"));
            Assert.That(manager.IsPacified(target.ToString()), Is.False);
        }
        finally
        {
            manager.RemoveFromPacify(target.ToString());
        }
    }
}
