using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Tests;

public class GameConfigValidateTests
{
    // Fail-fast guard: the shipped constants must satisfy every invariant. If a future edit sets a
    // value out of range (e.g. a zero tick, a soft-pity past hard-pity, a role without tuning), this
    // test goes red — and so does the app at startup (Program.cs calls Validate()).
    [Fact]
    public void shipped_defaults_satisfy_every_invariant()
    {
        Exception? ex = Record.Exception(GameConfig.Validate);

        Assert.Null(ex);
    }

    [Fact]
    public void every_playable_role_has_tuning()
    {
        foreach (KaeliRole role in Enum.GetValues<KaeliRole>())
            Assert.True(GameConfig.Roles.ContainsKey(role), $"role {role} has no RoleTuning");
    }
}
