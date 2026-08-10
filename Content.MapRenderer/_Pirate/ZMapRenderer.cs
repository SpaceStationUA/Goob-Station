using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Content.IntegrationTests;
using Content.MapRenderer.Extensions;
using Content.MapRenderer.Painters;
using Content.Shared._Pirate.ZLevels.Mapping.Prototypes;
using Robust.Shared.Prototypes;
using Robust.UnitTesting.Pool; // Pirate: multiz - ExternalTestContext moved to Robust.UnitTesting.Pool upstream
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.MapRenderer._Pirate;

/// <summary>
/// Renders zMap prototypes into per-level stacked images.
/// </summary>
public static class ZMapRenderer
{
    public static async Task RenderZMaps(CommandLineArguments arguments, ExternalTestContext testContext)
    {
        // Resolve ids once, then render each layer with a fresh pair.
        var toRender = new List<(string Id, List<string> LayerFiles)>();

        await using (var pair = await PoolManager.GetServerClient(testContext: testContext))
        {
            var protoMan = pair.Server.ResolveDependency<IPrototypeManager>();
            var zMaps = protoMan.EnumeratePrototypes<CEZLevelMapPrototype>().ToArray();
            Array.Sort(zMaps, (a, b) => string.Compare(a.ID, b.ID, StringComparison.Ordinal));

            var selectedIds = SelectIds(arguments, zMaps.Select(z => z.ID).ToArray());
            if (selectedIds == null)
                return;

            var resources = DirectoryExtensions.Resources().FullName;
            foreach (var id in selectedIds)
            {
                if (!protoMan.TryIndex<CEZLevelMapPrototype>(id, out var proto))
                {
                    Console.Error.WriteLine($"No zMap prototype found with id: {id}");
                    continue;
                }

                var layerFiles = proto.Maps
                    .Select(p => Path.Combine(resources, p.ToString().TrimStart('/')))
                    .ToList();

                toRender.Add((id, layerFiles));
            }
        }

        foreach (var (id, layerFiles) in toRender)
        {
            await RenderOne(arguments, id, layerFiles, testContext);
        }
    }

    private static async Task RenderOne(
        CommandLineArguments arguments,
        string id,
        List<string> layerFiles,
        ExternalTestContext testContext)
    {
        Console.WriteLine($"Rendering zMap '{id}' with {layerFiles.Count} layers");

        var layers = new List<ZLayerRender>();
        for (var depth = 0; depth < layerFiles.Count; depth++)
        {
            var file = layerFiles[depth];
            if (!File.Exists(file))
            {
                await Console.Error.WriteLineAsync($"zMap '{id}' layer {depth} file does not exist: {file}");
                continue;
            }

            Console.WriteLine($"Painting layer {depth} from {file}");

            var grids = new List<RenderedGridImage<Rgba32>>();
            await using var painter = new MapPainter(new RenderMapFile { FileName = file }, testContext);
            await painter.Initialize();
            await painter.SetupView(showMarkers: arguments.ShowMarkers);

            try
            {
                await foreach (var renderedGrid in painter.Paint())
                {
                    grids.Add(renderedGrid);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Painting layer {depth} of '{id}' failed: {ex}");
            }

            layers.Add(new ZLayerRender { Depth = depth, Grids = grids });

            try
            {
                await painter.CleanReturnAsync();
            }
            catch (Exception e)
            {
                Console.WriteLine($"Exception while shutting down painter: {e}");
            }
        }

        if (layers.All(l => l.Grids.Count == 0))
        {
            await Console.Error.WriteLineAsync($"zMap '{id}' produced no grids; nothing to write.");
            return;
        }

        var directory = Path.Combine(arguments.OutputPath, id);
        Directory.CreateDirectory(directory);

        using var composite = ZMapCompositor.Compose(layers);

        foreach (var (depth, image) in composite.Layers)
        {
            var path = Path.Combine(directory, $"{id}-z{depth}.{arguments.Format}");
            await SaveAsync(image, path, arguments.Format);
            Console.WriteLine($"Wrote level {depth} (+ levels below) ({image.Width}x{image.Height}) to {path}");
        }

        // Input grid images are no longer needed after compositing.
        foreach (var grid in layers.SelectMany(l => l.Grids))
            grid.Image.Dispose();
    }

    /// <summary>
    /// Resolves command-line ids or prompts for an interactive selection.
    /// </summary>
    private static List<string>? SelectIds(CommandLineArguments arguments, string[] available)
    {
        if (arguments.Maps.Count > 0)
            return arguments.Maps.ToList();

        if (available.Length == 0)
        {
            Console.WriteLine("No zMap prototypes exist.");
            return null;
        }

        Console.WriteLine("zMap List");
        Console.WriteLine(string.Join('\n', available.Select((id, i) => $"{i,3}: {id}")));
        Console.WriteLine("Select one, multiple separated by commas or \"all\":");
        Console.Write("> ");

        var input = Console.ReadLine();
        if (string.IsNullOrWhiteSpace(input))
        {
            Console.WriteLine("No zMaps were chosen");
            return null;
        }

        if (input is "all" or "\"all\"")
            return available.ToList();

        var selected = new List<string>();
        foreach (var part in input.Split(','))
        {
            if (!int.TryParse(part.Trim(), out var index) || index < 0 || index >= available.Length)
            {
                Console.WriteLine($"Invalid selection: {part}");
                return null;
            }

            selected.Add(available[index]);
        }

        return selected;
    }

    private static async Task SaveAsync(Image<Rgba32> image, string path, OutputFormat format)
    {
        switch (format)
        {
            case OutputFormat.webp:
                await image.SaveAsWebpAsync(path);
                break;
            default:
            case OutputFormat.png:
                await image.SaveAsPngAsync(path);
                break;
        }
    }
}
