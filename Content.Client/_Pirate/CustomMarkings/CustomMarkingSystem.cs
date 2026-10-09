// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Shared._Pirate.CustomMarkings;
using Content.Shared.Humanoid;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Configuration;

namespace Content.Client._Pirate.CustomMarkings;

/// <summary>
/// Draws the custom markings a humanoid wears. Art is fetched from the server once per hash and kept as an RSI in
/// <see cref="CustomMarkingResources"/>; bodies whose art hasn't arrived yet get it when it does.
/// </summary>
public sealed partial class CustomMarkingSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceCache _resCache = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    /// <summary>Layer keys are numbered, so this is also the most markings one body can show.</summary>
    private const int MaxLayers = CustomMarkingRules.MaxWornCap;

    /// <summary>The layers hair, frills and ears are drawn on.</summary>
    private static readonly HumanoidVisualLayers[] HeadLayers =
    {
        HumanoidVisualLayers.FacialHair,
        HumanoidVisualLayers.Hair,
        HumanoidVisualLayers.HeadSide,
        HumanoidVisualLayers.HeadTop,
    };

    /// <summary>What one species or another draws next, over those.</summary>
    private static readonly HumanoidVisualLayers[] OverHeadLayers =
    {
        HumanoidVisualLayers.Wings,
        HumanoidVisualLayers.TailOversuit,
        HumanoidVisualLayers.Tail,
    };

    private static readonly string[] OverHeadSlots = { "suitstorage", "balaclava", "maskalt", "mask", "head", "helmetcover" };

    private CustomMarkingResources _resources = default!;

    private readonly Dictionary<string, RSI> _art = new();

    /// <summary>Hashes the server has no art for, so they aren't asked for again.</summary>
    private readonly HashSet<string> _unavailable = new();

    /// <summary>Hashes asked for and not yet answered.</summary>
    private readonly HashSet<string> _requested = new();

    private readonly List<string> _toRequest = new();

    /// <summary>Raised when the art for a hash becomes drawable.</summary>
    public event Action<string>? ArtLoaded;

    public override void Initialize()
    {
        base.Initialize();

        _resources = CustomMarkingResources.For(_resCache);

        SubscribeNetworkEvent<CustomMarkingArtEvent>(OnArt);
        SubscribeLocalEvent<HumanoidAppearanceComponent, HumanoidMarkingsAppliedEvent>(OnMarkingsApplied);
        Subs.CVar(_cfg, CustomMarkingCVars.EraseBody, _ => RefreshAll());

        InitializeLibrary();
    }

    /// <summary>The art for a hash if it is here already. Otherwise asks the server for it, once.</summary>
    public bool TryGetArt(string hash, [NotNullWhen(true)] out RSI? rsi)
    {
        if (_art.TryGetValue(hash, out rsi))
            return true;

        if (!CustomMarkingRules.IsValidHash(hash) || _unavailable.Contains(hash))
            return false;

        // Fetched before a reconnect: the root outlives this system.
        if (_resources.Has(hash))
            return Load(hash, out rsi);

        if (_requested.Add(hash))
            _toRequest.Add(hash);

        return false;
    }

    /// <summary>
    /// Keeps art the player just saved under the hash the server gave it, so it shows without a trip to fetch it.
    /// </summary>
    public void Remember(string hash, CustomMarkingArt art)
    {
        if (!CustomMarkingRules.IsValidHash(hash) || _art.ContainsKey(hash))
            return;

        _unavailable.Remove(hash);
        Store(hash, art.ToPng(), art.GetFrameTimes(), art.Erase);
        if (Load(hash, out _))
            ArtLoaded?.Invoke(hash);
    }

    /// <summary>The PNG sheet for a hash that <see cref="TryGetArt"/> has returned.</summary>
    public bool TryGetPng(string hash, [NotNullWhen(true)] out byte[]? png)
    {
        return _resources.TryGetPng(hash, out png);
    }

    /// <summary>
    /// The art for a hash that <see cref="TryGetArt"/> has returned, read back whole for the editor: its frames,
    /// how long each shows and its erase mask.
    /// </summary>
    public bool TryReadArt(string hash, [NotNullWhen(true)] out CustomMarkingArt? art)
    {
        art = null;
        if (!_resources.TryGetPng(hash, out var png) || CustomMarkingPng.Read(png) is not { } read)
            return false;

        if (_resources.GetFrameTimes(hash) is { } times && times.Length == read.Frames)
        {
            for (var frame = 0; frame < times.Length; frame++)
            {
                read.SetFrameTime(frame, times[frame]);
            }
        }

        if (_resources.TryGetErase(hash, out var erase))
            erase.CopyTo(read.Erase, 0);

        art = read;
        return true;
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        while (_toRequest.Count > 0)
        {
            var count = Math.Min(_toRequest.Count, CustomMarkingRules.MaxRequestedArt);
            RaiseNetworkEvent(new CustomMarkingArtRequestEvent(_toRequest.GetRange(0, count)));
            _toRequest.RemoveRange(0, count);
        }
    }

    private void OnArt(CustomMarkingArtEvent ev)
    {
        if (!CustomMarkingRules.IsValidHash(ev.Hash))
            return;

        var asked = _requested.Remove(ev.Hash);
        if (ev.Png == null)
        {
            // Also sent unasked when an admin blocks art, so anything already fetched is dropped.
            _unavailable.Add(ev.Hash);
            _resources.Remove(ev.Hash);
            _eraseMasks.Remove(ev.Hash);
            _decoded.Remove(ev.Hash);
            if (_art.Remove(ev.Hash))
                RefreshWearers(ev.Hash);

            return;
        }

        if (!asked)
            return;

        Store(ev.Hash, ev.Png, ev.FrameTimes, ev.Erase);
        if (!Load(ev.Hash, out _))
            return;

        RefreshWearers(ev.Hash);
        ArtLoaded?.Invoke(ev.Hash);
    }

    private void Store(string hash, byte[] png, int[]? frameTimes, byte[]? erase)
    {
        _eraseMasks.Remove(hash);
        _resources.Store(hash, png, frameTimes, erase);
    }

    private void RefreshWearers(string hash)
    {
        var query = EntityQueryEnumerator<HumanoidAppearanceComponent>();
        while (query.MoveNext(out var uid, out var humanoid))
        {
            foreach (var marking in humanoid.CustomMarkings)
            {
                if (marking.Hash != hash)
                    continue;

                Refresh((uid, humanoid));
                break;
            }
        }
    }

    private void RefreshAll()
    {
        var query = EntityQueryEnumerator<HumanoidAppearanceComponent>();
        while (query.MoveNext(out var uid, out var humanoid))
        {
            if (humanoid.CustomMarkings.Count > 0)
                Refresh((uid, humanoid));
        }
    }

    private bool Load(string hash, [NotNullWhen(true)] out RSI? rsi)
    {
        try
        {
            rsi = _resCache.GetResource<RSIResource>(CustomMarkingResources.PathFor(hash), false).RSI;
        }
        catch (Exception e)
        {
            Log.Error($"Custom marking art {hash} didn't load: {e.Message}");
            _unavailable.Add(hash);
            rsi = null;
            return false;
        }

        _art[hash] = rsi;
        return true;
    }

    private void OnMarkingsApplied(Entity<HumanoidAppearanceComponent> ent, ref HumanoidMarkingsAppliedEvent args)
    {
        Refresh(ent);
    }

    /// <summary>Rebuilds a body's custom marking layers from the list on its humanoid component.</summary>
    public void Refresh(Entity<HumanoidAppearanceComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var spriteComp))
            return;

        var sprite = new Entity<SpriteComponent?>(ent, spriteComp);
        RemoveErase(sprite);
        for (var i = 0; i < MaxLayers; i++)
        {
            _sprite.RemoveLayer(sprite, LayerKey(i), false);
        }

        var erase = new byte[CustomMarkingRules.EraseBytes];
        var covered = new byte[CustomMarkingRules.EraseBytes];
        var worn = ent.Comp.CustomMarkings;
        for (var i = 0; i < worn.Count && i < MaxLayers; i++)
        {
            var marking = worn[i];
            if (!TryGetArt(marking.Hash, out var rsi))
                continue;

            // Art on the body goes without the pixels that lay on a hidden body part.
            var depth = GetLayerIndex(sprite, marking.Placement);
            if (ArtFor(sprite, marking, rsi, depth) is not { } shown)
                continue;

            if (TryGetErase(marking.Hash, out var mask))
                CustomMarkingErase.Add(erase, mask);

            if (shown.Rsi == null)
                continue;

            // Clothing that hides part of the body hides the art on that part, which ArtFor has cut out already;
            // the rest of the marking stays, under or over the clothing as its placement has it. Art on the hair
            // is the exception: it is there to change the hair, so all of it goes whenever the hair is hidden.
            var layer = _sprite.AddRsiLayer(sprite, CustomMarkingResources.State, shown.Rsi, depth);
            _sprite.LayerMapSet(sprite, LayerKey(i), layer);

            var visible = marking.Placement != CustomMarkingPlacement.Hair
                          || !ent.Comp.HiddenLayers.ContainsKey(HumanoidVisualLayers.Hair)
                          && !ent.Comp.PermanentlyHidden.Contains(HumanoidVisualLayers.Hair);
            _sprite.LayerSetVisible(sprite, layer, visible);
            if (visible)
                CustomMarkingErase.Add(covered, shown.Solid);
        }

        ApplyErase(sprite, erase, covered);
    }

    /// <summary>The key of the layer drawing a body's custom marking at this position in its list.</summary>
    public static string LayerKey(int index)
    {
        return $"wf-custom-marking-{index}";
    }

    /// <summary>
    /// Where a placement's layer goes: just under the first layer that draws over it, which puts it above the
    /// body part's own markings and above custom markings added before it. Null, for the top, when the sprite has
    /// no such layer.
    /// </summary>
    public int? GetLayerIndex(Entity<SpriteComponent?> sprite, CustomMarkingPlacement placement)
    {
        int index;
        switch (placement)
        {
            case CustomMarkingPlacement.Behind:
                return _sprite.LayerMapTryGet(sprite, HumanoidVisualLayers.Chest, out index, false) ? index : 0;
            case CustomMarkingPlacement.Skin:
                // Pirate: our underwear has two layers instead of Wolfgate's Genital layer.
                int? underClothes = null;
                foreach (var key in new object[] { HumanoidVisualLayers.UndergarmentTop, HumanoidVisualLayers.UndergarmentBottom, "jumpsuit" })
                {
                    if (TryGetLayer(sprite, key, out index) && (underClothes == null || index < underClothes))
                        underClothes = index;
                }

                return underClothes;
            case CustomMarkingPlacement.Hands:
                if (_sprite.LayerMapTryGet(sprite, "gloves", out index, false))
                    return index;

                break;
            case CustomMarkingPlacement.Hair:
                return GetIndexOverHead(sprite);
            case CustomMarkingPlacement.Front:
                if (_sprite.LayerMapTryGet(sprite, "helmetcover", out index, false))
                    return index;

                break;
        }

        return null;
    }

    /// <summary>
    /// Where art on the hair goes: over the hair, the frills and the ears with their markings, so those can be
    /// drawn on, and under masks and hats. Species order what follows differently, so that is under the lowest
    /// layer above them. Null, for the top, when the sprite has no such layers.
    /// </summary>
    private int? GetIndexOverHead(Entity<SpriteComponent?> sprite)
    {
        var top = -1;
        foreach (var layer in HeadLayers)
        {
            if (_sprite.LayerMapTryGet(sprite, layer, out var index, false))
                top = Math.Max(top, index);
        }

        if (top < 0)
            return null;

        int? over = null;
        foreach (var layer in OverHeadLayers)
        {
            if (_sprite.LayerMapTryGet(sprite, layer, out var index, false) && index > top && (over == null || index < over))
                over = index;
        }

        foreach (var slot in OverHeadSlots)
        {
            if (_sprite.LayerMapTryGet(sprite, slot, out var index, false) && index > top && (over == null || index < over))
                over = index;
        }

        return over;
    }
}
