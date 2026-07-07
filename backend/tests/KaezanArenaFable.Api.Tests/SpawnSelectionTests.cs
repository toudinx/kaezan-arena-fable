using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;

namespace KaezanArenaFable.Api.Tests;

public class SpawnSelectionTests
{
    private static DungeonTier Tier() =>
        new DungeonTier(1, "T1", "", ["Rat", "Bug"], ["Cave Rat"], "Boss", 0, 1.0);

    [Fact]
    public void themed_room_picks_only_from_theme()
    {
        Rng rng = new Rng(42UL);
        Room room = new Room { SpawnTheme = ["Rotworm", "Carrion Worm"] };
        for (int i = 0; i < 50; i++)
            Assert.Contains(SpawnSelection.CommonSpecies(rng, room, Tier()), room.SpawnTheme);
    }

    [Fact]
    public void unthemed_room_uses_tier_pool()
    {
        Rng rng = new Rng(42UL);
        Room room = new Room();
        for (int i = 0; i < 50; i++)
            Assert.Contains(SpawnSelection.CommonSpecies(rng, room, Tier()), Tier().CommonMobs);
    }
}
