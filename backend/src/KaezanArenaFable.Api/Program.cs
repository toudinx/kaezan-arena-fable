using KaezanArenaFable.Api.Api;
using KaezanArenaFable.Api.Content;
using KaezanArenaFable.Api.Domain;
using KaezanArenaFable.Api.Engine;
using KaezanArenaFable.Api.Hubs;
using KaezanArenaFable.Api.Meta;
using KaezanArenaFable.Api.Meta.Persistence;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<GameData>();
builder.Services.AddSingleton<ContentStore>();
builder.Services.AddSingleton<MonsterRegistry>();
builder.Services.AddSingleton<KaeliRegistry>();
builder.Services.AddSingleton<ItemRegistry>();
builder.Services.AddAccountPersistence(builder.Configuration);
builder.Services.AddSingleton<AccountStore>();
builder.Services.AddSingleton<GachaService>();
builder.Services.AddSingleton<KaeliService>();
builder.Services.AddSingleton<DailyService>();
builder.Services.AddSingleton<RewardService>();
builder.Services.AddSingleton<RunFactory>();
builder.Services.AddSingleton<SessionOrchestrator>();
builder.Services.AddSingleton<ISessionCoordinator>(sp => sp.GetRequiredService<SessionOrchestrator>());
builder.Services.AddSingleton<ReplayStore>();
builder.Services.AddSingleton<RunManager>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RunManager>());
builder.Services.AddSignalR();

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins("http://localhost:4200")
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials()));

WebApplication app = builder.Build();

MonsterRegistry monsterRegistry = app.Services.GetRequiredService<MonsterRegistry>();
TilesetRegistry.LoadFrom(Path.Combine(AppContext.BaseDirectory, "Content", "tilesets.json"));
Biomes.ValidateDefaults();
PrefabRegistry.LoadFrom(
    Path.Combine(AppContext.BaseDirectory, "Content", "prefabs"),
    monsterRegistry.Has);

app.UseCors();
app.MapMetaEndpoints();
app.MapHub<GameHub>("/hub/game");

app.Run();
