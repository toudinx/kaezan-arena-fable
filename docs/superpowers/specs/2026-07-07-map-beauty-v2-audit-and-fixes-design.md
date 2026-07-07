# Map Beauty v2 — Auditoria + fixes por causa raiz (Design)

**Data:** 2026-07-07 · **Status:** aprovado em brainstorm (aguardando review do spec)
**Antecessor:** `2026-07-07-map-beauty-and-map-lab-design.md` (entregue; este spec corrige a qualidade visual que ficou aquém)
**Sucessor previsto:** Map Lab v2 (catálogo de tiles, tipos de mapa, salvar mapas) — brainstorm separado, fora deste escopo.

## Problema

A fatia map-beauty entregou o pipeline (RME → tilesets.json → painter v2 → Map Lab), mas o
resultado visual tem defeitos objetivos, observados nas capturas de 2026-07-07 (Echoing Den,
Scaled Lair ×2, Shadowed Crypt, Uruk Fort):

1. **Costuras duras entre terrenos.** O `tilesets.json` só tem border sets genéricos
   (`grass->none`, `dirt->none`, ...) — as transições par-a-par do RME (`grass->dirt` etc.)
   foram deliberadamente puladas na Task 2 da fatia anterior (inner borders de grounds ignorados).
2. **Quadrados de lava sem borda** (Scaled Lair): lava não é família minerada — T4/T5 ainda
   pintam pela palette legada `Ground`, que não participa da passada de borders.
3. **Triângulos pretos** (Uruk Fort): causa não confirmada — sprite ausente no manifest ou peça
   de border 64px desenhada cortada.
4. **Pedras/boulders cortados** (Uruk Fort): a guarda de decor 1×1 da Task 9 cobre só as
   palettes dos biome defaults; decor vindo de prefab e slots de wall set podem passar por fora.
5. **Estruturas ilegíveis** (casa de tijolos do Scaled Lair): defeito de render (topos de parede,
   sprites 64px) ou crop mal curado — a diagnosticar.
6. **Wall set `crystal wall` lê "blocado"/chapado** — curadoria de família possivelmente errada
   para o bioma.

## Barra de aceite (decidida no brainstorm)

**Zero artefatos objetivos + costuras suaves.** Concretamente:

- Nenhum triângulo preto, sprite cortado ou terreno sem borda em 5 tiers × 3 seeds no Map Lab.
- Toda costura entre famílias de chão usa a transição específica do RME quando ela existe;
  `->none` só quando o RME também não tem par específico.
- Estruturas autorais legíveis: consertar render primeiro; crop que continuar confuso mesmo
  renderizando certo é **re-curado** (trocado por crop melhor do otservbr), não removido.
- Mapas devem parecer feitos no Remere's Map Editor.

## Abordagem (decidida no brainstorm)

**A — Auditoria primeiro, depois fixes por causa raiz.** A auditoria usa o Map Lab já entregue
(custo baixo) e evita consertar sintoma. As demais opções (fixes diretos sem auditoria; harness
de regressão visual por screenshot) foram descartadas: a primeira arrisca segunda rodada de
diagnóstico, a segunda é infra pesada demais para uma fatia de calibração.

## Arquitetura da fatia

### Task 0 — Auditoria (alimenta todo o resto)

- Map Lab: 5 tiers × 3 seeds fixos, screenshot de cada preview.
- Catalogar cada classe de artefato num doc de auditoria
  (`docs/superpowers/specs/2026-07-07-map-beauty-v2-audit.md`) com **causa raiz verificada**
  no código/dados — não hipótese. Classes esperadas: gap do conversor · bug do painter ·
  gap de extração de sprite · crop de prefab ruim · curadoria de família ruim.
- Entradas já conhecidas: pair borders nunca emitidos; lava sem família; `crystal wall` chapado.
- Incógnitas a fechar: triângulos pretos, boulders cortados, casa ilegível.

### Conversor — pair borders + famílias faltantes

- `tools/map-importer/lib/tilesets.mjs` passa a emitir sets `A->B`:
  - outer borders com `to="B"`;
  - **inner borders de grounds** (pulados na fatia anterior) — atenção à direção/semântica
    (inner é desenhado no tile do próprio brush).
- Minerar as famílias que os tiers realmente precisam (lava e afins para T4/T5), atualizando
  `tools/map-importer/tilesets-config.json`.
- **Gate de predição estendido:** o `test/predict.test.mjs` contra o `otservbr.otbm` passa a
  validar também costuras par-a-par (≥95%) — mesma disciplina que pegou a inversão c*/d*.

### Painter & biomas

- `TilesetRegistry.Borders(from, to)` já resolve par exato → `->none`; mudança pequena:
  verificar a semântica de direção dos inner sets na passada do `BorderAutotile`.
- T4/T5 ficam **100% family-based** (nenhuma palette legada `Ground` produzindo mancha sem
  borda). Reseed do `biomes.json` em disco (mesmo padrão da Task 6 anterior; edições de admin
  antigas são perdidas — aceitável e documentado).

### Fixes de sprite/prefab (guiados pela auditoria)

- Triângulos pretos: conforme causa — extração de sprite faltante (`content-config.json` +
  re-run do extractor) ou fallback do conversor para peças 64px.
- Boulders cortados: estender a guarda 1×1 para as superfícies que hoje passam por fora
  (decor de prefab, slots de wall set), conforme o que a auditoria confirmar.
- Estruturas: consertar render primeiro (topos de parede, ordem de desenho, 64px);
  crops ilegíveis após o fix são substituídos por crops melhores do otservbr.

### Curadoria

- Re-escolher famílias por tier de modo que os pares escolhidos **tenham** transição RME entre si.
- `crystal wall`: trocar a família ou corrigir o set se não tiver leitura boa.
- Escolhas anotadas em comentário nos defaults de `Biomes.cs` (padrão da fatia anterior).

### Verificação

- Testes: predict estendido (map-importer), xunit backend, `dotnet build` + `npx ng build` limpos.
- Visual: screenshots antes/depois no Map Lab por tier, comparados com as 5 capturas de
  2026-07-07.
- **UM rebaseline deliberado de golden** + regravação da bateria de replays no FIM da fatia
  (não por task). `--golden-check` e `--replay-check` verdes.

## Restrições herdadas (inegociáveis)

- Determinismo do engine: só `Rng` da run, ordem de varredura fixa; passada de borders 100%
  rng-free.
- Constantes novas só em `Domain/GameConfig.cs`. C# novo sem `var`. Código/strings em inglês.
- Fonte RME e `otservbr.otbm` fora do repo (caminhos no `tools/map-importer/config.json`);
  só o `tilesets.json` derivado é commitado.
- Commits pequenos direto na `main`, stage seletivo.

## Forma prevista do plano

~6 tasks (auditoria → conversor → painter/biomas → fixes de sprite/prefab → curadoria →
rebaseline+docs), cada uma com **Model · Effort** declarados, no formato dos planos do repo.
