using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace BalanceSim;

/// <summary>
/// Minimal <see cref="IWebHostEnvironment"/> stub for building <c>GameData</c>/<c>ContentStore</c>
/// outside the ASP.NET host. Only <see cref="ContentRootPath"/> matters; the engine reads
/// <c>Data/monsters.json</c>+<c>items.json</c> and editable <c>.data/content/</c> from it.
/// </summary>
internal sealed class SimHostEnvironment : IWebHostEnvironment
{
    public SimHostEnvironment(string contentRootPath)
    {
        ContentRootPath = contentRootPath;
        ContentRootFileProvider = new PhysicalFileProvider(contentRootPath);
        WebRootPath = contentRootPath;
        WebRootFileProvider = ContentRootFileProvider;
    }

    public string ApplicationName { get; set; } = "BalanceSim";
    public string EnvironmentName { get; set; } = "Production";
    public string ContentRootPath { get; set; }
    public IFileProvider ContentRootFileProvider { get; set; }
    public string WebRootPath { get; set; }
    public IFileProvider WebRootFileProvider { get; set; }

    /// <summary>
    /// Resolves the backend content root (<c>backend/src/KaezanArenaFable.Api</c>): uses
    /// <paramref name="explicit"/> if provided; otherwise walks upward from
    /// <see cref="AppContext.BaseDirectory"/> until it finds the Api project's <c>Data/monsters.json</c>.
    /// </summary>
    public static string ResolveContentRoot(string? @explicit)
    {
        if (!string.IsNullOrWhiteSpace(@explicit))
            return Path.GetFullPath(@explicit);

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "backend", "src", "KaezanArenaFable.Api");
            if (File.Exists(Path.Combine(candidate, "Data", "monsters.json")))
                return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "could not locate backend/src/KaezanArenaFable.Api while walking up from " + AppContext.BaseDirectory +
            ". Pass --content-root <Api project path>.");
    }
}
