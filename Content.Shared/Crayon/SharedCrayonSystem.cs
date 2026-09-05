// SPDX-License-Identifier: MIT

namespace Content.Shared.Crayon;

[Virtual]
public abstract class SharedCrayonSystem : EntitySystem
{
    #region Pirate: light paint glyph color
    public void SetColor(Entity<CrayonComponent> ent, Color color)
    {
        if (ent.Comp.Color == color)
            return;

        ent.Comp.Color = color;
        Dirty(ent);
    }
    #endregion Pirate: light paint glyph color
}
