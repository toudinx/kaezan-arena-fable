using System.Text.Json;
using KaezanArenaFable.Api.Content;
using KaezanArenaFable.Api.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace KaezanArenaFable.Api.Tests;

[Collection("Tileset registry")]
public class BiomesTests
{
    private sealed class TestEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "KaezanArenaFable.Api.Tests";
        public string WebRootPath { get; set; } = contentRootPath;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static string WriteTilesets(string json)
    {
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "tilesets.json");
        File.WriteAllText(path, json);
        return path;
    }

    private const string ValidTilesets = """
    {
      "families": {
        "cave": { "kind": "ground", "items": [351, 352], "zOrder": 200 },
        "mountain": { "kind": "mountain", "items": [1128], "zOrder": 9900 }
      },
      "wallSets": {
        "mountain": { "0": 1128, "1": 1129, "4": 1130, "5": 1131 }
      }
    }
    """;

    [Fact]
    public void resolve_with_valid_wall_family_fills_wall_set()
    {
        TilesetRegistry.LoadFrom(WriteTilesets(ValidTilesets));
        BiomeDef source = Biomes.Cave with { WallSet = null, WallFamily = "mountain" };

        BiomeDef resolved = Biomes.Resolve(source);

        Assert.NotNull(resolved.WallSet);
        Assert.Equal((ushort)1130, resolved.WallSet.Tiles[4]);
        Assert.Equal("mountain", resolved.WallFamily);
    }

    [Fact]
    public void resolve_with_unknown_wall_family_fails_fast()
    {
        TilesetRegistry.LoadFrom(WriteTilesets(ValidTilesets));
        BiomeDef source = Biomes.Cave with { WallSet = null, WallFamily = "missing" };

        InvalidDataException ex = Assert.Throws<InvalidDataException>(() => Biomes.Resolve(source));

        Assert.Contains("missing", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void resolve_without_wall_family_keeps_legacy_def_intact()
    {
        TilesetRegistry.LoadFrom(WriteTilesets(ValidTilesets));
        BiomeDef source = Biomes.Cave with { WallSet = null, WallFamily = "" };

        BiomeDef resolved = Biomes.Resolve(source);

        Assert.Same(source, resolved);
        Assert.Null(resolved.WallSet);
    }

    [Fact]
    public void default_biomes_use_curated_low_noise_material_pairs()
    {
        TilesetRegistry.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Content", "tilesets.json"));

        (int Tier, string Wall, string[] Ground, string Accent)[] expected =
        [
            (1, "mountain", ["cave", "dirt"], ""),
            (2, "mountain", ["grass", "dirt"], ""),
            (3, "mossy wall mountain", ["mossy floor", "rocky ground"], ""),
            (4, "crystal wall", ["rocky ground", "dark dirt"], "lava"),
            (5, "crystal wall", ["rocky ground", "dark dirt"], "lava"),
        ];

        foreach ((int tier, string wall, string[] ground, string accent) in expected)
        {
            BiomeDef biome = Biomes.ForTier(tier);

            Assert.Equal(wall, biome.WallFamily);
            Assert.Equal(ground, biome.GroundFamilies);
            Assert.Equal(accent, biome.AccentFamily);

            BiomeDef resolved = Biomes.Resolve(biome);
            Assert.NotNull(resolved.WallSet);
        }
    }

    [Fact]
    public void content_store_reseeds_provisional_biome_palettes()
    {
        string root = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string contentDir = Path.Combine(root, ".data", "content");
        Directory.CreateDirectory(contentDir);
        List<BiomeRow> oldRows = Biomes.AllDefaults()
            .Select(row => row.Tier switch
            {
                4 => row with { Def = row.Def with { GroundFamilies = ["dark dirt", "rocky ground"] } },
                5 => row with { Def = row.Def with { GroundFamilies = ["dark dirt", "rock soil"] } },
                _ => row
            })
            .ToList();
        File.WriteAllText(
            Path.Combine(contentDir, "biomes.json"),
            JsonSerializer.Serialize(oldRows, new JsonSerializerOptions { WriteIndented = true }));

        ContentStore store = new ContentStore(new TestEnvironment(root));
        BiomeDef? tier4 = store.Biome(4);
        BiomeDef? tier5 = store.Biome(5);

        Assert.NotNull(tier4);
        Assert.NotNull(tier5);
        string[] tier4GroundFamilies = tier4.GroundFamilies
            ?? throw new InvalidOperationException("tier 4 ground families were not reseeded");
        string[] tier5GroundFamilies = tier5.GroundFamilies
            ?? throw new InvalidOperationException("tier 5 ground families were not reseeded");
        Assert.Equal(["rocky ground", "dark dirt"], tier4GroundFamilies);
        Assert.Equal(["rocky ground", "dark dirt"], tier5GroundFamilies);
    }
}
