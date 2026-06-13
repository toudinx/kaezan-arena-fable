# DESIGN NOTES — Ideias do Tibia / Canary / OTClient + Kaezan World

> **O que é este documento.** Uma base de conhecimento de **design**, não de código.
> Reúne as ideias e mecânicas mais interessantes do stack original (Canary C++/Lua +
> OTClient) e das features que já foram desenhadas/implementadas no **Kaezan: World**
> (o MMORPG em `C:\Kaezan\kaezan`, **somente leitura**), traduzidas para o que faz sentido
> no Kaezan Arena Fable (C# + Angular).
>
> **Por que isto existe — e por que a linguagem não importa.** A documentação em
> `C:\Kaezan\kaezan\mapping` descreve _game design_: como uma mecânica se comporta, por que
> uma decisão foi tomada, como os sistemas se compõem e se balanceiam. Isso é independente da
> engine. O fato de o original ser C++20 + LuaJIT e o Fable ser C# + Angular **não nos impede
> de reusar nada** — porque o que reusamos são **ideias, regras e composição de sistemas**,
> não arquivos. Onde o original resolveu um problema em Lua sobre a engine do Tibia, nós
> resolvemos o mesmo problema de design no `GameWorld`/`Meta` em C#. A engine muda; o design
> permanece.
>
> **Regra:** nada em `C:\Kaezan\kaezan` é editado. É fonte de inspiração e de assets.

---

## 0. Como usar este documento

- É **referência de design**, não fila de trabalho. A fila de trabalho mecânica está em
  [ROADMAP.md](ROADMAP.md) (track Codex) e a fila de features complexas em
  [FABLE_TRACK.md](FABLE_TRACK.md) (track Claude Fable 5).
- Cada seção marca a **origem** (Tibia/Canary, OTClient, ou Kaezan World) e o **estado no
  Fable** (`✅ já temos` / `🔜 planejado` / `💡 ideia`).
- Quando uma ideia daqui virar trabalho real, ela vira uma task no ROADMAP ou no FABLE_TRACK,
  com referência de volta para a seção correspondente aqui.

---

## 1. Pilares de design herdados do Kaezan World

O GDD original (`KAEZAN_GDD_v0.5.md`) define pilares que se aplicam quase 1:1 ao Fable e
devem guiar toda decisão futura:

| Pilar | Significado no Kaezan World | Tradução para o Fable |
|---|---|---|
| **Less grinding, more playing** | Sessões de 15 min satisfatórias; grind infinito desincentivado por caps. | Uma run é a "sessão". Recompensas vêm de **completar** (boss + dailies), não de farmar mobs ad infinitum. ✅ parcialmente |
| **Morte não-catastrófica** | Morte é simbólica: perde-se tempo, não progresso. | Derrota mantém metade do ouro e dá XP de conta. Nunca punir com perda de coleção/ascensão. ✅ |
| **Identidade forte de personagem** | Cada Kaeli tem visual, kit e playstyle distintos. | Cada waifu tem outfit do Tibia, elemento, arma e kit de 4 shapes. Ascensão reforça o visual (addons). ✅ |
| **Conteúdo em camadas** | Casual (dailies/expedições) · Regular (dojos/bosses) · Hardcore (ranked/speedrun). | Casual = dailies + 1 run; Regular = subir tiers + ascensão; Hardcore = desafio diário/leaderboard (🔜). |
| **Single-player é fundamental** | Tudo de progressão é soloável; MMO é bônus. | O Fable é single-player por natureza. Co-op fica como bônus distante. ✅ por construção |
| **Sem spam de botões** | Cargas/cooldowns significativos; gerenciar, não martelar. | Skills com CD real + ultimate por gauge + auto-attack. Evitar designs que premiem só attack speed. ✅ |
| **Construir sobre o nativo** | Reskin/estender o que a engine já dá (Wheel, Bestiary, Reward Chest…) antes de reinventar. | Nosso "nativo" é o **conteúdo do Tibia já extraído** (monstros, outfits, FX, loot). Antes de inventar conteúdo, reusar o que o pipeline já entrega. ✅ |

**Conclusão de produto:** o Fable já nasceu alinhado a 5 dos 6 pilares. O que falta é
profundidade nas **camadas de conteúdo regular/hardcore** — exatamente onde as features
complexas (FABLE_TRACK) entram.

---

## 2. Dojos — modo de desafio de boss (Kaezan World)

> Origem: `mapping/changes/features/pilot_dojo/`. Estado no Fable: 💡 ideia (modo novo).

**O que é no original.** Um Dojo é uma **sala instanciada solo** onde o jogador entra com
seu Echo Team, enfrenta **um único boss escalado** (level-sync para um alvo fixo, ex. 400),
e ao derrotá-lo recebe um **Sealed Reward** (baú selado com parte fixa garantida + parte
rerollável = o loot do boss). Rejogo livre, sem cooldown. Um catálogo estilo "Bosstiary"
lista os dojos disponíveis, cada card ilustrado com o outfit do boss.

**Mecânicas-chave que valem a pena portar:**

1. **Level sync (escala simétrica).** Em vez de escalar o mob, escala o **dano recebido**
   pelo participante por um fator `clamp(playerPower / targetPower, min, max)`. Personagem
   forte leva mais dano, fraco leva menos — todos têm uma luta "justa" contra o mesmo boss.
   Isso permite **um boss servir a uma faixa larga de progressão** sem rebalancear o boss.
2. **Boss como destino, não como fim de corredor.** O Dojo separa "enfrentar o boss" de
   "atravessar a dungeon". É a fantasia de _boss rush_ / treino — entra, luta, sai.
3. **Catálogo visual.** Cada boss é um card com o sprite real — vitrine que dá vontade de
   colecionar vitórias (igual ao Bestiary do Tibia).

**Tradução para o Fable.** Um **Modo Desafio de Boss**: o jogador escolhe um boss já
derrotado em campanha (ou um boss-only especial), entra direto numa arena pequena com o
**Echo Team** (ver §4), luta com level-sync, e ganha um Sealed Reward (§5). Sem percorrer
dungeon. Casa perfeitamente com o **Desafio Diário** (seed do dia → mesmo boss + modificadores
para todos → leaderboard). Ver FABLE_TRACK F-C.

---

## 3. Boss Posture / Echo Break (Kaezan World)

> Origem: `mapping/changes/features/boss_posture/`. Estado no Fable: 🔜 ROADMAP T-31 (MVP) +
> FABLE_TRACK F-E (sistema completo).

**O que é.** Uma **segunda barra** do boss (além do HP): a **postura**. Acertar o boss reduz
a postura; ao zerar, o boss entra em **stagger** (janela de "Echo Break") — fica vulnerável e
o dano recebido é **multiplicado**. Ao fim do stagger, o **ciclo** aumenta e a postura reseta
maior. É o sistema que transforma um boss de "saco de HP" em **uma dança de ritmo**: pressione
para quebrar, então despeje seu burst na janela.

**Detalhes de design que fazem funcionar (do original):**

- **Multiplicador por ciclo, não bônus fixo.** Durante o stagger, o dano bruto é multiplicado
  por ciclo (`2.5x → 3.5x → 5x → 6.5x`). Decisão deliberada: bônus fixo por hit premiava só
  _attack speed/CDR/multi-hit_; multiplicar o dano bruto recompensa **golpes e spells fortes**
  na janela — recompensa skill expression, não spam.
- **Bônus de `% do HP máximo` com cooldown interno por jogador.** Some um pedaço fixo da vida
  máxima do boss por hit válido, mas com CD curto para não virar exploit de multi-hit.
- **Fraqueza elemental dá mais postura.** Bater com o elemento que o boss é fraco quebra a
  postura mais rápido. Liga a escolha de waifu/elemento à mecânica de luta.
- **Decaimento.** A postura **decai** quando você para de bater — não dá para "acumular de
  graça"; é preciso pressão sustentada.

**Por que é ouro para o Fable.** Hoje os bosses do Fable (Rotworm Queen → Demon) são lutas de
DPS plano. A postura adiciona **ritmo, leitura e payoff de burst** sem precisar de IA nova —
e amarra elemento, cards de attack/CDR e ultimate numa decisão tática ("guardo o ultimate para
a janela de break?"). Fórmula de referência durante o stagger:

```
dano_final = dano_base * damageMultiplier[ciclo] + bossMaxHp * maxHpBonusPct
```

---

## 4. Echo Team — companions que são seus próprios personagens (Kaezan World)

> Origem: `KAEZAN_GDD_v0.5.md §3` + `mapping/changes/features/companions`, `echo_team`,
> `hud_companions`. Estado no Fable: 💡 a feature flagship do FABLE_TRACK (F-A).

**O conceito central.** No Kaezan World você joga **1 Kaeli ativo + 2 companions automáticos**,
e os companions **são outros personagens da sua própria conta** — com stats, outfit, HP e kit
reais daquele char, controlados pela IA do servidor.

> "Companions são uma feature nativa do jogo, não um bot." — eles têm acesso direto ao estado
> autoritativo (HP de todos, resistências do mob, postura do boss), sem leitura de tela.

**Por que isto é, de longe, a ideia mais valiosa para o Fable.** O motor do gênero gacha é o
desejo de **colecionar e ver sua coleção em ação**. Hoje, depois de puxar 19 waifus, o jogador
usa **uma** por run; as outras ficam num menu. O Echo Team transforma a fantasia: você monta um
**time de 3 waifus suas** e elas lutam juntas. Cada pull novo passa a ter impacto direto na run
("agora minha Velvet 5★ luta ao lado da Sylwen"). É o elo que faltava entre o **lado gacha** e
o **lado gameplay**.

**O que torna isto complexo (e digno de um modelo forte):** IA de aliado boa de verdade —
mira, uso de skills com timing, **posicionamento que não bloqueia o jogador**, kiting para
waifus ranged, foco de alvo, tudo **determinístico** (mesma seed = mesmo comportamento do time);
rendering de aliados + barras de HP; e **balanceamento** (3 personagens não podem trivializar o
conteúdo — provavelmente os companions operam com um multiplicador de eficiência). Ver F-A.

---

## 5. Sealed Reward — o "gacha dentro da run" (Kaezan World)

> Origem: `mapping/changes/features/sealed_reward/` + `pilot_dojo` (integração).
> Estado no Fable: 🔜 ROADMAP T-30.

**O que é.** O baú de recompensa pós-boss tem **duas partes**:

- **Parte fixa/garantida** (shards/fragmentos) — não muda.
- **Parte rerollável** (o loot do boss) — pode ser re-rolada **uma vez** com uma "Echo Key",
  para o jogador **caçar um drop específico sem refazer a luta inteira**.

E a coleta é **estilo reward chest do Tibia**: o reward fica **pendente e persistente**
(sobrevive a logout/restart), o jogador é notificado e coleta quando quiser, revelando os
slots um a um. Sem popup forçado.

**Por que é bom no Fable.** Adiciona um **micro-momento de gacha** no fim de cada run vitoriosa
(revelação + reroll = dopamina), reaproveitando o vocabulário visual que já temos (FX de
abertura, sprites de item). É a ponte natural entre "matei o boss" e "valeu a pena".

---

## 6. Mastery / Wheel of Destiny — progressão persistente por personagem (Kaezan World ← Tibia)

> Origem: `mapping/changes/features/kaeli_mastery/` (que espelha a **Wheel of Destiny** nativa
> do Tibia). Estado no Fable: 💡 FABLE_TRACK F-B.

**O que é.** Uma **árvore de maestria por personagem**: o char acumula **pontos de maestria**
(jogando) e os gasta em **nodes** que dão bônus permanentes (stats, reforço de spell, bônus de
postura). No Tibia é a Wheel of Destiny; no Kaezan World foi reimplementada em KV como um modelo
espelhado (`points`, `spent`, `nodes.<id>`; nível derivado = `1 + points + spent`).

**Por que serve ao Fable.** Hoje a única progressão **por waifu** é a Ascensão (linear, via
shards). A Mastery dá um **eixo de progressão de longo prazo, com escolhas** — cada waifu vira
um pequeno projeto de build. Compõe com cards de run (temporário) e ascensão (linear) para criar
três camadas: **run (efêmero) · mastery (permanente, com escolha) · ascensão (permanente, linear)**.

---

## 7. Daily Hub & contratos (Kaezan World)

> Origem: `mapping/changes/features/daily_hub/`. Estado no Fable: ✅ (versão simples).

O Daily Hub original é um board de tarefas (kill / entrega / eventos) que alimenta a moeda
premium e é a fonte primária de progressão casual. **Já temos** a versão essencial (3 contratos
determinísticos/dia alimentados pelo resultado da run). Ideias ainda não portadas:

- **Tarefas encadeadas/semanais** (objetivos maiores que dão recompensas maiores).
- **Tarefa ligada ao "elemento do dia"** (rotação de modificador global — ver §9).
- **Streak/login reward** (recompensa crescente por dias consecutivos).

---

## 7.5 Decisões de fundação (correções de v0 — pedidas pelo dono)

> Estado: 🔜 viram trabalho na **Fase 6** do [ROADMAP.md](ROADMAP.md) (T-50..T-53).

Três decisões de design/arquitetura que corrigem atalhos rasos do v0:

### a) Um pipeline só de assets — o extractor animado, auto-curado por slot

Existe uma biblioteca **estática** em `C:\xampp\htdocs\assets` (31k ícones de item + outfits/
mounts/creatures + `outfit_layers`). Avaliamos usá-la para a UI e **descartamos** (decisão de
2026-06-12; ver ROADMAP T-50):

- Tudo que é **animado** (outfits/Kaelis, monstros, FX, missiles) fica no `AssetExtractor` — o
  xampp é estático e achataria a animação.
- Os **ícones de item** a UI já obtém do próprio extractor (categoria `objects`, single-frame
  limpo). Com equipamento reduzido a **6 slots sem backpack/legs/boots**, não há cobertura em
  massa a fazer; e o extractor pode **se auto-curar** por `clothes.slot`.
- Importar do xampp daria **mais** trabalho (normalizar framing + segunda fonte de verdade), não
  menos. Logo: **um pipeline só**; o xampp fica como referência/fallback, não como produção.

Princípio: **animação onde o olho percebe movimento; uma única fonte de verdade de assets**
(things/1500 via extractor), em vez de dois pipelines a sincronizar.

### b) Waifu = skin de uma das 4 classes Kaeli (não um kit raso por waifu)

O v0 inventou ~19 kits rasos. O **Kaezan World** já tinha 4 classes reais com kits espelhados de
spells do Tibia e **stance** (Tab troca o elemento dos slots 1-4): **Warrior · Sentinel · Shaman ·
Wizard** (`mapping/changes/features/kaeli_spell_library/`). Decisão: a waifu vira **skin** (nome,
raridade, outfit, stats-base, afinidade) de **uma classe**; o kit vem da classe. Colapsa 19 kits
inventados em 4 profundos, e novas classes entram **aos poucos** (ex.: Necromancer para `death`)
sem refatorar. Ver ROADMAP T-52. **Mecânica-chave a preservar:** a stance (postura) — é o que dá
profundidade tática ("luto em Gelo ou em Terra?") e amarra com a postura de boss e as reações (F-E).

### d) Persistência em MySQL — banco separado, estado mutável só (não conteúdo)

O dono tem MySQL rodando (XAMPP; o Canary usa o banco `otservbr-global`). Decisão: criar um banco
**`kaezan_fable` separado** para o estado mutável do jogador (conta, roster, equipamento,
inventário, maestria, pity/histórico de gacha, bestiário, dailies, run_results, replays,
leaderboard). Duas fronteiras inegociáveis:
- **Banco separado, nunca dentro de `otservbr-global`** — só compartilham a instância MySQL.
- **DB = estado mutável; conteúdo de design fica em código/JSON** (monstros, itens, defs de
  waifu/classe, cards, biomas). Balance e determinismo não devem depender de escrever no DB.
- **Nada de DB dentro do tick.** Já é assim: a conta é lida 1× no join (snapshot p/ o `GameWorld`)
  e escrita 1× no fim (`RewardService`). MySQL só troca o backing de `AccountStore`; a fronteira de
  determinismo já está de pé. Mantém-se o fallback JSON (boot sem MySQL). Ver ROADMAP T-54.

### c) Montaria = equipamento (reaproveitar visual subutilizado)

No Tibia montarias são cosméticas e subutilizadas. Decisão: viram um **slot de equipamento** que
dá **stats** e reaproveita o **visual** (`lookMount` — o `AssetExtractor` já lê patterns de mount).
A waifu aparece montada no mundo quando há mount equipado. Equipamento total é enxuto de propósito:
**6 slots** (`helmet, armor, weapon, necklace, ring, mount`) — itemização com identidade sem virar
um Diablo de afixos. Ver ROADMAP T-51.

---

## 8. Padrões de UX do OTClient que valem adotar

> Origem: `mapping/canary/client/` (ui_system, hud, visuals). Estado no Fable: vários ✅/🔜.

O OTClient tem 20 anos de refino de UX de ARPG isométrico. Padrões que traduzem bem para o
nosso canvas/Angular:

| Padrão OTClient | O que é | Aplicação no Fable |
|---|---|---|
| **Outfit window dirigida pelo servidor** | O cliente não tem lista própria de outfits; renderiza o que o servidor manda. | Nosso catálogo de waifus/monstros já vem do `/catalog`. Manter essa direção (server = fonte da verdade visual). ✅ |
| **MiniWindow + ProgressBar** | Janelas laterais reabríveis com barras nativas (foi como a Boss Posture apareceu). | HUD de postura/buffs como painéis reabríveis, não overlays fixos. 🔜 |
| **Attached effects (auras/asas)** | Efeitos visuais anexados à criatura via opcode, independentes do sprite. | Auras de raridade/ascensão na waifu (5★ brilha), telegraph de boss. 💡 |
| **Battle list / target window** | Lista de alvos com HP/elemento; clicar foca. | Frame de alvo no HUD (ROADMAP T-21). 🔜 |
| **Floating combat text + cores por tipo** | Números de dano com cor por origem/elemento. | Já temos; falta pop/empilhamento (T-20). ✅/🔜 |
| **Light system** | Criaturas/itens emitem luz (halo). | Ambiência de bioma (lava emite luz, cripta escura). 💡 |
| **Sons por criatura** (`monster.sounds`) | Cada monstro tem ids de som. | Não extraível (arquivos indisponíveis) — usar SFX CC0 (T-34). 🔜 |

**Princípio transversal do OTClient que devemos honrar:** _o cliente é burro de propósito_ —
ele renderiza o que o servidor descreve. É exatamente o nosso invariante "backend autoritativo".

---

## 9. Sistemas de mundo vivo (Kaezan World ← Tibia) — ideias para profundidade

> Origem: `KAEZAN_GDD_v0.5.md §10`, `mapping/canary/systems/`. Estado no Fable: 💡 backlog.

- **Elemento do dia / world change.** Rotação diária determinística de um elemento global que
  recebe buff (o kaezan-arena tinha isto). Cria variedade diária e um motivo para trocar de
  waifu conforme o dia. Casa com o Daily Hub e o Desafio Diário.
- **Pet/Companion expeditions (idle).** Waifus **fora do time** vão em expedições idle que pagam
  materiais com o tempo. Dá uso ao restante da coleção e respeita o pilar "casual 15 min".
- **Forge / Imbuements.** Loot do Tibia → materiais → encantamentos. Dá propósito à Mochila além
  de vender e cria um eixo de itemização ortogonal ao gacha (ROADMAP T-32 = lite; FABLE = full).
- **World boss / raid assíncrono.** No futuro multiplayer, um boss compartilhado sem agendamento.
  Distante, mas alinhado ao "MMO é bônus".

---

## 10. Mapa rápido: ideia original → onde virou trabalho no Fable

| Ideia (origem) | Documentada em | Vira trabalho em |
|---|---|---|
| Companions / Echo Team | §4 | **FABLE_TRACK F-A** |
| Mastery / Wheel | §6 | **FABLE_TRACK F-B** → ✅ Maestria de Eco (§11) |
| Kaeli profunda: traits/lore/afinidade/presentes/skins | §11 | ✅ refundação 2026-06-12 |
| Dojo + Level sync + Desafio | §2 | **FABLE_TRACK F-C** (desafio diário/leaderboard) |
| Geração de dungeon com intenção | (Tibia caves) | **FABLE_TRACK F-D** |
| Boss Posture completo + reações elementais | §3 | ROADMAP T-31 (MVP) → **FABLE_TRACK F-E** |
| Sealed Reward com reroll | §5 | ROADMAP T-30 |
| Imbuements/Forge | §9 | ROADMAP T-32 (lite) |
| Daily encadeado / elemento do dia / streak | §7, §9 | ROADMAP backlog |
| Padrões de HUD/UX do OTClient | §8 | ROADMAP T-20/T-21/T-22 |
| Um pipeline de assets (xampp descartado) | §7.5a | ROADMAP T-50 |
| Waifu = skin de classe + stance | §7.5b | ROADMAP T-52 |
| Montaria como equipamento (6 slots) | §7.5c | ROADMAP T-51 |
| Kit/IA fiel de monstro do Canary | §8 (princípio cliente-burro) | ROADMAP T-53 |

---

## 11. Profundidade de Kaeli — skins, afinidade, presentes e maestria (refundação 2026-06-12)

> Origem: pedido do dono ("poucas Kaelis, mas profundas" — inspiração Wuthering Waves) +
> Kaezan World (mastery §6). Estado no Fable: ✅ implementado.

**Decisão central: cortar para 9 e aprofundar.** O roster caiu de 13 para **9 Kaelis (3 por
raridade, todas as 4 classes cobertas)**; Tessa, Nyx, Lyra e Rosa saíram (sanitização de conta
devolve `CutKaeliRefundKaeros` por Kaeli removida). Em um gacha, o personagem É o produto —
um roster pequeno em que cada unidade tem identidade mecânica + narrativa vale mais que um
catálogo raso. Cada Kaeli agora tem **cinco eixos de identidade**:

1. **Trait de assinatura** (`TraitDef`, engine-suportado): a passiva que a diferencia dentro da
   classe — ex.: Velvet executa alvos <30% HP, Sylwen congela, Kaela é a tanque de -12% dano.
   Kinds data-driven (executioner/fortress/bulwark/pack_hunter/deadeye/slayer/skill_lifesteal/
   chiller/overcharge); novos traits = dado novo + 1 ponto de aplicação no GameWorld.
2. **Lore em camadas**: bio + personalidade + **4 ecos de memória** destravados por afinidade
   (níveis 2/4/6/8). Os ecos se cruzam (Mira recusou o convite de Kaela; Aurora sela a cripta
   do tier 3) — o roster é um elenco, não uma lista.
3. **Afinidade 1-10** (xp por run com ela ativa + presentes; marcos pagam Kaeros; +1% ATK/HP
   por nível na run). **Presentes**: qualquer item da Mochila (XP ∝ preço NPC), favoritos ×2,
   cap de 3/dia por Kaeli — dá uso afetivo ao loot do Tibia e cria sessão diária curta.
4. **Skins por outfit** (`SkinDef`): cada Kaeli tem 2-3 outfits do pipeline (os 7 outfits
   extraídos sem uso + recolors da paleta HSI). Unlock por afinidade (vínculo), ouro ou Kaeros
   (sink de moedas). A skin em uso vai no `KaeliLoadout` e renderiza no Hub e na run; addons de
   ascensão aplicam por cima de qualquer skin.
5. **Maestria de Eco** (= FABLE_TRACK F-B): árvore de 3 ramos × 4 nodes (Ofensiva / Defensiva /
   Eco), template por classe com nomes amarrados às skills reais e ramo Eco que **amplifica o
   trait**. Pontos por run (vitória 3 / derrota 1, com ela ativa), respec por ouro. Efeitos
   agregados em `MasteryAggregates` no início da run — nada de dispatch paralelo, nada de DB
   no tick.

**Camadas de progressão por Kaeli (ordem de horizonte):** cards de run (efêmero) → maestria
(permanente, com escolha) → ascensão (permanente, linear) → afinidade (permanente, afetiva).

---

## 12. Notas de mercado gacha — o que copiar e o que evitar

> Referências estudadas: Wuthering Waves, Genshin/HSR/ZZZ (HoYo), Arknights, Blue Archive,
> Nikke, FGO, Azur Lane. Estado: guia para as próximas decisões de produto.

**O que os líderes fazem e já adotamos:**
- **Poucos personagens, identidade altíssima** (WuWa/ZZZ): roster pequeno, cada unidade com
  passiva única + animações/skins próprias. → traits + skins + lore (✅ §11).
- **Afinidade com recompensa narrativa** (Blue Archive cafés, Nikke advise, FGO bond): jogar
  com o personagem desbloqueia história dele, não só stat. → ecos de memória (✅).
- **Dupes nunca são lixo** (universal): shards → ascensão (✅).
- **Pity transparente** (todos pós-Genshin): contador visível, 50/50 com garantia (✅).

**Próximos passos sugeridos, em ordem de impacto:**
1. **Echo Team (F-A) continua sendo o maior salto** — todo gacha de sucesso deixa a coleção
   lutar junta; é o elo coleção→gameplay.
2. **Banner rotativo com rerun** — hoje só Velvet tem banner. Rotação quinzenal determinística
   (seed da data) de 4★ em destaque + rerun do 5★ dá motivo de retorno sem conteúdo novo.
3. **Skins de evento/estação** (líder de receita em todo o gênero; aqui, líder de retenção):
   o pipeline de outfits torna barato lançar "skin do festival" sazonal — amarrar ao
   elemento-do-dia/eventos (§9) em vez de FOMO pago.
4. **Trilha de novato** ("login de 7 dias" / missões de tutorial que pagam um 4★ à escolha):
   padrão Arknights/HSR; barato e resolve o early-game.
5. **Modo de teste de Kaeli** (test drive de banner, padrão HoYo): deixar jogar 1 sala com a
   Kaeli em destaque mesmo sem possuí-la — vende o pull melhor que qualquer vitrine.
6. **Evitar**: stamina/energia (mata o "less grinding"), pity que reseta entre banners,
   dailies >15 min, e power creep de banner (cada 5★ nova invalidando a anterior — preferir
   nichos por trait/elemento, estilo Arknights).

---

## 13. Economia de combate, loot, poder e autoria (decisões 2026-06-13)

> Origem: sessão de feedback do dono jogando o MVP (morria muito no tier 2; loot lixo; magia
> bugada; queria autorar conteúdo). Estado: parte ✅ implementada, parte 📋 roadmap travado.

### a) Loot e drops com função — ✅ implementado
As tabelas cruas do Tibia entopem a mochila de lixo e dropam itens de slot removido (legs/feet).
Novo modelo no `DropLoot`/`TickPickup`:
- **Comida** (`GameData.IsFood`, resolve `GameConfig.FoodNameWords` → ids) cura `FoodHealPct` (5%)
  da vida máx **ao pegar** (heal-on-pickup; não há mecânica de usar item mid-run).
- **Poção de vida** (`GameData.PotionHealFraction`, por nome) cura 15/25/40% (basic/strong/
  great) — poções mais fortes caem em tiers mais altos, então a cura escala com o tier.
- **Equip** (slot válido) cai no chão pra equipar/vender.
- **Lixo** (ossos, peles, mats) é **auto-vendido virando ouro** no kill — sem clutter, e mantém
  o ouro fluindo (pedido do dono).

### b) Sustain baseline + dificuldade — ✅ implementado (tunável)
Player morria porque sustain/dano só vinham de card sorteado. Baseline garantido (sem card):
`BaselineRegenPctPerSec` 0.6%/s (+0.06%/s por run-level), `BaselineLifesteal` 2%. Dano
recalibrado por knobs globais: `PlayerDamageMult` 1.4 (damos +40% dano), `MonsterDamageTuning`
0.35→0.26 (recebemos ~26% menos). **Não nerfar mobs** (mantém fidelidade Tibia da IA) — a
dificuldade cai pelo lado do player. Tudo em GameConfig, marcado MVP/dificuldade.

### c) Bugs de magia — ✅ corrigidos
- **Parede**: player agora checa LOS no auto-attack ranged e skills `single`/`area` (mobs já
  checavam). `beam` anda tile a tile.
- **Geometria por direção**: `ConeTiles` reescrito por projeção forward/perp (meia-abertura 45°)
  — MESMA abertura angular nas 8 direções.
- **Mira "na reta"**: skill com alvo fora de alcance não fizzla mais; dispara na direção do alvo
  até o alcance máx ou antes da parede (`AimAlongLine`). RELACIONADO pendente: `beam` pode errar
  alvo fora de eixo (snap pra diagonal) — follow-up.

### d) Modelo de poder — 📋 travado, não implementado
**Princípio:** o personagem é o produto do gacha; o gear é farmável (o drop é peça central do
jogo — NÃO há gacha de arma). Mitiga pay-to-win.
- **Gear (stats)**: dropado nas dungeons (sets curados por tier), **sobe de nível** com materiais.
  Fonte de poder bruto, F2P.
- **Cores (sublimações estilo Wakfu)**: encaixam **só nas armas** (arma tem slots de core, +1 ao
  melhorar). Dão **passivas + efeitos únicos**, casados com o kit da Kaeli (ex.: core da Velvet
  amplia execução <30% HP; da Sylwen estende slow).
- **Duas gachas**: (1) personagem (Kaelis); (2) **cores**, banner separado. Cada Kaeli tem um
  **core de assinatura** em destaque no gacha de cores.
- **Contrapeso F2P**: existe pool de **cores craftáveis** com materiais de drop — quem não paga
  constrói build à altura.
- **Progressão "morrer ≠ zero progresso"**: gear-leveling + cores craftáveis. Árvore permanente
  estilo Hades **em espera** (evitar bloat de uma 3ª camada agora).

### e) Sets curados por tier — 📋 travado
5 sets temáticos (1 por tier dos `GameConfig.Tiers`), 6 slots cada (helmet/armor/weapon/necklace/
ring/mount). Set do tier dá o pico de poder pra encarar o próximo. Drop de elite/baú/boss.

### f) Autoria de conteúdo — 📋 painel admin in-game completo
O dono quer autorar, por conta própria:
- **Arenas (tiers 1-5)**: quais mobs (comuns/elites), tema/descrição, qual boss.
- **Kaelis**: nome, classe, skin principal, skins extras.
- **Itens/sets**: definir os itens, montar os sets, e **quem dropa o quê** (tabelas de drop).
Decisão: **painel admin in-game completo**. Implica primeiro tornar esse conteúdo **data-driven**
(hoje em `GameConfig.Tiers`/`Waifus.cs` C# + monsters.json/items.json) com persistência e API; o
painel é a UI que lê/escreve esse dado. Construir como **fatias verticais** (arena → kaeli →
sets/drops) pra provar a arquitetura cedo.

---

> **Lembrete final.** Tudo aqui é _design_. Ao implementar, não copie código Lua/C++ — releia a
> seção relevante, entenda a **intenção** e a **decisão de balanceamento**, e reescreva no
> idioma do Fable (engine determinística em C#, snapshot por SignalR, renderer Canvas). O valor
> do Kaezan World para nós é o **design já validado**, não os arquivos.
