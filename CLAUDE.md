# CLAUDE.md — Kaezan Arena Fable

Instruções para assistentes de IA trabalhando neste repo.

## Primeiros passos (toda sessão)

1. Leia `README.md` — é a documentação viva. Confie nele acima de qualquer outro `.md`.
2. Identifique de qual fila a tarefa vem:
   - **Codex** (task pequena/bem-especificada): leia `docs/ROADMAP.md` §0 + a task inteira.
   - **Claude Fable 5** (feature grande/cross-cutting): leia `docs/FABLE_TRACK.md` (critérios +
     a feature inteira) e a seção do `docs/DESIGN_NOTES.md` que ela referencia.
   `docs/GDD.md` e `docs/DESIGN_NOTES.md` dão o contexto de design (este último é a base de
   ideias do Tibia/Canary + Kaezan World).
3. Não leia arquivos especulativamente; só o que a tarefa pede.
4. Ao consultar APIs de biblioteca (Angular, EF Core, SignalR, ASP.NET Core), use **context7**
   para obter documentação versionada e precisa: adicione `use context7` ao prompt de busca.
5. Ao concluir uma task/feature, marque-a `[x]` no ROADMAP.md / FABLE_TRACK.md com 1 linha de resumo.

## Invariantes inegociáveis

- **Backend autoritativo.** Frontend nunca simula combate/movimento — apenas interpola e renderiza.
- **Determinismo do engine.** `GameWorld` usa apenas o `Rng` da run (xorshift seedado).
  Nunca use `Random`, `DateTime.Now`, ou iteração de coleção instável dentro do tick.
- **Todas as constantes em `Domain/GameConfig.cs`.** Nunca hardcode valor de simulação em outro lugar.
- **IDs estáveis** (`waifu:*`, `card:*`, `banner:*`, nomes de espécies do Tibia): não renomear —
  quebra persistência de conta.
- Tick de 100ms (`GameConfig.TickMs`); snapshots enviados por conexão via `RunManager`.
- Skills são data-driven por *shape* (`single|beam|nova|area|cone|buff`) em `Domain/Waifus.cs`.
  Para skill nova, prefira parametrizar um shape existente a criar dispatch paralelo.

## Convenções

- Backend: namespaces `Domain` (dados/config), `Engine` (simulação), `Meta` (conta/gacha/dailies),
  `Hubs`, `Api`. Mantenha as fronteiras.
- Frontend: `core/` (serviços + renderer puro), `pages/` (componentes standalone com template inline),
  `shell/`. Signals em vez de RxJS onde possível.
- Sprites: somente via `AssetsService` (drawOutfit/drawObject/drawEffect/drawMissile).
  Nenhum componente deve mapear caminhos de arquivo de sprite diretamente.
- Assets novos: edite `tools/AssetExtractor/content-config.json` e re-rode o extractor
  (instruções no README). Monstros novos: adicione o `.lua` em `tools/convert-monsters/config.json`
  e re-rode `node convert.mjs` (o extractor puxa lookTypes/loot do monsters.json sozinho).
- Arte de Kaeli (GPT Image + ComfyUI), cutscenes (idle/summon/skill/reels via Remotion/ComfyUI) e
  sprites autorais: metodologia PC↔celular em `docs/WORKFLOW_imagem_e_cutscenes.md`; roadmap único em
  `docs/roadmap_producao_visual.md` (3 etapas: imagem estática · movimento · sprites).

## Após implementar

- Atualize o `README.md` se o comportamento visível mudou.
- `dotnet build` no backend e `npx ng build` no frontend devem passar sem erros antes de concluir.
