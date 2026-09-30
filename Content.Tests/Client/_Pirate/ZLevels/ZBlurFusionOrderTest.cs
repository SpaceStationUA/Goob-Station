// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using Content.Client.Atmos.Overlays;
using Content.Client._Pirate.PostProcess;
using Content.Client._Pirate.ZLevels.Core;
using Content.Client.Eye.Blinding;
using Content.Client.Flash;
using Content.Client.Overlays;
using Content.Client.UserInterface.Systems.DamageOverlays.Overlays;
using NUnit.Framework;
using Robust.Shared.Enums;

namespace Content.Tests.Client._Pirate.ZLevels;

[TestFixture]
public sealed class ZBlurFusionOrderTest
{
    [Test]
    public void InactiveAtmosCanBeSkippedButActiveAtmosAndOtherBlockersCannot()
    {
        var order = new ZBlurFusionOrder();
        order.Add(typeof(CEZLevelBlurOverlay), OverlaySpace.WorldSpace);
        Assert.That(order.Add(typeof(AtmosDebugOverlay), OverlaySpace.WorldSpace, knownInactive: true), Is.False);
        Assert.That(order.CanDefer, Is.True);
        Assert.That(order.Add(typeof(AtmosDebugOverlay), OverlaySpace.WorldSpace), Is.True);
        Assert.That(order.Add(typeof(NoirOverlay), OverlaySpace.WorldSpace), Is.True,
            "Report every blocker, even after the first one.");
        Assert.That(order.CanDefer, Is.False);
        Assert.That(order.Blocker, Is.EqualTo(typeof(AtmosDebugOverlay)));
    }

    [Test]
    public void OnlyKnownPlayerEyeEffectsMayFollowDeferredBlur()
    {
        var order = new ZBlurFusionOrder();
        order.Add(typeof(CEZLevelBlurOverlay), OverlaySpace.WorldSpace);
        foreach (var type in new[] { typeof(BlindOverlay), typeof(BlurryVisionOverlay), typeof(FlashOverlay),
                     typeof(DamageOverlay), typeof(CEPostProcessOverlay) })
            order.Add(type, OverlaySpace.WorldSpace);
        Assert.That(order.CanDefer, Is.True);
        order.Add(typeof(NoirOverlay), OverlaySpace.WorldSpace);
        Assert.That(order.CanDefer, Is.False, "Noir must still see the already-blurred deck.");
        Assert.That(order.Blocker, Is.EqualTo(typeof(NoirOverlay)));
    }

    [Test]
    public void EarlierEffectsAndOtherRenderStagesDoNotBlockFusion()
    {
        var order = new ZBlurFusionOrder();
        order.Add(typeof(NoirOverlay), OverlaySpace.WorldSpace);
        order.Add(typeof(CEZLevelBlurOverlay), OverlaySpace.WorldSpace);
        order.Add(typeof(object), OverlaySpace.WorldSpaceEntities | OverlaySpace.ScreenSpace);
        Assert.That(order.CanDefer, Is.True);
    }

    [Test]
    public void UnknownWorldEffectsIncludingMixedSpacesBlockFusion()
    {
        var order = new ZBlurFusionOrder();
        order.Add(typeof(CEZLevelBlurOverlay), OverlaySpace.WorldSpace);
        order.Add(typeof(object), OverlaySpace.WorldSpace | OverlaySpace.ScreenSpace);
        order.Add(typeof(CEPostProcessOverlay), OverlaySpace.WorldSpace);
        Assert.That(order.CanDefer, Is.False);
        Assert.That(order.Blocker, Is.EqualTo(typeof(object)));
    }

    [Test]
    public void MissingBlurCannotBeDeferred()
    {
        var order = new ZBlurFusionOrder();
        order.Add(typeof(CEPostProcessOverlay), OverlaySpace.WorldSpace);
        Assert.That(order.CanDefer, Is.False);
    }
}
