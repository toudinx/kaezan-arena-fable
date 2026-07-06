using System.Security.Cryptography;
using System.Text;
using KaezanArenaFable.Api.Domain;

namespace KaezanArenaFable.Api.Engine;

/// <summary>
/// FF-01 (bit-perfect replay): command recording + canonical state hash. Kept in a partial so the
/// replay machinery does not grow GameWorld.cs further. Everything here is OBSERVATION ONLY — it
/// must never mutate simulation state or consume <c>_rng</c>.
/// </summary>
public sealed partial class GameWorld
{
    // The helper profile string as passed to the constructor (commands can change the live helper
    // config afterwards, so the ctor input must be frozen separately for re-simulation).
    private readonly string? _initialHelperProfile;

    private readonly List<ReplayCommandEntry> _commandLog = [];
    private readonly List<ReplayHashEntry> _tickHashes = [];

    /// <summary>Intermediate hashes recorded every <see cref="GameConfig.ReplayHashEveryTicks"/>
    /// ticks, used by --replay-check to bisect the first divergent tick.</summary>
    public IReadOnlyList<ReplayHashEntry> TickHashes => _tickHashes;

    /// <summary>Called from <c>Apply</c> for every command the engine actually consumed. Commands
    /// discarded by the card-pause filter are never applied, so they are never recorded either.</summary>
    private void RecordReplayCommand(Command cmd) =>
        _commandLog.Add(new ReplayCommandEntry(TickCount, cmd.Kind, cmd.A, cmd.B, cmd.S));

    /// <summary>Called from <c>Tick</c> right before the snapshot is built. Recording stops once the
    /// run ends: RunManager keeps ticking a finished world (~50 grace ticks), and those must not
    /// pollute the hash timeline.</summary>
    private void RecordReplayTickHash()
    {
        if (Ended is null && TickCount % GameConfig.ReplayHashEveryTicks == 0)
            _tickHashes.Add(new ReplayHashEntry(TickCount, ComputeStateHash()));
    }

    /// <summary>Freezes the run into a self-contained replay. Call right after the tick that set
    /// <see cref="Ended"/> (before any grace ticks), so FinalTick/FinalHash pin that exact state.</summary>
    public ReplayFile BuildReplay() => new(
        ReplayFile.CurrentVersion,
        Seed, Mode, Waifu.Id, Ascension,
        Tier, EquipmentStats,
        Loadout.AffinityLevel, Loadout.Mastery, Loadout.Skin.Id,
        new Dictionary<string, long>(_bestiaryKills),
        _initialHelperProfile,
        _roles.ToDictionary(p => p.Key, p => p.Value),
        _biome,
        TickCount, ComputeStateHash(),
        _tickHashes.ToList(),
        _commandLog.ToList());

    /// <summary>
    /// SHA-256 over a canonical binary serialization of every mutable simulation field: actors in
    /// list order (list order is itself simulation state — FirstOrDefault scans depend on it),
    /// mutable map state, run progression, player/trait/helper state, the sim clock and the RNG
    /// stream position. Doubles are quantized (Math.Round, 6 decimals) so the hash tolerates
    /// sub-epsilon float noise across Debug/Release while still catching real divergence.
    /// Excluded on purpose: _events (visual, derived), MapDirty (resume sets it), _commands queue
    /// (transient transport, always empty between ticks).
    /// </summary>
    public string ComputeStateHash()
    {
        using var ms = new MemoryStream(16 * 1024);
        using (var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
        {
            // clock + rng
            w.Write(TickCount);
            w.Write(_simulationMs);
            var (s0, s1) = _rng.State;
            w.Write(s0); w.Write(s1);
            w.Write(_currentFloor);
            w.Write(_nextActorId);
            w.Write(_nextPoiId);

            // actors
            WriteActor(w, Player);
            w.Write(_monsters.Count);
            foreach (var m in _monsters) WriteActor(w, m);

            // mutable map state
            w.Write(_groundItems.Count);
            foreach (var g in _groundItems)
            {
                w.Write(g.Id); w.Write(g.Floor); w.Write(g.X); w.Write(g.Y);
                w.Write(g.ItemId); w.Write(g.Count);
            }
            w.Write(_pois.Count);
            foreach (var p in _pois)
            {
                w.Write(p.Id); w.Write(p.Kind); w.Write(p.Variant);
                w.Write(p.Floor); w.Write(p.X); w.Write(p.Y); w.Write(p.Used);
            }
            w.Write(_summons.Count);
            foreach (var s in _summons)
            {
                w.Write(s.Floor); w.Write(s.X); w.Write(s.Y); w.Write(s.Element); w.Write(s.Fx);
                w.Write(s.Radius); WriteQ(w, s.DamagePerPulse); w.Write(s.PulseMs);
                w.Write(s.NextPulseAtMs); w.Write(s.ExpireAtMs); w.Write(s.IsEchoSpectre);
                w.Write(s.Roams); w.Write(s.LeavesField); WriteQ(w, s.FieldPower);
                w.Write(s.FieldTickMs); w.Write(s.FieldLifeMs);
                w.Write(s.FieldSpreadChance); w.Write(s.FieldSpreadGenerations);
            }
            w.Write(_fields.Count);
            foreach (var f in _fields)
            {
                w.Write(f.Floor); w.Write(f.X); w.Write(f.Y); w.Write(f.Element); w.Write(f.Fx);
                WriteQ(w, f.DamagePerTick); WriteQ(w, f.SlowFactor); w.Write(f.SlowMs);
                w.Write(f.TickMs); w.Write(f.NextTickAtMs); w.Write(f.ExpireAtMs);
                w.Write(f.SpreadChance); w.Write(f.SpreadGenerationsLeft);
            }
            w.Write(_pendingStrikes.Count);
            foreach (var st in _pendingStrikes)
            {
                w.Write(st.Floor); w.Write(st.X); w.Write(st.Y); w.Write(st.AtMs);
                w.Write(st.Element); w.Write(st.Fx); WriteQ(w, st.Damage);
                w.Write(st.Radius); w.Write(st.RingInner); w.Write(st.StunMs);
                WriteQ(w, st.SlowFactor); w.Write(st.SlowMs);
                w.Write(st.DotTicks); w.Write(st.DotTickMs); WriteQ(w, st.DotPower);
                w.Write(st.LeavesField); WriteQ(w, st.FieldPower);
                w.Write(st.FieldRadius); w.Write(st.FieldTickMs); w.Write(st.FieldLifeMs);
                w.Write(st.FieldSpreadChance); w.Write(st.FieldSpreadGenerations);
                WriteQ(w, st.FieldSlowFactor); w.Write(st.FieldSlowMs);
                w.Write(st.IsDeathOrb); w.Write(st.StacksBurnMult); w.Write(st.DetonatesStaticMarks);
                WriteQ(w, st.SkillLifesteal); WriteQ(w, st.StaticChargeGain);
            }

            // run progression
            w.Write(_runLevel); w.Write(_runXp); w.Write(_gold); w.Write(_kills);
            WriteQ(w, _gauge);
            w.Write(_cards.Count);
            foreach (var (cardId, stacks) in _cards) { w.Write(cardId); w.Write(stacks); }
            var banned = _bannedCards.Order(StringComparer.Ordinal).ToList();
            w.Write(banned.Count);
            foreach (var b in banned) w.Write(b);
            w.Write(_pendingOffer is not null);
            if (_pendingOffer is not null)
            {
                w.Write(_pendingOffer.Count);
                foreach (var o in _pendingOffer) { w.Write(o.Id); w.Write(o.CurrentStacks); }
            }
            w.Write(_cardOfferStartedTick); w.Write(_queuedOffers); w.Write(_choicesOffered);
            w.Write(_offerBlessed); w.Write(_cardRerollsRemaining);
            w.Write(KillsBySpecies.Count);
            foreach (var (species, count) in KillsBySpecies) { w.Write(species); w.Write(count); }
            w.Write(ItemsLooted.Count);
            foreach (var it in ItemsLooted) { w.Write(it.ItemId); w.Write(it.Count); }
            w.Write(_potionCharges); w.Write(_potionReadyAtMs);
            w.Write(_chestsOpened); w.Write(_killsSinceChest);

            // player combat state
            foreach (var v in _skillReadyAtMs) w.Write(v);
            w.Write(_stanceId);
            w.Write(_autoAttackReadyAtMs);
            w.Write(_dashReadyAtMs); w.Write(_dashInvulnUntilMs);
            w.Write(_dashHasteUntilMs); WriteQ(w, _dashHasteFactor);

            // helper config (mutable via ToggleAutoHelper)
            w.Write(_autoHelperTargeting); w.Write(_autoHelperSkills); w.Write(_autoHelperUltimate);
            w.Write(_autoHelperAutoHeal); w.Write(_autoHelperHealPct);
            w.Write(_autoHelperNavMode); w.Write(_autoHelperAutoCards);
            w.Write(_autoHelperTargetPreference); w.Write(_autoHelperMovementMode);
            w.Write(_savedAutoHelperMovementMode); w.Write(_defaultAutoHelperMovementMode);
            w.Write(_trainingFreeCast);
            w.Write(_floorEnteredMs);
            w.Write(_mobLastX); w.Write(_mobLastY);
            w.Write(_helperMovementOverrideTargetId); w.Write(_manualTargetId);
            w.Write(_moveDirX); w.Write(_moveDirY);
            w.Write(_bufferedMoveDirX); w.Write(_bufferedMoveDirY);
            w.Write(_hasBufferedMoveDir); w.Write(_moveDirChangedAtMs);

            // buffs/conditions
            w.Write(_buffsUntilMs.Count);
            foreach (var (buff, until) in _buffsUntilMs) { w.Write(buff); w.Write(until); }
            WriteQ(w, _regenCarry);
            w.Write(_playerConditions.Count);
            foreach (var c in _playerConditions)
            {
                w.Write(c.Type); w.Write(c.DamagePerTick); w.Write(c.TicksLeft);
                w.Write(c.TickMs); w.Write(c.NextTickAtMs);
            }
            w.Write(_playerSlowUntilMs); WriteQ(w, _playerSlowFactor);

            // trait / automod / echo state
            w.Write(_comboTargetId); w.Write(_comboHits); w.Write(_comboExpireMs);
            WriteQ(w, _staticCharge);
            w.Write(_preyId); w.Write(_preyStartMs); w.Write(_preyHuntBonusUntilMs);
            w.Write(_traitHasteUntilMs); WriteQ(w, _traitHasteFactor);
            w.Write(_contagionNextJumpMs);
            w.Write(_rinBurnMultStacks); w.Write(_rinBurnMultNextDecayMs);
            w.Write(_cardDoubleStrikeHits);
            w.Write(_autoModKind ?? ""); w.Write(_autoModUntilMs);
            w.Write(_autoModChargesLeft); w.Write(_autoModMaxCharges); w.Write(_autoModResetOnKill);
            WriteQ(w, _echoShield); w.Write(_eloaSentenceStacks); w.Write(_preyId2);
            w.Write(_resolvingDeathOrb);

            // end state
            w.Write(Ended is not null);
            if (Ended is not null) { w.Write(Ended.Victory); w.Write(Ended.Reason); }
        }
        return Convert.ToHexString(SHA256.HashData(ms.ToArray())).ToLowerInvariant();
    }

    private static void WriteActor(BinaryWriter w, Actor a)
    {
        w.Write(a.Id); w.Write(a.IsPlayer); w.Write(a.Species?.Name ?? "");
        w.Write(a.Floor); w.Write(a.X); w.Write(a.Y); w.Write(a.FromX); w.Write(a.FromY);
        w.Write(a.StepStartMs); w.Write(a.StepDurMs); w.Write((int)a.Facing);
        w.Write(a.Hp); w.Write(a.MaxHp);
        w.Write(a.StunUntilMs);
        w.Write(a.AttackReadyAtMs.Length);
        foreach (var v in a.AttackReadyAtMs) w.Write(v);
        w.Write(a.NextWanderAtMs); w.Write(a.NextVoiceAtMs);
        w.Write(a.TargetId); w.Write(a.LastSawPlayerAtMs); w.Write(a.AggroOutOfRangeSinceMs);
        w.Write(a.ExposedUntilMs); w.Write(a.SappedUntilMs); w.Write(a.TauntedUntilMs);
        w.Write(a.IsBossActor); w.Write(a.IsElite); w.Write(a.IsMimic); w.Write(a.IsTrainingDummy);
        WriteQ(w, a.StatMult);
        w.Write(a.DefenseReadyAtMs.Length);
        foreach (var v in a.DefenseReadyAtMs) w.Write(v);
        w.Write(a.SummonReadyAtMs.Length);
        foreach (var v in a.SummonReadyAtMs) w.Write(v);
        w.Write(a.OwnerId);
        w.Write(a.HasteUntilMs); WriteQ(w, a.HasteFactor);
        w.Write(a.SlowUntilMs); WriteQ(w, a.SlowFactor);
        WriteQ(w, a.MonsterShield); w.Write(a.ShieldCastReadyAtMs);
        w.Write(a.SinStacks); w.Write(a.SinUntilMs);
        w.Write(a.DecayStacks); w.Write(a.DecayUntilMs);
        w.Write(a.FrostHits); w.Write(a.FrostUntilMs);
        w.Write(a.IsPrey); w.Write(a.HasStaticMark); w.Write(a.StaticMarkUntilMs);
        w.Write(a.Killed);
        w.Write(a.Dots.Count);
        foreach (var d in a.Dots)
        {
            w.Write(d.Element); w.Write(d.Fx); WriteQ(w, d.DamagePerTick);
            w.Write(d.TicksLeft); w.Write(d.TickMs); w.Write(d.NextTickAtMs);
        }
        WriteQ(w, a.PostureBaseMax); WriteQ(w, a.PostureMax); WriteQ(w, a.Posture);
        w.Write(a.PostureCycle); w.Write(a.StaggerUntilMs); WriteQ(w, a.StaggerMultiplier);
        w.Write(a.PostureLastHitMs); w.Write(a.PostureBonusReadyAtMs);
        w.Write(a.ElementMark); w.Write(a.ElementMarkUntilMs);
    }

    /// <summary>Quantized double: rounded to 6 decimals before hashing (see class remarks).</summary>
    private static void WriteQ(BinaryWriter w, double v) => w.Write(Math.Round(v, 6));
}
