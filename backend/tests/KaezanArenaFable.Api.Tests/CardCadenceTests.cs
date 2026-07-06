using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Tests;

public class CardCadenceTests
{
    [Fact]
    public void OnlyFloorClearAndEchoSanctuaryOpenCardOffers()
    {
        Assert.True(GameConfig.OpensCardOffer(CardOfferBeat.FloorClear));
        Assert.True(GameConfig.OpensCardOffer(CardOfferBeat.EchoSanctuary));

        Assert.False(GameConfig.OpensCardOffer(CardOfferBeat.EliteKill));
        Assert.False(GameConfig.OpensCardOffer(CardOfferBeat.Chest));
        Assert.False(GameConfig.OpensCardOffer(CardOfferBeat.CursedChest));
    }

    [Fact]
    public void CardChoiceCapMatchesSparseBeatCadence()
    {
        Assert.Equal(4, GameConfig.MaxCardChoicesPerRun);
    }

    [Fact]
    public void EarlyRarityWeightsMakeRareAndEchoCardsStrategicBySecondPick()
    {
        var secondPickProgress = 1.0 / (GameConfig.MaxCardChoicesPerRun - 1);

        var common = GameConfig.CardRarityWeight(Cards.Common, secondPickProgress);
        var rare = GameConfig.CardRarityWeight(Cards.Rare, secondPickProgress);
        var echo = GameConfig.CardRarityWeight(Cards.Echo, secondPickProgress);

        Assert.True(rare >= common * 0.8);
        Assert.True(echo >= common * 0.5);
    }
}
