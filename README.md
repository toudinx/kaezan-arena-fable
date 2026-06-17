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

A aba **Kaelis** tem duas sub-telas. O **Guarda-roupa** é a entrada e a face de gestão de skins:
lista o roster e mostra **todas** as skins de cada Kaeli — a padrão e as estáticas (do código) e as
autorais (Kaezan). **Qualquer** skin pode ser editada por “Editar visual”, inclusive a padrão e as
estáticas: a edição vira um *override* autoral com o **mesmo id** (a invariante de ids estáveis fica
intacta — nada é renomeado), aparece como “Editada” e ganha **Restaurar padrão** para voltar à
definição do código (`KaeliRegistry` substitui a estática pelo override por id). As skins autorais
(id novo) também podem ter desbloqueio/ordem ajustados inline, ser **re-vinculadas** a outra Kaeli e
reordenadas (afeta o seletor de skin no Hub); a skin padrão mantém sempre o desbloqueio Padrão. O
**Outfit Studio** cria *skins* autorais para as Kaelis do roster, no espírito
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
equipáveis no Hub e dentro das runs. **Os addons exibidos vêm da skin** (bitmask 0/1/2/3 marcado no
estúdio): o que a skin define é o que aparece no Hub, na página Kaelis e nas runs — a ascensão não
força mais addons. Montaria fixada na skin sobrescreve o equipamento.

A aba **Itens** segue o mesmo fluxo do Outfit Studio: biblioteca Canary à esquerda, Item Studio no
centro e itens Kaezan à direita. O catálogo base tem 2.488 objetos, incluindo armas/equipamentos
descobertos por `clothes.slot` **ou** pelos metadados do `items.xml`; o admin cria uma cópia autoral
com ID estável próprio e reutiliza o sprite da fonte como referência visual. Slot, tipo de arma,
elemento, tier e números de gameplay pertencem ao item Kaezan criado, não ao item Canary original.
Itens criados ficam em `.data/content/authored-items.json` e recebem um atributo base por tipo:
armas usam ataque, armaduras/capacetes usam armadura, anéis/amuletos usam defesa e montarias usam
velocidade. O Item Studio aplica automaticamente o valor recomendado do tier quando tier, tipo ou
bônus habilitado muda; depois cada campo continua editável e valores fora da faixa aparecem com
aviso, mas continuam salváveis para testes. Bônus extras são curados por tipo: arma pode ter dano
crítico, armadura pode ter resistência física e uma elemental, capacete pode ter recarga e
vampirismo, montaria pode ter movimento, anel pode ter chance crítica e amuleto pode ter afinidade
elemental. A afinidade elemental não cria dano misto: ela aumenta dano apenas quando o elemento ativo
combina com o elemento do item; armas também ganham uma passiva fixa de +10% quando elemento da arma
e postura/Kaeli combinam. T0 é sem-tier/legado e pode entrar em qualquer loadout; T1-T5 ficam
travados ao set daquele tier. Classes permitidas começam vazias; vazio significa sem restrição, e
marcar classes transforma o item em equipamento restrito por classe. Depois de salvar, **Adicionar 1
à Mochila** concede uma cópia para testes sem depender de drop; os bônus são congelados no início da
run pelo `EquipmentStatAggregator`.

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
| WASD / setas | Movimento cardinal (combinações de duas teclas também formam diagonais) |
| Q / E / Z / C | Movimento diagonal (sem cortar quinas; diagonal bloqueada desliza pelo eixo livre) |
| Espaço | Mirar no inimigo mais próximo |
| Clique | Mirar inimigo / interagir (baú, escada) |
| Painel Helper | Controla alvo automático, preferência de alvo, skills, ultimate e modo de movimento |
| 1 / 2 / 3 / 4 | Slots 1-4 do kit da classe |
| R | Ultimate da classe (gauge) |
| Tab | Alterna a postura elemental (quando a classe possui duas) |
| ESC | Sair da run (abandono = metade do ouro) |

## Fluidez e segurança da run

- Passos encadeiam sem pausa entre ticks, com buffer de direção e reenvio periódico do input
  enquanto uma tecla de movimento estiver pressionada.
- O renderer mantém um tick de histórico e suaviza a deriva do relógio do servidor, preservando
  a animação de caminhada durante todo o deslocamento entre tiles mesmo com jitter de snapshots.
- Monstros desviam de bloqueios e aglomerações, perdem aggro após distância/LOS prolongados e
  respeitam `staticAttackChance` para sustentar posições de ataque.
- O helper vem ligado por padrão e pode ser modularizado no HUD: alvo automático, preferência de
  alvo (`HP` ou `Perto`), skills 1-4, ultimate e modo de movimento (`Stand`, `Follow` ou `Avoid`).
  Kaelis melee começam preferindo `Perto` + `Follow`; ranged começa em `HP` + `Avoid`, tentando
  manter 2 SQM do alvo. A escolha manual continua prevalecendo até o alvo morrer/sair da zona.
  Skills e ultimate só são usadas quando a área/linha alcançaria algum mob; movimento continua
  manual salvo quando um modo automático está ativo.
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
   Black Knight → Dragon Lord → Demon). Cada tier tem um **bioma visual próprio**
   (`Domain/Biomes.cs`): caverna de terra (1), forte gramado (2), cripta de pedra com ossos (3),
   covil escuro com poças de lava (4) e abismo (5). As paredes escolhem a peça por vizinhança
   (horizontal/vertical/canto), e os acentos de lava ficam na camada de decoração — nunca
   bloqueiam o caminho.
3. Durante a run: XP → level-ups oferecem **cards passivos** (escolha 1 de 3, max 3 stacks);
   loot clássico do Tibia dropa no chão e é coletado andando por cima.
4. **Recrutar** — banners com pity (4★ a cada 10; 5★ hard 80 / soft 65; 50/50 com garantia).
   Dupes viram Echo Shards → **Ascensão** (+8% stats por nível; os addons do outfit são definidos
   por skin no Outfit Studio, não pela ascensão).
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

Cada Kaeli usa o kit completo de uma das nove classes canônicas do Kaezan World:

| Classe | Elemento | Kaelis | Traits |
|---|---|---|---|
| Warrior | Physical | Mirai 4★, Kaela 5★ | Instinto de Matilha (+dano cercada) · Última Muralha (-12% dano) |
| Sentinel | Physical | Wren 3★ | Olho de Águia (+crit à distância) |
| Oracle | Holy | Aurora 5★ | Luz Purificadora (+dano em undead) |
| Pyromancer | Fire | Ember 4★ | Combustão (gauge +30%) |
| Stormcaller | Energy | — (sem Kaeli ainda) | — |
| Cryomancer | Ice | Neva 4★ | Precisão Glacial (gelo aplica slow 25%/2s) |
| Shaman | Earth | Sage 3★ | Seiva Vital (skills curam 6% do dano) |
| Barbarian | Physical | Mira 3★ | Coração Valente (DR com HP baixo) |
| Necromancer | Death | Velvet 5★ | Fome do Abismo (+dano em alvos <30% HP) |

Todas as classes são **single-stance** (sem alternância de postura). Cada kit usa um **shape
diferente por slot** para que nenhuma habilidade seja "a mesma área com elemento trocado":
`single`, `area`, `cone`, `beam`, `nova`, `chain`, `ring`, `field`, `barrage`, `summon` e `buff`
podem todos aparecer no mesmo kit.

**Warrior** é tanque de controle: stun melee (Shield Bash), taunt (Challenge), 1 AoE (Front Sweep)
e escudo defensivo (Shield Wall). **Sentinel** é atiradora física: projétil que desacelera, chain
ricocheteia, slam que atordoa e zona de vento (field). **Oracle** é invocadora sagrada: barrage de
lanças, feixe divino e halo sagrado (ring). **Barbarian** é combo/mobilidade: chain (Rampage),
shockwave (Palm Shockwave) e War Cry (haste). **Cryomancer** desacelera a cada hit, congela o
terreno (Glacial Prison field) e lança avalanches. **Shaman** atordoa com espinhos, chuva de
pedras em sequência (barrage) e armadilha de areia (ring). **Pyromancer** encadeia e incendeia
(Combustion chain+DoT), poça de fogo (field) e meteoros (barrage). **Stormcaller** paralisa,
estoura anel elétrico (ring) e termina com Rage of the Skies (nova r3). **Necromancer** corrói com
DoT, ergue construto (summon) e dispara feixe de morte. Geometrias seguem o Tibia,
**reescaladas para o mapa da arena** (sem círculos de ~37 tiles em slots básicos).

Os cooldowns pertencem aos slots 1-4 e continuam correndo ao trocar de postura; alternar com
`Tab` não reseta habilidades. A página Kaelis (abas Perfil / Skins / Maestria / Equipamento)
permite visualizar os dois kits elementais, presentear, trocar skins e gastar pontos de maestria.

## Estrutura

```
backend/src/KaezanArenaFable.Api/
  Domain/    GameConfig (TODAS as constantes), Waifus (roster+traits+skins+lore), Mastery
             (árvores de maestria), Cards, Biomes (tema visual por tier), GameData (monsters.json)
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
incluindo dano elemental, crítico, roubo de vida, poder mágico, velocidade e resistências, além dos
itens sintéticos de montaria usados pelo equipamento. O modo `--equipment` inclui automaticamente
objetos cujo `clothes.slot` corresponde a helmet, armor, weapon, necklace ou ring **e** itens que
possuem slot/`weaponType` no XML; legs, feet e backpack permanecem fora do pacote.

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
