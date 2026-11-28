// SPDX-FileCopyrightText: Contributors to Impstation
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Client._Pirate.CrystalMass
{
    [RegisterComponent]
    public sealed partial class CrystalMassVisualsComponent : Component
    {
        [DataField("layer")]
        public int Layer { get; private set; } = 0;
    }

}
