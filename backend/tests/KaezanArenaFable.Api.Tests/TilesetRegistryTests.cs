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
    public void multi_tile_ids_load_from_sibling_sizes_file()
    {
        string tilesetsPath = WriteTilesets(Valid);
        string sizesPath = Path.Combine(Path.GetDirectoryName(tilesetsPath)!, "appearance-sizes.json");
        File.WriteAllText(sizesPath, """{ "multiTile": [958, 1047] }""");

        TilesetRegistry.LoadFrom(tilesetsPath);

        Assert.True(TilesetRegistry.IsMultiTile(958));
        Assert.True(TilesetRegistry.IsMultiTile(1047));
        Assert.False(TilesetRegistry.IsMultiTile(351));
    }

    [Fact]
    public void validate_defaults_rejects_multi_tile_decor()
    {
        string tilesetsPath = WriteTilesets(Valid);
        string sizesPath = Path.Combine(Path.GetDirectoryName(tilesetsPath)!, "appearance-sizes.json");
        // 1772 is in the Cave default Decor palette — flagging it as multi-tile must fail fast.
        File.WriteAllText(sizesPath, """{ "multiTile": [1772] }""");
        TilesetRegistry.LoadFrom(tilesetsPath);

        InvalidDataException ex = Assert.Throws<InvalidDataException>(Biomes.ValidateDefaults);
        Assert.Contains("1772", ex.Message);
    }

    [Fact]
    public void validate_defaults_passes_with_single_tile_palettes()
    {
        string tilesetsPath = WriteTilesets(Valid);
        string sizesPath = Path.Combine(Path.GetDirectoryName(tilesetsPath)!, "appearance-sizes.json");
        // A multi-tile id no default palette uses — the guard must stay quiet.
        File.WriteAllText(sizesPath, """{ "multiTile": [1047] }""");
        TilesetRegistry.LoadFrom(tilesetsPath);

        Biomes.ValidateDefaults();
    }

    [Fact]
    public void validate_defaults_passes_with_the_shipped_content()
    {
        // The committed tilesets.json + appearance-sizes.json and the canonical biome defaults
        // must stay compatible — this is the startup path of Program.cs.
        TilesetRegistry.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Content", "tilesets.json"));

        Biomes.ValidateDefaults();
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
