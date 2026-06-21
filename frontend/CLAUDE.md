# CLAUDE.md — Frontend (Angular)

Escopo local do frontend. O `CLAUDE.md` raiz vale acima deste; aqui só o que é específico do Angular.

## Stack

- **Angular 21** standalone (sem NgModules), TypeScript ~5.9, **signals** em vez de RxJS
  onde der. `@microsoft/signalr` para o canal de jogo. Vitest + jsdom para testes.

## Build & run

```bash
cd frontend
npm start               # ng serve em http://localhost:4200 (proxy.conf.json → backend :5210)
npx ng build            # deve passar sem erros antes de concluir qualquer task
npm test                # vitest
```

O dev server **não** sobe o backend — rode `dotnet run` em paralelo. `/api` e `/hub`
(WebSocket) são proxied pra `localhost:5210` via `proxy.conf.json`.

## Estrutura

- `core/` — serviços e lógica pura. `api.service.ts` (REST), `game-client.service.ts`
  (SignalR), `assets.service.ts` (sprites), `renderer.ts` (canvas puro), `types.ts`
  (DTOs espelhando o backend), `sound.service.ts`, `core/ui/` (componentes reutilizáveis).
- `pages/` — componentes standalone, um por rota, **template inline** (sem `.html` separado).
  Ex.: `mode/`, `game/`, `kaelis/`, `backpack/`, `prerun/`, `hunt/`, `admin/`.
- `shell/` — layout/navegação raiz.

## Convenções

- Componente standalone com `imports: [...]` explícito e `template` inline (veja
  `pages/mode/mode.ts`). Use control flow novo (`@if`, `@for` com `track`), não `*ngIf`/`*ngFor`.
- Estado reativo: `signal()` / `computed()`. Evite `Subject`/`BehaviorSubject` salvo
  interop com APIs RxJS.
- **Sprites só via `AssetsService`** (`drawOutfit`/`drawObject`/`drawEffect`/`drawMissile`).
  Nenhum componente mapeia caminho de arquivo de sprite direto.
- **Frontend nunca simula combate/movimento** — apenas interpola snapshots e renderiza.
  O backend é autoritativo.
- Tipos vindos do backend ficam em `core/types.ts`; mantenha em sincronia com `Engine/GameDtos.cs`
  e `Api/MetaEndpoints.cs`.

## Ao concluir

- `npx ng build` limpo.
- Se um DTO mudou no backend, atualize `core/types.ts` no mesmo passo.
