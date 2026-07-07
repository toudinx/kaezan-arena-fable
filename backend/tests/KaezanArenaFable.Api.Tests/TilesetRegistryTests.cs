using KaezanArenaFable.Api.Content;
using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Tests;

[Collection("Tileset registry")]
public class TilesetRegistryTests
{
    private static string WriteTilesets(string json)
    {
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "tilesets.json");
        File.WriteAllText(path, json);
        return path;
    }

    private const string Valid = """
    {
      "families": {
        "grass": { "kind": "ground", "items": [4515, 4516], "zOrder": 3200 },
        "mountain": { "kind": "mountain", "items": [1128], "zOrder": 9900 }
      },
      "borderSets": {
        "grass->none": { "n": 4531, "e": 4532 },
        "mountain->OPEN": { "s": 4447 }
      },
      "wallSets": {
        "mountain": { "0": 1128, "1": 1128, "4": 4815, "5": 4815 }
      }
    }
    """;

    [Fact]
    public void loads_families_borders_and_wall_sets()
    {
        TilesetRegistry.LoadFrom(WriteTilesets(Valid));

        TileFamily grass = TilesetRegistry.Family("grass");
        Assert.True(TilesetRegistry.HasFamily("grass"));
        Assert.Equal("ground", grass.Kind);
        Assert.Equal<ushort>([4515, 4516], grass.Items);
        Assert.Equal(["grass", "mountain"], TilesetRegistry.FamilyNames);

        WallTileSet? mountain = TilesetRegistry.WallSet("mountain");
        Assert.NotNull(mountain);
        Assert.Equal((ushort)4815, mountain.Tiles[4]);

        BorderSet? fallbackBorder = TilesetRegistry.Borders("grass", "stone");
        Assert.NotNull(fallbackBorder);
        Assert.Equal((ushort)4531, fallbackBorder.Edges["n"]);

        BorderSet? openBorder = TilesetRegistry.Borders("mountain", "OPEN");
        Assert.NotNull(openBorder);
        Assert.Equal((ushort)4447, openBorder.Edges["s"]);
    }

    [Fact]
    public void missing_file_loads_empty_registry()
    {
        string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "tilesets.json");
        TilesetRegistry.LoadFrom(path);

        Assert.Empty(TilesetRegistry.FamilyNames);
        Assert.False(TilesetRegistry.HasFamily("grass"));
        Assert.Null(TilesetRegistry.WallSet("mountain"));
        Assert.Null(TilesetRegistry.Borders("grass", "stone"));
    }

    [Fact]
    public void wall_set_with_non_canonical_mask_fails_fast()
    {
        string broken = Valid.Replace("\"4\": 4815", "\"2\": 4815");
        Assert.Throws<InvalidDataException>(() => TilesetRegistry.LoadFrom(WriteTilesets(broken)));
    }

    [Fact]
    public void border_set_with_unknown_family_fails_fast()
    {
        string broken = Valid.Replace("\"grass->none\"", "\"bog->none\"");
        Assert.Throws<InvalidDataException>(() => TilesetRegistry.LoadFrom(WriteTilesets(broken)));
    }
}
