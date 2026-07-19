// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Gravity;
using Content.Shared._Pirate.ZLevels.Core.Components; // Pirate: multiz
using JetBrains.Annotations;
using Robust.Shared.Map.Components;

namespace Content.Server.Gravity
{
    [UsedImplicitly]
    public sealed class GravitySystem : SharedGravitySystem
    {
        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<GravityComponent, ComponentInit>(OnGravityInit);
        }

        /// <summary>
        /// Iterates gravity components and checks if this entity can have gravity applied.
        /// </summary>
        public void RefreshGravity(EntityUid uid, GravityComponent? gravity = null)
        {
            if (!GravityQuery.Resolve(uid, ref gravity))
                return;

            if (gravity.Inherent && !TryComp<CEZLinkedGridComponent>(uid, out _)) // Pirate: multiz
                return;

            var targets = GetGravityTargets(uid); // Pirate: multiz
            var enabled = LinkedTargetsHaveInherentGravity(targets) || LinkedTargetsHaveActiveGravityGenerator(targets); // Pirate: multiz
            ApplyGravityState(targets, enabled); // Pirate: multiz
        }

        private void OnGravityInit(EntityUid uid, GravityComponent component, ComponentInit args)
        {
            RefreshGravity(uid);
        }

        /// <summary>
        /// Enables gravity. Note that this is a fast-path for GravityGeneratorSystem.
        /// This means it does nothing if Inherent is set and it might be wiped away with a refresh
        ///  if you're not supposed to be doing whatever you're doing.
        /// </summary>
        public void EnableGravity(EntityUid uid, GravityComponent? gravity = null)
        {
            if (!GravityQuery.Resolve(uid, ref gravity))
                return;

            if (TryComp<CEZLinkedGridComponent>(uid, out _)) // Pirate: multiz
            {
                RefreshGravity(uid, gravity); // Pirate: multiz
                return;
            }

            if (gravity.Enabled || gravity.Inherent)
                return;

            gravity.Enabled = true;
            var ev = new GravityChangedEvent(uid, true);
            RaiseLocalEvent(uid, ref ev, true);
            Dirty(uid, gravity);

            if (HasComp<MapGridComponent>(uid))
            {
                StartGridShake(uid);
            }
        }

        #region Pirate: multiz
        private List<EntityUid> GetGravityTargets(EntityUid uid)
        {
            var targets = new List<EntityUid> { uid };

            if (!TryComp<CEZLinkedGridComponent>(uid, out var linked))
                return targets;

            foreach (var (_, peerUid) in linked.PeerGrids)
            {
                if (!targets.Contains(peerUid))
                    targets.Add(peerUid);
            }

            return targets;
        }

        private bool LinkedTargetsHaveInherentGravity(List<EntityUid> targets)
        {
            foreach (var targetUid in targets)
            {
                if (TryComp<GravityComponent>(targetUid, out var gravity) && gravity.Inherent)
                    return true;
            }

            return false;
        }

        private bool LinkedTargetsHaveActiveGravityGenerator(List<EntityUid> targets)
        {
            var query = EntityQueryEnumerator<GravityGeneratorComponent, TransformComponent>();
            while (query.MoveNext(out _, out var comp, out var xform))
            {
                if (!comp.GravityActive)
                    continue;

                if (targets.Contains(xform.ParentUid))
                    return true;
            }

            return false;
        }

        private void ApplyGravityState(List<EntityUid> targets, bool enabled)
        {
            foreach (var targetUid in targets)
            {
                if (!TryComp<GravityComponent>(targetUid, out var targetGravity) ||
                    targetGravity.Inherent ||
                    targetGravity.Enabled == enabled)
                {
                    continue;
                }

                targetGravity.Enabled = enabled;
                var ev = new GravityChangedEvent(targetUid, enabled);
                RaiseLocalEvent(targetUid, ref ev, true);
                Dirty(targetUid, targetGravity);

                if (enabled && HasComp<MapGridComponent>(targetUid))
                    StartGridShake(targetUid);
            }
        }
        #endregion
    }
}