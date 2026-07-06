using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Tests;

public class MonsterCastRuleTests
{
    [Fact]
    public void NonBossConeIsCappedAtDragonWaveReach()
    {
        Assert.Equal(3, GameConfig.MonsterConeReach(isBoss: false, length: 8));
        Assert.Equal(2, GameConfig.MonsterConeReach(isBoss: false, length: 2));
    }

    [Fact]
    public void NonBossAoeRadiusIsCapped()
    {
        Assert.Equal(2, GameConfig.MonsterAoeRadius(isBoss: false, radius: 4));
        Assert.Equal(1, GameConfig.MonsterAoeRadius(isBoss: false, radius: 1));
    }

    [Fact]
    public void BossKeepsBigShapesAsSignature()
    {
        Assert.Equal(5, GameConfig.MonsterConeReach(isBoss: true, length: 8));
        Assert.Equal(4, GameConfig.MonsterAoeRadius(isBoss: true, radius: 6));
    }

    [Fact]
    public void SelfCenteredAoeOnlyFiresNearThePlayer()
    {
        Assert.True(GameConfig.SelfCenteredAoeInRange(dist: 4, radius: 2));
        Assert.False(GameConfig.SelfCenteredAoeInRange(dist: 5, radius: 2));
    }

    [Fact]
    public void RetunedProfilesRespectTheCaps()
    {
        foreach (MonsterBehaviorProfile profile in GameConfig.MonsterBehaviorProfiles)
        foreach (MonsterAttackPattern attack in profile.Attacks)
        {
            Assert.True(attack.Length <= GameConfig.MonsterConeReachCap,
                $"{profile.Id}: cone length {attack.Length} > cap");
            Assert.True(attack.Radius <= GameConfig.MonsterAoeRadiusCap,
                $"{profile.Id}: radius {attack.Radius} > cap");
        }
    }
}
