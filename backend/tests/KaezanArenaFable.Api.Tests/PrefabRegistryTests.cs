using KaezanArenaFable.Api.Content;

namespace KaezanArenaFable.Api.Tests;

public class PrefabRegistryTests
{
    private static string WritePrefab(string dir, string json)
    {
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, "test.json");
        File.WriteAllText(path, json);
        return dir;
    }

    // 4x4 prefab: the middle rows are open, with a left-edge mouth at (0,1).
    private const string Valid = """
    { "id": "prefab:test", "role": "mob", "tier": 1, "theme": "cave", "w": 4, "h": 4,
      "ground": [0,0,0,0, 351,351,351,351, 351,351,351,351, 0,0,0,0],
      "wall":   [356,356,356,356, 0,0,0,0, 0,0,0,0, 356,356,356,356],
      "decor":  [0,0,0,0, 0,0,0,0, 0,0,0,0, 0,0,0,0],
      "blocked":[1,1,1,1, 0,0,0,0, 0,0,0,0, 1,1,1,1],
      "mouths": [{ "x": 0, "y": 1 }],
      "chests": [],
      "spawnTheme": ["Rotworm"],
      "source": { "map": "otservbr", "x": 0, "y": 0, "z": 0 } }
    """;

    [Fact]
    public void loads_valid_prefab()
    {
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        PrefabRegistry.LoadFrom(WritePrefab(dir, Valid), name => name == "Rotworm");
        Assert.Single(PrefabRegistry.All);
        Assert.Equal("prefab:test", PrefabRegistry.All[0].Id);
        Assert.Single(PrefabRegistry.ForTier(1));
        Assert.Empty(PrefabRegistry.ForTier(2));
    }

    [Fact]
    public void missing_directory_loads_empty_registry()
    {
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        PrefabRegistry.LoadFrom(dir, name => true);
        Assert.Empty(PrefabRegistry.All);
    }

    [Fact]
    public void unknown_species_fails_fast()
    {
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        WritePrefab(dir, Valid);
        Assert.Throws<InvalidDataException>(() => PrefabRegistry.LoadFrom(dir, _ => false));
    }

    [Fact]
    public void disconnected_open_cells_fail_fast()
    {
        string dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string broken = Valid.Replace("\"blocked\":[1,1,1,1, 0,0,0,0, 0,0,0,0, 1,1,1,1]",
                                      "\"blocked\":[1,1,1,1, 0,1,1,0, 0,1,1,0, 1,1,1,1]");
        WritePrefab(dir, broken);
        Assert.Throws<InvalidDataException>(() => PrefabRegistry.LoadFrom(dir, name => true));
    }
}
