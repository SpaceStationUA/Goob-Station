using Robust.Shared.Serialization;
using Robust.Shared.Configuration;
using Content.Shared._Pirate.CCVars;
using Content.Shared.GameTicking;

namespace Content.Shared.Psionics.Glimmer
{
    /// <summary>
    /// This handles setting / reading the value of glimmer.
    /// </summary>
    public sealed class GlimmerSystem : EntitySystem
    {
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        private int _glimmer = 0;
        public int Glimmer
        {
            get { return _glimmer; }
            set => SetGlimmer(value, "unspecified");
        }

        /// <summary>
        /// Sets glimmer and records why it changed. Prefer this over assigning <see cref="Glimmer"/> directly,
        /// since the reason is what makes glimmer changes traceable in the admin logs.
        /// </summary>
        /// <param name="value">The new glimmer value.</param>
        /// <param name="reason">Human-readable cause of the change.</param>
        /// <param name="source">Optional entity that caused the change.</param>
        /// <param name="changeType">Whether this is a player-driven or ambient change.</param>
        public void SetGlimmer(int value, string reason, EntityUid? source = null,
            GlimmerChangeType changeType = GlimmerChangeType.Active)
        {
            var oldGlimmer = _glimmer;
            var newGlimmer = _enabled ? Math.Clamp(value, 0, MaxGlimmer) : 0;
            if (oldGlimmer == newGlimmer)
                return;

            _glimmer = newGlimmer;
            RaiseNetworkEvent(new GlimmerChangedEvent(_glimmer));
            RaiseLocalEvent(new GlimmerChangedLogEvent(oldGlimmer, newGlimmer, reason, source, changeType));
        }

        /// <summary>
        /// Adjusts glimmer by a delta and records why it changed.
        /// </summary>
        /// <param name="delta">How much to add (negative values subtract).</param>
        /// <param name="reason">Human-readable cause of the change.</param>
        /// <param name="source">Optional entity that caused the change.</param>
        /// <param name="changeType">Whether this is a player-driven or ambient change.</param>
        public void AdjustGlimmer(int delta, string reason, EntityUid? source = null,
            GlimmerChangeType changeType = GlimmerChangeType.Active)
        {
            SetGlimmer(_glimmer + delta, reason, source, changeType);
        }

        /// <summary>
        /// Pirate - The maximum possible glimmer value, used to normalize the current glimmer.
        /// </summary>
        public int MaxGlimmer => 1000;
        private bool _enabled;
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<RoundRestartCleanupEvent>(Reset);
            _enabled = _cfg.GetCVar(PirateVars.GlimmerEnabled);
            _cfg.OnValueChanged(PirateVars.GlimmerEnabled, value => _enabled = value, true);

            // Pirate - Keep the client-side glimmer value in sync so client prediction (e.g. xenoarch glimmer display) is accurate.
            // The server never receives its own RaiseNetworkEvent, so this only affects the client.
            SubscribeNetworkEvent<GlimmerChangedEvent>(ev => _glimmer = ev.Glimmer);
        }

        private void Reset(RoundRestartCleanupEvent args)
        {
            SetGlimmer(0, "round restart", changeType: GlimmerChangeType.Passive);
        }

        /// <summary>
        /// Return an abstracted range of a glimmer count.
        /// </summary>
        /// <param name="glimmer">What glimmer count to check. Uses the current glimmer by default.</param>
        public GlimmerTier GetGlimmerTier(int? glimmer = null)
        {
            if (glimmer == null)
                glimmer = Glimmer;

            return (glimmer) switch
            {
                <= 49 => GlimmerTier.Minimal,
                >= 50 and <= 99 => GlimmerTier.Low,
                >= 100 and <= 299 => GlimmerTier.Moderate,
                >= 300 and <= 499 => GlimmerTier.High,
                >= 500 and <= 899 => GlimmerTier.Dangerous,
                _ => GlimmerTier.Critical,
            };
        }
    }

    [Serializable, NetSerializable]
    public enum GlimmerTier : byte
    {
        Minimal,
        Low,
        Moderate,
        High,
        Dangerous,
        Critical,
    }

    [Serializable, NetSerializable]
    public sealed class GlimmerChangedEvent : EntityEventArgs
    {
        public int Glimmer { get; }

        public GlimmerChangedEvent(int glimmer)
        {
            Glimmer = glimmer;
        }
    }

    /// <summary>
    /// Whether a glimmer change is player-driven and should be recorded in admin logs,
    /// or ambient and should only be announced in the server debug log.
    /// </summary>
    public enum GlimmerChangeType : byte
    {
        Passive,
        Active,
    }

    /// <summary>
    /// Raised locally whenever glimmer changes so the server can write it to the admin logs.
    /// </summary>
    public sealed class GlimmerChangedLogEvent : EntityEventArgs
    {
        public int OldGlimmer { get; }
        public int NewGlimmer { get; }
        public int Delta => NewGlimmer - OldGlimmer;
        public string Reason { get; }
        public EntityUid? Source { get; }
        public GlimmerChangeType ChangeType { get; }

        public GlimmerChangedLogEvent(int oldGlimmer, int newGlimmer, string reason, EntityUid? source,
            GlimmerChangeType changeType)
        {
            OldGlimmer = oldGlimmer;
            NewGlimmer = newGlimmer;
            Reason = reason;
            Source = source;
            ChangeType = changeType;
        }
    }
}
