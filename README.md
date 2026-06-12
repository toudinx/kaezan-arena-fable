# Kaezan Arena Fable

Jogo browser **gacha + roguelike de dungeon**, recriação do kaezan-arena com a alma do Tibia:
movimentação livre em grid, mapas gerados proceduralmente, monstros/outfits/itens/FX **reais do Tibia**
(extraídos dos assets do Canary/OTClient do repo `kaezan`), e um lado gacha completo
(banners com pity, coleção de Kaelis, missões diárias, bestiário).

| Camada | Stack |
|---|---|
| Backend | ASP.NET Core 8 (C#) — engine server-authoritative com tick de 100ms via SignalR |
| Frontend | Angular 21 (standalone, signals) — renderer Canvas 2D com sprites do Tibia |
| Assets | Pipeline próprio de extração (protobuf appearances + sheets LZMA → PNG atlases) |
| Dados | Monstros convertidos dos `.lua` do Canary para JSON |

## Como rodar

```powershell
# 1. Backend (porta 5210)
cd backend/src/KaezanArenaFable.Api
dotnet run --urls http://localhost:5210

# 2. Frontend (porta 4200, proxy /api e /hub para o backend)
cd frontend
npm install
npx ng serve
```

Abra `http://localhost:4200`. A conta local é criada automaticamente em
`backend/src/KaezanArenaFable.Api/.data/account.json` com a Mirai (4★) e 4000 Kaeros.

## Controles (em run)

| Tecla | Ação |
|---|---|
| WASD / setas | Movimento (8 direções, deslize em diagonal bloqueada) |
| Espaço | Mirar no inimigo mais próximo |
| Clique | Mirar inimigo / interagir (baú, escada) |
| 1 / 2 / 3 / 4 (alias Q/E/R) | Skills do kit + Ultimate (gauge) |
| ESC | Sair da run (abandono = metade do ouro) |

## Fluidez e segurança da run

- Passos encadeiam sem pausa entre ticks, com buffer de direção e reenvio periódico do input
  enquanto uma tecla de movimento estiver pressionada.
- Monstros desviam de bloqueios e aglomerações, perdem aggro após distância/LOS prolongados e
  respeitam `staticAttackChance` para sustentar posições de ataque.
- Ofertas de card pausam o relógio da simulação; após 20s sem escolha, a primeira opção é aplicada.
- Atualizar a página preserva a run por até 60s e retoma o mesmo mapa, HP e estado do mundo.

## Loop de jogo

1. **Home Hub** — vitrine da Kaeli ativa, contratos diários, progresso de conta.
2. **Caçada** — 5 tiers de dungeon (gate por nível de conta). Cada run: 2 andares procedurais
   (salas de mobs com spawn por *budget* estilo Echo Spots, baús com chance de emboscada,
   escada para o covil) e um **boss do Tibia** no fundo (Rotworm Queen → Orc Warlord →
   Black Knight → Dragon Lord → Demon).
3. Durante a run: XP → level-ups oferecem **cards passivos** (escolha 1 de 3, max 3 stacks);
   loot clássico do Tibia dropa no chão e é coletado andando por cima.
4. **Recrutar** — banners com pity (4★ a cada 10; 5★ hard 80 / soft 65; 50/50 com garantia).
   Dupes viram Echo Shards → **Ascensão** (+8% stats; A2/A4 desbloqueiam os addons visuais
   do outfit do Tibia, visíveis em jogo).
5. **Mochila** — inventário com sprites reais + bestiário (ranks por abates = dano permanente).

## Estrutura

```
backend/src/KaezanArenaFable.Api/
  Domain/    GameConfig (TODAS as constantes), Waifus, Cards, GameData (monsters.json)
  Engine/    GameWorld (tick/movimento/IA/combate), DungeonGenerator, Rng, RunManager, Snapshot
  Meta/      AccountStore (JSON), GachaService, DailyService, RewardService
  Hubs/      GameHub (SignalR)
  Api/       MetaEndpoints (REST /api/v1)
frontend/src/app/
  core/      assets.service (atlases+recolor de outfit), renderer, game-client (SignalR), api.service
  pages/     home, hunt, recruit, kaelis, backpack, game (canvas + HUD)
  shell/     top bar com moedas e navegação
tools/
  AssetExtractor/     C#: things/1500 do otclient → PNG atlases + manifest.json
  convert-monsters/   Node+wasmoon: monster .lua do canary → monsters.json
docs/GDD.md           design e mapeamento kaezan-arena × Tibia
docs/DESIGN_NOTES.md  base de conhecimento de design (ideias do Tibia/Canary + Kaezan World)
docs/ROADMAP.md       fila de tasks pequenas/bem-especificadas (track Codex)
docs/FABLE_TRACK.md   fila de features complexas/cross-cutting (track Claude Fable 5)
```

## Documentos de planejamento

- **[docs/DESIGN_NOTES.md](docs/DESIGN_NOTES.md)** — referência de design: as ideias mais
  interessantes do Tibia/Canary/OTClient e das features do Kaezan World (dojos, boss posture,
  echo team, mastery, sealed reward) traduzidas para o Fable. É design, não código — a engine
  muda, o design permanece. Cada ideia aponta para onde virou trabalho.
- **[docs/ROADMAP.md](docs/ROADMAP.md)** — fila do **Codex**: 23 tasks bem-especificadas e
  bounded (conteúdo, UI/UX, juice, bugs). T-01..T-04 já concluídas.
- **[docs/FABLE_TRACK.md](docs/FABLE_TRACK.md)** — fila do **Claude Fable 5**: 5 features
  grandes, cross-cutting e sensíveis a determinismo (Echo Team, Maestria, Determinismo+Desafio
  Diário, Geração v2, Postura+Reações) — onde vale pagar o modelo premium.

## Pipeline de assets (re-rodar quando quiser mais conteúdo)

```powershell
# monstros (lua → json)
cd tools/convert-monsters && npm install && node convert.mjs

# sprites (requer o repo kaezan em C:\Kaezan\kaezan)
cd tools/AssetExtractor
dotnet run -- --things "C:\Kaezan\kaezan\otclient-4.0\data\things\1500" `
  --out "..\..\frontend\public\assets\tibia" `
  --config content-config.json `
  --monsters "..\..\backend\src\KaezanArenaFable.Api\Data\monsters.json" `
  --static-items "C:\xampp\htdocs\assets"
```

O extractor decodifica o formato moderno do Tibia (catalog-content.json + appearances.dat
protobuf + sheets BMP comprimidas com LZMA1 raw + header CIP) — mesmo algoritmo do
`spriteappearances.cpp` do OTClient. O manifest descreve patterns (direções, addons,
camada de máscara de cor) e o frontend recoloriza outfits em runtime com a paleta HSI
de 133 cores do Tibia.

`--static-items` é uma fonte opcional de importação: para objetos simples de um único frame,
o extractor normaliza os thumbnails antigos em células transparentes ancoradas no canto
inferior direito. Objetos animados, pilhas, terrenos e itens com patterns continuam vindo das
sheets completas. A saída é sempre copiada para `frontend/public/assets/tibia`; o jogo não
referencia caminhos externos ao repo. Omita o argumento quando essa extração local não existir.

## Invariantes (não quebre)

- **Backend é autoritativo.** O frontend nunca simula gameplay; só renderiza snapshots.
- **Determinismo**: mesma seed + mesmos comandos = mesma run (Rng xorshift próprio; nada de
  `Random` compartilhado no engine). Gacha usa `Random` não-determinístico de propósito.
- **Todas as constantes de simulação/meta em `GameConfig.cs`.**
- IDs estáveis: waifus `waifu:*`, cards `card:*`, banners `banner:*`. Não renomear.
- Assets do Tibia são **propriedade da CipSoft** — uso apenas em projeto privado/educacional.
