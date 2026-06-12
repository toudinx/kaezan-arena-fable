using System.Text.Json;
using System.Text.Json.Nodes;
using Canary.Protobuf.Appearances;
using SharpCompress.Compressors.LZMA;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace AssetExtractor;

/// <summary>
/// Extracts Tibia sprites from the modern protobuf asset format (catalog-content.json +
/// appearances.dat + LZMA-compressed BMP sprite sheets) into per-appearance PNG atlases
/// plus a JSON manifest the web client can consume.
///
/// Decode logic mirrors otclient-4.0 src/client/spriteappearances.cpp.
/// </summary>
internal static class Program
{
    private const int SheetSize = 384;
    private const int SheetBytes = SheetSize * SheetSize * 4;

    private static int Main(string[] args)
    {
        string? thingsDir = null, outDir = null, configPath = null, monstersPath = null;
        var dumpNames = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--things": thingsDir = args[++i]; break;
                case "--out": outDir = args[++i]; break;
                case "--config": configPath = args[++i]; break;
                case "--monsters": monstersPath = args[++i]; break;
                case "--dump-names": dumpNames = true; break;
            }
        }

        if (thingsDir is null || outDir is null)
        {
            Console.Error.WriteLine("usage: AssetExtractor --things <things/1500 dir> --out <output dir> [--config content-config.json] [--monsters monsters.json] [--dump-names]");
            return 1;
        }

        var catalog = LoadCatalog(thingsDir);
        var appearances = LoadAppearances(thingsDir, catalog);
        Console.WriteLine($"appearances: {appearances.Object.Count} objects, {appearances.Outfit.Count} outfits, {appearances.Effect.Count} effects, {appearances.Missile.Count} missiles");

        var sheets = new SheetCache(thingsDir, catalog);

        if (dumpNames)
        {
            DumpNames(appearances, outDir);
            return 0;
        }

        if (configPath is null)
        {
            Console.Error.WriteLine("--config is required unless --dump-names is used");
            return 1;
        }

        var config = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
        var manifest = new JsonObject();

        var outfitIds = new SortedSet<uint>(ReadIdList(config, "outfitIds"));
        var objectIds = new SortedSet<uint>(ReadIdList(config, "objectIds"));
        if (monstersPath is not null)
        {
            var monsters = JsonNode.Parse(File.ReadAllText(monstersPath))!.AsArray();
            foreach (var m in monsters)
            {
                var lt = m!["outfit"]?["lookType"]?.GetValue<uint>() ?? 0;
                if (lt > 0) outfitIds.Add(lt);
                var corpse = m["corpse"]?.GetValue<uint>() ?? 0;
                if (corpse > 0) objectIds.Add(corpse);
                foreach (var lootEntry in m["loot"]?.AsArray() ?? new JsonArray())
                {
                    var itemId = lootEntry!["itemId"]?.GetValue<uint>() ?? 0;
                    if (itemId > 0) objectIds.Add(itemId);
                }
            }
        }
        var objectsByName = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase);
        var wantedNames = config["objectsByName"]?.AsArray().Select(n => n!.GetValue<string>()).ToHashSet(StringComparer.OrdinalIgnoreCase)
                          ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (wantedNames.Count > 0)
        {
            foreach (var obj in appearances.Object)
            {
                var name = obj.Name?.ToStringUtf8() ?? "";
                if (name.Length > 0 && wantedNames.Contains(name) && !objectsByName.ContainsKey(name))
                {
                    objectsByName[name] = obj.Id;
                    objectIds.Add(obj.Id);
                }
            }
            foreach (var missing in wantedNames.Where(n => !objectsByName.ContainsKey(n)))
                Console.Error.WriteLine($"WARN: object name not found: {missing}");
        }

        // semantic groups: { "ground.cave": [ids...] } merged into objectIds and echoed in manifest
        var semantic = new JsonObject();
        if (config["semantic"] is JsonObject sem)
        {
            foreach (var (key, val) in sem)
            {
                var ids = val!.AsArray().Select(v => v!.GetValue<uint>()).ToArray();
                foreach (var id in ids) objectIds.Add(id);
                semantic[key] = new JsonArray(ids.Select(i => JsonValue.Create((long)i)).ToArray());
            }
        }

        manifest["outfits"] = ExportCategory(appearances.Outfit, outfitIds, "outfits", outDir, sheets);
        manifest["objects"] = ExportCategory(appearances.Object, objectIds, "objects", outDir, sheets);
        manifest["effects"] = ExportCategory(appearances.Effect, ReadIdList(config, "effectIds"), "effects", outDir, sheets);
        manifest["missiles"] = ExportCategory(appearances.Missile, ReadIdList(config, "missileIds"), "missiles", outDir, sheets);
        manifest["semantic"] = semantic;
        var namesNode = new JsonObject();
        foreach (var (name, id) in objectsByName.OrderBy(kv => kv.Key)) namesNode[name] = id;
        manifest["objectNames"] = namesNode;

        File.WriteAllText(Path.Combine(outDir, "manifest.json"), manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
        Console.WriteLine($"manifest written to {Path.Combine(outDir, "manifest.json")}");
        return 0;
    }

    private static IEnumerable<uint> ReadIdList(JsonObject config, string key) =>
        config[key]?.AsArray().Select(v => v!.GetValue<uint>()) ?? Enumerable.Empty<uint>();

    // ----- catalog -----

    private sealed record SheetEntry(string File, int SpriteType, int FirstId, int LastId);

    private static List<SheetEntry> LoadCatalog(string thingsDir)
    {
        var json = JsonNode.Parse(File.ReadAllText(Path.Combine(thingsDir, "catalog-content.json")))!.AsArray();
        var sheets = new List<SheetEntry>();
        foreach (var entry in json)
        {
            if (entry!["type"]?.GetValue<string>() != "sprite") continue;
            sheets.Add(new SheetEntry(
                entry["file"]!.GetValue<string>(),
                entry["spritetype"]!.GetValue<int>(),
                entry["firstspriteid"]!.GetValue<int>(),
                entry["lastspriteid"]!.GetValue<int>()));
        }
        Console.WriteLine($"catalog: {sheets.Count} sprite sheets");
        return sheets;
    }

    private static Appearances LoadAppearances(string thingsDir, List<SheetEntry> _)
    {
        var file = Directory.GetFiles(thingsDir, "appearances-*.dat").Single();
        return Appearances.Parser.ParseFrom(File.ReadAllBytes(file));
    }

    // ----- sheet decoding -----

    private sealed class SheetCache(string thingsDir, List<SheetEntry> catalog)
    {
        private readonly Dictionary<string, Image<Rgba32>> _decoded = new();

        public (Image<Rgba32> Sheet, int W, int H, int Col, int Row) Locate(int spriteId)
        {
            var entry = catalog.FirstOrDefault(s => spriteId >= s.FirstId && spriteId <= s.LastId)
                        ?? throw new InvalidOperationException($"sprite id {spriteId} not in any sheet");
            var (w, h) = entry.SpriteType switch
            {
                0 => (32, 32),
                1 => (32, 64),
                2 => (64, 32),
                3 => (64, 64),
                _ => throw new InvalidOperationException($"unsupported spritetype {entry.SpriteType}")
            };
            var index = spriteId - entry.FirstId;
            var cols = SheetSize / w;
            return (GetSheet(entry.File), w, h, index % cols, index / cols);
        }

        private Image<Rgba32> GetSheet(string file)
        {
            if (_decoded.TryGetValue(file, out var img)) return img;
            var raw = DecodeSheet(Path.Combine(thingsDir, file));
            img = Image.LoadPixelData<Rgba32>(raw, SheetSize, SheetSize);
            _decoded[file] = img;
            return img;
        }

        /// <summary>Decodes a CIP-header + raw-LZMA1 compressed BMP sheet into RGBA pixels (top-down).</summary>
        private static byte[] DecodeSheet(string path)
        {
            var data = File.ReadAllBytes(path);
            var p = 0;
            while (data[p] == 0x00) p++;          // leading pad
            p += 5;                                // const sequence 0x70 0x0A 0xFA 0x80 0x24 (first byte already at p)
            while ((data[p] & 0x80) == 0x80) p++;  // 7-bit encoded lzma file size
            p++;                                   // final size byte (high bit clear)

            var props = new byte[5];               // lclppb + dictSize(4)
            Array.Copy(data, p, props, 0, 5);
            p += 5;
            p += 8;                                // cip "compressed size" field

            using var input = new MemoryStream(data, p, data.Length - p);
            using var lzma = new LzmaStream(props, input);

            // BMP header: pixel data offset at byte 10 (LE u32)
            var header = ReadExactly(lzma, 14);
            var pixelOffset = header[10] | (header[11] << 8) | (header[12] << 16) | (header[13] << 24);
            ReadExactly(lzma, pixelOffset - 14);   // skip remaining header bytes
            var bgra = ReadExactly(lzma, SheetBytes);

            // BMP rows are bottom-up BGRA; convert to top-down RGBA + magenta -> transparent
            var rgba = new byte[SheetBytes];
            const int stride = SheetSize * 4;
            for (var y = 0; y < SheetSize; y++)
            {
                var src = (SheetSize - 1 - y) * stride;
                var dst = y * stride;
                for (var x = 0; x < SheetSize; x++)
                {
                    var b = bgra[src]; var g = bgra[src + 1]; var r = bgra[src + 2]; var a = bgra[src + 3];
                    if (r == 0xFF && g == 0x00 && b == 0xFF) { r = 0; g = 0; b = 0; a = 0; }
                    rgba[dst] = r; rgba[dst + 1] = g; rgba[dst + 2] = b; rgba[dst + 3] = a;
                    src += 4; dst += 4;
                }
            }
            return rgba;
        }

        private static byte[] ReadExactly(Stream s, int count)
        {
            var buf = new byte[count];
            var read = 0;
            while (read < count)
            {
                var n = s.Read(buf, read, count - read);
                if (n <= 0) throw new EndOfStreamException($"expected {count} bytes, got {read}");
                read += n;
            }
            return buf;
        }
    }

    // ----- atlas export -----

    private static JsonObject ExportCategory(
        IEnumerable<Appearance> pool, IEnumerable<uint> ids, string category, string outDir, SheetCache sheets)
    {
        var byId = pool.ToDictionary(a => a.Id);
        var dir = Path.Combine(outDir, category);
        Directory.CreateDirectory(dir);
        var node = new JsonObject();

        foreach (var id in ids.Distinct().OrderBy(i => i))
        {
            if (!byId.TryGetValue(id, out var app))
            {
                Console.Error.WriteLine($"WARN: {category} id {id} not found in appearances");
                continue;
            }
            node[id.ToString()] = ExportAppearance(app, category, dir, sheets);
        }
        Console.WriteLine($"{category}: exported {node.Count}");
        return node;
    }

    private static JsonObject ExportAppearance(Appearance app, string category, string dir, SheetCache sheets)
    {
        // collect all sprite ids across frame groups, in order
        var allSprites = new List<int>();
        var groups = new JsonArray();
        var cellW = 32; var cellH = 32;

        foreach (var fg in app.FrameGroup)
        {
            var si = fg.SpriteInfo;
            foreach (var sid in si.SpriteId)
            {
                var (_, w, h, _, _) = sheets.Locate((int)sid);
                cellW = Math.Max(cellW, w);
                cellH = Math.Max(cellH, h);
            }
        }

        foreach (var fg in app.FrameGroup)
        {
            var si = fg.SpriteInfo;
            var phases = new JsonArray();
            if (si.Animation is not null)
                foreach (var ph in si.Animation.SpritePhase)
                    phases.Add(new JsonArray(JsonValue.Create(ph.DurationMin), JsonValue.Create(ph.DurationMax)));

            groups.Add(new JsonObject
            {
                ["kind"] = fg.FixedFrameGroup switch
                {
                    FIXED_FRAME_GROUP.OutfitIdle => "idle",
                    FIXED_FRAME_GROUP.OutfitMoving => "moving",
                    _ => "object"
                },
                ["patternX"] = si.PatternWidth,
                ["patternY"] = si.PatternHeight,
                ["patternZ"] = si.PatternDepth,
                ["layers"] = si.Layers,
                ["phases"] = phases,
                ["start"] = allSprites.Count,
                ["count"] = si.SpriteId.Count
            });
            allSprites.AddRange(si.SpriteId.Select(s => (int)s));
        }

        // compose atlas
        var cols = Math.Min(Math.Max(allSprites.Count, 1), 512 / cellW * 2); // keep atlases reasonably square
        cols = Math.Max(1, Math.Min(cols, 16));
        var rows = (allSprites.Count + cols - 1) / cols;
        using var atlas = new Image<Rgba32>(cols * cellW, Math.Max(rows, 1) * cellH);

        for (var i = 0; i < allSprites.Count; i++)
        {
            var (sheet, w, h, scol, srow) = sheets.Locate(allSprites[i]);
            var dx = i % cols * cellW + (cellW - w); // anchor bottom-right (tibia convention)
            var dy = i / cols * cellH + (cellH - h);
            var sx = scol * w; var sy = srow * h;
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                    atlas[dx + x, dy + y] = sheet[sx + x, sy + y];
        }

        var fileName = $"{app.Id}.png";
        atlas.SaveAsPng(Path.Combine(dir, fileName));

        var flags = new JsonObject();
        if (app.Flags is { } f)
        {
            if (f.Bank is not null) flags["groundSpeed"] = f.Bank.Waypoints;
            if (f.Unpass) flags["unpass"] = true;
            if (f.Unsight) flags["unsight"] = true;
            if (f.Top) flags["top"] = true;
            if (f.Bottom) flags["bottom"] = true;
            if (f.Clip) flags["clip"] = true;
            if (f.Height is not null) flags["elevation"] = f.Height.Elevation;
            if (f.Light is not null) { flags["lightBrightness"] = f.Light.Brightness; flags["lightColor"] = f.Light.Color; }
        }

        return new JsonObject
        {
            ["name"] = app.Name?.ToStringUtf8() ?? "",
            ["file"] = $"{category}/{fileName}",
            ["cellW"] = cellW,
            ["cellH"] = cellH,
            ["cols"] = cols,
            ["groups"] = groups,
            ["flags"] = flags
        };
    }

    private static void DumpNames(Appearances appearances, string outDir)
    {
        Directory.CreateDirectory(outDir);
        using var w = new StreamWriter(Path.Combine(outDir, "object-names.txt"));
        foreach (var obj in appearances.Object)
        {
            var name = obj.Name?.ToStringUtf8() ?? "";
            if (name.Length == 0) continue;
            var f = obj.Flags;
            var tags = new List<string>();
            if (f?.Bank is not null) tags.Add($"ground:{f.Bank.Waypoints}");
            if (f?.Unpass == true) tags.Add("unpass");
            if (f?.Unsight == true) tags.Add("unsight");
            if (f?.Top == true) tags.Add("top");
            if (f?.Clip == true) tags.Add("clip");
            w.WriteLine($"{obj.Id}\t{name}\t{string.Join(",", tags)}");
        }
        Console.WriteLine($"names dumped to {Path.Combine(outDir, "object-names.txt")}");
    }
}
