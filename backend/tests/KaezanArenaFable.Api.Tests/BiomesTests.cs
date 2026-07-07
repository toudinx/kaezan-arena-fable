using KaezanArenaFable.Api.Content;
using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Tests;

[Collection("Tileset registry")]
public class BiomesTests
{
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
}
