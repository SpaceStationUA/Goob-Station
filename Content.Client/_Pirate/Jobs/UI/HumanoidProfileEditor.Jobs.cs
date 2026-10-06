using System.Linq;
using System.Numerics;
using Content.Client._Pirate.Jobs.UI;
using Content.Client.Lobby.UI.Roles;
using Content.Pirate.Common.AlternativeJobs;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private static readonly (string Key, int Value)[] JobPriorityOptions =
    [
        ("humanoid-profile-editor-job-priority-never-button", (int) JobPriority.Never),
        ("humanoid-profile-editor-job-priority-low-button", (int) JobPriority.Low),
        ("humanoid-profile-editor-job-priority-medium-button", (int) JobPriority.Medium),
        ("humanoid-profile-editor-job-priority-high-button", (int) JobPriority.High),
    ];

    private readonly Dictionary<string, AlternativeJobSelector> _jobAlternatives = new();
    private readonly Dictionary<string, (TextureRect Control, Texture Default)> _jobIcons = new();

    public void RefreshJobs()
    {
        JobList.RemoveAllChildren();
        _jobPriorities.Clear();
        _jobAlternatives.Clear();
        _jobIcons.Clear();

        var departments = _prototypeManager.EnumeratePrototypes<DepartmentPrototype>()
            .Where(department => !department.EditorHidden)
            .OrderBy(department => department, DepartmentUIComparer.Instance);

        foreach (var department in departments)
        {
            var content = CreateDepartmentSection(department);

            var jobs = department.Roles.Select(jobId => _prototypeManager.Index(jobId))
                .Where(job => job.SetPreference)
                .OrderBy(job => job, JobUIComparer.Instance);

            foreach (var job in jobs)
                CreateJobEntry(job, content);
        }

        UpdateJobPriorities();
        UpdateAlternativeJobs();
    }

    private BoxContainer CreateDepartmentSection(DepartmentPrototype department)
    {
        var departmentName = Loc.GetString(department.Name);
        var content = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            Margin = new Thickness(10, 8),
        };
        var category = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Name = department.ID,
            ToolTip = Loc.GetString("humanoid-profile-editor-jobs-amount-in-department-tooltip", ("departmentName", departmentName)),
            Margin = new Thickness(0, 0, 0, 8),
        };
        var expandIcon = new Label
        {
            Text = "\u25BC",
            StyleClasses = { "ProfileCategoryExpandIcon" },
            Margin = new Thickness(0, 0, 8, 0),
        };
        var headerContent = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(8, 6),
            Children =
            {
                expandIcon,
                new Label { Text = departmentName, StyleClasses = { "ProfileCategoryNameLabel" } },
            },
        };
        var categoryButton = new Button
        {
            HorizontalExpand = true,
            StyleClasses = { "ProfileCategoryHeaderButton" },
            Children = { headerContent },
        };
        var header = new PanelContainer { Children = { categoryButton } };
        header.StyleClasses.Add("ProfileCategoryHeader");
        category.AddChild(header);
        category.AddChild(new PanelContainer
        {
            SetHeight = 3,
            StyleClasses = { "ProfileDepartmentAccent" },
            PanelOverride = new StyleBoxFlat { BackgroundColor = department.Color },
        });

        var contentPanel = new PanelContainer
        {
            HorizontalExpand = true,
            StyleClasses = { "ProfileCategoryContent" },
            Children = { content },
        };
        category.AddChild(contentPanel);
        categoryButton.OnPressed += _ =>
        {
            content.Visible = !content.Visible;
            expandIcon.Text = content.Visible ? "\u25BC" : "\u25B6";
        };
        JobList.AddChild(category);
        return content;
    }

    private void CreateJobEntry(JobPrototype job, BoxContainer departmentContent)
    {
        var selector = new RequirementsSelector
        {
            Margin = new Thickness(3f, 0f, 3f, 0f),
            VerticalAlignment = VAlignment.Center,
        };
        selector.OnOpenGuidebook += OnOpenGuidebook;
        selector.UseFlexibleOptionsWidth(50f);
        selector.SetOptionsMinHeight(28f);
        selector.Setup(JobPriorityOptions, string.Empty, 0, job.LocalizedDescription, null, job.Guides);

        if (_requirements.IsAllowed(job, Profile, out var reason))
            selector.UnlockRequirements();
        else
            selector.LockRequirements(reason);

        selector.OnSelected += priority => OnJobPrioritySelected(job.ID, priority);

        var alternativeSelector = new AlternativeJobSelector(job.ID)
        {
            HorizontalAlignment = HAlignment.Left,
            VerticalAlignment = VAlignment.Center,
        };
        alternativeSelector.OnAlternativeSelected += alternativeId =>
        {
            Profile = Profile?.WithJobAlternative(new(job.ID, alternativeId));
            UpdateJobIcon(job.ID, alternativeId);
            SetDirty();
        };
        _jobAlternatives[job.ID] = alternativeSelector;
        _jobPriorities.Add((job.ID, selector));

        var jobIcon = _prototypeManager.Index(job.Icon);
        var icon = new TextureRect
        {
            Texture = _sprite.Frame0(jobIcon.Icon),
            TextureScale = new Vector2(3, 3),
            MinSize = new Vector2(40, 40),
            Margin = new Thickness(0, 3, 0, -3),
            VerticalAlignment = VAlignment.Center,
        };
        _jobIcons[job.ID] = (icon, icon.Texture);

        var jobName = alternativeSelector.Visible
            ? (Control) alternativeSelector
            : new Label { Text = job.LocalizedName, StyleClasses = { "ProfileJobNameLabel" } };
        jobName.VerticalAlignment = VAlignment.Center;

        var nameContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SizeFlagsStretchRatio = 1f,
            VerticalAlignment = VAlignment.Center,
            SeparationOverride = 0,
            Children =
            {
                new CenterContainer
                {
                    MinSize = new Vector2(48, 48),
                    VerticalAlignment = VAlignment.Center,
                    Children = { icon },
                },
                jobName,
            },
        };
        var priority = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Align = BoxContainer.AlignMode.Center,
            HorizontalAlignment = HAlignment.Right,
            VerticalAlignment = VAlignment.Center,
            Children = { selector },
        };
        var row = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
            SeparationOverride = 0,
            Margin = new Thickness(10, 0),
            Children = { nameContainer, priority },
        };
        var card = new PanelContainer
        {
            HorizontalExpand = true,
            Margin = new Thickness(0, 0, 0, 4),
            StyleClasses = { "ProfileEntryPanel" },
            Children = { row },
        };
        departmentContent.AddChild(card);
    }

    private void OnJobPrioritySelected(string selectedJobId, int selectedPriority)
    {
        var priority = (JobPriority) selectedPriority;
        Profile = Profile?.WithJobPriority(selectedJobId, priority);

        foreach (var (jobId, selector) in _jobPriorities)
        {
            if (jobId == selectedJobId)
            {
                selector.Select(selectedPriority);
                continue;
            }

            if (priority != JobPriority.High || (JobPriority) selector.Selected != JobPriority.High)
                continue;

            selector.Select((int) JobPriority.Medium);
            Profile = Profile?.WithJobPriority(jobId, JobPriority.Medium);
        }

        ReloadPreview();
        UpdateJobPriorities();
        UpdatePirateKnowledgeEditor();
        RefreshLoadouts();
        RefreshTraits();
        SetDirty();
    }

    private void UpdateAlternativeJobs()
    {
        if (Profile == null)
            return;

        foreach (var (jobId, selector) in _jobAlternatives)
        {
            var selected = Profile.JobAlternatives.TryGetValue(jobId, out var alternativeId)
                ? alternativeId
                : new ProtoId<AlternativeJobPrototype>(jobId);
            selector.SelectAlternative(selected);
            UpdateJobIcon(jobId, selected);
        }
    }

    private void UpdateJobIcon(string jobId, ProtoId<AlternativeJobPrototype> alternativeId)
    {
        if (!_jobIcons.TryGetValue(jobId, out var icons))
            return;

        icons.Control.Texture = _prototypeManager.TryIndex(alternativeId, out AlternativeJobPrototype? alternative)
                       && !string.IsNullOrWhiteSpace(alternative.JobIconProtoId)
                       && _prototypeManager.TryIndex<JobIconPrototype>(alternative.JobIconProtoId, out var alternativeIcon)
            ? _sprite.Frame0(alternativeIcon.Icon)
            : icons.Default;
    }
}
