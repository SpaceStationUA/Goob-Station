// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.ListeningPost.DropConsole;

[RegisterComponent]
public sealed partial class SyndicateDropConsoleComponent : Component
{
    [ViewVariables]
    public bool Operational;

    [ViewVariables]
    public EntityUid Dispatcher;
}
