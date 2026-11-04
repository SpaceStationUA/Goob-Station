using System.Linq;
using Content.Shared.Humanoid.Prototypes;
using Content.Shared.Preferences.Loadouts;
using Robust.Shared.Prototypes;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private readonly List<SpeciesPrototype> _subspecies = new();
    private readonly List<ProtoId<LoadoutPrototype>> _speciesFrames = new();

    private OptionButton _subspeciesButton = default!;
    private OptionButton _speciesFrameButton = default!;
    private Control _subspeciesContainer = default!;
    private Control _speciesFrameContainer = default!;

    private void InitializeSubspecies()
    {
        _subspeciesButton = this.FindControl<OptionButton>("SubspeciesButton");
        _speciesFrameButton = this.FindControl<OptionButton>("SpeciesFrameButton");
        _subspeciesContainer = this.FindControl<Control>("SubspeciesContainer");
        _speciesFrameContainer = this.FindControl<Control>("SpeciesFrameContainer");
        _subspeciesButton.OnItemSelected += args =>
        {
            if (args.Id >= 0 && args.Id < _subspecies.Count)
                SetSpecies(_subspecies[args.Id].ID);
        };
        _speciesFrameButton.OnItemSelected += args =>
        {
            if (Profile == null || args.Id < 0 || args.Id >= _speciesFrames.Count)
                return;
            var loadout = new RoleLoadout("NeocyteLoadout");
            loadout.SelectedLoadouts["NeocyteCybernetics"] = new() { new Loadout { Prototype = _speciesFrames[args.Id] } };
            Profile = Profile.WithLoadout(loadout);
            _speciesFrameButton.SelectId(args.Id);
            SetDirty();
            ReloadPreview();
        };
    }

    private void RefreshSubspecies()
    {
        _subspecies.Clear();
        _subspeciesButton.Clear();
        _subspeciesContainer.Visible = false;
        _speciesFrameContainer.Visible = false;
        _speciesFrameButton.Clear();
        _speciesFrames.Clear();

        if (Profile == null || !_prototypeManager.TryIndex<SpeciesPrototype>(Profile.Species, out var species))
            return;

        var parentId = species.SubspeciesOf?.Id ?? species.ID;
        if (species.Loadout == "NeocyteLoadout" && _prototypeManager.TryIndex<LoadoutGroupPrototype>("NeocyteCybernetics", out var frames))
        {
            _speciesFrames.AddRange(frames.Loadouts);
            var selected = 0;
            if (Profile.Loadouts.TryGetValue("NeocyteLoadout", out var loadout) &&
                loadout.SelectedLoadouts.TryGetValue("NeocyteCybernetics", out var choices) && choices.Count > 0)
                selected = Math.Max(0, _speciesFrames.IndexOf(choices[0].Prototype));
            for (var i = 0; i < _speciesFrames.Count; i++)
                _speciesFrameButton.AddItem(Loc.GetString($"loadout-name-{_speciesFrames[i]}"), i);
            _speciesFrameButton.SelectId(selected);
            _speciesFrameContainer.Visible = true;
        }
        if (!_prototypeManager.TryIndex<SpeciesPrototype>(parentId, out var parent) || !parent.HasSubspecies)
            return;

        _subspecies.Add(parent);
        _subspecies.AddRange(_species.Where(s => s.SubspeciesOf?.Id == parentId));
        for (var i = 0; i < _subspecies.Count; i++)
        {
            var subspecies = _subspecies[i];
            _subspeciesButton.AddItem(Loc.GetString(subspecies.SubspeciesName ?? subspecies.Name), i);
            if (subspecies.ID == Profile.Species)
                _subspeciesButton.SelectId(i);
        }

        SpeciesButton.SelectId(_species.IndexOf(parent));
        _subspeciesContainer.Visible = true;
    }

    private void ApplySpeciesFramePreview()
    {
        if (Profile == null || !_entManager.EntityExists(PreviewDummy) ||
            !_prototypeManager.TryIndex(Profile.Species, out var species) || species.Loadout != "NeocyteLoadout" ||
            !_prototypeManager.TryIndex<LoadoutGroupPrototype>("NeocyteCybernetics", out var group) || group.Loadouts.Count == 0)
            return;

        var selected = group.Loadouts[0];
        if (Profile.Loadouts.TryGetValue("NeocyteLoadout", out var role) &&
            role.SelectedLoadouts.TryGetValue("NeocyteCybernetics", out var choices) && choices.Count > 0 &&
            group.Loadouts.Contains(choices[0].Prototype))
            selected = choices[0].Prototype;
        if (!_prototypeManager.TryIndex(selected, out var loadout) ||
            !loadout.Equipment.TryGetValue("outerClothing2", out var frame))
            return;
        var inventory = _entManager.System<Content.Shared.Inventory.InventorySystem>();
        if (!inventory.TryGetSlotEntity(PreviewDummy, "outerClothing2", out _))
            inventory.SpawnItemInSlot(PreviewDummy, "outerClothing2", frame, silent: true, force: true);
    }
}
