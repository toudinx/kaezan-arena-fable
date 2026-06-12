# Kaezan Arena Fable — GDD & Propostas de Melhorias

> Documento de design da recriação do **kaezan-arena** com a engine/conteúdo do **Tibia/Canary**
> (documentado em `C:\Kaezan\kaezan\mapping`). Registra o que veio de cada lado, o que foi
> melhorado, e o roadmap de melhorias futuras.

---

## 1. Tese

O kaezan-arena tem um core loop divertido (arena, kits, cards, contas, dailies), mas:

- O player é **fixo no tile (3,3)** — sem movimento, sem exploração, sem mapa.
- O **Recruit (gacha) é um placeholder vazio** — a fantasia de coleção não existe ainda.
- Os assets (0x72 dungeon pack) são genéricos e limitados.

O Tibia/Canary resolve exatamente esses três buracos: tem **20+ anos de assets**
(1399 outfits, 40k+ itens, 201 efeitos, 56 projéteis), uma **linguagem visual de mundo**
(caverna, loot no chão, corpses, nomes verdes) e **dados prontos de monstros**
(stats, ataques com FX, resistências elementais, loot tables, vozes).

**Fable = corpo do Tibia + coração gacha do kaezan-arena.**

---

## 2. O que veio de onde

### Do kaezan-arena (mantido e melhorado)

| Sistema | Origem | Melhoria no Fable |
|---|---|---|
| Backend C# + frontend Angular | igual | SignalR em vez de HTTP step-loop (tempo real de verdade) |
| `ArenaConfig` com todas as constantes | igual | `GameConfig.cs`, mesma disciplina |
| Conta JSON local + determinismo por seed | igual | Rng xorshift próprio, seed por run exposta |
| Kits Q/W/E/R + Ultimate com gauge | igual | Skills viraram **data-driven por shape** (single/beam/nova/area/cone/buff) — adicionar skill não exige switch novo |
| Cards passivos em level-up (1 de 3, 3 stacks) | igual | Pool rebalanceado (12 cards) + ofertas enfileiradas |
| Daily Contracts determinísticos por dia UTC | igual | Progresso alimentado pelo resultado da run (kills/clears/baús/ouro) |
| Bestiário com ranks | igual | Rank dá +1% dano permanente por espécie (account-wide) |
| Bosses com identidade | igual | Bosses são monstros **reais do Tibia** com stats/loot/FX originais |
| Mimics | adaptado | Baús têm 25% de **emboscada** (spawn de mobs estilo Tibia) |
| Personagens Mirai/Sylwen/Velvet | mantidas | Continuidade de elenco, agora com outfits do Tibia |

### Do Tibia/Canary (novo no Fable)

| Sistema | Fonte no repo kaezan | Implementação |
|---|---|---|
| Movimentação em grid 8-dir com velocidade | engine Canary (speed/friction) | Fórmula `1000·friction/speed`, diagonal 1.5×, deslize em parede |
| Sprites de outfits/itens/FX/missiles | `otclient-4.0/data/things/1500` | `tools/AssetExtractor` (LZMA+protobuf → atlases PNG) |
| Recolor de outfit (head/body/legs/feet) | `outfit.cpp` (paleta HSI 133 cores) | Reimplementado em TS; máscara layer 2 multiplicada |
| Addons de outfit | sistema de addons | **Recompensa de Ascensão do gacha** (A2 = addon 1, A4 = addon 2) |
| Monstros com ataques/elementos/loot | `data-otservbr-global/monster/*.lua` | `tools/convert-monsters` (wasmoon) → 29 espécies |
| FX de magias (`CONST_ME_*`) e projéteis (`CONST_ANI_*`) | `utils_definitions.hpp` | IDs reais usados pelas skills das waifus e ataques dos mobs |
| Spawn procedural por budget (custo/peso/tier) | `changes/features/echo_spots` | Salas de mobs com budget escalado por tier e tamanho da sala |
| Loot no chão + corpses + coleta andando | gameplay clássico | Corpse real do monstro (`monster.corpse`) com decay |
| Vozes de monstros ("Grrr", etc.) | `monster.voices` | Balões de fala amarelos no canvas |
| Line of sight | engine | Bresenham: sem aggro/tiro através de parede |
| Tiles de caverna (dirt floor/wall 351-367) | `items.xml` (ids clássicos) | Gerador procedural pinta chão/paredes/decoração |

### Novo (nem um nem outro)

- **Gacha completo**: 2 banners, rates 0.8%/6%, soft pity 65+, hard pity 80, 4★ a cada 10,
  50/50 com garantia, reveal animado por raridade, dupes → shards → ascensão.
- **Dungeons multi-andar**: escada para o covil do boss (sensação de "descer a caverna" do Tibia).
- **13 waifus** em 3 raridades, cada uma com outfit clássico do Tibia colorido por identidade.

---

## 3. Sistemas (estado atual)

### Run (tier 1–5)
- 2 andares procedurais (40×40 e 30×30): salas + corredores L com loop extra, papel por sala
  (entrada/mob/tesouro/escada/boss). Seed determinística.
- Spawn: budget `14 × (1 + 0.55·(tier−1))` ajustado pelo tamanho da sala; comum custa 2, elite 5,
  25% de chance de elite quando o budget permite.
- Boss room no andar 2 com guardas elite; matar o boss = vitória + recompensas
  (ouro, Kaeros 120+40/tier, XP de conta). Morte/abandono = metade do ouro.
- Tuning de dano: dano cru do Tibia × 0.35 (mobs), armor/resistências elementais respeitados,
  bestiary rank somado.

### Gacha & coleção
- Custo 160 Kaeros/pull. Dailies pagam ~100/dia + ouro; vitórias pagam Kaeros.
- Ascensão A1–A6 (+8% stats cada): shards de dupe (3★=5, 4★=20, 5★=50);
  custos 10/15/25/40/60/80. A2/A4 = addons visuais.

### Meta
- Conta Lv 1–100 (gates dos tiers: 1/4/8/14/22).
- 3 contratos diários determinísticos (kill espécie / limpar tier / abrir baús / saquear ouro).
- Inventário com venda por ouro; bestiário com ranks 10/50/100/250.

---

## 4. Roadmap de melhorias propostas (próximos passos)

Ordenado por valor/custo, com a fonte de inspiração no repo kaezan:

1. **Sealed Reward / Echo Cache** (`changes/features/sealed_reward`, `echo_cache`): baú selado
   pós-boss com reveal item-a-item e 1 reroll — gacha dentro da run. Alto impacto, baixo custo.
2. **Boss Posture / Echo Break** (`changes/features/boss_posture`): barra de postura no boss;
   quebrar = stun + janela de dano. Dá profundidade ao fight sem IA nova.
3. **Mais biomas visuais**: crypt (stone wall 1112+, undead), lava lair (tiles 727-730 já
   extraídos) — o gerador já suporta tilesets por tier, falta só curadoria de ids.
4. **Imbuements/Forge lite** (`canary/systems/economy.md`): usar o loot do Tibia como material
   para socketar bônus elementais em waifus — dá propósito à Mochila além de vender.
5. **Pet/Companion expedition** (`changes/features/pet_expedition`, `companions`): expedições
   idle que pagam materiais — waifus fora da equipe trabalham.
6. **Raids/eventos de mundo** (`canary/systems/events.md`): modificadores diários de dungeon
   (elemento do dia já existe no kaezan-arena; trazer de volta com FX do Tibia).
7. **Condições do Tibia**: poison/burn/freeze como DoT (dados já vêm nos `.lua` convertidos,
   hoje ignorados pelo converter).
8. **Outfit shop / montarias**: extractor já lê mounts (patternY) — montaria como drop raro de boss.
9. **Replay determinístico** (kaezan-arena tem): gravar comandos+seed e reproduzir runs.
10. **Áudio**: o Canary tem ids de som por monstro (`monster.sounds`) — mapear para web audio.

### Dívidas técnicas conhecidas

- `preview_screenshot`/rAF: em aba não-visível o canvas congela (rAF) — ok para uso real,
  mas testes headless usam o hook `window.__kaezanRenderer`.
- Orientação das paredes usa só 3 variantes (pole/horizontal/vertical) — cantos dedicados
  (358-367) melhorariam o visual.
- Monsters empilham em corredores (IA greedy sem desvio) — A* leve por sala resolveria.
- `wasMovingWithW` removido: skills em 1/2/3/4 (Q/E/R como alias); W é só movimento.
- Cards de oferta não pausam o jogo (decisão de design: action roguelike) — revisar se ficar punitivo.
