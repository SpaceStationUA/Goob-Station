// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.UIKit.UserInterface.Lobby;
using Content.Shared.Preferences.Loadouts;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private ProtoId<RoleLoadoutPrototype>? _ghostRoleLoadoutRole;
    private BoxContainer? _ghostRoleLoadoutHeader;
    private Label? _ghostRoleLoadoutTitle;
    private int _ghostRoleLoadoutReturnTab;

    private void RefreshGhostRoleLoadouts()
    {
        AntagList.AddChild(new Label
        {
            Text = Loc.GetString("humanoid-profile-editor-ghost-role-loadouts"),
            Margin = new Thickness(3f, 12f, 3f, 3f),
            StyleClasses = { HullStyleClasses.Section },
        });

        (ProtoId<RoleLoadoutPrototype> Role, string Name)[] roles =
        [
            ("GhostRoleCybersun", "roles-antag-cybersun-name"),
            ("GhostRoleMercenary", "roles-antag-mercenary-name"),
            ("GhostRoleFriendlyWizard", "humanoid-profile-editor-friendly-wizard"),
            ("GhostRoleLizardGasStation", "humanoid-profile-editor-lizard-gas-station"),
        ];

        foreach (var (role, name) in roles)
        {
            if (!_prototypeManager.HasIndex(role))
                continue;

            var row = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                Margin = new Thickness(3f, 3f, 3f, 0f),
            };
            row.AddChild(new Label
            {
                Text = Loc.GetString(name),
                MinWidth = 250,
                VerticalAlignment = VAlignment.Center,
                StyleClasses = { HullStyleClasses.Text },
            });

            var button = new Button
            {
                Text = Loc.GetString("loadout-window"),
                StyleClasses = { HullStyleClasses.Plate },
            };
            button.OnPressed += _ => OpenGhostRoleLoadoutEditor(role, Loc.GetString(name));

            row.AddChild(button);
            AntagList.AddChild(row);
        }
    }

    private void OpenGhostRoleLoadoutEditor(ProtoId<RoleLoadoutPrototype> role, string title)
    {
        if (Profile == null || _playerManager.LocalSession == null || !_prototypeManager.HasIndex(role))
            return;

        if (_ghostRoleLoadoutHeader == null)
        {
            _ghostRoleLoadoutTitle = new Label
            {
                HorizontalExpand = true,
                VerticalAlignment = VAlignment.Center,
                StyleClasses = { HullStyleClasses.Heading },
            };
            var back = new Button
            {
                Text = Loc.GetString("humanoid-profile-editor-ghost-role-loadouts-back"),
                StyleClasses = { HullStyleClasses.Plate },
            };
            back.OnPressed += _ => TabContainer.CurrentTab = _ghostRoleLoadoutReturnTab;
            _ghostRoleLoadoutHeader = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                Margin = new Thickness(0, 0, 0, 6),
                Children = { _ghostRoleLoadoutTitle, back },
            };
            LoadoutsTab.AddChild(_ghostRoleLoadoutHeader);
            _ghostRoleLoadoutHeader.SetPositionFirst();

            TabContainer.OnTabChanged += tab =>
            {
                if (_ghostRoleLoadoutRole == null || tab == LoadoutsTab.GetPositionInParent())
                    return;

                ResetGhostRoleLoadoutEditor();
                RefreshLoadouts();
                ReloadPreview();
            };
        }

        if (_ghostRoleLoadoutRole == null)
            _ghostRoleLoadoutReturnTab = TabContainer.CurrentTab;

        _ghostRoleLoadoutRole = role;
        _ghostRoleLoadoutTitle!.Text = Loc.GetString("loadout-window-title-loadout", ("job", title));
        _ghostRoleLoadoutHeader.Visible = true;
        ClearLoadoutSearch();
        RefreshLoadouts();
        ReloadPreview();
        TabContainer.CurrentTab = LoadoutsTab.GetPositionInParent();
    }

    private void ResetGhostRoleLoadoutEditor()
    {
        if (_ghostRoleLoadoutRole == null)
            return;

        _ghostRoleLoadoutRole = null;
        if (_ghostRoleLoadoutHeader != null)
            _ghostRoleLoadoutHeader.Visible = false;
        ClearLoadoutSearch();
    }

    private EntityUid? LoadGhostRoleLoadoutPreview()
    {
        if (_ghostRoleLoadoutRole is not { } role || Profile == null)
            return null;

        var dummy = _controller.LoadProfileEntity(Profile, null, false);
        if (ShowClothes.Pressed)
        {
            var loadout = Profile.GetLoadoutOrDefault(role.Id, _playerManager.LocalSession,
                Profile.Species, _entManager, _prototypeManager);
            _controller.GiveDummyLoadout(dummy, loadout);
        }

        return dummy;
    }
}
