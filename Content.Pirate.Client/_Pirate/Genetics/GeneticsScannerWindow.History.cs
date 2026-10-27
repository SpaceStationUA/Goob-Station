// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Pirate.Shared.Genetics.Console;
using Content.Pirate.Shared.Genetics.Mutations;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Pirate.Client.Genetics.UI;

public sealed partial class GeneticsScannerWindow
{
    public event Action<int?>? OnSelectScan;

    private enum TargetSearch
    {
        All,
        Name,
        Sequence
    }

    private const float ChipWidth = 50;

    private static readonly Color SearchHitColor = Color.FromHex("#E0B040");

    private const string ChipTextures = "/Textures/_Pirate/Interface/Genetics/";

    private Texture? _chipFill;
    private Texture? _chipBorder;
    private Texture? _chipBorderThick;

    private HandheldGeneticsHistoryState? _history;
    private bool _hasStoredSequences;
    private readonly List<(Button Button, int Index)> _targetButtons = new();

    private void InitializeHistory()
    {
        Sequencer.HideHandheldScanButton();
        Sequencer.ListenSequenceSelected(UpdateCarriers);

        var cache = IoCManager.Resolve<IResourceCache>();
        _chipFill = cache.GetTexture(ChipTextures + "chip_fill.png");
        _chipBorder = cache.GetTexture(ChipTextures + "chip_border.png");
        _chipBorderThick = cache.GetTexture(ChipTextures + "chip_border_thick.png");

        SearchMode.AddItem(Loc.GetString("genetics-scanner-search-all"), (int) TargetSearch.All);
        SearchMode.AddItem(Loc.GetString("genetics-scanner-search-name"), (int) TargetSearch.Name);
        SearchMode.AddItem(Loc.GetString("genetics-scanner-search-sequence"), (int) TargetSearch.Sequence);
        SearchMode.OnItemSelected += args =>
        {
            SearchMode.SelectId(args.Id);
            UpdateTargetList();
        };
        SearchText.OnTextChanged += _ => UpdateTargetList();
    }

    private void UpdateHistoryState(GeneticsConsoleState state)
    {
        _history = state.HandheldHistory;
        _hasStoredSequences = state.Sequences.Count != 0;
        HistoryContainer.Visible = _history != null;
        UpdateTargetList();
        UpdateCarriers();
        UpdateHistoryView();
    }

    private void UpdateCarriers()
    {
        if (_history == null || Sequencer.SelectedSequence is not {} sequence)
        {
            Sequencer.SetCarriers(null, null, 0, null);
            return;
        }

        var carriers = new List<(string Name, int Index)>();
        for (var i = 0; i < _history.Targets.Count; i++)
        {
            var target = _history.Targets[i];
            if (target.Sequences.Any(s => s.Number == sequence.Number))
                carriers.Add((target.Name, i));
        }

        carriers.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase));
        Sequencer.SetCarriers(carriers, _history.SelectedScan, _history.Targets.Count, index =>
        {
            // Keep the current target selected while busy.
            if (_busy)
            {
                UpdateCarriers();
                return;
            }

            if (index != _history?.SelectedScan)
                OnSelectScan?.Invoke(index);
        });
    }

    private void UpdateTargetList()
    {
        TargetRows.RemoveAllChildren();
        _targetButtons.Clear();
        NoTargets.Visible = _history != null && _history.Targets.Count == 0;
        if (_history == null)
            return;

        var mode = (TargetSearch) SearchMode.SelectedId;
        var search = SearchText.Text.Trim();
        int? hit = mode != TargetSearch.Name && int.TryParse(search, out var number) ? number : null;

        var matches = new List<int>();
        for (var i = 0; i < _history.Targets.Count; i++)
        {
            if (Matches(_history.Targets[i], mode, search))
                matches.Add(i);
        }
        matches.Sort((a, b) => string.Compare(_history.Targets[a].Name, _history.Targets[b].Name,
            StringComparison.CurrentCultureIgnoreCase));

        foreach (var index in matches)
        {
            TargetRows.AddChild(CreateTargetRow(_history.Targets[index], index, hit));
        }
        SyncTargetSelection();
    }

    private Button CreateTargetRow(HandheldGeneticsTarget target, int index, int? hit)
    {
        var sequences = new GridContainer
        {
            Columns = 4,
            HSeparationOverride = 4,
            VSeparationOverride = 3,
            Margin = new Thickness(0, 3, 0, 1)
        };
        foreach (var sequence in target.Sequences)
        {
            sequences.AddChild(CreateSequenceChip(sequence, sequence.Number == hit));
        }

        var button = new Button
        {
            ToggleMode = true,
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 2),
            StyleClasses = { "ButtonSquare" }
        };
        button.AddChild(new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(0, 2),
            Children =
            {
                new Label { Text = target.Name, ClipText = true },
                sequences
            }
        });
        button.OnPressed += _ => SelectTarget(index);
        _targetButtons.Add((button, index));
        return button;
    }

    private Control CreateSequenceChip(HandheldGeneticsTargetSequence sequence, bool hit)
    {
        var (background, border, text) = sequence.Rarity switch
        {
            MutationRarity.Rare => ("#1E3550", "#3B82C4", "#A8D0FF"),
            MutationRarity.Mythical => ("#35224A", "#8E5BBF", "#DCC0F5"),
            _ => ("#2A2E38", "#4A5060", "#B8BEC8")
        };
        var fill = new StyleBoxTexture
        {
            Texture = _chipFill,
            Modulate = Color.FromHex(background)
        };
        fill.SetPatchMargin(StyleBox.Margin.All, 3);
        fill.SetContentMarginOverride(StyleBox.Margin.All, 0);
        var ring = new StyleBoxTexture
        {
            Texture = hit ? _chipBorderThick : _chipBorder,
            Modulate = hit ? SearchHitColor : Color.FromHex(border)
        };
        ring.SetPatchMargin(StyleBox.Margin.All, 3);
        ring.SetContentMarginOverride(StyleBox.Margin.Horizontal, 3);
        ring.SetContentMarginOverride(StyleBox.Margin.Vertical, 1);

        return new PanelContainer
        {
            PanelOverride = fill,
            MinWidth = ChipWidth,
            Children =
            {
                new PanelContainer
                {
                    PanelOverride = ring,
                    Children =
                    {
                        new Label
                        {
                            Text = Loc.GetString("genetics-console-sequence-text",
                                ("rarity", sequence.Rarity.RarityChar()), ("number", sequence.Number)),
                            StyleClasses = { "LabelSubText" },
                            FontColorOverride = Color.FromHex(text),
                            HorizontalAlignment = HAlignment.Center
                        }
                    }
                }
            }
        };
    }

    private static bool Matches(HandheldGeneticsTarget target, TargetSearch mode, string search)
    {
        if (search.Length == 0)
            return true;

        var name = mode != TargetSearch.Sequence &&
            target.Name.Contains(search, StringComparison.CurrentCultureIgnoreCase);
        var sequence = mode != TargetSearch.Name &&
            int.TryParse(search, out var number) && target.Sequences.Any(s => s.Number == number);
        return name || sequence;
    }

    private void SelectTarget(int index)
    {
        if (_busy || index == _history?.SelectedScan)
        {
            // Keep the shown target selected while busy or re-clicked.
            SyncTargetSelection();
            return;
        }

        OnSelectScan?.Invoke(index);
        foreach (var (button, i) in _targetButtons)
        {
            button.Pressed = i == index;
        }
    }

    private void SyncTargetSelection()
    {
        foreach (var (button, index) in _targetButtons)
        {
            button.Pressed = index == _history?.SelectedScan;
        }
    }

    private void UpdateHistoryView()
    {
        Sequencer.ShowHandheldScan(_history?.SelectedScan != null, _hasStoredSequences, _busy);
    }
}

public sealed partial class GeneticSequencer
{
    public SequenceState? SelectedSequence => SequenceButtons.Sequence;

    public void ListenSequenceSelected(Action action)
    {
        SequenceButtons.OnSelected += _ => action();
    }

    public void SetCarriers(List<(string Name, int Index)>? carriers, int? current, int total, Action<int>? select)
    {
        Info.SetCarriers(carriers, current, total, select);
    }

    public void HideHandheldScanButton()
    {
        Scanner.HideHandheldScanButton();
    }

    public void ShowHandheldScan(bool hasRecord, bool hasSequences, bool busy)
    {
        Scanner.SetError(hasRecord
            ? hasSequences ? null : "genetics-console-sequencer-no-sequences"
            : busy ? "genetics-scanner-scanning" : "genetics-scanner-use-on-subject");
    }
}

public sealed partial class SequenceInfo
{
    private Action<int>? _selectCarrier;
    private bool _carriersHooked;

    public void SetCarriers(List<(string Name, int Index)>? carriers, int? current, int total, Action<int>? select)
    {
        if (!_carriersHooked)
        {
            _carriersHooked = true;
            CarriersSelect.OnItemSelected += args =>
            {
                CarriersSelect.SelectId(args.Id);
                _selectCarrier?.Invoke(args.Id);
            };
        }

        _selectCarrier = select;
        CarriersRow.Visible = carriers != null;
        CarriersSelect.Clear();
        if (carriers == null)
            return;

        CarriersCount.Text = Loc.GetString("genetics-scanner-carriers-count", ("count", carriers.Count), ("total", total));
        foreach (var (name, index) in carriers)
        {
            CarriersSelect.AddItem(name, index);
        }
        if (current is {} selected)
            CarriersSelect.SelectId(selected);
        CarriersSelect.Disabled = carriers.Count <= 1;
    }
}

public sealed partial class GeneticScanner
{
    public void HideHandheldScanButton()
    {
        ScanButton.Visible = false;
    }
}
