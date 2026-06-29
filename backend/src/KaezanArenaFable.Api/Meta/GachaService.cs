using KaezanArenaFable.Api.Content;
using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Meta;

public sealed record BannerDef(string Id, string Name, string Description, string? FeaturedWaifuId);

public sealed record PullResult(
    string Kind, string? WaifuId, int? ItemId, int Count,
    string Name, string Title, int Rarity, bool IsNew,
    int ShardsGained, bool WasFeatured);

public sealed record PullResponse(
    List<PullResult> Results, long KaerosLeft,
    int PullsSinceFiveStar, int PullsSinceFourStar, bool FeaturedGuaranteed);

/// <summary>
/// Banner gacha with genshin-style pity: hard 5★ pity at 80, soft pity ramp from 65.
/// Non-5 pulls grant a random item while the curated equipment pool is provisional.
/// Featured 50/50 keeps its guarantee after losing one.
/// Dupes convert to per-waifu Echo Shards used for ascension (Tibia outfit addons).
/// </summary>
public sealed class GachaService(AccountStore store, ItemRegistry items, ContentStore content)
{
    private static readonly BannerDef StandardBanner =
        new("banner:standard", "Standard Summon", "Permanent banner featuring all Kaelis.", null);

    /// <summary>
    /// Stable banner ID for a waifu. Velvet keeps "banner:nightmare" for pity compatibility;
    /// others use "banner:{suffix}", e.g. "banner:rin".
    /// </summary>
    public static string BannerIdFor(string waifuId) =>
        waifuId == Waifus.FeaturedFiveStarId ? "banner:nightmare" : $"banner:{waifuId[6..]}";

    /// <summary>
    /// Active banners: featured waifus (from ContentStore) + standard banner at the end.
    /// </summary>
    public IReadOnlyList<BannerDef> GetBanners()
    {
        var result = new List<BannerDef>();
        foreach (var waifuId in content.ActiveBannerWaifuIds)
        {
            if (!Waifus.ById.TryGetValue(waifuId, out var waifu)) continue;
            result.Add(new BannerDef(
                BannerIdFor(waifuId),
                waifu.Title,
                $"Promotional banner: increased rate for {waifu.Name}, {waifu.Title}.",
                waifuId));
        }
        result.Add(StandardBanner);
        return result;
    }

    public PullResponse Pull(string bannerId, int count)
    {
        if (count is not (1 or 10)) throw new ArgumentException("count must be 1 or 10");
        var banner = GetBanners().FirstOrDefault(b => b.Id == bannerId)
                     ?? throw new ArgumentException("unknown banner");
        var cost = GameConfig.PullCostKaeros * count;

        return store.Mutate(state =>
        {
            if (state.Kaeros < cost) throw new InvalidOperationException("insufficient Kaeros");
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

    private PullResult PullOne(AccountState state, BannerDef banner, PityState pity, Random rng)
    {
        pity.TotalPulls++;
        pity.PullsSinceFiveStar++;
        pity.PullsSinceFourStar = 0;

        var fiveRate = GameConfig.FiveStarBaseRate;
        if (pity.PullsSinceFiveStar > GameConfig.FiveStarSoftPityStart)
            fiveRate += (pity.PullsSinceFiveStar - GameConfig.FiveStarSoftPityStart) * GameConfig.FiveStarSoftPityRamp;

        var isFiveStar = pity.PullsSinceFiveStar >= GameConfig.FiveStarHardPity || rng.NextDouble() < fiveRate;
        if (!isFiveStar)
            return PullItem(state, rng);

        var wasFeatured = false;
        WaifuDef picked;
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

        return new PullResult("waifu", picked.Id, null, 0,
            picked.Name, picked.Title, picked.Rarity, isNew, shards, wasFeatured);
    }

    private PullResult PullItem(AccountState state, Random rng)
    {
        var pool = items.All.Values
            .Where(item =>
                item.IsAuthored
                && item.ItemId >= GameConfig.AuthoredItemIdBase
                && item.ItemId > 0
                && item.Tag != GameConfig.AuthoredItemTagRelic
                && !string.IsNullOrWhiteSpace(item.Name))
            .OrderBy(item => item.ItemId)
            .ToList();
        if (pool.Count == 0)
            pool = items.All.Values
                .Where(item => item.ItemId > 0 && !string.IsNullOrWhiteSpace(item.Name))
                .OrderBy(item => item.ItemId)
                .ToList();
        if (pool.Count == 0)
            throw new InvalidOperationException("empty item catalog");

        var picked = pool[rng.Next(pool.Count)];
        if (state.Inventory.TryGetValue(picked.ItemId, out var stack))
            stack.Count++;
        else
            state.Inventory[picked.ItemId] = new InventoryStack
            {
                ItemId = picked.ItemId,
                Name = picked.Name,
                Count = 1
            };

        return new PullResult("item", null, picked.ItemId, 1,
            picked.Name, "Item obtained", 3, false, 0, false);
    }

    public object Ascend(string waifuId)
    {
        var waifu = Waifus.ById.GetValueOrDefault(waifuId)
                    ?? throw new ArgumentException("unknown waifu");
        return store.Mutate(state =>
        {
            if (!state.OwnedWaifus.Contains(waifuId)) throw new InvalidOperationException("not owned");
            var current = state.Ascension.GetValueOrDefault(waifuId);
            if (current >= GameConfig.AscensionShardCost.Length)
                throw new InvalidOperationException("maximum ascension");
            var cost = GameConfig.AscensionShardCost[current];
            var have = state.Shards.GetValueOrDefault(waifuId);
            if (have < cost) throw new InvalidOperationException($"insufficient shards ({have}/{cost})");
            state.Shards[waifuId] = have - cost;
            state.Ascension[waifuId] = current + 1;
            return new { waifuId, ascension = current + 1, shardsLeft = state.Shards[waifuId], name = waifu.Name };
        });
    }
}
