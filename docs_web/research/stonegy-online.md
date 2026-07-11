# Stonegy Online — Mapeamento Kaezan  (modo: rival direto / "Tibia Idle")

> Fonte: https://stonegy-online.com (v2.2.3) + wiki de fãs https://stonegy-wiki.com ("Tempest").
> Pesquisado em 2026-07-11. A comunidade chama o jogo abertamente de **"Tibia Idle"**.

## 1. Resumo

Stonegy é um **MMORPG idle de browser** (Next.js, sem download, PWA no celular) cuja tese é
brutalmente simples: **pegar o conteúdo do Tibia inteiro — vocações, magias, monstros, itens,
hunts — e trocar só o loop de jogo** (de MMO ativo para idle com hunt visível na tela). Não é
"inspirado em Tibia": as 4 classes são Knight/Paladin/Sorcerer/Druid, as skills do Knight são a
linha `exori` real (Berserk, Fierce Berserk, Annihilation, Groundshaker, Front Sweep...) com
level/mana/cooldown, os 365 monstros incluem bosses reais (Bakragore, The Rootkraken, Abyssador,
Brainstealer), os 1.423 itens usam **sprites reais do Tibia** (Crown Armor, Demon Shield, Cobra
Crossbow — servidos como `.gif` no próprio site), e as 113 hunts são os respawns clássicos
(Sewers, Venore Rotworms, Hero Fortress, Dragon Lair, Asura Palace, Secret Library).

**Por que interessa ao Kaezan:** valida com hype real (YouTube BR, wiki de fãs com hunt finder,
calculadora, sorteios em PIX) que existe demanda por "Tibia no browser sem esforço". E o Kaezan
**já tem os componentes difíceis** que o Stonegy usa: pipeline de sprites/monstros do Tibia
(AssetExtractor + convert-monsters), backend autoritativo com tick, cliente browser. A diferença
é o *frame* de apresentação do conteúdo — e esse frame é barato de puxar.

## 2. O que o Stonegy é, por sistema

| Sistema | Como funciona lá |
|---|---|
| Loop central | Idle: você escolhe uma **hunt**, o personagem caça sozinho com a cena visível; você "dá uma checada" e toma decisões pontuais |
| Classes | As 4 vocações do Tibia, sem invenção; skills = lista de magias do Tibia com level de destrave, mana e CD |
| Hunts | 113 hunts nomeadas (respawns do Tibia), cada uma com **Lv recomendado**, **Lv mínimo**, **XP top** (xp/h esperado) e **LURE** (faixa de monstros puxados por vez, ex. 3–7) |
| Lure | Dial de risco/recompensa por hunt: puxar pilha maior = mais xp/h, mais perigo |
| Social | Party hunt de até 4 no mesmo mapa, "dano, loot e evolução na mesma hunt" |
| Mobile | Abre como app (PWA), pensado pra "checada rápida" no celular |
| Monetização | Hunts **PREMIUM** (as melhores xp/h são pagas), referral links, comunidade fala de RMT |
| Meta-comunidade | Wiki de fãs com Hunt Finder, bestiário, loot reverso, calculadora de ouro, party finder, criadores de YouTube |

## 3. Mecânicas adaptáveis

| Mecânica do Stonegy | Tradução p/ o Fable | Shape/Sistema âncora |
|---|---|---|
| **Hunt board nomeada** (respawn com Lv rec./mín., xp/h, mix de criaturas) | "Contratos de caçada": em vez de só "Tier 1–5", cada tier oferece 2–4 **hunts nomeadas** com mix de spawn distinto do bestiário já importado (ex. "Cripta dos Rotworms", "Forte dos Heros") — mesmo mapgen, população temática | tiers + bestiário + `SpawnBudget` |
| **LURE como dial** | Expor o tamanho de pilha como escolha pré-run ou carta ("lure +2: spawns em packs maiores, +xp/ouro") — casa 1:1 com a direção "autopilot deve mobar (orbitar pilha + AoE)" | `GameConfig` spawn/aggro + cartas |
| **XP/h transparente** | Mostrar no seletor de tier/hunt o **xp e ouro esperados por run** (dados já existem: recap pós-run) — vira meta-jogo de otimização como o Hunt Finder deles | seletor de tier + recap |
| **Idle/expedição** | Modo "Expedição": mandar uma Kaeli **não-ativa** para uma hunt em background (tempo real, resolve offline com o engine determinístico em fast-forward) → ouro, xp de afinidade, kills de bestiário. Não substitui a run ativa; ocupa o roster parado | engine determinístico (seed) + afinidade + bestiário |
| **Grimoire do Tibia como skill data** | Em vez de inventar skill nova, **portar a lista de magias do Tibia por papel** como parametrização dos shapes existentes: `exori` = nova, `exori min/mas` = cone/area frontal, `exori hur` = single ranged, `exura` = buff/heal, linha `vis/flam/frigo` = beam/single elemental do Mage. Level de destrave/mana/CD vêm de tabela conhecida e testada há 25 anos | `Domain/Waifus.cs` shapes (single/beam/nova/area/cone/buff) |
| **Boss lendário como evento** | Bosses de bestiário com HP gigante (estilo Bakragore 660k) como **caçada especial semanal** — o bestiário já dá dano permanente por ranks; um alvo raidável dá função ao late game | bestiário + dailies/weeklies |
| **PWA "checada rápida"** | Manifest + service worker no Angular: Hub/dailies/recap utilizáveis no celular como app (a run ativa pode continuar desktop-only) | frontend shell |
| **Hunt Finder como UI in-game** | O que lá é wiki de fã, aqui nasce dentro do jogo: tela de escolha de hunt com criaturas, loot notável e xp/h — reaproveita sprites e dados do bestiário | Mochila/bestiário UI |

## 4. Sobre "usar o que o Tibia possui" (a pergunta do usuário)

A tese do Stonegy confirma a intuição: **não precisa inventar conteúdo quando o Tibia já dá o
conteúdo pronto** — o que se inventa é o *loop*. O Kaezan já segue isso pela metade (monstros,
sprites, loot). Os próximos degraus de reuso barato, em ordem de custo:

1. **Magias por papel** — a lista de spells do Tibia mapeia direto nos shapes do engine (ver
   tabela). É data, não código novo. Kaelis continuam tendo trait de assinatura por cima.
2. **Hunts nomeadas** — o "level design" do Tibia é uma tabela respawn→criaturas→nível. Vira
   população temática do mapgen atual.
3. **Curva de progressão** — xp de monstro e pacing de nível do Tibia são tabelas públicas e
   balanceadas há décadas; úteis como baseline de tuning dos tiers.

O que **não** reutilizar do Tibia: mapa/geografia real (nosso mapgen orgânico é melhor pro
formato run), nomes de itens com sprite idêntico em vitrine pública de marketing (ver riscos).

## 5. Riscos / o que NÃO puxar

- **IP da CipSoft.** O Stonegy usa sprites, nomes de itens, magias e bosses do Tibia **em site
  público de marketing com monetização direta** (hunts premium, sorteios). É o mesmo risco legal
  de OT server, ampliado. O Kaezan já opera na zona cinza (sprites via AssetExtractor, nomes de
  espécie como IDs), mas a lição é de *postura*: conteúdo Tibia dentro do jogo ≠ conteúdo Tibia
  como material de divulgação. Não expor sprite/nome do Tibia em landing page, loja ou banner.
- **Pivô total para idle.** O core do Fable é a run ativa de 40–55s com feeling calibrado
  (dash, cartas, boss). Virar "idle MMO" descartaria o que já está bom. O idle entra como
  **camada** (expedição de roster parado), não como substituto.
- **Party hunt online (4 players).** Custo altíssimo (netcode, persistência de mundo, anti-cheat)
  para um jogo single-player determinístico. Não puxar agora; a fantasia social pode vir depois
  como leaderboard de seed/replay (FF-track já dá replay bit-perfect).
- **Hunts premium (paywall de conteúdo).** Conflita com a economia gacha (banner/pity). Melhor
  gate por progressão (nível de conta) do que por assinatura.

## 6. Candidatos a roadmap desktop

- [ ] **Hunts nomeadas por tier** — 2–4 "contratos" por tier com mix de spawn temático do
  bestiário + xp/ouro esperado no seletor; mexe em `Domain` (tabela de hunts), população do
  mapgen e UI do seletor de tier. É o item de maior retorno/custo.
- [ ] **Lure dial** — parâmetro de tamanho de pack exposto como escolha pré-run ou carta de run;
  mexe em `GameConfig` (spawn/aggro) e oferta de cartas. Sinergia direta com a direção de
  gameplay "mobar/orbitar pilha".
- [ ] **Grimoire Tibia → skill data** — portar a lista de magias por papel (Knight/Archer/Mage)
  como parametrização dos shapes existentes, com destrave por nível; mexe só em `Domain/Waifus.cs`
  + tabela nova. Zero dispatch novo.
- [ ] **Modo Expedição (idle)** — enviar Kaeli não-ativa para hunt em background resolvida por
  fast-forward determinístico (seed + fórmula, sem tick real); recompensas: ouro, afinidade,
  kills de bestiário. Mexe em `Meta` (conta) + engine (resolução offline). Maior feature da lista.
- [ ] **Caçada lendária semanal** — 1 boss de bestiário com HP massivo como alvo semanal da conta
  (dano acumulado entre runs); mexe em dailies/weeklies + bestiário.
- [ ] **PWA shell** — manifest + service worker para Hub/dailies/recap no celular; mexe só no
  frontend shell.
- [ ] **Higiene de IP** — auditar o que de sprite/nome Tibia aparece em superfícies "públicas"
  (landing, screenshots de divulgação) vs. dentro do jogo; doc de política curta em `docs/`.
