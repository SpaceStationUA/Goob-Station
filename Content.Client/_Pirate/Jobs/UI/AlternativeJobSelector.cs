using Content.Pirate.Common.AlternativeJobs;
using Content.Shared.Roles;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Client._Pirate.Jobs.UI;

[Virtual]
public class AlternativeJobSelector : OptionButton
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private readonly ProtoId<JobPrototype> _parentJobId;
    private readonly List<ProtoId<AlternativeJobPrototype>> _optionIds = new();
    public event Action<ProtoId<AlternativeJobPrototype>>? OnAlternativeSelected;

    public AlternativeJobSelector(ProtoId<JobPrototype> parentJobId)
    {
        IoCManager.InjectDependencies(this);
        _parentJobId = parentJobId;
        PopulateAlternatives();
        StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = Color.Transparent,
            Padding = new Thickness(0),
            ContentMarginLeftOverride = 0,
            ContentMarginRightOverride = 0,
            ContentMarginTopOverride = 0,
            ContentMarginBottomOverride = 0,
        };

        var nameLabel = (Label) GetChild(0).GetChild(0);
        nameLabel.StyleClasses.Remove(OptionButton.StyleClassOptionButton);
        nameLabel.StyleClasses.Add("ProfileJobNameLabel");
        nameLabel.Align = Label.AlignMode.Left;
        nameLabel.Margin = new Thickness(-4, 0, 0, 0);
        nameLabel.HorizontalExpand = false;
        GetChild(0).GetChild(1).Margin = new Thickness(8, 0, 0, 0);
        HorizontalExpand = false;
        OnItemSelected += OnItemSelectedHandler;
    }

    private void OnItemSelectedHandler(ItemSelectedEventArgs args)
    {
        if (args.Id < 0 || args.Id >= _optionIds.Count)
            return;

        SelectId(args.Id);
        OnAlternativeSelected?.Invoke(_optionIds[args.Id]);
    }

    private void PopulateAlternatives()
    {
        Clear();
        _optionIds.Clear();

        if (_prototypeManager.TryIndex(_parentJobId, out var jobProto, false))
            AddItem(jobProto.LocalizedName, 0);
        else
            AddItem(_parentJobId, 0);

        _optionIds.Add(new ProtoId<AlternativeJobPrototype>(_parentJobId.Id));

        foreach (var altJob in _prototypeManager.EnumeratePrototypes<AlternativeJobPrototype>())
        {
            if (altJob.ParentJobId != _parentJobId.Id)
                continue;

            AddItem(altJob.LocalizedJobName, _optionIds.Count);
            _optionIds.Add(altJob.ID);
        }

        Visible = _optionIds.Count >= 2;
    }

    public void SelectAlternative(ProtoId<AlternativeJobPrototype> alternativeId)
    {
        var index = _optionIds.IndexOf(alternativeId);
        if (index >= 0)
            SelectId(index);
    }
}
