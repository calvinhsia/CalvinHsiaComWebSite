using DictionaryLib;
using Client;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using WordScapeBlazorWasm.Services;
using Microsoft.AspNetCore.Components;
using WordScapeBlazorWasm.Games.Cartoon.Services;
using Client.Services;

internal class Program
{
    public static WebAssemblyHost? Host { get; private set; }
    private static async Task Main(string[] args)
    {
        var builder = WebAssemblyHostBuilder.CreateDefault(args);
        builder.RootComponents.Add<App>("#app");
        builder.RootComponents.Add<HeadOutlet>("head::after");

        builder.Services.AddScoped<GraphAPIAuthorizationMessageHandler>();
        builder.Services.AddHttpClient("GraphAPI",
                client => client.BaseAddress = new Uri("https://graph.microsoft.com"))
            .AddHttpMessageHandler<GraphAPIAuthorizationMessageHandler>();

        var apipref = builder.Configuration["API_Prefix"];
        var uri = new Uri(apipref ?? builder.HostEnvironment.BaseAddress);
        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = uri });
        builder.Services.AddOptions();
        
        // Add Application Insights logger for client-side telemetry
        builder.Services.AddScoped<ApplicationInsightsLogger>();
        
        // ?? Add centralized Random service as SINGLETON
        builder.Services.AddSingleton<RandomService>();
        
        // Add dictionary service as singleton (expensive to create, should be shared)
        builder.Services.AddSingleton<IDictionaryService, DictionaryService>();
        
        // Add word handler service using shared dictionary
        builder.Services.AddScoped<WordHandler>();
        
        // Add game services
        builder.Services.AddScoped<WordScapeGameService>();
        builder.Services.AddScoped<GameSettingsService>();
        builder.Services.AddScoped<GameStateService>(); // New comprehensive state service
        builder.Services.AddScoped<DebugHelper>();
        builder.Services.AddScoped<PuzzleStateFactory>(); // Factory for complex model creation
        
        // Add Wordament game services
        builder.Services.AddScoped<WordamentGridWordFinder>(); // New dedicated word finder
        builder.Services.AddScoped<WordamentGameService>();
        
        // Add Logo game service
        builder.Services.AddScoped<LogoGameService>();
        
        // Add Cartoon game service
        builder.Services.AddScoped<CartoonService>();
        
        // Add authentication helper for centralized token handling
        builder.Services.AddScoped<AuthTokenHelper>();
        
        // Add album service for OneDrive album operations
        builder.Services.AddScoped<AlbumService>();
        
        builder.Services.AddMsalAuthentication(options =>
        {
            builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
            
            var baseUri2 = builder.HostEnvironment.BaseAddress.TrimEnd('/');
            
            // Set redirect URI based on environment
            var redirectUri = GetRedirectUri(baseUri2, builder.HostEnvironment.Environment);
            options.ProviderOptions.Authentication.RedirectUri = redirectUri;
            options.ProviderOptions.Authentication.PostLogoutRedirectUri = baseUri2;
            
            Console.WriteLine($"🔐 MSAL Configuration:");
            Console.WriteLine($"   - Client ID: {options.ProviderOptions.Authentication.ClientId}");
            Console.WriteLine($"   - Authority: {options.ProviderOptions.Authentication.Authority}");
            Console.WriteLine($"   - Redirect URI: {redirectUri}");
            Console.WriteLine($"   - Post Logout URI: {baseUri2}");
            
            options.ProviderOptions.DefaultAccessTokenScopes.Add("User.Read");
            options.ProviderOptions.DefaultAccessTokenScopes.Add("Mail.Read");
            options.ProviderOptions.DefaultAccessTokenScopes.Add("Files.Read.All");
            options.ProviderOptions.DefaultAccessTokenScopes.Add("Files.ReadWrite");
            options.ProviderOptions.DefaultAccessTokenScopes.Add("Files.ReadWrite.All");
            
            options.ProviderOptions.Cache.CacheLocation = "localStorage";
            options.ProviderOptions.Cache.StoreAuthStateInCookie = true;
            
            options.ProviderOptions.LoginMode = "redirect";
            options.ProviderOptions.Authentication.NavigateToLoginRequestUrl = true;
        });

        Console.WriteLine("[Startup v1] Building Blazor WASM host...");
        Host = builder.Build();

        // Log startup to Application Insights
        await LogStartupInfo();

        // ?? CRITICAL: Check sessionStorage for debug mode AFTER host is built
        // The JavaScript in index.html already parsed the URL and stored debug mode
        await ConfigureDebugFromUrl();

        Console.WriteLine("[Startup v1] Blazor starting up...");
        Console.WriteLine($"[Startup v1] Final debug mode state before run: {DebugHelper.IsDebugEnabled}");
        
        // Log final startup event
        await LogStartupComplete();
        
        await Host.RunAsync();
    }

    private static async Task LogStartupInfo()
    {
        try
        {
            // Wait for Application Insights SDK to load (removed duplicate delay)
            await Task.Delay(1000);
            
            var appInsights = Host!.Services.GetRequiredService<ApplicationInsightsLogger>();
            var navigationManager = Host!.Services.GetRequiredService<NavigationManager>();
            
            var uri = new Uri(navigationManager.Uri);
            var properties = new Dictionary<string, string>
            {
                { "environment", Host!.Services.GetRequiredService<IWebAssemblyHostEnvironment>().Environment },
                { "baseAddress", Host!.Services.GetRequiredService<IWebAssemblyHostEnvironment>().BaseAddress },
                { "host", uri.Host },
                { "userAgent", await GetUserAgent() }
            };
            
            await appInsights.TrackEvent("ApplicationStartup", properties);
            await appInsights.TrackTrace("[Startup v1] Blazor WASM application initialized", SeverityLevel.Information, properties);
            
            Console.WriteLine($"[Startup v1] Application Insights startup logging complete");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Startup v1] Failed to log startup info: {ex.Message}");
        }
    }

    private static async Task LogStartupComplete()
    {
        try
        {
            var appInsights = Host!.Services.GetRequiredService<ApplicationInsightsLogger>();
            
            var properties = new Dictionary<string, string>
            {
                { "debugMode", DebugHelper.IsDebugEnabled.ToString() }
            };
            
            // BuildInfo is generated at build time - use reflection to access it safely
            try
            {
                // Try multiple possible type names for BuildInfo
                string[] possibleNames = new[]
                {
                    "BuildInfo, Client",           // Assembly-qualified short form (most likely)
                    "Client.BuildInfo, Client",    // Namespaced with assembly
                    "BuildInfo",                   // Global namespace
                    "Client.BuildInfo"             // Namespaced without assembly
                };

                Type? buildInfoType = null;
                foreach (var typeName in possibleNames)
                {
                    buildInfoType = Type.GetType(typeName);
                    if (buildInfoType != null)
                    {
                        Console.WriteLine($"[Startup v1] Found BuildInfo type: {typeName}");
                        break;
                    }
                }

                if (buildInfoType != null)
                {
                    // BuildInfo uses const fields, not properties
                    var buildTimeField = buildInfoType.GetField("BuildTime");
                    var gitBranchField = buildInfoType.GetField("GitBranch");
                    
                    var buildTime = buildTimeField?.GetValue(null) as string;
                    var gitBranch = gitBranchField?.GetValue(null) as string;
                    
                    properties.Add("buildTime", buildTime ?? "unknown");
                    properties.Add("gitBranch", gitBranch ?? "unknown");
                    
                    Console.WriteLine($"[Startup v1] BuildInfo values - Branch: {gitBranch}, Time: {buildTime}");
                }
                else
                {
                    Console.WriteLine($"[Startup v1] Could not find BuildInfo type with any attempted name");
                    properties.Add("buildTime", "unknown");
                    properties.Add("gitBranch", "unknown");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Startup v1] BuildInfo reflection error: {ex.Message}");
                properties.Add("buildTime", "unknown");
                properties.Add("gitBranch", "unknown");
            }
            
            await appInsights.TrackTrace("[Startup v1] Application startup complete", SeverityLevel.Information, properties);
            await appInsights.TrackMetric("StartupComplete", 1.0, properties);
            
            Console.WriteLine($"[Startup v1] Logged startup complete - Debug: {DebugHelper.IsDebugEnabled}, Branch: {properties["gitBranch"]}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Startup v1] Failed to log startup complete: {ex.Message}");
        }
    }

    private static async Task<string> GetUserAgent()
    {
        try
        {
            var jsRuntime = Host!.Services.GetRequiredService<IJSRuntime>();
            return await jsRuntime.InvokeAsync<string>("eval", "navigator.userAgent");
        }
        catch
        {
            return "unknown";
        }
    }

    private static string GetRedirectUri(string baseUri, string environment)
    {
        // Handle different environments
        var uri = new Uri(baseUri);
        var host = uri.Host.ToLower();
        
        Console.WriteLine($"[Startup v1] Base URI: {baseUri}");
        Console.WriteLine($"[Startup v1] Host: {host}");
        Console.WriteLine($"[Startup v1] Environment: {environment}");
        
        // For localhost development
        if (host.Contains("localhost") || host == "127.0.0.1")
        {
            var redirectUri = $"{baseUri}/authentication/login-callback";
            Console.WriteLine($"[Startup v1] Using localhost redirect URI: {redirectUri}");
            return redirectUri;
        }
        
        // For Azure Static Web Apps - map specific hostnames to registered redirect URIs
        var redirectMappings = new Dictionary<string, string>
        {
            // Production environment
            { "calvinhsia.com", "https://calvinhsia.com/authentication/login-callback" },
            { "www.calvinhsia.com", "https://calvinhsia.com/authentication/login-callback" },
            
            // Azure Static Web Apps production environment (main branch)
            { "nice-coast-0273ff81e.westus2.3.azurestaticapps.net", "https://nice-coast-0273ff81e.westus2.3.azurestaticapps.net/authentication/login-callback" },
            
            // TODO: Add your preview environment URLs here as they're created
            // Example: { "nice-coast-0273ff81e-123.westus2.3.azurestaticapps.net", "https://nice-coast-0273ff81e-123.westus2.3.azurestaticapps.net/authentication/login-callback" },
        };
        
        Console.WriteLine($"[Startup v1] Available mappings: {string.Join(", ", redirectMappings.Keys)}");
        
        // Check for exact host match first
        if (redirectMappings.TryGetValue(host, out var exactRedirectUri))
        {
            Console.WriteLine($"[Startup v1] Found exact match redirect URI: {exactRedirectUri}");
            return exactRedirectUri;
        }
        
        // Handle Azure Static Web Apps pattern matching for pull request environments
        if (host.Contains(".azurestaticapps.net"))
        {
            // Auto-generate redirect URI for Azure Static Web Apps
            var fallbackUri = $"{baseUri}/authentication/login-callback";
            Console.WriteLine($"[Startup v1] Warning: Using auto-generated redirect URI for host: {host} -> {fallbackUri}");
            Console.WriteLine($"[Startup v1] ACTION REQUIRED: Add this URL to Azure AD App Registration:");
            Console.WriteLine($"   {fallbackUri}");
            return fallbackUri;
        }
        
        // Default fallback
        var defaultUri = $"{baseUri}/authentication/login-callback";
        Console.WriteLine($"[Startup v1] Using default fallback redirect URI: {defaultUri}");
        return defaultUri;
    }

    private static async Task ConfigureDebugFromUrl()
    {
        ApplicationInsightsLogger? appInsights = null;
        try
        {
            appInsights = Host!.Services.GetRequiredService<ApplicationInsightsLogger>();
            var jsRuntime = Host!.Services.GetRequiredService<IJSRuntime>();
            
            // Check sessionStorage for debug mode (set by JavaScript in index.html)
            var debugModeFromStorage = await jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "debugMode");
            Console.WriteLine($"[Startup v1] sessionStorage.debugMode = '{debugModeFromStorage}'");
            
            if (debugModeFromStorage == "true")
            {
                DebugHelper.SetDebugMode(true);
                Console.WriteLine($"[Startup v1] CRITICAL: Debug mode ENABLED from sessionStorage (set by index.html JavaScript)");
                
                await appInsights.TrackEvent("DebugModeEnabled", new Dictionary<string, string>
                {
                    { "source", "sessionStorage" },
                    { "value", debugModeFromStorage }
                });
                
                // Reset RandomService to use fixed seed
                try
                {
                    var randomService = Host!.Services.GetRequiredService<RandomService>();
                    randomService.Reset();
                    Console.WriteLine($"[Startup v1] RandomService reset to use fixed seed 1");
                }
                catch (Exception rsEx)
                {
                    Console.WriteLine($"[Startup v1] Could not reset RandomService: {rsEx.Message}");
                    await appInsights.TrackException(rsEx, new Dictionary<string, string>
                    {
                        { "operation", "ResetRandomService" }
                    });
                }
            }
            else
            {
                DebugHelper.SetDebugMode(false);
                Console.WriteLine($"[Startup v1] Debug mode DISABLED (sessionStorage check complete)");
            }
            
            // Also check URL directly as fallback
            var navigationManager = Host!.Services.GetRequiredService<NavigationManager>();
            var uri = new Uri(navigationManager.Uri);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

            // Check for debug parameter
            if (query["debug"] != null)
            {
                bool debugEnabled = query["debug"] == "true" || query["debug"] == "1";
                if (debugEnabled != DebugHelper.IsDebugEnabled)
                {
                    DebugHelper.SetDebugMode(debugEnabled);
                    Console.WriteLine($"[Startup v1] Debug mode changed from URL query: {debugEnabled}");
                    
                    await appInsights.TrackEvent("DebugModeChanged", new Dictionary<string, string>
                    {
                        { "source", "urlQuery" },
                        { "enabled", debugEnabled.ToString() }
                    });
                    
                    // Reset RandomService if debug mode changed
                    try
                    {
                        var randomService = Host!.Services.GetRequiredService<RandomService>();
                        randomService.Reset();
                        Console.WriteLine($"[Startup v1] RandomService reset after URL check");
                    }
                    catch (Exception rsEx)
                    {
                        Console.WriteLine($"[Startup v1] Could not reset RandomService after URL check: {rsEx.Message}");
                        await appInsights.TrackException(rsEx, new Dictionary<string, string>
                        {
                            { "operation", "ResetRandomServiceFromUrl" }
                        });
                    }
                }
            }

            // Check for console parameter (enhanced debugging)
            if (query["console"] == "true" || query["console"] == "1")
            {
                if (!DebugHelper.IsDebugEnabled)
                {
                    DebugHelper.SetDebugMode(true);
                    Console.WriteLine("[Startup v1] Enhanced console debugging enabled from URL");
                    
                    await appInsights.TrackEvent("ConsoleDebuggingEnabled", new Dictionary<string, string>
                    {
                        { "source", "urlQuery" }
                    });
                    
                    try
                    {
                        var randomService = Host!.Services.GetRequiredService<RandomService>();
                        randomService.Reset();
                    }
                    catch { }
                }
            }

            // Check for verbose parameter
            if (query["verbose"] == "true" || query["verbose"] == "1")
            {
                if (!DebugHelper.IsDebugEnabled)
                {
                    DebugHelper.SetDebugMode(true);
                    Console.WriteLine("[Startup v1] Verbose debugging enabled from URL");
                    
                    await appInsights.TrackEvent("VerboseDebuggingEnabled", new Dictionary<string, string>
                    {
                        { "source", "urlQuery" }
                    });
                    
                    try
                    {
                        var randomService = Host!.Services.GetRequiredService<RandomService>();
                        randomService.Reset();
                    }
                    catch { }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Startup v1] Error configuring debug from URL: {ex.Message}");
            Console.WriteLine($"   Stack trace: {ex.StackTrace}");
            
            if (appInsights != null)
            {
                try
                {
                    await appInsights.TrackException(ex, new Dictionary<string, string>
                    {
                        { "operation", "ConfigureDebugFromUrl" }
                    });
                }
                catch
                {
                    // Ignore if logging fails
                }
            }
        }
    }
}