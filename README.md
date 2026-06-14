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

Abra `http://localhost:4200`. Sem configuração de banco, a conta local é criada automaticamente
em `backend/src/KaezanArenaFable.Api/.data/account.json` com a Mirai (4★) e 4000 Kaeros.

O painel `http://localhost:4200/admin` traz um bestiário visual com abas de monstros e bosses.
Cada tier pode receber muitos mobs comuns, um grupo menor de elites e um boss; salvar persiste a
composição em `.data/content/tiers.json` e afeta somente as próximas runs.

A aba **Monstros** cria conteúdo autoral sem copiar os stats do Canary. Cada criatura escolhe
uma aparência, power tier, função (`common|elite|boss`), comportamento curado e elemento ofensivo.
Os presets de HP/dano/velocidade/cadência são pontos de partida e continuam editáveis; fraquezas
e resistências são independentes por elemento, sem relação automática de pedra-papel-tesoura.
Monstros novos usam IDs imutáveis `monster:*` e são persistidos em
`.data/content/monsters.json`. As espécies de `monsters.json` permanecem somente como placeholders
legados durante a substituição gradual.

O editor separa as três responsabilidades em colunas: a biblioteca visual Canary (somente leitura),
a configuração Kaezan e os monstros autorais salvos. O catálogo visual contém 1.542 definições Lua
deduplicadas em 758 outfits, com filtros de monstro/boss, classe e placeholder legado. Monstros
Kaezan podem ser reabertos, duplicados e excluídos; a exclusão é recusada enquanto a criatura ainda
estiver referenciada por alguma dungeon.

A aba **Kaelis** é o **Outfit Studio**: cria *skins* autorais para as Kaelis do roster, no espírito
da janela de outfit do Tibia. A biblioteca classifica os lookTypes em **Feminino / Masculino /
Monstros / Bosses / Todos** (com nome real e contadores por categoria): os outfits de jogador vêm de
`assets/tibia/outfit-catalog.json` (gerado de `outfits.xml` do Canary — nome + gênero por lookType,
248 entradas; a biblioteca mostra os que estão extraídos no manifesto), e monstros/bosses das
aparências nomeadas do Canary. Assim uma Kaeli pode vestir tanto um outfit de jogador (masculino ou
feminino) quanto o visual de um monstro ou boss. A montaria fica num seletor à parte no estúdio
(montarias não entram na lista de outfits). Recolorizam-se as quatro regiões (cabeça/corpo/pernas/
pés) com a paleta HSI de
133 cores, ativam-se os addons 1/2 e o preview anima/gira a Kaeli em tempo real. Cada skin é
atribuída a uma Kaeli e a uma regra de desbloqueio (padrão/afinidade/ouro/Kaeros). As skins são
persistidas em `.data/content/kaeli-skins.json` e mescladas ao roster estático pelo `KaeliRegistry`
(catálogo, seleção/compra de skin e sanitização passam a enxergá-las), ficando imediatamente
equipáveis no Hub e dentro das runs. Addons e montaria fixados na skin sobrescrevem o padrão
(addons por ascensão, montaria por equipamento).

### Persistência MySQL opcional

Para usar o MySQL/MariaDB do XAMPP, configure a connection string antes de iniciar o backend:

```powershell
$env:ConnectionStrings__KaezanFable = `
  "Server=127.0.0.1;Port=3306;Database=kaezan_fable;User=root;Password=;"
dotnet run --urls http://localhost:5210
```

O backend cria somente o banco separado `kaezan_fable`, aplica as migrations do EF Core e, se
ainda não houver conta no banco, importa `.data/account.json` uma única vez. Uma connection string
apontando para outro database (inclusive `otservbr-global`) é recusada antes da conexão.

## Controles (em run)

| Tecla | Ação |
|---|---|
| WASD / setas | Movimento (8 direções, deslize em diagonal bloqueada) |
| Espaço | Mirar no inimigo mais próximo |
| Clique | Mirar inimigo / interagir (baú, escada) |
| 1 / 2 / 3 / 4 (aliases Q/E) | Slots 1-4 do kit da classe |
| R | Ultimate da classe (gauge) |
| Tab | Alterna a postura elemental (quando a classe possui duas) |
| ESC | Sair da run (abandono = metade do ouro) |

## Fluidez e segurança da run

- Passos encadeiam sem pausa entre ticks, com buffer de direção e reenvio periódico do input
  enquanto uma tecla de movimento estiver pressionada.
- Monstros desviam de bloqueios e aglomerações, perdem aggro após distância/LOS prolongados e
  respeitam `staticAttackChance` para sustentar posições de ataque.
- **Kit real do Canary** (T-53): cada espécie executa o kit do seu `.lua` — condições viram DoT
  no player (veneno/fogo/energia, com chip no HUD, FX e cor de dano por tipo), ataques `speed`
  aplicam lentidão, invocadores summonam de verdade (Necromancer → Ghoul/Ghost/Mummy, Demon →
  Fire Elemental, respeitando `maxSummons` + orçamento global; summons dão XP mas não loot),
  curandeiros se curam (capado a 10% do HP máx por proc), `defenses` de haste aceleram o monstro
  e espécies com `runHealth` fogem com vida baixa. O card `Antídoto` reduz dano de condições.
- O catálogo tem 62 espécies do Tibia distribuídas pelos cinco tiers, com pelo menos 5 comuns e
  3 elites por dungeon; os sprites, corpses, loot e kits vêm do pipeline Canary.
- Ofertas de card pausam o relógio da simulação; após 20s sem escolha, a primeira opção é aplicada.
- Atualizar a página preserva a run por até 60s e retoma o mesmo mapa, HP e estado do mundo.

## Postura de boss e reações elementais (F-E)

- **Postura (Echo Break).** Todo boss tem uma segunda barra (dourada, sob o HP). Acertá-lo enche
  a postura — *skills* pressionam mais que auto-attack, e bater no **elemento fraco** (resist < 0)
  quebra mais rápido. Cheia → **Echo Break**: o boss fica atordoado e o dano recebido é
  multiplicado por ciclo (`2.5× → 3.5× → 5× → 6.5×`), com um bônus por hit de % do HP máx do boss
  (com cooldown interno anti multi-hit). Ao fim do stagger o ciclo sobe e a postura volta maior;
  parar de bater faz a postura **decair**, então é preciso pressão sustentada. A janela de break é
  o momento de despejar o burst (guardar a ultimate vale a pena).
- **Reações elementais.** Aplicar um elemento **marca** o alvo (ícone colorido sobre o mob); um
  segundo elemento diferente dispara uma **reação** com FX e dano (uma fração do hit, nunca um
  multiplicador explosivo). A matriz é data-driven em `Domain/ElementReactions.cs`: Gelo+Fogo =
  **Estilhaço** (dano em área), Gelo+Terra = **Permafrost** (lentidão), Energia+Fogo =
  **Sobrecarga** (área), Energia+Gelo = **Supercondução** (atordoa), Fogo+Terra = **Detonação**,
  Sagrado+Morte = **Aniquilação**. As reações premiam alternar a postura (`Tab`) e, no futuro,
  times de elementos complementares (Echo Team).

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
5. **Kaelis (profundidade)** — cada Kaeli tem **trait de assinatura** (passiva única no engine),
   **afinidade** 1-10 (XP por runs com ela ativa + **presentes** — itens da Mochila, favoritos
   ×2, máx. 3/dia; níveis destravam **ecos de memória** (lore), Kaeros, skins e +1% ATK/HP por
   nível), **skins por outfit** (padrão / afinidade / compradas com ouro ou Kaeros — a skin em
   uso aparece no Hub e dentro das runs) e a **Maestria de Eco**: árvore de 3 ramos
   (Ofensiva/Defensiva/Eco) com pontos por run (vitória +3 / derrota +1) e respec por ouro.
6. **Mochila** — inventário com sprites reais + bestiário (ranks por abates = dano permanente).
   Itens são vendidos pelos preços reais dos NPCs do Tibia; itens sem comprador valem 5 ouro.
   Loot equipável exibe os atributos do Tibia e pode ser colocado, por Kaeli, nos slots
   `helmet`, `armor`, `weapon`, `necklace`, `ring` e `mount`.
7. **Equipamento** — o paperdoll da página Kaelis troca itens por clique. Os bônus são congelados
   ao iniciar a run e aparecem no HUD; montarias raras de boss dão HP/velocidade e também mudam
   o visual da Kaeli no mundo.

## Kaelis: roster enxuto e profundo

Refundação 2026-06-12: o roster foi cortado de 13 para **9 Kaelis** (3 por raridade), cada uma
com trait de assinatura, personalidade, 4 ecos de memória (lore por afinidade), presentes
favoritos e 2-3 skins. Tessa, Nyx, Lyra e Rosa saíram (contas antigas recebem 600 Kaeros por
Kaeli removida via sanitização automática no boot). Kaela foi promovida a 5★.

Cada Kaeli usa o kit completo de uma das quatro classes canônicas do Kaezan World:

| Classe | Posturas | Kaelis | Traits |
|---|---|---|---|
| Warrior | Physical (fixa) | Mira 3★, Mirai 4★, Kaela 5★ | Coração Valente (DR com HP baixo) · Instinto de Matilha (+dano cercada) · Última Muralha (-12% dano) |
| Sentinel | Holy ↔ Physical | Wren 3★, Aurora 5★ | Olho de Águia (+crit à distância) · Luz Purificadora (+dano em undead) |
| Shaman | Ice ↔ Earth | Sage 3★, Sylwen 4★ | Seiva Vital (skills curam) · Mordida do Norte (gelo aplica slow) |
| Wizard | Energy ↔ Fire | Ember 4★, Velvet 5★ | Combustão (gauge +30%) · Fome do Abismo (+dano em alvos <30% HP) |

Os cooldowns pertencem aos slots 1-4 e continuam correndo ao trocar de postura; alternar com
`Tab` não reseta habilidades. A página Kaelis (abas Perfil / Skins / Maestria / Equipamento)
permite visualizar os dois kits elementais, presentear, trocar skins e gastar pontos de maestria.

## Estrutura

```
backend/src/KaezanArenaFable.Api/
  Domain/    GameConfig (TODAS as constantes), Waifus (roster+traits+skins+lore), Mastery
             (árvores de maestria), Cards, GameData (monsters.json)
  Engine/    GameWorld (tick/movimento/IA/combate), DungeonGenerator, Rng, RunManager, GameDtos
  Meta/      AccountStore (JSON/MySQL), GachaService, KaeliService (presentes/skins/maestria),
             AccountSanitizer, DailyService, RewardService
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
# placeholders legados (lua → json) + catálogo amplo de aparências para o admin
cd tools/convert-monsters
npm install
node convert.mjs
npm run scan:appearances

# sprites (requer o repo kaezan em C:\Kaezan\kaezan)
cd tools/AssetExtractor
dotnet run -- --things "C:\Kaezan\kaezan\otclient-4.0\data\things\1500" `
  --out "..\..\frontend\public\assets\tibia" `
  --config content-config.json `
  --equipment `
  --monsters "..\..\backend\src\KaezanArenaFable.Api\Data\monsters.json" `
  --monster-appearances "..\..\backend\src\KaezanArenaFable.Api\Data\monster-appearances.json" `
  --items-out "..\..\backend\src\KaezanArenaFable.Api\Data\items.json" `
  --items-xml "C:\Kaezan\kaezan\canary-3.4.1\data\items\items.xml" `
  --mounts-xml "C:\Kaezan\kaezan\canary-3.4.1\data\XML\mounts.xml" `
  --outfits-xml "C:\Kaezan\kaezan\canary-3.4.1\data\XML\outfits.xml"
```

O extractor decodifica o formato moderno do Tibia (catalog-content.json + appearances.dat
protobuf + sheets BMP comprimidas com LZMA1 raw + header CIP) — mesmo algoritmo do
`spriteappearances.cpp` do OTClient. O manifest descreve patterns (direções, addons,
camada de máscara de cor) e o frontend recoloriza outfits em runtime com a paleta HSI
de 133 cores do Tibia. O mesmo comando cruza `items.xml` para gerar slots e atributos reais,
além dos itens sintéticos de montaria usados pelo equipamento. O modo `--equipment` inclui
automaticamente objetos cujo `clothes.slot` corresponde a helmet, armor, weapon, necklace ou
ring; legs, feet e backpack permanecem fora do pacote.

`--outfits-xml` extrai todos os outfits de jogador listados em `outfits.xml` do Canary (ambos os
gêneros) e gera `outfit-catalog.json` (lookType → nome + gênero) ao lado do manifest — é a fonte
que o Outfit Studio usa para as categorias Feminino/Masculino com nomes reais. Sem esse argumento,
só os `outfitIds` curados em `content-config.json` são extraídos.

`--static-items` é uma fonte opcional de importação: para objetos simples de um único frame,
o extractor normaliza os thumbnails antigos em células transparentes ancoradas no canto
inferior direito. Objetos animados, pilhas, terrenos e itens com patterns continuam vindo das
sheets completas. A saída é sempre copiada para `frontend/public/assets/tibia`; o jogo não
referencia caminhos externos ao repo. Omita o argumento quando essa extração local não existir.

Use `--dry-run` para auditar quantos IDs serão processados sem escrever arquivos. Use
`--sprites-only` para atualizar outfits, corpses e o manifest sem regenerar `items.json`; esse é o
modo recomendado ao atualizar apenas a biblioteca visual do editor de monstros.

## Invariantes (não quebre)

- **Backend é autoritativo.** O frontend nunca simula gameplay; só renderiza snapshots.
- **Determinismo**: mesma seed + mesmos comandos = mesma run (Rng xorshift próprio; nada de
  `Random` compartilhado no engine). Gacha usa `Random` não-determinístico de propósito.
- **Todas as constantes de simulação/meta em `GameConfig.cs`.**
- IDs estáveis: waifus `waifu:*`, cards `card:*`, banners `banner:*`. Não renomear.
- Assets do Tibia são **propriedade da CipSoft** — uso apenas em projeto privado/educacional.
