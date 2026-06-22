---
name: franchise-mining
description: >-
  Mapeia uma obra (gacha, anime, mangá, light novel) contra o universo do Kaezan Arena Fable e
  produz um doc de pesquisa acionável: resumo, por que combina, lore hooks adaptáveis, mecânicas
  mapeadas aos shapes de skill/sistemas do jogo, arquétipos/Kaelis que ela inspira, e uma seção
  final "Candidatos a roadmap desktop". Tem modos: gacha, anime/manga, economia-rival
  (pity/banner/economia em números), conceito-kaeli (pitch de Kaeli nova) e scout (indicar obras
  novas que combinam). Use SEMPRE que o usuário pedir para "mapear/analisar" uma franquia (Wuthering
  Waves, Genshin, Honkai Star Rail, Nikke, Zenless Zone Zero, Solo Leveling, Mushoku Tensei, Douluo
  Dalu, Tales of Demons and Gods, etc.), "ver o que dá pra puxar de X pro projeto", "sugira obras
  novas que combinam com o universo", "mapeia a economia/pity do gacha Y", ou "cria um conceito de
  Kaeli baseado em Z". É a skill principal do roadmap_web_research.md no Claude Code Web. Não
  implementa nada — só produz markdown de pesquisa em docs_web/.
---

# Franchise Mining

Extrai **ideias, regras e composição de sistemas** de uma obra externa e as traduz para o que faz
sentido no Kaezan Arena Fable. É a versão "trilha web" da filosofia do `docs/DESIGN_NOTES.md`:
reusamos *design*, nunca arquivos nem nomes registrados.

## Quando rodar (no Claude Code Web)
Disparada pelos prompts `RS-*` do `docs_web/roadmap_web_research.md`. Respeita a doutrina
`docs_web/CLAUDE_WEB.md`: **lê** só `README.md` + `docs_web/roster_digest.md` (+ doc nomeado),
**escreve** só em `docs_web/research/` (mapeamentos, economia) ou `docs_web/concepts/` (Kaelis).
Não toca em código.

## Princípios inegociáveis
- **Nunca copiar IP.** Nada de nomes próprios, personagens ou marcas registradas no entregável.
  Extraia o *padrão de design* e reescreva no universo Kaezan. Se citar a obra-fonte, é só como
  referência ("inspirado no pacing de banner de X"), nunca como conteúdo a colar no jogo.
- **Ancorar no que existe.** Mapeie mecânicas aos **shapes de skill** do engine
  (`single|beam|nova|area|cone|chain|ring|field|barrage|summon|buff`) e aos sistemas já citados no
  README (banners com pity, coleção, dailies, bestiário, dungeon roguelike, papéis Knight/Mage/Archer).
- **Cobrir buracos.** Priorize elementos/raças/papéis pouco usados no roster atual (`roster_digest.md`).
- **Fechar com gancho.** Todo doc termina em **"Candidatos a roadmap desktop"** — é o que o
  `roadmap_web_specs.md` consome para gerar roadmap de implementação.

## Modos

### `gacha` — mapear um gacha (WuWa, Genshin, HSR, Nikke, ZZZ)
Foco em **sistemas**: estrutura de banner, progressão de personagem, eco do endgame, UX de coleção,
identidade de elenco. Ver formato abaixo.

### `anime/manga` — mapear uma obra narrativa (Solo Leveling, Mushoku Tensei, Douluo Dalu, ToDG)
Foco em **lore e fantasia de poder**: sistemas de magia/cultivo, hierarquias, progressão, conceitos
de mundo que viram lore/mecânica. Mesmo formato.

### `economia-rival` — pity/banner/economia em números
Tabela acionável: taxa base por raridade, soft/hard pity, garantia (50/50), moedas premium vs
gratuitas, custo por pull, fontes de moeda por dia, pacing de banner. Compare com o que o README
diz do nosso pity e aponte ajustes candidatos (vira input de balance no desktop).

### `conceito-kaeli` — pitch de Kaeli nova → `docs_web/concepts/<nome>.md`
A partir de um arquétipo/raça/elemento que falta (ver matriz no digest), proponha:
- Nome provisório, título, **elemento** (cobrir buraco), **papel** (Mage/Archer/Knight), raça/silhueta.
- **Personalidade** (no tom seco/charmoso do roster) + 2-4 fragmentos de lore curtos.
- **Trait de assinatura**: 1 ideia mecânica mapeada a um *kind*/shape existente (Value/Param + tag).
- **Identidade visual** + **cenário ancorado** (pronto para a skill `kaeli-asset-prompts` depois).
- IDs sugeridos `waifu:*`/`trait:*` (só viram estáveis quando entrarem no backend).

### `scout` — indicar obras novas → `docs_web/research/scout-<data>.md`
Indique **3 obras** (qualquer mídia) que combinam com o universo Kaezan e **por quê**, cada uma com
1 gancho mecânico + 1 de lore + qual buraco do roster/sistema ela ajudaria a preencher. Não repita
obras já mapeadas em `docs_web/research/`.

## Formato do entregável (modos gacha / anime-manga)

```markdown
# <Obra> — Mapeamento Kaezan  (modo: gacha | anime/manga)

## 1. Resumo
<1 parágrafo do que é a obra> — e **por que combina com Kaezan** (2-3 linhas).

## 2. Lore hooks adaptáveis
- <conceito> → como vira lore/mundo Kaezan (sem nomes da obra)

## 3. Mecânicas adaptáveis
| Mecânica da obra | Tradução p/ o Fable | Shape/Sistema âncora |
|---|---|---|
| ... | ... | ex: field + dailies |

## 4. Arquétipos / Kaelis que inspira
- <arquétipo> — elemento sugerido (cobre buraco?) · papel · 1 linha de traço

## 5. Riscos / o que NÃO puxar
- <coisas que não cabem no escopo/tom, ou que seriam cópia de IP>

## 6. Candidatos a roadmap desktop
- [ ] <unidade de trabalho clara> — por que vale, e em qual sistema mexe
```

## Notas
- **Profundidade calibrada:** entregável objetivo e escaneável, não enciclopédia. O valor está nos
  *candidatos a roadmap*, não em recontar a obra.
- Uma obra por doc. Nome do arquivo em kebab-case, igual ao item do roadmap de research.
- Isto gera **pesquisa em markdown**, não código nem imagens.
