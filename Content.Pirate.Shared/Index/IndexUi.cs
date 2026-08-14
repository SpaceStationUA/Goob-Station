// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Pirate.Shared.Index;

/// <summary>UI key for the pager window (opened by the member holding/using their pager).</summary>
[Serializable, NetSerializable]
public enum IndexPagerUiKey : byte
{
    Key,
}

/// <summary>UI key for the admin Index menu (opened by admins via a verb on the pager / member).</summary>
[Serializable, NetSerializable]
public enum IndexAdminUiKey : byte
{
    Key,
}

/// <summary>State pushed to the pager window.</summary>
[Serializable, NetSerializable]
public sealed class IndexPagerBoundUserInterfaceState : BoundUserInterfaceState
{
    /// <summary>KARMIC CONSEQUENCE of the bound member.</summary>
    public int KarmicConsequence;

    /// <summary>The latest prescription received (only the last one is kept).</summary>
    public List<string> Prescriptions;

    /// <summary>Name of the bound member (empty when unbound).</summary>
    public string MemberName;

    /// <summary>Whether this pager has been claimed.</summary>
    public bool Bound;

    public IndexPagerBoundUserInterfaceState(int karmicConsequence, List<string> prescriptions, string memberName, bool bound)
    {
        KarmicConsequence = karmicConsequence;
        Prescriptions = prescriptions;
        MemberName = memberName;
        Bound = bound;
    }
}

/// <summary>State pushed to the admin Index menu.</summary>
[Serializable, NetSerializable]
public sealed class IndexAdminBoundUserInterfaceState : BoundUserInterfaceState
{
    /// <summary>KARMIC CONSEQUENCE of the targeted member.</summary>
    public int KarmicConsequence;

    /// <summary>Name of the targeted member.</summary>
    public string MemberName;

    /// <summary>Whether the member exists (pager might not be claimed yet).</summary>
    public bool HasMember;

    /// <summary>Whether the next Caduceus transformation is guaranteed to be a fpoon.</summary>
    public bool NextWeaponFpoon;

    public IndexAdminBoundUserInterfaceState(int karmicConsequence, string memberName, bool hasMember, bool nextWeaponFpoon)
    {
        KarmicConsequence = karmicConsequence;
        MemberName = memberName;
        HasMember = hasMember;
        NextWeaponFpoon = nextWeaponFpoon;
    }
}

/// <summary>Admin: add KARMIC CONSEQUENCE to the targeted member.</summary>
[Serializable, NetSerializable]
public sealed class IndexAdminAddKarmaMessage : BoundUserInterfaceMessage
{
    public int Amount;

    public IndexAdminAddKarmaMessage(int amount)
    {
        Amount = amount;
    }
}

/// <summary>Admin: remove KARMIC CONSEQUENCE from the targeted member (clamped at 0).</summary>
[Serializable, NetSerializable]
public sealed class IndexAdminRemoveKarmaMessage : BoundUserInterfaceMessage
{
    public int Amount;

    public IndexAdminRemoveKarmaMessage(int amount)
    {
        Amount = amount;
    }
}

/// <summary>Admin: send a prescription (message) to the targeted member's pager.</summary>
[Serializable, NetSerializable]
public sealed class IndexAdminSendPrescriptionMessage : BoundUserInterfaceMessage
{
    public string Text = string.Empty;

    public IndexAdminSendPrescriptionMessage(string text)
    {
        Text = text;
    }
}

/// <summary>Admin: toggle the guarantee that the member's next Caduceus form is a fpoon.</summary>
[Serializable, NetSerializable]
public sealed class IndexAdminGuaranteeFpoonMessage : BoundUserInterfaceMessage
{
    public bool Enabled;

    public IndexAdminGuaranteeFpoonMessage(bool enabled)
    {
        Enabled = enabled;
    }
}

/// <summary>Appearance key for the prescription-received animation on the pager item.</summary>
[Serializable, NetSerializable]
public enum IndexPagerVisuals : byte
{
    Receiving,
}

/// <summary>Sprite layer map keys used by the pager.</summary>
[Serializable, NetSerializable]
public enum IndexPagerVisualLayers : byte
{
    Icon,
}
