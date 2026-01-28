using Microsoft.JSInterop;

namespace WordScapeBlazorWasm.Services;

/// <summary>
/// Service for lazy-loading CSS files on demand to improve initial page load time.
/// Game-specific CSS is loaded only when the user navigates to that game.
/// </summary>
public class CssLoaderService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly HashSet<string> _loadedCss = new(StringComparer.OrdinalIgnoreCase);
    
    /// <summary>
    /// Delay after CSS loads to ensure browser has applied styles.
    /// Some platforms (especially mobile) need time for CSS to be parsed and applied.
    /// </summary>
    private const int CssApplyDelayMs = 50;

    public CssLoaderService(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Loads a CSS file if not already loaded, with a built-in delay to ensure styles are applied.
    /// </summary>
    /// <param name="cssPath">Path to CSS file (e.g., "css/fish-game.css")</param>
    /// <param name="version">Optional version for cache busting</param>
    /// <returns>True if newly loaded, false if already loaded</returns>
    public async Task<bool> LoadCssAsync(string cssPath, string? version = null)
    {
        // Quick check in C# to avoid JS call if already loaded this session
        if (_loadedCss.Contains(cssPath))
        {
            return false;
        }

        try
        {
            var wasLoaded = await _jsRuntime.InvokeAsync<bool>("loadCssFile", cssPath, version);
            if (wasLoaded)
            {
                _loadedCss.Add(cssPath);
                // Wait for browser to parse and apply the CSS
                await Task.Delay(CssApplyDelayMs);
            }
            return wasLoaded;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CssLoader] Error loading {cssPath}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Loads multiple CSS files in parallel, with a built-in delay to ensure styles are applied.
    /// </summary>
    /// <param name="cssFiles">Dictionary of cssPath -> version</param>
    /// <returns>Count of newly loaded files</returns>
    public async Task<int> LoadCssFilesAsync(params (string path, string? version)[] cssFiles)
    {
        var toLoad = cssFiles
            .Where(f => !_loadedCss.Contains(f.path))
            .Select(f => new { href = f.path, version = f.version })
            .ToArray();

        if (toLoad.Length == 0)
        {
            return 0;
        }

        try
        {
            var count = await _jsRuntime.InvokeAsync<int>("loadCssFiles", (object)toLoad);
            foreach (var file in toLoad)
            {
                _loadedCss.Add(file.href);
            }
            
            if (count > 0)
            {
                // Wait for browser to parse and apply the CSS
                await Task.Delay(CssApplyDelayMs);
            }
            
            return count;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CssLoader] Error loading CSS files: {ex.Message}");
            return 0;
        }
    }

    /// <summary>
    /// Preloads a CSS file for future use (low priority).
    /// </summary>
    public async Task PreloadCssAsync(string cssPath)
    {
        if (_loadedCss.Contains(cssPath))
        {
            return;
        }

        try
        {
            await _jsRuntime.InvokeVoidAsync("preloadCssFile", cssPath);
        }
        catch
        {
            // Preload failures are non-critical
        }
    }
}

/// <summary>
/// Game-specific CSS files with cache versioning.
/// Each game lazily loads its CSS on demand.
/// Change CacheVersion to bust cache for ALL CSS files at once.
/// </summary>
public static class GameCss
{
    // Global cache buster - increment this ONE number to refresh all CSS files
    public const string CacheVersion = "28"; // INCREMENTED: Hearts selected cards behind hand (z-index 1 vs 10)
    
    // Card games
    public static readonly (string Path, string Version) PlayingCards = ("css/playing-cards.css", CacheVersion);
    public static readonly (string Path, string Version) FreeCell = ("css/freecell-game.css", CacheVersion);
    public static readonly (string Path, string Version) Solitaire = ("css/solitaire-game.css", CacheVersion);
    public static readonly (string Path, string Version) Hearts = ("css/hearts-game.css", CacheVersion);

    // Word games
    public static readonly (string Path, string Version) WordScape = ("css/wordscape-game.css", CacheVersion);
    public static readonly (string Path, string Version) Wordament = ("css/wordament-game.css", CacheVersion);

    // Drawing/Animation
    public static readonly (string Path, string Version) Logo = ("css/logo-game.css", CacheVersion);
    public static readonly (string Path, string Version) Cartoon = ("css/cartoon-game.css", CacheVersion);

    // Simulations
    public static readonly (string Path, string Version) Bounce = ("css/bounce-game.css", CacheVersion);
    public static readonly (string Path, string Version) Fish = ("css/fish-game.css", CacheVersion);
    public static readonly (string Path, string Version) Life = ("css/life-game.css", CacheVersion);
    public static readonly (string Path, string Version) Ant = ("css/ant-game.css", CacheVersion);

    // Classic games
    public static readonly (string Path, string Version) Mandelbrot = ("css/mandelbrot-game.css", CacheVersion);
    public static readonly (string Path, string Version) Snake = ("css/snake-game.css", CacheVersion);
    public static readonly (string Path, string Version) Tetris = ("css/tetris-game.css", CacheVersion);
    public static readonly (string Path, string Version) Minesweeper = ("css/minesweeper-game.css", CacheVersion);
}
