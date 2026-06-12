using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Meta;

public sealed record BannerDef(string Id, string Name, string Description, string? FeaturedWaifuId);

public sealed record PullResult(
    string WaifuId, string Name, string Title, int Rarity, bool IsNew,
    int ShardsGained, bool WasFeatured);

public sealed record PullResponse(
    List<PullResult> Results, long KaerosLeft,
    int PullsSinceFiveStar, int PullsSinceFourStar, bool FeaturedGuaranteed);

/// <summary>
/// Banner gacha with genshin-style pity: hard 5★ pity at 80, soft pity ramp from 65,
/// 4★ guaranteed every 10, featured 50/50 with guarantee after losing one.
/// Dupes convert to per-waifu Echo Shards used for ascension (Tibia outfit addons).
/// </summary>
public sealed class GachaService(AccountStore store)
{
    public static readonly IReadOnlyList<BannerDef> Banners =
    [
        new("banner:nightmare", "Ecos do Pesadelo", "Banner promocional: taxa aumentada para Velvet, o Eco do Pesadelo.", Waifus.FeaturedFiveStarId),
        new("banner:standard", "Convocação Padrão", "Banner permanente com todas as Kaelis.", null),
    ];

    public PullResponse Pull(string bannerId, int count)
    {
        if (count is not (1 or 10)) throw new ArgumentException("count deve ser 1 ou 10");
        var banner = Banners.FirstOrDefault(b => b.Id == bannerId)
                     ?? throw new ArgumentException("banner desconhecido");
        var cost = GameConfig.PullCostKaeros * count;

        return store.Mutate(state =>
        {
            if (state.Kaeros < cost) throw new InvalidOperationException("Kaeros insuficiente");
            state.Kaeros -= cost;

            if (!state.Pity.TryGetValue(banner.Id, out var pity))
                state.Pity[banner.Id] = pity = new PityState();

            var rng = new Random();
            var results = new List<PullResult>(count);
            for (var i = 0; i < count; i++)
                results.Add(PullOne(state, banner, pity, rng));

            return new PullResponse(results, state.Kaeros,
                pity.PullsSinceFiveStar, pity.PullsSinceFourStar, pity.FeaturedGuaranteed);
        });
    }

    private static PullResult PullOne(AccountState state, BannerDef banner, PityState pity, Random rng)
    {
        pity.TotalPulls++;
        pity.PullsSinceFiveStar++;
        pity.PullsSinceFourStar++;

        var fiveRate = GameConfig.FiveStarBaseRate;
        if (pity.PullsSinceFiveStar > GameConfig.FiveStarSoftPityStart)
            fiveRate += (pity.PullsSinceFiveStar - GameConfig.FiveStarSoftPityStart) * GameConfig.FiveStarSoftPityRamp;

        int rarity;
        if (pity.PullsSinceFiveStar >= GameConfig.FiveStarHardPity || rng.NextDouble() < fiveRate)
            rarity = 5;
        else if (pity.PullsSinceFourStar >= GameConfig.FourStarPity || rng.NextDouble() < GameConfig.FourStarBaseRate)
            rarity = 4;
        else
            rarity = 3;

        var wasFeatured = false;
        WaifuDef picked;
        if (rarity == 5)
        {
            pity.PullsSinceFiveStar = 0;
            var fivePool = Waifus.All.Where(w => w.Rarity == 5).ToList();
            if (banner.FeaturedWaifuId is not null)
            {
                if (pity.FeaturedGuaranteed || rng.NextDouble() < 0.5)
                {
                    picked = Waifus.ById[banner.FeaturedWaifuId];
                    wasFeatured = true;
                    pity.FeaturedGuaranteed = false;
                }
                else
                {
                    var offPool = fivePool.Where(w => w.Id != banner.FeaturedWaifuId).ToList();
                    picked = offPool[rng.Next(offPool.Count)];
                    pity.FeaturedGuaranteed = true;
                }
            }
            else
            {
                picked = fivePool[rng.Next(fivePool.Count)];
            }
        }
        else
        {
            if (rarity == 4) pity.PullsSinceFourStar = 0;
            var pool = Waifus.All.Where(w => w.Rarity == rarity).ToList();
            picked = pool[rng.Next(pool.Count)];
        }

        var isNew = !state.OwnedWaifus.Contains(picked.Id);
        var shards = 0;
        if (isNew)
        {
            state.OwnedWaifus.Add(picked.Id);
        }
        else
        {
            shards = GameConfig.DupeShards[picked.Rarity];
            state.Shards[picked.Id] = state.Shards.GetValueOrDefault(picked.Id) + shards;
        }

        return new PullResult(picked.Id, picked.Name, picked.Title, picked.Rarity, isNew, shards, wasFeatured);
    }

    public object Ascend(string waifuId)
    {
        var waifu = Waifus.ById.GetValueOrDefault(waifuId)
                    ?? throw new ArgumentException("waifu desconhecida");
        return store.Mutate(state =>
        {
            if (!state.OwnedWaifus.Contains(waifuId)) throw new InvalidOperationException("não possuída");
            var current = state.Ascension.GetValueOrDefault(waifuId);
            if (current >= GameConfig.AscensionShardCost.Length)
                throw new InvalidOperationException("ascensão máxima");
            var cost = GameConfig.AscensionShardCost[current];
            var have = state.Shards.GetValueOrDefault(waifuId);
            if (have < cost) throw new InvalidOperationException($"shards insuficientes ({have}/{cost})");
            state.Shards[waifuId] = have - cost;
            state.Ascension[waifuId] = current + 1;
            return new { waifuId, ascension = current + 1, shardsLeft = state.Shards[waifuId], name = waifu.Name };
        });
    }
}
