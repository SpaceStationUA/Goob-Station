// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Paper;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PaperComponent : Component
{
    public PaperAction Mode;
    [DataField("content"), AutoNetworkedField]
    public string Content { get; set; } = "";

    [DataField("contentSize")]
    public int ContentSize { get; set; } = 10000;

    [DataField("stampedBy"), AutoNetworkedField]
    public List<StampDisplayInfo> StampedBy { get; set; } = new();

    /// <summary>
    ///     Stamp to be displayed on the paper, state from bureaucracy.rsi
    /// </summary>
    [DataField("stampState"), AutoNetworkedField]
    public string? StampState { get; set; }

    [DataField, AutoNetworkedField]
    public bool EditingDisabled;

    /// <summary>
    /// Pirate: stamped/signed documents are final, but a stamp-ignoring pen (cyberpen)
    /// may still edit them. Set while the open write session was started with such a pen
    /// so later save attempts (page flips, direct messages) are checked against it.
    /// Deliberately neither a data field nor networked: the client knows its own pen.
    /// </summary>
    public bool WriteSessionIgnoresStamps;

    /// <summary>
    /// Sound played after writing to the paper.
    /// </summary>
    [DataField("sound")]
    public SoundSpecifier? Sound { get; private set; } = new SoundCollectionSpecifier("PaperScribbles", AudioParams.Default.WithVariation(0.1f));

    [Serializable, NetSerializable]
    public sealed class PaperBoundUserInterfaceState : BoundUserInterfaceState
    {
        public readonly string Text;
        public readonly List<StampDisplayInfo> StampedBy;
        public readonly PaperAction Mode;

        // Pirate: persistent diary pages - leaf on screen and total leaf count.
        // PageCount is 0 for plain (unpaged) paper.
        public readonly int CurrentPage;
        public readonly int PageCount;

        public PaperBoundUserInterfaceState(
            string text,
            List<StampDisplayInfo> stampedBy,
            PaperAction mode = PaperAction.Read,
            int currentPage = 0,
            int pageCount = 0)
        {
            Text = text;
            StampedBy = stampedBy;
            Mode = mode;
            CurrentPage = currentPage;
            PageCount = pageCount;
        }
    }

    [Serializable, NetSerializable]
    public sealed class PaperInputTextMessage : BoundUserInterfaceMessage
    {
        public readonly string Text;

        /// <summary>
        /// Pirate: the leaf this text was typed on (0 for plain paper). pages.CurrentPage
        /// is shared by every viewer, so the server aims the write at this leaf instead.
        /// </summary>
        public readonly int Page;

        public PaperInputTextMessage(string text, int page = 0)
        {
            Text = text;
            Page = page;
        }
    }

    #region Pirate: paperwork tags
    [Serializable, NetSerializable]
    public sealed class PaperMacroMenuUsedMessage : BoundUserInterfaceMessage
    {
        public readonly PaperAction Action;

        public PaperMacroMenuUsedMessage(PaperAction action)
        {
            Action = action;
        }
    }
    #endregion

    #region Pirate: persistent diary pages
    [Serializable, NetSerializable]
    public sealed class PaperPageActionMessage : BoundUserInterfaceMessage
    {
        public readonly PaperPageAction Action;
        public readonly int Page;

        /// <summary>
        /// Text still being typed on the leaf being left behind. Sent together with
        /// Turn/Add so flipping pages never drops an unsaved edit; null otherwise.
        /// </summary>
        public readonly string? Text;

        /// <summary>
        /// Pirate: the leaf <see cref="Text"/> was typed on, so a concurrent flip of the
        /// shared current page by another viewer cannot reroute the write.
        /// </summary>
        public readonly int TextPage;

        public PaperPageActionMessage(PaperPageAction action, int page = 0, string? text = null, int textPage = 0)
        {
            Action = action;
            Page = page;
            Text = text;
            TextPage = textPage;
        }
    }

    [Serializable, NetSerializable]
    public enum PaperPageAction : byte
    {
        /// <summary>Flip to another leaf.</summary>
        Turn,

        /// <summary>Insert a fresh leaf after the current one.</summary>
        Add,

        /// <summary>Tear out the current leaf.</summary>
        Remove,
    }
    #endregion

    // Starlight-start
    [Serializable, NetSerializable]
    public sealed class PaperSignatureRequestMessage : BoundUserInterfaceMessage
    {
        public readonly int SignatureIndex;

        public PaperSignatureRequestMessage(int signatureIndex)
        {
            SignatureIndex = signatureIndex;
        }
    }
    // Starlight-end
    [Serializable, NetSerializable]
    public enum PaperUiKey
    {
        Key
    }

    [Serializable, NetSerializable]
    public enum PaperAction
    {
        Read,
        Write,
    }

    [Serializable, NetSerializable]
    public enum PaperVisuals : byte
    {
        Status,
        Stamp
    }

    [Serializable, NetSerializable]
    public enum PaperStatus : byte
    {
        Blank,
        Written
    }
}
