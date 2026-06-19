# Roadmap — Refatoração das Kaelis

Este documento é a trilha nova para refatorar o roster, os kits e o gacha das Kaelis. Ele é
independente do `ROADMAP.md` antigo, que fica como histórico de ideias e não como fonte da verdade
para esta mudança.

## Tese

Kaeli jogável é sempre personagem premium. Não existem Kaelis 3★/4★ "de preenchimento": quando o
jogo ganha uma Kaeli nova, ela deve chegar com arte completa, lore, kit autoral, trait, afinidade,
skins e lugar real no mundo.

O gacha continua existindo, mas a raridade deixa de significar "personagem menor". Enquanto a
curadoria de itens não está pronta, qualquer roll que antes entregaria uma Kaeli 3★/4★ entrega
provisoriamente **1 item aleatório**. Depois, esse espaço pode virar pool curado de equipamentos,
presentes, shards, skins ou materiais.

## Decisões Fechadas

- Todas as Kaelis jogáveis são **5★**.
- O roster inicial refatorado terá 6 Kaelis definidas + 1 Earth a criar.
- As Kaelis antigas não precisam ser preservadas como jogáveis. Elas podem virar NPCs, skins,
  memórias ou simplesmente sair durante esta refundação.
- Não criar 4★ só para "valorizar" 5★. A valorização vem do gacha dar item comum na maior parte
  das vezes e Kaeli premium quando acerta personagem.
- Kits podem e devem ser ajustados/criados por arquétipo, mas continuam usando os shapes
  data-driven existentes (`single`, `beam`, `nova`, `area`, `cone`, `chain`, `ring`, `field`,
  `barrage`, `summon`, `buff`). Não criar um dispatch paralelo no engine.
- Backend continua autoritativo; frontend só renderiza/interpola.
- Constantes de simulação novas entram em `Domain/GameConfig.cs`.

## Roster Alvo

| Kaeli | Elemento | Alcance | Função de fantasia |
|---|---|---:|---|
| **Eloa** | Holy | ranged | anjo/serafim de luz, julgamento e absolvição |
| **Seren** | Physical | melee | cavaleira astral, duelo e disciplina |
| **Velvet** | Death | ranged | pesadelo, maldição, DoT e execução |
| **Rin** | Fire | ranged | súcubus, pacto, charme e burn |
| **Rynna** | Energy | melee | dragoa guerreira de raio, engage e stun |
| **Lunara** | Ice | melee | lebre lunar, mobilidade e slow |
| **Earth TBD** | Earth | TBD | fechar o círculo elemental sem repetir Sage/terra druídica |

Distribuição inicial: 3 ranged (`Eloa`, `Velvet`, `Rin`), 3 melee (`Seren`, `Rynna`, `Lunara`) e a
Earth decide o desempate depois. Se o combate pedir mais variedade, a Earth pode ser ranged
mineral/gravidade ou melee guardiã de pedra.

## Assets Autorais

| Pasta atual | Destino proposto | Ação |
|---|---|---|
| `frontend/public/assets/kaelis/kaelis-1` | `eloa` | renomear pasta após consolidar ID |
| `frontend/public/assets/kaelis/astra` | `seren` | renomear pasta após consolidar ID |
| `frontend/public/assets/kaelis/velvet` | `velvet` | manter |
| `frontend/public/assets/kaelis/kaelis-2` | `rin` | renomear pasta após consolidar ID |
| `frontend/public/assets/kaelis/kaelis-3` | `rynna` | renomear pasta após consolidar ID |
| `frontend/public/assets/kaelis/kaelis-4` | `lunara` | renomear pasta após consolidar ID |

Cada pasta nova já tem o pacote visual completo: `idle-1..3`, `thumb`, `banner`, `wallpaper`,
`bg-landscape` e `bg-portrait`. O `manifest.json` precisa ser regenerado/atualizado para todos os
IDs finais, porque hoje só registra a Velvet.

## Cuidado Com IDs

O projeto ainda é jovem, então podemos refatorar com mais liberdade, mas IDs persistidos ainda
merecem respeito. Existem duas opções válidas:

1. **Conservadora:** manter IDs antigos quando a personagem é uma substituição direta. Exemplo:
   `waifu:aurora` passa a se chamar Eloa. Isso preserva contas locais sem migração.
2. **Limpa:** usar IDs finais (`waifu:eloa`, `waifu:seren`, etc.) e adicionar sanitização/migração
   para contas existentes. Como o projeto tem menos de uma semana, esta provavelmente é a opção
   melhor se quisermos nomes limpos para sempre.

Decisão recomendada: usar IDs finais e criar uma migração simples no `AccountSanitizer`.

## Roadmap

### K-01 — Congelar a Nova Direção

**Objetivo:** registrar no README a tese "Kaelis jogáveis são sempre 5★" e abandonar a noção de
3★/4★ jogáveis.

**Arquivos prováveis:** `README.md`, este documento.

**Aceite:**
- README descreve o modelo novo sem falar em roster 3/4/5★.
- O texto deixa claro que rolls não-Kaeli dão item aleatório por enquanto.
- Não há mudança de gameplay ainda.

### K-02 — Reescrever o Roster Base

**Objetivo:** substituir o roster atual por Eloa, Seren, Velvet, Rin, Rynna e Lunara, todas 5★.

**Arquivos prováveis:** `backend/src/KaezanArenaFable.Api/Domain/Waifus.cs`,
`backend/src/KaezanArenaFable.Api/Meta/AccountSanitizer.cs`,
`frontend/public/assets/kaelis/manifest.json`.

**Instruções:**
- Criar/ajustar `WaifuDef` para as 6 Kaelis.
- Remover Kaelis antigas do pool jogável.
- Definir descriptions, personalities, 4 ecos de memória e presentes favoritos provisórios.
- Atualizar `StarterWaifuId` para a Kaeli inicial desejada. Sugestão: `waifu:seren` se quisermos
  começo melee simples, ou `waifu:eloa` se quisermos apresentação mais premium.
- Atualizar `FeaturedFiveStarId` para o banner inicial.
- Atualizar `manifest.json` com as artes das 6.
- Se IDs mudarem, sanitizar contas locais: trocar removidas por compensação ou por starter.

**Aceite:**
- Catálogo mostra só as 6 Kaelis 5★.
- Página Kaelis renderiza arte autoral para todas.
- Conta nova inicia com uma Kaeli válida.
- Contas antigas não quebram ao carregar.

### K-03 — Definir Classes/Kits Autorais por Kaeli

**Objetivo:** parar de depender de classes genéricas quando elas não servirem à fantasia. Cada
Kaeli 5★ ganha um arquétipo claro, mas ainda data-driven por shape.

**Arquivos prováveis:** `backend/src/KaezanArenaFable.Api/Domain/Classes.cs`,
`backend/src/KaezanArenaFable.Api/Domain/GameConfig.cs`,
`backend/src/KaezanArenaFable.Api/Engine/GameWorld.cs` somente se algum shape existente precisar
de suporte já previsto.

**Kits propostos:**

| Kaeli | Arquétipo | Slots sugeridos |
|---|---|---|
| Eloa | Holy ranged | `single` lança de luz, `barrage` julgamento, `beam` raio sacro, `ring` halo, ult `nova` absolvição |
| Seren | Physical melee | `single` corte preciso, `chain` avanço entre alvos, `cone` arco de espada, `buff` postura, ult `nova` zênite |
| Velvet | Death ranged | `single` death strike, `area` maldição/DoT, `beam` pesadelo, `summon` sombra, ult `nova` praga |
| Rin | Fire ranged | `single` beijo de brasa, `chain` contrato ardente, `field` salão em chamas, `cone` asas de cinza, ult `barrage` baile infernal |
| Rynna | Energy melee | `single` garra elétrica, `cone` cauda trovejante, `chain` descarga curta, `buff` escama condutora, ult `nova` coração da tempestade |
| Lunara | Ice melee | `single` corte lunar, `chain` saltos de geada, `field` jardim congelado, `ring` crescente, ult `nova` lua nova |

**Aceite:**
- Cada Kaeli joga de forma diferente.
- Nenhuma Kaeli nova é apenas "elemento trocado" de outra.
- Cooldowns, dano, range e FX estão em dados/config, não hardcoded no tick.
- `dotnet build` passa.

### K-04 — Traits Assinatura

**Objetivo:** criar traits que diferenciem as Kaelis sem duplicar o kit inteiro.

**Sugestões:**
- Eloa: dano extra contra undead ou bônus de postura em alvo marcado por Holy.
- Seren: bônus contra alvo único ou pressão de postura em melee.
- Velvet: executar alvos com HP baixo, mantendo a identidade atual.
- Rin: lifesteal leve em inimigos queimando ou burn mais forte após chain.
- Rynna: ultimate carrega ao aplicar stun/paralyze curto.
- Lunara: bater em alvo lento dá crit/haste breve.

**Aceite:**
- Traits aparecem na UI.
- Traits são suportadas pelo engine de forma determinística.
- Valores novos ficam em `GameConfig.cs` quando forem constantes de simulação.

### K-05 — Gacha Provisório Sem 3★/4★ Kaeli

**Objetivo:** remover Kaelis 3★/4★ do resultado do gacha. Enquanto não existe curadoria de itens,
roll não-Kaeli entrega 1 item aleatório.

**Arquivos prováveis:** `backend/src/KaezanArenaFable.Api/Meta/GachaService.cs`,
`backend/src/KaezanArenaFable.Api/Meta/AccountState.cs`,
`frontend/src/app/pages/recruit/recruit.ts`.

**Regras provisórias:**
- 5★ continua sendo chance de Kaeli.
- Resultado não-5★ vira item aleatório com quantidade 1.
- Pity de 5★ continua funcionando.
- Pity/garantia de 4★ deve ser removido, ignorado ou convertido para "item raro" depois.
- UI do reveal precisa mostrar item e sprite quando o resultado não for Kaeli.

**Aceite:**
- Pull nunca entrega Kaeli 3★/4★.
- Pull não-5★ adiciona 1 item real ao inventário.
- Pull 5★ entrega Kaeli ou dupe convertido conforme regra existente.
- O frontend não quebra ao revelar resultados mistos de Kaeli/item.

### K-06 — Earth 5★

**Objetivo:** criar a sétima Kaeli para fechar o círculo elemental.

**Decisões em aberto:**
- Earth ranged: rainha mineral, gravidade, monólitos, raízes antigas.
- Earth melee: guardiã de pedra, pugilista sísmica, armadura viva.
- Nome. Candidatos: `Ivyra`, `Sienna`, `Talia`, `Maelis`, `Gaia`.

**Aceite:**
- Earth entra como 5★ com arte, lore, kit e trait próprios.
- Não repete a fantasia de Sage/druida verdejante, caso Sage vire NPC/legado.
- Fecha a matriz elemental: Holy, Physical, Death, Fire, Energy, Ice, Earth.

### K-07 — Limpeza De UI E Texto

**Objetivo:** remover linguagem antiga de raridade baixa nas telas de Kaelis/recruit.

**Arquivos prováveis:** `frontend/src/app/pages/kaelis/kaelis.ts`,
`frontend/src/app/pages/recruit/recruit.ts`, `frontend/src/app/core/types.ts`.

**Aceite:**
- UI não vende 3★/4★ como personagens jogáveis.
- Filtros, chips e textos de roster refletem "Kaelis premium".
- Reveal distingue claramente item comum vs Kaeli.

### K-08 — Balance E Verificação

**Objetivo:** garantir que o novo roster passa por uma run real e que nenhum kit domina por erro
óbvio de número.

**Verificação mínima:**
- `dotnet build`.
- `npx ng build`.
- Testar pelo menos uma run tier 1 com Seren ou Lunara.
- Testar uma run com uma ranged (Eloa, Rin ou Velvet).
- Fazer 10-pull e confirmar item aleatório + eventual Kaeli.

**Aceite:**
- Builds verdes.
- Run inicia com cada Kaeli do roster.
- Gacha adiciona itens ao inventário sem erro.
- Nenhum asset autoral cai no placeholder por falta de manifest.

## Depois

- Curadoria real do pool de itens do gacha.
- Armas assinatura por Kaeli.
- Skins premium/afinidade.
- Banner por personagem com história curta.
- Eventual segunda Kaeli 5★ por elemento para variar melee/ranged sem criar personagens menores.
