using KaezanArenaFable.Api.Content;
using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;

namespace KaezanArenaFable.Api.Tests;

[Collection("Tileset registry")]
public class DungeonValidatorTests
{
    public DungeonValidatorTests()
    {
        // Default biomes carry GroundFamilies, so generation reads the real tileset registry;
        // reload it per test because other classes in this collection load fakes.
        TilesetRegistry.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Content", "tilesets.json"));
    }

    [Fact]
    public void valid_floor_passes()
    {
        var rng = new Rng(42UL);
        var floor = DungeonGenerator.Generate(rng, 0, isBossFloor: false, Biomes.ForTier(1));
        DungeonValidator.Validate(floor); // must not throw
    }

    [Fact]
    public void unreachable_chest_fails_loudly()
    {
        var rng = new Rng(42UL);
        var floor = DungeonGenerator.Generate(rng, 0, isBossFloor: false, Biomes.ForTier(1));
        floor.Chests.Add((0, 0)); // corner is always rock (margin band)
        var ex = Assert.Throws<InvalidOperationException>(() => DungeonValidator.Validate(floor));
        Assert.Contains("chest", ex.Message);
    }

    [Fact]
    public void every_seed_and_tier_generates_valid_floors()
    {
        // the in-repo sweep: 200 seeds x 5 tiers x 2 floors, all must validate
        for (var tier = 1; tier <= 5; tier++)
        {
            var biome = Biomes.ForTier(tier);
            for (long seed = 1; seed <= 200; seed++)
            {
                var rng = new Rng((ulong)seed);
                DungeonValidator.Validate(DungeonGenerator.Generate(rng, 0, false, biome));
                DungeonValidator.Validate(DungeonGenerator.Generate(rng, 1, true, biome));
            }
        }
    }
}
