// SPDX-FileCopyrightText: 2025 CrateTheDragon <drago.felcon@gmail.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.ContentPack;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Utility;


namespace Content.Client._Pirate.Changelog
{
    public sealed partial class PirateChangelogManager
    {
        private readonly IResourceManager _resource = default!;
        private readonly ISerializationManager _serilization = default!;

        public PirateChangelogManager(IResourceManager resource, ISerializationManager serialization)
        {
            _resource = resource;
            _serilization = serialization;
        }

        public List<PirateChangelog> Changelogs { get; private set; } = new();

        public void Load()
        {
            Changelogs.Clear();

            var file = new ResPath("/Changelog/pirate.yml");

            if (_resource.ContentFileExists(file))
            {
                var yamlData = _resource.ContentFileReadYaml(file);

                if (yamlData.Documents.Count > 0)
                {
                    var node = yamlData.Documents[0].RootNode.ToDataNodeCast<MappingDataNode>();
                    var log = _serilization.Read<PirateChangelog>(node, notNullableOverride: true);

                    if (string.IsNullOrWhiteSpace(log.Name))
                    {
                        log.Name = file.FilenameWithoutExtension;
                    }

                    Changelogs.Add(log);
                }
            }

            Changelogs.Sort((a, b) => a.Order.CompareTo(b.Order));
        }
    }
    
    [DataDefinition]
    public sealed partial class PirateChangelog
    {
        [DataField("Name")]
        public string Name = string.Empty;
        [DataField("Entries")]
        public List<PirateChangelogEntry> Entries = new();
        [DataField("AdminOnly")]
        public bool AdminOnly;
        [DataField("Order")]
        public int Order;
    }

    [DataDefinition]
    public sealed partial class PirateChangelogEntry
    {
        [DataField("id")]
        public int Id { get; private set; }
        [DataField("author")]
        public string Author { get; private set; } = "";
        [DataField]
        public DateTime Time { get; private set; }
        [DataField("changes")]
        public List<PirateChangelogChange> Changes { get; private set; } = default!;
    }

    [DataDefinition]
    public sealed partial class PirateChangelogChange
    {
        [DataField("type")]
        public PirateChangelogLineType Type { get; private set; }
        [DataField("message")]
        public string Message { get; private set; } = "";
    }
    public enum PirateChangelogLineType
    {
        Add,
        Remove,
        Fix,
        Tweak,
    }
}