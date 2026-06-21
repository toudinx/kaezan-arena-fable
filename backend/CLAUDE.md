# CLAUDE.md — Backend (KaezanArenaFable.Api)

Escopo local do backend. O `CLAUDE.md` raiz vale acima deste; aqui só o que é específico do .NET.

## Stack

- ASP.NET Core **net8.0**, `Nullable` + `ImplicitUsings` ligados, `InvariantGlobalization`.
- SignalR para o canal de jogo; EF Core + **Pomelo MySQL** para persistência de conta.
- Projeto único: `src/KaezanArenaFable.Api/`.

## Build & run

```bash
cd backend/src/KaezanArenaFable.Api
dotnet build            # deve passar sem erros antes de concluir qualquer task
dotnet run              # sobe em http://localhost:5210 (proxy do frontend aponta pra cá)
```

EF Core (rode a partir de `src/KaezanArenaFable.Api`):

```bash
dotnet ef migrations add <Nome>   # após mudar AccountEntities/AccountDbContext
dotnet ef database update
```

## Namespaces (mantenha as fronteiras)

- `Domain/` — dados + config. **Toda constante de simulação mora em `Domain/GameConfig.cs`.**
  Conteúdo data-driven: `Waifus.cs`, `Cards.cs`, `Biomes.cs`, `Equipment.cs`, `Classes.cs`, etc.
- `Engine/` — simulação determinística. `GameWorld` (tick), `RunManager` (hosted service que
  roda os ticks e empurra snapshots), `DungeonGenerator`, `Rng` (xorshift seedado), `GameDtos`.
- `Meta/` — conta, gacha, dailies, recompensas, mastery. `Persistence/` tem EF Core
  (`AccountDbContext`, `AccountEntities`, migrations) + os dois `IAccountRepository`
  (`JsonFileAccountRepository`, `MySqlAccountRepository`).
- `Hubs/` — `GameHub` (mapeado em `/hub/game`).
- `Api/` — `MetaEndpoints` (minimal API, mapeado em `Program.cs`).
- `Content/` — `ContentStore` (conteúdo editável em runtime).

## Invariantes do engine (críticas)

- **Determinismo:** dentro do tick use **apenas** o `Rng` da run. Proibido `Random`,
  `DateTime.Now`, `Guid.NewGuid()`, ou iterar coleção sem ordem estável.
- **Backend é autoritativo:** combate/movimento decididos aqui; o cliente só renderiza.
- Tick de 100ms (`GameConfig.TickMs`); snapshots por conexão via `RunManager`.
- **IDs estáveis** (`waifu:*`, `card:*`, `banner:*`, espécies do Tibia) — renomear quebra
  persistência de conta. Migre dado, não renomeie ID.
- Skills são data-driven por *shape* (`single|beam|nova|area|cone|buff`). Skill nova =
  parametrizar shape existente, não criar dispatch paralelo.

## DI

Serviços registrados em `Program.cs` como singletons. `AddAccountPersistence` escolhe o
repositório (JSON vs MySQL) por configuração. Ao adicionar serviço novo, registre lá.

## Ao concluir

- `dotnet build` limpo.
- Se mudou shape de DTO/endpoint, alinhe `frontend/src/app/core/types.ts` e
  `api.service.ts` / `game-client.service.ts`.
