namespace Content.Shared.Stunnable;

public abstract partial class SharedStunSystem
{
    private static readonly TimeSpan StandupRetryCooldown = TimeSpan.FromMilliseconds(250);
}
