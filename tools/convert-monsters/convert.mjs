// Converts Canary monster .lua files into backend monsters.json.
// Runs each file in a sandboxed Lua VM (wasmoon): Game.createMonsterType is stubbed,
// unknown globals (COMBAT_*, CONST_ME_*, ...) resolve to their own name as a string.
import { LuaFactory } from "wasmoon";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const config = JSON.parse(readFileSync(join(here, "config.json"), "utf8"));

// ---- items.xml: name -> client/appearance id ----
function loadItemNames(itemsXmlPath) {
  const xml = readFileSync(itemsXmlPath, "utf8");
  const byName = new Map();
  const byId = new Map();
  const re = /<item\s+(?:fromid="(\d+)"\s+toid="(\d+)"|id="(\d+)")[^>]*?name="([^"]+)"/g;
  let m;
  while ((m = re.exec(xml)) !== null) {
    const id = m[3] ? Number(m[3]) : Number(m[1]);
    const name = m[4].toLowerCase();
    if (!byName.has(name)) byName.set(name, id);
    if (!byId.has(id)) byId.set(id, name);
  }
  return { byName, byId };
}

// ---- FX constant maps (from canary src/utils/utils_definitions.hpp) ----
const MAGIC_EFFECTS = {
  CONST_ME_DRAWBLOOD: 1, CONST_ME_LOSEENERGY: 2, CONST_ME_POFF: 3, CONST_ME_BLOCKHIT: 4,
  CONST_ME_EXPLOSIONAREA: 5, CONST_ME_EXPLOSIONHIT: 6, CONST_ME_FIREAREA: 7,
  CONST_ME_YELLOW_RINGS: 8, CONST_ME_GREEN_RINGS: 9, CONST_ME_HITAREA: 10,
  CONST_ME_TELEPORT: 11, CONST_ME_ENERGYHIT: 12, CONST_ME_MAGIC_BLUE: 13,
  CONST_ME_MAGIC_RED: 14, CONST_ME_MAGIC_GREEN: 15, CONST_ME_HITBYFIRE: 16,
  CONST_ME_HITBYPOISON: 17, CONST_ME_MORTAREA: 18, CONST_ME_SOUND_GREEN: 19,
  CONST_ME_SOUND_RED: 20, CONST_ME_POISONAREA: 21, CONST_ME_SOUND_YELLOW: 22,
  CONST_ME_SOUND_PURPLE: 23, CONST_ME_SOUND_BLUE: 24, CONST_ME_SOUND_WHITE: 25,
  CONST_ME_STUN: 32, CONST_ME_SLEEP: 33, CONST_ME_GROUNDSHAKER: 35, CONST_ME_HEARTS: 36,
  CONST_ME_FIREATTACK: 37, CONST_ME_ENERGYAREA: 38, CONST_ME_SMALLCLOUDS: 39,
  CONST_ME_HOLYDAMAGE: 40, CONST_ME_BIGCLOUDS: 41, CONST_ME_ICEAREA: 42,
  CONST_ME_ICETORNADO: 43, CONST_ME_ICEATTACK: 44, CONST_ME_STONES: 45,
  CONST_ME_SMALLPLANTS: 46, CONST_ME_CARNIPHILA: 47, CONST_ME_PURPLEENERGY: 48,
  CONST_ME_YELLOWENERGY: 49, CONST_ME_HOLYAREA: 50, CONST_ME_BIGPLANTS: 51,
  CONST_ME_GIANTICE: 53, CONST_ME_WATERSPLASH: 54, CONST_ME_PLANTATTACK: 55,
  CONST_ME_BATS: 67, CONST_ME_SMOKE: 68, CONST_ME_INSECTS: 69, CONST_ME_DRAGONHEAD: 70,
  CONST_ME_ORCSHAMAN: 71, CONST_ME_ORCSHAMAN_FIRE: 72, CONST_ME_THUNDER: 73,
  CONST_ME_BLACKSMOKE: 158, CONST_ME_REDSMOKE: 167, CONST_ME_YELLOWSMOKE: 168,
  CONST_ME_GREENSMOKE: 169, CONST_ME_PURPLESMOKE: 170, CONST_ME_CRITICAL_DAMAGE: 173,
  CONST_ME_MAGIC_POWDER: 182, CONST_ME_PIXIE_EXPLOSION: 184, CONST_ME_SLASH: 216,
  CONST_ME_BITE: 217,
};
const MISSILES = {
  CONST_ANI_SPEAR: 1, CONST_ANI_BOLT: 2, CONST_ANI_ARROW: 3, CONST_ANI_FIRE: 4,
  CONST_ANI_ENERGY: 5, CONST_ANI_POISONARROW: 6, CONST_ANI_BURSTARROW: 7,
  CONST_ANI_THROWINGSTAR: 8, CONST_ANI_THROWINGKNIFE: 9, CONST_ANI_SMALLSTONE: 10,
  CONST_ANI_DEATH: 11, CONST_ANI_LARGEROCK: 12, CONST_ANI_SNOWBALL: 13,
  CONST_ANI_POWERBOLT: 14, CONST_ANI_POISON: 15, CONST_ANI_INFERNALBOLT: 16,
  CONST_ANI_HUNTINGSPEAR: 17, CONST_ANI_ENCHANTEDSPEAR: 18, CONST_ANI_ROYALSPEAR: 21,
  CONST_ANI_SNIPERARROW: 22, CONST_ANI_ONYXARROW: 23, CONST_ANI_PIERCINGBOLT: 24,
  CONST_ANI_WHIRLWINDSWORD: 25, CONST_ANI_WHIRLWINDAXE: 26, CONST_ANI_WHIRLWINDCLUB: 27,
  CONST_ANI_ETHEREALSPEAR: 28, CONST_ANI_ICE: 29, CONST_ANI_EARTH: 30, CONST_ANI_HOLY: 31,
  CONST_ANI_SUDDENDEATH: 32, CONST_ANI_FLASHARROW: 33, CONST_ANI_FLAMMINGARROW: 34,
  CONST_ANI_SHIVERARROW: 35, CONST_ANI_ENERGYBALL: 36, CONST_ANI_SMALLICE: 37,
  CONST_ANI_SMALLHOLY: 38, CONST_ANI_SMALLEARTH: 39, CONST_ANI_EARTHARROW: 40,
  CONST_ANI_EXPLOSION: 41,
};
const DAMAGE_TYPES = {
  COMBAT_PHYSICALDAMAGE: "physical", COMBAT_ENERGYDAMAGE: "energy",
  COMBAT_EARTHDAMAGE: "earth", COMBAT_FIREDAMAGE: "fire", COMBAT_LIFEDRAIN: "lifedrain",
  COMBAT_MANADRAIN: "manadrain", COMBAT_HEALING: "healing", COMBAT_DROWNDAMAGE: "drown",
  COMBAT_ICEDAMAGE: "ice", COMBAT_HOLYDAMAGE: "holy", COMBAT_DEATHDAMAGE: "death",
};
const CONDITION_TYPES = {
  CONDITION_POISON: "poison", CONDITION_FIRE: "fire", CONDITION_ENERGY: "energy",
  CONDITION_BLEEDING: "bleed", CONDITION_CURSED: "curse", CONDITION_DAZZLED: "dazzle",
  CONDITION_FREEZING: "freeze", CONDITION_DROWN: "drown",
};

const BOOTSTRAP = readFileSync(join(here, "bootstrap.lua"), "utf8");

function toNum(v, fallback = 0) {
  const n = Number(v);
  return Number.isFinite(n) ? n : fallback;
}

function convertMonster(raw, items) {
  const out = {
    name: raw.__name,
    description: raw.description ?? "",
    experience: toNum(raw.experience),
    health: toNum(raw.health, 100),
    speed: toNum(raw.speed, 100),
    corpse: toNum(raw.corpse),
    outfit: {
      lookType: toNum(raw.outfit?.lookType),
      head: toNum(raw.outfit?.lookHead), body: toNum(raw.outfit?.lookBody),
      legs: toNum(raw.outfit?.lookLegs), feet: toNum(raw.outfit?.lookFeet),
      addons: toNum(raw.outfit?.lookAddons),
    },
    targetDistance: toNum(raw.flags?.targetDistance, 1),
    staticAttackChance: toNum(raw.flags?.staticAttackChance, 90),
    runOnHealth: toNum(raw.flags?.runHealth),
    armor: toNum(raw.defenses?.armor),
    defense: toNum(raw.defenses?.defense),
    mitigation: toNum(raw.defenses?.mitigation),
    isBoss: raw.flags?.rewardBoss === true,
    bestiaryClass: raw.Bestiary?.class ?? "",
    maxSummons: toNum(raw.summon?.maxSummons),
    summons: [],
    attacks: [],
    defenses: [],
    elements: {},
    loot: [],
    voices: [],
  };

  for (const s of raw.summon?.summons ?? []) {
    if (typeof s?.name !== "string") continue;
    out.summons.push({
      name: s.name,
      chance: toNum(s.chance, 10),
      intervalMs: toNum(s.interval, 2000),
      count: toNum(s.count, 1),
    });
  }

  for (const atk of raw.attacks ?? []) {
    const condType = CONDITION_TYPES[atk.condition?.type];
    const a = {
      kind: atk.name === "melee" ? "melee" : "spell",
      label: typeof atk.name === "string" ? atk.name : "combat",
      interval: toNum(atk.interval, 2000),
      chance: toNum(atk.chance, 100),
      range: toNum(atk.range, atk.name === "melee" ? 1 : 0),
      radius: toNum(atk.radius),
      length: toNum(atk.length),
      spread: toNum(atk.spread),
      target: atk.target === true,
      minDamage: Math.abs(toNum(atk.minDamage)),
      maxDamage: Math.abs(toNum(atk.maxDamage)),
      damageType: DAMAGE_TYPES[atk.type] ?? "physical",
      shootEffect: MISSILES[atk.shootEffect] ?? 0,
      areaEffect: MAGIC_EFFECTS[atk.effect] ?? 0,
      condition: condType
        ? {
            type: condType,
            totalDamage: Math.abs(toNum(atk.condition.totalDamage)),
            tickMs: toNum(atk.condition.interval, 2000),
            durationMs: toNum(atk.condition.duration),
          }
        : null,
      speedChange: toNum(atk.speedChange),
      durationMs: toNum(atk.duration),
      isHealing: DAMAGE_TYPES[atk.type] === "healing",
    };
    // no mana on the arena side: mana drain attacks have no equivalent
    if (a.damageType === "manadrain") continue;
    // keep anything with a gameplay payload; drop pure field attacks (firefield etc.)
    if (a.maxDamage > 0 || a.kind === "melee" || a.condition || a.speedChange !== 0 || a.isHealing)
      out.attacks.push(a);
  }

  // defenses array part: self-heals and self-haste (the monster's reactive kit)
  for (const d of raw.defenses?.__items ?? []) {
    const isHealing = DAMAGE_TYPES[d.type] === "healing";
    const speedChange = toNum(d.speedChange);
    if (!isHealing && speedChange === 0) continue;
    out.defenses.push({
      kind: isHealing ? "healing" : "speed",
      intervalMs: toNum(d.interval, 2000),
      chance: toNum(d.chance, 100),
      minValue: Math.abs(toNum(d.minDamage)),
      maxValue: Math.abs(toNum(d.maxDamage)),
      speedChange,
      durationMs: toNum(d.duration),
      areaEffect: MAGIC_EFFECTS[d.effect] ?? 0,
    });
  }

  for (const el of raw.elements ?? []) {
    const type = DAMAGE_TYPES[el.type];
    if (type && toNum(el.percent) !== 0) out.elements[type] = toNum(el.percent);
  }

  for (const entry of raw.loot ?? []) {
    let id = toNum(entry.id);
    let name = entry.name ?? (id ? items.byId.get(id) ?? "" : "");
    if (!id && typeof entry.name === "string") id = items.byName.get(entry.name.toLowerCase()) ?? 0;
    if (!id) continue;
    out.loot.push({
      itemId: id,
      name,
      chance: toNum(entry.chance, 100000), // canary chance base = 100000
      maxCount: toNum(entry.maxCount, 1),
    });
  }
  out.loot.sort((a, b) => b.chance - a.chance);
  out.loot = out.loot.slice(0, 8);

  for (const v of raw.voices?.__items ?? []) {
    if (typeof v === "object" && typeof v.text === "string") out.voices.push(v.text);
  }
  return out;
}

const items = loadItemNames(resolve(here, config.itemsXml));
const factory = new LuaFactory();
const results = [];

for (const rel of config.monsters) {
  const path = join(resolve(here, config.canaryMonsterDir), rel);
  const src = readFileSync(path, "utf8");
  const lua = await factory.createEngine();
  try {
    await lua.doString(BOOTSTRAP);
    await lua.doString(src);
    const json = await lua.doString(`return __tojson(__registered)`);
    const raw = JSON.parse(json);
    if (!raw || !raw.__name) throw new Error("no monster registered");
    const converted = convertMonster(raw, items);
    results.push(converted);
    const kit = [
      converted.attacks.some((a) => a.condition) ? "cond" : null,
      converted.attacks.some((a) => a.speedChange < 0) ? "slow" : null,
      converted.summons.length ? `summons:${converted.summons.length}` : null,
      converted.defenses.length ? `defenses:${converted.defenses.length}` : null,
    ].filter(Boolean).join(",");
    console.log(`ok: ${converted.name} (lookType ${converted.outfit.lookType}, hp ${converted.health}, ${converted.attacks.length} attacks, ${converted.loot.length} loot${kit ? ", " + kit : ""})`);
  } catch (err) {
    console.error(`FAIL ${rel}: ${err.message}`);
  } finally {
    lua.global.close();
  }
}

const outPath = resolve(here, config.output);
mkdirSync(dirname(outPath), { recursive: true });
writeFileSync(outPath, JSON.stringify(results, null, 2));
console.log(`\nwrote ${results.length} monsters to ${outPath}`);
