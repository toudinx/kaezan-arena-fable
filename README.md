# Kaezan Arena Fable

Jogo browser **gacha + roguelike de dungeon**, recriação do kaezan-arena com a alma do Tibia:
movimentação livre em grid, mapas gerados proceduralmente, conteúdo de combate/equipamento **Kaezan**
com sprites/FX reaproveitados dos assets do Canary/OTClient do repo `kaezan`, e um lado gacha completo
(banners com pity, coleção de Kaelis, missões diárias, bestiário).

| Camada | Stack |
|---|---|
| Backend | ASP.NET Core 8 (C#) — engine server-authoritative com tick de 100ms via SignalR |
| Frontend | Angular 21 (standalone, signals) — renderer Canvas 2D com sprites do Tibia |
| Assets | Pipeline próprio de extração (protobuf appearances + sheets LZMA → PNG atlases) |
| Dados | Monstros convertidos dos `.lua` do Canary para JSON |

### Identidade visual

O frontend segue a direção **"Cathedral Ink + Aurum"** — gacha premium (alvo Wuthering Waves):
superfícies de vidro com aresta de luz, tipografia editorial (Fraunces display + Sora UI) e
**acento duplo** (íris para UI, aurum para recompensa). A Home é um hub full-bleed com a Kaeli
fixada; a página Kaelis traz idle rotativo (3 poses, crossfade a cada 7s); o reveal de convocação
é cinematográfico em CSS. O contrato de tokens/primitivos vive em
[`docs/STYLE_GUIDE.md`](docs/STYLE_GUIDE.md); o remap completo está em
[`docs/FRONTEND_REMAP.md`](docs/FRONTEND_REMAP.md). Todas as animações de destaque respeitam
`prefers-reduced-motion`.

A Caçada usa telas full-bleed inspiradas em seleção de desafio/deploy: fundos panorâmicos por bioma,
rails de escolha, boss/sprite sobre a cena, recompensas em faixa e CTAs em pílula.
O pré-run (`/play/:tier`) é apenas o seletor cinematográfico de Kaeli; detalhes e equipamentos ficam
na página Kaelis.

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
em `backend/src/KaezanArenaFable.Api/.data/account.json` com uma Kaeli inicial e 4000 Kaeros.

O painel `http://localhost:4200/admin` traz o editor de conteúdo Kaezan com abas **Monstros**,
**Items**, **Dungeons** e **Skins**. Cada tier de dungeon pode receber mobs comuns, elites e um
boss autorais; salvar persiste a composição em `.data/content/tiers.json` e afeta somente as
próximas runs.
Quando `.data/content` ainda está vazio ou contém só o conteúdo legado/de teste, o backend faz seed
do catálogo Kaezan versionado: **50 monstros autorais** (6 comuns, 3 elites e 1 boss por tier),
**135 equipamentos/armas autorais** (130 normais + 5 relíquias de boss) e 5 dungeons que
referenciam apenas IDs `monster:*`.
Na navegação principal, o Admin fica fora do fluxo de jogo: use a engrenagem discreta no shell ou
acesse `/admin` diretamente.

A aba **Monstros** cria conteúdo autoral sem copiar os stats do Canary. Cada criatura escolhe
uma aparência, power tier, função (`common|elite|boss`), comportamento curado e elemento ofensivo.
Os presets de HP/dano/velocidade/cadência são pontos de partida e continuam editáveis; fraquezas
e resistências são independentes por elemento, sem relação automática de pedra-papel-tesoura.
Monstros novos usam IDs imutáveis `monster:*` e são persistidos em
`.data/content/monsters.json`. As espécies de `monsters.json` permanecem somente como biblioteca
visual/legado; a composição seedada das dungeons usa apenas monstros Kaezan.

O editor separa as três responsabilidades em colunas: a biblioteca visual Canary (somente leitura),
a configuração Kaezan e os monstros autorais salvos. O catálogo visual contém 1.542 definições Lua
deduplicadas em 758 outfits, com filtros de monstro/boss, classe e placeholder legado. Monstros
Kaezan podem ser reabertos, duplicados e excluídos; a exclusão é recusada enquanto a criatura ainda
estiver referenciada por alguma dungeon.

A aba **Skins** tem duas sub-telas. O **Guarda-roupa** é a entrada e a face de gestão de skins:
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

A aba **Items** segue o mesmo fluxo do Outfit Studio: biblioteca Canary à esquerda, Item Studio no
centro e itens Kaezan à direita. O catálogo base tem 2.488 objetos, incluindo armas/equipamentos
descobertos por `clothes.slot` **ou** pelos metadados do `items.xml`; o admin cria uma cópia autoral
com ID estável próprio e reutiliza o sprite da fonte como referência visual. Slot, tipo de arma,
elemento, tier, bônus especial e classes permitidas pertencem ao item Kaezan criado, não ao item
Canary original. Os números de gameplay não são editados manualmente: o tier define preço, atributo
base e magnitude dos bônus por uma curva única de balanceamento; a tag `relic` aplica um
multiplicador percentual em cima dessa curva. Itens criados ficam em
`.data/content/authored-items.json` e recebem um atributo base por tipo: armas usam ataque,
armaduras/capacetes usam armadura, anéis/amuletos usam defesa e montarias usam velocidade.
Bônus extras são curados por tipo: arma pode ter dano crítico, armadura pode ter resistência física
e uma elemental, capacete pode ter recarga e vampirismo, montaria pode ter movimento, anel pode ter
chance crítica e amuleto pode ter afinidade elemental. A afinidade elemental não cria dano misto:
ela aumenta dano apenas quando o elemento ativo combina com o elemento do item; armas também ganham
uma passiva fixa de +10% quando elemento da arma e postura/Kaeli combinam. T0 é sem-tier/legado e
pode entrar em qualquer loadout; T1-T5 ficam
travados ao set daquele tier. Relíquias são itens de boss: só entram no pool de boss do próprio tier,
não aparecem em mobs comuns, elites, baús ou gacha. Classes permitidas começam vazias; vazio
significa sem restrição, e
marcar classes transforma o item em equipamento restrito por classe. Depois de salvar, **Adicionar 1
à Mochila** concede uma cópia para testes sem depender de drop; os bônus são congelados no início da
run pelo `EquipmentStatAggregator`.
O seed inicial usa uma estratégia híbrida: cada família de item reaproveita a mesma sprite nos 5
tiers, mas cada peça tem `ItemId`, nome, tier e moldura próprios; os status vêm da curva do tier.
Armas, capacetes e armaduras são restritos às 7 classes jogáveis atuais; anéis, amuletos e montarias
são genéricos. O frontend desenha uma moldura por `item.tier`, e relíquias recebem uma marca dourada
extra, então a progressão visual não depende de sprite nova.

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
| 5 | Poção de cura (2 cargas por run; cura escala com o tier; cooldown curto) |
| B | Abre/fecha a mochila da caçada (loot acumulado na run) |
| Tab | Alterna a postura elemental (quando a classe possui duas) |
| ESC | Sair da run (abandono = metade do ouro) |

O loot agora é **coletado automaticamente no abate**: moedas e itens explodem do monstro e voam
em arco até a Kaeli (com som de "cha-ching"), sem precisar pisar sobre eles. Equipamentos vão
direto pra mochila da caçada; comida/poções dropadas curam na hora. O slot 5 é uma poção própria
da run (independente do loot), com 2 cargas que escalam de cura conforme o tier.

## Fluidez e segurança da run

- Passos encadeiam sem pausa entre ticks, com buffer de direção e reenvio periódico do input
  enquanto uma tecla de movimento estiver pressionada.
- O renderer mantém um tick de histórico e suaviza a deriva do relógio do servidor, preservando
  a animação de caminhada durante todo o deslocamento entre tiles mesmo com jitter de snapshots.
- **Peso de combate (juice).** O impacto é feedback puramente client-side reagindo aos `EventDto`
  (engine intocado, determinismo preservado): hit-stop (pop de escala no alvo), screen-shake decaído
  proporcional à magnitude, números de dano com outline/pop-in — crítico maior e dourado, escala por
  fração do HP do alvo —, proc text destacado (QUEBRADO!/JULGADO/…), flash aditivo no sprite atingido
  e dissolve por pixels na morte. A intensidade vem sempre do dado do servidor, nunca de RNG no front.
- Monstros desviam de bloqueios e aglomerações, perdem aggro após distância/LOS prolongados e
  respeitam `staticAttackChance` para sustentar posições de ataque.
- O helper vem ligado por padrão e pode ser modularizado no HUD: alvo automático, preferência de
  alvo (`HP` ou `Perto`), skills 1-4, ultimate e modo de movimento (`Stand`, `Follow` ou `Avoid`).
  Kaelis melee começam preferindo `Perto` + `Follow`; ranged começa em `HP` + `Avoid`, tentando
  manter 2 SQM do alvo. A escolha manual continua prevalecendo até o alvo morrer/sair da zona.
  Skills e ultimate só são usadas quando a área/linha alcançaria algum mob; movimento continua
  manual salvo quando um modo automático está ativo.
- **Legibilidade do helper (client-side).** Dá pra "ler" o que a build vai fazer: um retículo animado
  marca o alvo atual do helper, uma linha de intenção liga a Kaeli ao alvo (dourada quando há skill
  pronta para cair) e um *telegraph* pulsante prevê o shape que vai disparar (cone/beam saindo da
  Kaeli, anel/área em volta dela ou do alvo). Os shapes vêm do catálogo de skills; nada é simulado no
  front, só leitura do snapshot.
- **Kits Kaezan autorais:** monstros seedados combinam power tier, função (`common|elite|boss`),
  comportamento curado e elemento ofensivo. Condições, slows, cura própria e ataques em área vêm dos
  perfis data-driven do engine; sprites/corpses ainda podem reaproveitar a biblioteca Canary.
- O catálogo seedado tem 50 monstros Kaezan nos cinco tiers, com 6 comuns, 3 elites e 1 boss por
  dungeon. Loot de equipamentos vem de tabelas curadas por tier/classe, não das loot tables Tibia.
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
- **Echo Break como clímax (FX client-side).** Quando o boss entra em stagger, o instante vira um
  momento da run: um *slow-mo* breve (replay da interpolação atrás do relógio autoritativo, que
  ressincroniza sozinho — não toca a simulação), flash dourado em tela cheia com banner
  `⚡ ECHO BREAK ×N`, shockwave saindo do boss, screen-shake e um boom sintetizado. Durante o stagger,
  uma aura dourada pulsante + rótulo `JANELA DE DANO` marcam a janela. Tudo reage ao flag
  `bossStaggered`/`bossPostureCycle` do snapshot; o engine continua dono da regra.
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
   escada para o covil) e um **boss Kaezan** no fundo. Cada tier tem um **bioma visual próprio**
   (`Domain/Biomes.cs`): caverna de terra (1), forte gramado (2), cripta de pedra com ossos (3),
   covil escuro com poças de lava (4) e abismo (5). As paredes escolhem a peça por vizinhança
   (horizontal/vertical/canto), e os acentos de lava ficam na camada de decoração — nunca
   bloqueiam o caminho.
3. Durante a run: XP → level-ups oferecem **cards passivos** (escolha 1 de 3, max 3 stacks);
   monstros e baús entregam ouro e equipamentos Kaezan do tier atual. Drops priorizam a classe ativa
   e alternam com acessórios genéricos; bosses também têm chance de relíquia do próprio tier.
4. **Recrutar** — banners com pity para Kaelis jogáveis. Quando um roll não acerta uma Kaeli, ele
   entrega **1 item Kaezan aleatório**; quando acerta uma Kaeli repetida, o dupe vira Echo
   Shards → **Ascensão** (+8% stats por nível; os addons do outfit são definidos por skin no Outfit
   Studio, não pela ascensão).
5. **Kaelis (profundidade)** — cada Kaeli tem **trait de assinatura** (passiva única no engine),
   **afinidade** 1-10 (XP por runs com ela ativa + **presentes** — itens da Mochila, favoritos
   ×2, máx. 3/dia; níveis destravam **ecos de memória** (lore), Kaeros, skins e +1% ATK/HP por
   nível), **skins por outfit** (padrão / afinidade / compradas com ouro ou Kaeros — a skin em
   uso aparece no Hub e dentro das runs) e a **Maestria de Eco**: árvore de 3 ramos
   (Ofensiva/Defensiva/Eco) com pontos por run (vitória +3 / derrota +1) e respec por ouro.
6. **Mochila** — inventário com sprites reaproveitados + bestiário (ranks por abates = dano permanente).
   Itens Kaezan usam preço autoral; itens legados sem comprador valem 5 ouro.
   Loot equipável exibe atributos Kaezan e pode ser colocado, por Kaeli e por tier, nos slots
   `helmet`, `armor`, `weapon`, `necklace`, `ring` e `mount`.
7. **Equipamento** — o paperdoll da página Kaelis troca itens por clique. Os bônus são congelados
   ao iniciar a run e aparecem no HUD; montarias Kaezan dão HP/velocidade e também mudam o visual
   da Kaeli no mundo.

## Kaelis: personagens jogáveis 5★

Nova direção: toda Kaeli jogável é personagem de topo (5★), com arte completa, trait de
assinatura, personalidade, 4 ecos de memória (lore por afinidade), presentes favoritos e skins.
Não há mais Kaeli jogável de preenchimento; o espaço de roll comum do gacha entrega
**1 item Kaezan aleatório** da curadoria autoral de equipamentos.

O roster alvo da refundação reúne 7 Kaelis autorais:

| Kaeli | Elemento | Alcance | Fantasia |
|---|---|---:|---|
| Eloa | Holy | ranged | anjo/serafim de luz, julgamento e absolvição |
| Seren | Physical | melee | cavaleira astral, duelo e disciplina |
| Velvet | Death | ranged | pesadelo, maldição, DoT e execução |
| Rin | Fire | ranged | súcubus, pacto, charme e burn |
| Rynna | Energy | melee | dragoa guerreira de raio, engage e stun |
| Lunara | Ice | melee | lebre lunar, mobilidade e slow |
| Gaia | Earth | ranged | arqueira da terra, raízes, caça e monólitos |

Cada kit usa um **shape diferente por slot** para que nenhuma habilidade seja "a mesma área com
elemento trocado":
`single`, `area`, `cone`, `beam`, `nova`, `chain`, `ring`, `field`, `barrage`, `summon` e `buff`
podem todos aparecer no mesmo kit.

Os kits autorais seguem a fantasia de cada Kaeli: Eloa julga e absolve com luz, Seren duela com
disciplina astral, Velvet corrói e executa, Rin queima por pacto, Rynna engaja com raio, Lunara
dança entre slows lunares e Gaia prende alvos com raízes e monólitos. Geometrias seguem o Tibia,
**reescaladas para o mapa da arena** (sem círculos de ~37 tiles em slots básicos).

Cada Kaeli também tem uma **passiva assinatura** de arquétipo distinto — um mini-game dentro da run,
com decisão de gameplay e **estado vivo visível no HUD** (chip da passiva + marcas sobre os alvos):

| Kaeli | Passiva | O que faz | Decisão |
|---|---|---|---|
| Eloa | **Selo de Julgamento** | acertos marcam Pecado; ao chegar a 3 o alvo é Julgado e o próximo acerto detona um estouro sacro em área que cura | espalhar marcas (sustain) vs focar pra detonar |
| Seren | **Disciplina** | acertos consecutivos no mesmo alvo escalam o dano (+8%/acerto até +40%); cada 3º é crit garantido | comprometer-se num duelo vs limpar adds |
| Velvet | **Maldição Acumulada** | cada acerto empilha Decadência (DoT) e eleva o limiar de execução (de <15% até <25%) | empilhar maldição e então executar |
| Rin | **Contágio** | acertos de fogo incendeiam; o burn salta entre inimigos e cada tick cura Rin | posicionar pra encadear o incêndio |
| Rynna | **Carga Estática** | acertos enchem a Carga; cheia, descarrega numa corrente que paralisa e acelera a ultimate | ritmar os golpes pra soltar no pico |
| Lunara | **Estilhaçar** | bater num alvo lento dá dano bônus e haste; o 3º acerto estilhaça e consome o slow | hit-and-run: slow, mergulho, estilhaço |
| Gaia | **Presa** | marca um alvo; o dano contra a Presa cresce com o tempo de caça; ao morrer, a marca salta e dá cadência | escolher a prioridade e perseguir |

Os cooldowns pertencem aos slots 1-4. A página Kaelis (abas Perfil / Skins / Maestria /
Equipamento / Informação) permite visualizar o kit, presentear, trocar skins e gastar pontos de
maestria. Visualmente é um "ateliê": rail de roster à esquerda, alcova de arte central com a Kaeli
em idle rotativo (`<app-kaeli-idle>`, 3 poses/7s) sobre seu `bg-portrait` quando há arte autoral —
senão o sprite da skin selecionada — e o dossiê de abas em vidro à direita.

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
