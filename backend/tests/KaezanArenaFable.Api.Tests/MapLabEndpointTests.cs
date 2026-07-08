using KaezanArenaFable.Api.Api;
using KaezanArenaFable.Api.Content;
using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;

namespace KaezanArenaFable.Api.Tests;

[Collection("Tileset registry")]
public class MapLabEndpointTests
{
    private static void LoadRealRegistry()
    {
        string tilesetsPath = Path.Combine(AppContext.BaseDirectory, "Content", "tilesets.json");
        TilesetRegistry.LoadFrom(tilesetsPath);
    }

    [Fact]
    public void map_preview_is_deterministic_for_the_same_request()
    {
        LoadRealRegistry();
        MapPreviewRequest request = new MapPreviewRequest(2, 123456789L, 0, false, null);

        MapDto first = MetaEndpoints.BuildMapPreview(request, null);
        MapDto second = MetaEndpoints.BuildMapPreview(request, null);

        Assert.Equal(first.Floor, second.Floor);
        Assert.Equal(first.W, second.W);
        Assert.Equal(first.H, second.H);
        Assert.Equal(first.Flat, second.Flat);
        Assert.Equal(first.Tall, second.Tall);
        Assert.Equal(first.Blocked, second.Blocked);
        Assert.Equal(first.EntryX, second.EntryX);
        Assert.Equal(first.EntryY, second.EntryY);
        Assert.Equal(first.LadderX, second.LadderX);
        Assert.Equal(first.LadderY, second.LadderY);
        Assert.Equal(first.Rooms, second.Rooms);
        Assert.Equal(first.Biome, second.Biome);
    }

    [Fact]
    public void biome_validation_rejects_unknown_tileset_family()
    {
        LoadRealRegistry();
        List<BiomeRow> rows = Biomes.AllDefaults()
            .Select(row => row.Tier == 2
                ? row with { Def = row.Def with { GroundFamilies = ["missing-ground-family"] } }
                : row)
            .ToList();

        string? error = MetaEndpoints.ValidateMapLabBiomes(rows);

        Assert.NotNull(error);
        Assert.Contains("missing-ground-family", error, StringComparison.Ordinal);
    }
}
