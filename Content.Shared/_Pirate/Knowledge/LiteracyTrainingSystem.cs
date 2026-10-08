// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._Pirate.Knowledge;

[ByRefEvent]
public readonly record struct PaperWrittenEvent(EntityUid Paper);

[RegisterComponent]
public sealed partial class LiteracyTrainedComponent : Component
{
    [ViewVariables]
    public HashSet<EntityUid> Trainees = new();
}

public sealed class LiteracyTrainingSystem : EntitySystem
{
    [Dependency] private readonly SharedKnowledgeSystem _knowledge = default!;

    public const int ReadingExperience = 5;

    public const int WritingExperience = 5;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KnowledgeHolderComponent, PaperWrittenEvent>(OnPaperWritten);
    }

    private void OnPaperWritten(Entity<KnowledgeHolderComponent> ent, ref PaperWrittenEvent args)
    {
        TrainOnce(ent.Owner, args.Paper, WritingExperience);
    }

    // Award each holder once per source so others can still train from it.
    public void TrainOnce(EntityUid holder, EntityUid source, int experience)
    {
        if (!EnsureComp<LiteracyTrainedComponent>(source).Trainees.Add(holder))
            return;

        Train(holder, experience);
    }

    public void Train(EntityUid holder, int experience)
    {
        if (_knowledge.GetContainer(holder) is not { } store)
            return;

        _knowledge.AddExperience(store, SharedKnowledgeSystem.LiteracyKnowledge, experience);
    }
}
