using System.IO;
using System.Linq;
using Content.Client.Audio.Midi;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests.Audio.Midi;

[TestFixture]
public sealed class MidiFileCollectionManagerTests
{
    private static readonly byte[] TestBytes = [1, 2, 3, 4, 5, 6];
    private static readonly ResPath TestFileName = new("unit_test.midi");
    private static ResPath TestUserDataDir => new("/UserMidis/");
    private static ResPath TestFullPath => TestUserDataDir / TestFileName;

    [Test]
    public async Task TestAddMidiFile()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var client = pair.Client;
        var resManager = client.ResolveDependency<IResourceManager>();
        var midiLibManager = client.ResolveDependency<MidiFileCollectionManager>();

        var addedFileName = new ResPath("");
        Stream stream = new MemoryStream(TestBytes);
        midiLibManager.MidiFileAdded += s => { addedFileName = s; };

        await midiLibManager.AddMidiFile(TestFileName, stream);
        var outputBytes = resManager.UserData.ReadAllBytes(TestFullPath);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(midiLibManager.GetMidiFiles(), Contains.Item(TestFileName));
            Assert.That(outputBytes, Is.EqualTo(TestBytes));
            Assert.That(addedFileName, Is.EqualTo(TestFileName));
        }

        midiLibManager.RemoveAllMidiFiles();
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TestGetMidiData()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var client = pair.Client;
        var resManager = client.ResolveDependency<IResourceManager>();
        var midiLibManager = client.ResolveDependency<MidiFileCollectionManager>();

        resManager.UserData.WriteAllBytes(TestFullPath, TestBytes);
        var midiBytes = midiLibManager.GetMidiData(TestFileName);

        Assert.That(TestBytes, Is.EqualTo(midiBytes));

        midiLibManager.RemoveAllMidiFiles();
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TestRemoveMidiFile()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var client = pair.Client;
        var resManager = client.ResolveDependency<IResourceManager>();
        var midiLibManager = client.ResolveDependency<MidiFileCollectionManager>();

        var removedFileName = new ResPath("");
        midiLibManager.MidiFileRemoved += s => { removedFileName = s; };

        resManager.UserData.WriteAllBytes(TestFullPath, TestBytes);
        Assert.That(resManager.UserData.Exists(TestFullPath), Is.True);

        midiLibManager.RemoveMidiFile(TestFileName);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(resManager.UserData.Exists(TestFullPath), Is.False);
            Assert.That(midiLibManager.GetMidiFiles(), Is.Empty);
            Assert.That(removedFileName, Is.EqualTo(TestFileName));
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TestRemoveAllMidiFiles()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var client = pair.Client;
        var midiLibManager = client.ResolveDependency<MidiFileCollectionManager>();

        var resetFired = false;

        midiLibManager.MidiFilesReset += () => { resetFired = true; };
        await midiLibManager.AddMidiFile(new ResPath("1_unit_test.midi"), TestBytes);
        await midiLibManager.AddMidiFile(new ResPath("2_unit_test.midi"), TestBytes);
        await midiLibManager.AddMidiFile(new ResPath("3_unit_test.midi"), TestBytes);

        Assert.That(midiLibManager.GetMidiFiles().Count(), Is.EqualTo(3));

        midiLibManager.RemoveAllMidiFiles();
        using (Assert.EnterMultipleScope())
        {
            Assert.That(midiLibManager.GetMidiFiles(), Is.Empty);
            Assert.That(resetFired, Is.True);
        }

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task TestRenameMidiFile()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        var client = pair.Client;
        var resManager = client.ResolveDependency<IResourceManager>();
        var midiLibManager = client.ResolveDependency<MidiFileCollectionManager>();

        var renamedFileName = new ResPath("unit_test_renamed.midi");
        var removedFileName = new ResPath("");
        var addedFileName = new ResPath("");

        midiLibManager.MidiFileRemoved += s => { removedFileName = s; };
        midiLibManager.MidiFileAdded += s => { addedFileName = s; };

        resManager.UserData.WriteAllBytes(TestFullPath, TestBytes);
        Assert.That(resManager.UserData.Exists(TestFullPath), Is.True);

        midiLibManager.RenameMidiFile(TestFileName, renamedFileName);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(resManager.UserData.Exists(TestUserDataDir / renamedFileName), Is.True);
            Assert.That(resManager.UserData.Exists(TestFullPath), Is.False);
            Assert.That(removedFileName, Is.EqualTo(TestFileName));
            Assert.That(addedFileName, Is.EqualTo(renamedFileName));
        }

        midiLibManager.RemoveAllMidiFiles();
        await pair.CleanReturnAsync();
    }
}
