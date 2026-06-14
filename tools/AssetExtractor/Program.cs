using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
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
        string? thingsDir = null, outDir = null, configPath = null, monstersPath = null, appearancesPath = null, itemsOutPath = null;
        string? itemsXmlPath = null, mountsXmlPath = null, outfitsXmlPath = null, staticItemsDir = null;
        var dumpNames = false;
        var itemsOnly = false;
        var equipment = false;
        var dryRun = false;
        var spritesOnly = false;
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--things": thingsDir = args[++i]; break;
                case "--out": outDir = args[++i]; break;
                case "--config": configPath = args[++i]; break;
                case "--monsters": monstersPath = args[++i]; break;
                case "--monster-appearances": appearancesPath = args[++i]; break;
                case "--items-out": itemsOutPath = args[++i]; break;
                case "--items-xml": itemsXmlPath = args[++i]; break;
                case "--mounts-xml": mountsXmlPath = args[++i]; break;
                case "--outfits-xml": outfitsXmlPath = args[++i]; break;
                case "--items-only": itemsOnly = true; break;
                case "--equipment": equipment = true; break;
                case "--dry-run": dryRun = true; break;
                case "--sprites-only": spritesOnly = true; break;
                case "--static-items": staticItemsDir = args[++i]; break;
                case "--dump-names": dumpNames = true; break;
            }
        }

        if (thingsDir is null || (!itemsOnly && outDir is null))
        {
            Console.Error.WriteLine(
                "usage: AssetExtractor --things <things/1500 dir> --out <output dir> " +
                "[--config content-config.json] [--monsters monsters.json] [--monster-appearances monster-appearances.json] " +
                "[--items-out items.json] [--items-xml items.xml] [--mounts-xml mounts.xml] [--outfits-xml outfits.xml] " +
                "[--items-only] [--equipment] [--dry-run] [--sprites-only] " +
                "[--static-items <legacy assets dir>] [--dump-names]");
            return 1;
        }

        var catalog = LoadCatalog(thingsDir);
        var appearances = LoadAppearances(thingsDir, catalog);
        Console.WriteLine($"appearances: {appearances.Object.Count} objects, {appearances.Outfit.Count} outfits, {appearances.Effect.Count} effects, {appearances.Missile.Count} missiles");

        if (configPath is null)
        {
            Console.Error.WriteLine("--config is required unless --dump-names is used");
            return 1;
        }

        var config = JsonNode.Parse(File.ReadAllText(configPath))!.AsObject();
        var (lootIds, lootNames) = ReadLootItems(monstersPath);
        var mountIds = ReadIdList(config, "mountIds").ToArray();
        var itemIds = new SortedSet<uint>(lootIds);
        if (equipment)
        {
            foreach (var id in ReadEquipmentItems(appearances.Object))
                itemIds.Add(id);
            Console.WriteLine($"equipment: selected {itemIds.Count - lootIds.Count} additional objects by clothes.slot");
        }

        if (itemsOnly)
        {
            if (itemsOutPath is null)
            {
                Console.Error.WriteLine("--items-out is required with --items-only");
                return 1;
            }
            ExportItems(
                appearances.Object,
                itemIds,
                lootNames,
                mountIds,
                itemsXmlPath,
                mountsXmlPath,
                itemsOutPath);
            return 0;
        }

        var sheets = new SheetCache(thingsDir, catalog);

        if (dumpNames)
        {
            DumpNames(appearances, outDir);
            return 0;
        }

        var manifest = new JsonObject();
        var staticItems = staticItemsDir is null ? null : new StaticItemSource(staticItemsDir);

        var outfitIds = new SortedSet<uint>(ReadIdList(config, "outfitIds"));
        foreach (var mountId in mountIds) outfitIds.Add(mountId);
        var outfitCatalog = LoadOutfitCatalog(outfitsXmlPath);
        foreach (var outfit in outfitCatalog) outfitIds.Add(outfit.LookType);
        if (outfitCatalog.Count > 0)
            Console.WriteLine($"outfits.xml: {outfitCatalog.Count} player outfits added to selection");
        var objectIds = new SortedSet<uint>(ReadIdList(config, "objectIds"));
        foreach (var id in itemIds) objectIds.Add(id);
        AddMonsterAssets(monstersPath, outfitIds, objectIds);
        AddMonsterAssets(appearancesPath, outfitIds, objectIds);
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

        if (dryRun)
        {
            var availableOutfits = appearances.Outfit.Select(entry => entry.Id).ToHashSet();
            var availableObjects = appearances.Object.Select(entry => entry.Id).ToHashSet();
            Console.WriteLine(
                $"dry run: {outfitIds.Count} outfits selected, "
                + $"{outfitIds.Count(id => !availableOutfits.Contains(id))} missing");
            Console.WriteLine(
                $"dry run: {objectIds.Count} objects selected, "
                + $"{objectIds.Count(id => !availableObjects.Contains(id))} missing");
            return 0;
        }

        manifest["outfits"] = ExportCategory(appearances.Outfit, outfitIds, "outfits", outDir, sheets);
        manifest["objects"] = ExportCategory(appearances.Object, objectIds, "objects", outDir, sheets, staticItems);
        manifest["effects"] = ExportCategory(appearances.Effect, ReadIdList(config, "effectIds"), "effects", outDir, sheets);
        manifest["missiles"] = ExportCategory(appearances.Missile, ReadIdList(config, "missileIds"), "missiles", outDir, sheets);
        manifest["semantic"] = semantic;
        var namesNode = new JsonObject();
        foreach (var (name, id) in objectsByName.OrderBy(kv => kv.Key)) namesNode[name] = id;
        manifest["objectNames"] = namesNode;

        File.WriteAllText(Path.Combine(outDir, "manifest.json"), manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
        Console.WriteLine($"manifest written to {Path.Combine(outDir, "manifest.json")}");
        if (outfitCatalog.Count > 0)
        {
            // authoritative player-outfit catalog (lookType -> name + gender) for the Outfit Studio;
            // only entries that actually exist in this asset version are emitted
            var availableOutfits = appearances.Outfit.Select(o => o.Id).ToHashSet();
            var catalogNode = new JsonArray();
            foreach (var outfit in outfitCatalog
                         .Where(o => availableOutfits.Contains(o.LookType))
                         .OrderBy(o => o.LookType))
                catalogNode.Add(new JsonObject
                {
                    ["lookType"] = outfit.LookType,
                    ["name"] = outfit.Name,
                    ["gender"] = outfit.Gender
                });
            File.WriteAllText(
                Path.Combine(outDir, "outfit-catalog.json"),
                catalogNode.ToJsonString(new JsonSerializerOptions { WriteIndented = false }));
            Console.WriteLine($"outfit-catalog.json written: {catalogNode.Count} entries");
        }
        if (monstersPath is not null && !spritesOnly)
        {
            itemsOutPath ??= Path.Combine(Path.GetDirectoryName(Path.GetFullPath(monstersPath))!, "items.json");
            ExportItems(
                appearances.Object,
                objectIds,
                lootNames,
                mountIds,
                itemsXmlPath,
                mountsXmlPath,
                itemsOutPath);
        }
        if (staticItems is not null)
            Console.WriteLine($"static item thumbnails imported: {staticItems.Imported}");
        return 0;
    }

    private static IEnumerable<uint> ReadIdList(JsonObject config, string key) =>
        config[key]?.AsArray().Select(v => v!.GetValue<uint>()) ?? Enumerable.Empty<uint>();

    private static IEnumerable<uint> ReadEquipmentItems(IEnumerable<Appearance> objects) =>
        objects
            .Where(obj => EquipmentSlot(obj.Flags?.Clothes?.Slot ?? 0) is not null)
            .Select(obj => obj.Id);

    private static (SortedSet<uint> Ids, Dictionary<uint, string> Names) ReadLootItems(string? monstersPath)
    {
        var ids = new SortedSet<uint>();
        var names = new Dictionary<uint, string>();
        if (monstersPath is null) return (ids, names);

        var monsters = JsonNode.Parse(File.ReadAllText(monstersPath))!.AsArray();
        foreach (var monster in monsters)
        foreach (var lootEntry in monster!["loot"]?.AsArray() ?? new JsonArray())
        {
            var itemId = lootEntry!["itemId"]?.GetValue<uint>() ?? 0;
            if (itemId == 0) continue;
            ids.Add(itemId);
            var name = lootEntry["name"]?.GetValue<string>() ?? "";
            if (name.Length > 0) names.TryAdd(itemId, name);
        }
        return (ids, names);
    }

    private static void AddMonsterAssets(
        string? monstersPath, ISet<uint> outfitIds, ISet<uint> objectIds)
    {
        if (monstersPath is null) return;
        var monsters = JsonNode.Parse(File.ReadAllText(monstersPath))!.AsArray();
        foreach (var monster in monsters)
        {
            var lookType = monster!["outfit"]?["lookType"]?.GetValue<uint>() ?? 0;
            if (lookType > 0) outfitIds.Add(lookType);
            var corpse = monster["corpse"]?.GetValue<uint>() ?? 0;
            if (corpse > 0) objectIds.Add(corpse);
        }
    }

    private static void ExportItems(
        IEnumerable<Appearance> objects, IEnumerable<uint> ids,
        IReadOnlyDictionary<uint, string> lootNames, IEnumerable<uint> mountIds,
        string? itemsXmlPath, string? mountsXmlPath, string outputPath)
    {
        var byId = objects.ToDictionary(a => a.Id);
        var xmlItems = LoadXmlItems(itemsXmlPath);
        var items = new JsonArray();
        foreach (var id in ids.Distinct().OrderBy(i => i))
        {
            if (!byId.TryGetValue(id, out var app)) continue;
            xmlItems.TryGetValue(id, out var xml);
            var appearanceName = app.Name?.ToStringUtf8() ?? "";
            var name = xml?.Name ?? (appearanceName.Length > 0
                ? appearanceName
                : lootNames.GetValueOrDefault(id, $"item {id}"));
            var salePrice = app.Flags?.Npcsaledata.Count > 0
                ? app.Flags.Npcsaledata.Max(npc => npc.SalePrice)
                : 0;
            var slot = EquipmentSlot(app.Flags?.Clothes?.Slot ?? 0) ?? xml?.Slot;
            if (slot == "weapon"
                && string.Equals(xml?.WeaponType, "shield", StringComparison.OrdinalIgnoreCase))
                slot = null;
            items.Add(new JsonObject
            {
                ["itemId"] = id,
                ["name"] = name,
                ["salePrice"] = salePrice,
                ["slot"] = slot,
                ["weaponType"] = xml?.WeaponType,
                ["attack"] = xml?.Attack ?? 0,
                ["armor"] = xml?.Armor ?? 0,
                ["defense"] = xml?.Defense ?? 0,
                ["mountLookType"] = 0,
                ["mountSpeed"] = 0
            });
        }

        foreach (var mount in LoadMounts(mountsXmlPath, mountIds))
        {
            items.Add(new JsonObject
            {
                ["itemId"] = -(int)mount.LookType,
                ["name"] = mount.Name,
                ["salePrice"] = 0,
                ["slot"] = "mount",
                ["weaponType"] = null,
                ["attack"] = 0,
                ["armor"] = 0,
                ["defense"] = 0,
                ["mountLookType"] = mount.LookType,
                ["mountSpeed"] = mount.Speed
            });
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        var temporaryPath = outputPath + ".tmp";
        File.WriteAllText(temporaryPath, items.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporaryPath, outputPath, true);
        Console.WriteLine($"items: exported {items.Count} entries to {outputPath}");
    }

    private sealed record XmlItem(
        string Name, string? Slot, string? WeaponType, int Attack, int Armor, int Defense);
    private sealed record MountItem(uint LookType, string Name, int Speed);
    private sealed record OutfitCatalogItem(uint LookType, string Name, string Gender);

    /// <summary>Reads Canary outfits.xml (type 0 = female, type 1 = male) into a deduped catalog.</summary>
    private static List<OutfitCatalogItem> LoadOutfitCatalog(string? path)
    {
        if (path is null || !File.Exists(path)) return [];
        var items = new List<OutfitCatalogItem>();
        var seen = new HashSet<uint>();
        foreach (var outfit in XDocument.Load(path).Root!.Elements("outfit"))
        {
            if (!uint.TryParse(outfit.Attribute("looktype")?.Value, out var lookType)) continue;
            if (!seen.Add(lookType)) continue;
            var name = outfit.Attribute("name")?.Value ?? $"LookType {lookType}";
            var gender = outfit.Attribute("type")?.Value == "1" ? "male" : "female";
            items.Add(new OutfitCatalogItem(lookType, name, gender));
        }
        return items;
    }

    private static Dictionary<uint, XmlItem> LoadXmlItems(string? path)
    {
        if (path is null || !File.Exists(path)) return [];
        return XDocument.Load(path).Root!.Elements("item")
            .Where(item => uint.TryParse(item.Attribute("id")?.Value, out _))
            .ToDictionary(
                item => uint.Parse(item.Attribute("id")!.Value),
                item =>
                {
                    var attributes = item.Elements("attribute")
                        .Where(attribute => attribute.Attribute("key") is not null)
                        .GroupBy(
                            attribute => attribute.Attribute("key")!.Value,
                            StringComparer.OrdinalIgnoreCase);
                    var values = attributes.ToDictionary(
                        group => group.Key,
                        group => group.Last().Attribute("value")?.Value ?? "",
                        StringComparer.OrdinalIgnoreCase);
                    return new XmlItem(
                        item.Attribute("name")?.Value ?? "",
                        NormalizeXmlSlot(item.Descendants("attribute")
                            .LastOrDefault(attribute =>
                                string.Equals(attribute.Attribute("key")?.Value, "slot", StringComparison.OrdinalIgnoreCase))
                            ?.Attribute("value")?.Value),
                        values.GetValueOrDefault("weaponType"),
                        ParseInt(values.GetValueOrDefault("attack")),
                        ParseInt(values.GetValueOrDefault("armor")),
                        ParseInt(values.GetValueOrDefault("defense")));
                });
    }

    private static IEnumerable<MountItem> LoadMounts(string? path, IEnumerable<uint> wantedIds)
    {
        if (path is null || !File.Exists(path)) return [];
        var wanted = wantedIds.ToHashSet();
        return XDocument.Load(path).Root!.Elements("mount")
            .Select(mount => new MountItem(
                uint.Parse(mount.Attribute("clientid")!.Value),
                mount.Attribute("name")?.Value ?? "Mount",
                ParseInt(mount.Attribute("speed")?.Value)))
            .Where(mount => wanted.Contains(mount.LookType))
            .OrderBy(mount => mount.LookType)
            .ToArray();
    }

    private static int ParseInt(string? value) =>
        int.TryParse(value, out var parsed) ? parsed : 0;

    private static string? EquipmentSlot(uint slot) => slot switch
    {
        1 => "helmet",
        2 => "necklace",
        4 => "armor",
        5 or 6 => "weapon",
        9 => "ring",
        _ => null
    };

    private static string? NormalizeXmlSlot(string? slot) => slot?.ToLowerInvariant() switch
    {
        "head" => "helmet",
        "armor" => "armor",
        "hand" => "weapon",
        "necklace" => "necklace",
        "ring" => "ring",
        _ => null
    };

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
        IEnumerable<Appearance> pool, IEnumerable<uint> ids, string category, string outDir, SheetCache sheets,
        StaticItemSource? staticItems = null)
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
            node[id.ToString()] = staticItems?.TryExport(app, category, dir, sheets)
                                  ?? ExportAppearance(app, category, dir, sheets);
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

        return new JsonObject
        {
            ["name"] = app.Name?.ToStringUtf8() ?? "",
            ["file"] = $"{category}/{fileName}",
            ["cellW"] = cellW,
            ["cellH"] = cellH,
            ["cols"] = cols,
            ["groups"] = groups,
            ["flags"] = BuildFlags(app)
        };
    }

    private static JsonObject BuildFlags(Appearance app)
    {
        var flags = new JsonObject();
        if (app.Flags is not { } f) return flags;

        if (f.Bank is not null) flags["groundSpeed"] = f.Bank.Waypoints;
        if (f.Unpass) flags["unpass"] = true;
        if (f.Unsight) flags["unsight"] = true;
        if (f.Top) flags["top"] = true;
        if (f.Bottom) flags["bottom"] = true;
        if (f.Clip) flags["clip"] = true;
        if (f.Height is not null) flags["elevation"] = f.Height.Elevation;
        if (f.Light is not null)
        {
            flags["lightBrightness"] = f.Light.Brightness;
            flags["lightColor"] = f.Light.Color;
        }
        if (f.Clothes is not null)
        {
            flags["clothes"] = new JsonObject
            {
                ["slot"] = f.Clothes.Slot
            };
        }
        return flags;
    }

    private sealed class StaticItemSource
    {
        private readonly string _itemsDir;
        private readonly string _equipmentDir;

        public int Imported { get; private set; }

        public StaticItemSource(string assetsDir)
        {
            _itemsDir = Path.Combine(assetsDir, "thumbnails", "items");
            _equipmentDir = Path.Combine(assetsDir, "equipment-thumbnails");
            if (!Directory.Exists(_itemsDir) && !Directory.Exists(_equipmentDir))
                throw new DirectoryNotFoundException(
                    $"static item source has neither '{_itemsDir}' nor '{_equipmentDir}'");
        }

        public JsonObject? TryExport(Appearance app, string category, string dir, SheetCache sheets)
        {
            if (category != "objects" || !IsSimpleStatic(app)) return null;

            var sourcePath = Find(app.Id);
            if (sourcePath is null) return null;

            var spriteId = (int)app.FrameGroup[0].SpriteInfo.SpriteId[0];
            var (_, spriteW, spriteH, _, _) = sheets.Locate(spriteId);
            using var source = Image.Load<Rgba32>(sourcePath);
            var cellW = Math.Max(spriteW, RoundUpToTile(source.Width));
            var cellH = Math.Max(spriteH, RoundUpToTile(source.Height));
            using var atlas = new Image<Rgba32>(cellW, cellH);

            var dx = cellW - source.Width;
            var dy = cellH - source.Height;
            for (var y = 0; y < source.Height; y++)
                for (var x = 0; x < source.Width; x++)
                    atlas[dx + x, dy + y] = source[x, y];

            var fileName = $"{app.Id}.png";
            atlas.SaveAsPng(Path.Combine(dir, fileName));
            Imported++;

            return new JsonObject
            {
                ["name"] = app.Name?.ToStringUtf8() ?? "",
                ["file"] = $"{category}/{fileName}",
                ["cellW"] = cellW,
                ["cellH"] = cellH,
                ["cols"] = 1,
                ["groups"] = new JsonArray(new JsonObject
                {
                    ["kind"] = "object",
                    ["patternX"] = 1,
                    ["patternY"] = 1,
                    ["patternZ"] = 1,
                    ["layers"] = 1,
                    ["phases"] = new JsonArray(),
                    ["start"] = 0,
                    ["count"] = 1
                }),
                ["flags"] = BuildFlags(app)
            };
        }

        private string? Find(uint id)
        {
            var fileName = $"{id}.png";
            var itemPath = Path.Combine(_itemsDir, fileName);
            if (File.Exists(itemPath)) return itemPath;
            var equipmentPath = Path.Combine(_equipmentDir, fileName);
            return File.Exists(equipmentPath) ? equipmentPath : null;
        }

        private static bool IsSimpleStatic(Appearance app)
        {
            if (app.FrameGroup.Count != 1) return false;
            var sprite = app.FrameGroup[0].SpriteInfo;
            var phases = sprite.Animation?.SpritePhase.Count ?? 0;
            return sprite.PatternWidth == 1
                   && sprite.PatternHeight == 1
                   && sprite.PatternDepth == 1
                   && sprite.Layers == 1
                   && sprite.SpriteId.Count == 1
                   && phases <= 1;
        }

        private static int RoundUpToTile(int value) =>
            Math.Max(32, (value + 31) / 32 * 32);
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
