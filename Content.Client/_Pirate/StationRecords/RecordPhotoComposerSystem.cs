// SPDX-License-Identifier: AGPL-3.0-or-later

using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Content.Client.Inventory;
using Content.Shared._Pirate;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Shared;
using Robust.Shared.Configuration;
using Robust.Shared.Maths;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client._Pirate.StationRecords;

public enum RecordPhotoStyle : byte
{
    Security,

    Civilian,

    Psionics,
}

public readonly record struct MugshotLabel(string Name, int Number, int BirthYear);

public sealed class RecordPhotoComposerSystem : EntitySystem
{
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly ClientInventorySystem _inventory = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public const int PhotoSize = 240;

    private static readonly string[] PhotoHiddenSlots = ["head", "mask", "eyes"];
    private const int CharacterScale = 6;

    private static readonly ResPath BackgroundRsi = new("/Textures/_Pirate/Interface/record_photos.rsi");
    private static readonly ResPath PlacardFont = new("/Fonts/unifont-17.0.03.otf");

    private readonly ComposerControl _control = new();
    private Font? _font;

    public override void Initialize()
    {
        base.Initialize();
        _ui.RootControl.AddChild(_control);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        while (_control.Queue.TryDequeue(out var job))
        {
            job.Target.Dispose();
            job.Cleanup?.Invoke();
            job.Result.TrySetCanceled();
        }

        _ui.RootControl.RemoveChild(_control);
    }

    public Texture GetBackground(RecordPhotoStyle style)
    {
        return _sprite.Frame0(new SpriteSpecifier.Rsi(BackgroundRsi, StateFor(style)));
    }

    public Task<byte[]?> Compose(byte[] renderPng, RecordPhotoStyle style, MugshotLabel label)
    {
        Texture character;
        try
        {
            using var stream = new MemoryStream(renderPng, writable: false);
            character = Texture.LoadFromPNGStream(stream, "record-mugshot-character");
        }
        catch (Exception)
        {
            return Task.FromResult<byte[]?>(null);
        }

        // Unifont stays crisp at 16 px; 12 pt at 96 DPI produces that size.
        var font = _font ??= new VectorFont(_resourceCache.GetResource<FontResource>(PlacardFont), 12);
        var fontScale = 96f / Math.Max(1, _cfg.GetCVar(CVars.DisplayFontDpi));
        var background = GetBackground(style);

        return Render(new Vector2i(PhotoSize, PhotoSize),
            handle => DrawMugshot(handle, background, character, font, fontScale, label),
            () => DisposeTexture(character));
    }

    public void DressForPhoto(EntityUid dummy)
    {
        foreach (var slot in PhotoHiddenSlots)
        {
            if (_inventory.TryUnequip(dummy, slot, out var removed, silent: true, force: true, reparent: false))
                Del(removed.Value);
        }
    }

    public Task<byte[]?> RenderEntity(EntityUid entity)
    {
        // Render in memory; sprite export writes the PNG to the player's data folder.
        if (!TryComp<SpriteComponent>(entity, out var sprite))
            return Task.FromResult<byte[]?>(null);

        var size = Vector2i.Zero;
        foreach (var layer in sprite.AllLayers)
        {
            if (layer.Visible)
                size = Vector2i.ComponentMax(size, layer.PixelSize);
        }

        if (size == Vector2i.Zero)
            return Task.FromResult<byte[]?>(null);

        return Render(size, handle =>
        {
            if (!Deleted(entity))
                handle.DrawEntity(entity, size / 2, Vector2.One, Angle.Zero, overrideDirection: Direction.South);
        });
    }

    private Task<byte[]?> Render(Vector2i size, Action<DrawingHandleScreen> draw, Action? cleanup = null)
    {
        var target = _clyde.CreateRenderTarget(size,
            new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb), name: "record-photo");
        var result = new TaskCompletionSource<byte[]?>();
        // Render targets are drawn during the UI pass; readback happens after that frame.
        _control.Queue.Enqueue(new RenderJob(target, draw, result, cleanup));
        return result.Task;
    }

    private static string StateFor(RecordPhotoStyle style)
    {
        return style switch
        {
            RecordPhotoStyle.Security => "security",
            RecordPhotoStyle.Psionics => "psionics",
            _ => "civilian",
        };
    }

    private static void DisposeTexture(Texture texture)
    {
        if (texture is OwnedTexture owned)
            owned.Dispose();
    }

    private sealed record RenderJob(
        IRenderTexture Target,
        Action<DrawingHandleScreen> Draw,
        TaskCompletionSource<byte[]?> Result,
        Action? Cleanup);

    private sealed class ComposerControl : Control
    {
        public readonly Queue<RenderJob> Queue = new();

        protected override void Draw(DrawingHandleScreen handle)
        {
            base.Draw(handle);

            while (Queue.TryDequeue(out var job))
            {
                try
                {
                    handle.RenderInRenderTarget(job.Target, () =>
                    {
                        var oldTransform = handle.GetTransform();
                        handle.SetTransform(Matrix3x2.Identity);
                        job.Draw(handle);
                        handle.SetTransform(oldTransform);
                    }, Color.Transparent);

                    job.Target.CopyPixelsToMemory<Rgba32>(image =>
                    {
                        try
                        {
                            using var png = new MemoryStream();
                            image.SaveAsPng(png);
                            job.Result.TrySetResult(png.ToArray());
                        }
                        catch (Exception e)
                        {
                            job.Result.TrySetException(e);
                        }
                        finally
                        {
                            job.Target.Dispose();
                        }
                    });
                }
                catch (Exception e)
                {
                    job.Target.Dispose();
                    job.Result.TrySetException(e);
                }
                finally
                {
                    job.Cleanup?.Invoke();
                }
            }
        }
    }

    private static void DrawMugshot(DrawingHandleScreen handle, Texture background, Texture character, Font font, float fontScale, MugshotLabel label)
    {
        handle.DrawTextureRect(background, UIBox2.FromDimensions(0, 0, PhotoSize, PhotoSize));

        var size = character.Size;
        var scale = Math.Clamp(PhotoSize / Math.Max(size.X, size.Y), 1, CharacterScale);
        var width = size.X * scale;
        var height = size.Y * scale;
        handle.DrawTextureRect(character,
            UIBox2.FromDimensions((PhotoSize - width) / 2, PhotoSize - height, width, height));

        DrawPlacard(handle, font, fontScale, label);
    }

    private static void DrawPlacard(DrawingHandleScreen handle, Font font, float scale, MugshotLabel label)
    {
        const char block = '\u2588';
        const int padX = 8;
        const int padY = 4;
        const int lineGap = 2;
        const int kneeHeight = 192;

        var number = $"#{label.Number:D6}";
        var year = label.BirthYear > 0
            ? label.BirthYear.ToString()
            : new string(block, 4);
        var birth = $"{block}{block}.{block}{block}.{year}";

        var numberWidth = handle.GetDimensions(font, number, scale).X;
        var birthWidth = handle.GetDimensions(font, birth, scale).X;
        var minGap = handle.GetDimensions(font, "   ", scale).X;
        var textWidth = MathF.Ceiling(numberWidth + minGap + birthWidth);
        var lineHeight = MathF.Round(font.GetLineHeight(scale));

        var name = label.Name.ToUpperInvariant();
        while (name.Length > 1 && handle.GetDimensions(font, name, scale).X > textWidth)
        {
            name = name[..^2] + '\u2026';
        }

        var nameWidth = handle.GetDimensions(font, name, scale).X;
        var width = textWidth + padX * 2;
        var height = padY * 2 + lineHeight * 2 + lineGap;
        var left = MathF.Round((PhotoSize - width) / 2);
        var top = MathF.Round(kneeHeight - height / 2);

        var box = UIBox2.FromDimensions(left, top, width, height);
        handle.DrawRect(box, Color.FromHex("#121216"));
        handle.DrawRect(box, Color.FromHex("#E1E1E1"), filled: false);
        handle.DrawRect(new UIBox2(box.Left + 1, box.Top + 1, box.Right - 1, box.Bottom - 1), Color.FromHex("#E1E1E1"), filled: false);

        var secondLine = top + padY + lineHeight + lineGap;
        handle.DrawString(font, new Vector2(MathF.Round((PhotoSize - nameWidth) / 2), top + padY), name, scale, Color.FromHex("#F0F0F0"));
        handle.DrawString(font, new Vector2(left + padX, secondLine), number, scale, Color.FromHex("#C3C3C3"));
        handle.DrawString(font, new Vector2(MathF.Round(left + width - padX - birthWidth), secondLine), birth, scale, Color.FromHex("#C3C3C3"));
    }
}
