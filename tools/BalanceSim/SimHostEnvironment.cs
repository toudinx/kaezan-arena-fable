using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace BalanceSim;

/// <summary>
/// Stub mínimo de <see cref="IWebHostEnvironment"/> para construir <c>GameData</c>/<c>ContentStore</c>
/// fora do host ASP.NET. Só o <see cref="ContentRootPath"/> importa — é dele que o engine lê
/// <c>Data/monsters.json</c>+<c>items.json</c> e o conteúdo editável em <c>.data/content/</c>.
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
    /// Resolve o content-root do backend (<c>backend/src/KaezanArenaFable.Api</c>): usa <paramref name="explicit"/>
    /// se fornecido, senão sobe a partir de <see cref="AppContext.BaseDirectory"/> até achar o
    /// <c>Data/monsters.json</c> do projeto Api.
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
            "não localizei backend/src/KaezanArenaFable.Api subindo de " + AppContext.BaseDirectory +
            ". Passe --content-root <caminho do projeto Api>.");
    }
}
