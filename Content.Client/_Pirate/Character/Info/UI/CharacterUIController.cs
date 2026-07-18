// SPDX-FileCopyrightText: 2025 Starlight
// SPDX-FileCopyrightText: 2026 SpaceStationUA
// SPDX-License-Identifier: MIT

using System.Linq;
using Content.Client._Pirate.Character.Info.UI;
using Content.Shared.IdentityManagement;

// ReSharper disable once CheckNamespace
namespace Content.Client.UserInterface.Systems.Character;

public sealed partial class CharacterUIController
{
    private readonly Dictionary<EntityUid, CharacterInspectWindow> _openInspectionWindows = new();

    public void OpenInspectCharacterWindow(EntityUid target, EntityUid viewer)
    {
        if (!target.Valid)
            return;

        if (target == viewer)
        {
            if (_window == null || _window.IsOpen)
                return;

            _characterInfo.RequestCharacterInfo();
            SetPirateSelfCharacterInfo(target);
            _window.Open();
            return;
        }

        if (_openInspectionWindows.TryGetValue(target, out var existing))
        {
            existing.SetCharacter(target, EntityManager, viewer);
            existing.OpenCentered();
            return;
        }

        var window = new CharacterInspectWindow
        {
            Title = Loc.GetString(
                "character-info-window-title",
                ("player", Identity.Name(target, EntityManager, viewer))),
        };

        window.SetCharacter(target, EntityManager, viewer.Valid ? viewer : target);
        _openInspectionWindows[target] = window;
        window.OnClose += () => _openInspectionWindows.Remove(target);
        window.OpenCentered();
    }

    private void SetPirateSelfCharacterInfo(EntityUid entity)
    {
        if (_window == null)
            return;

        _window.InfoIC.SetCharacter(entity, EntityManager, entity);
        _window.InfoOOC.SetCharacter(entity, EntityManager, entity);
    }

    private void ClearPirateSelfCharacterInfo()
    {
        if (_window == null)
            return;

        _window.InfoIC.ClearCharacter();
        _window.InfoOOC.ClearCharacter();
    }

    private void ClosePirateCharacterInfoWindows()
    {
        foreach (var window in _openInspectionWindows.Values.ToArray())
        {
            window.Close();
        }

        _openInspectionWindows.Clear();
        ClearPirateSelfCharacterInfo();
    }
}
