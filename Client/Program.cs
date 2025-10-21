using DictionaryLib;
using Client;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using WordScapeBlazorWasm.Services;
using Microsoft.AspNetCore.Components;

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
        
        builder.Services.AddMsalAuthentication(options =>
        {
            builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
            
            var baseUri2 = builder.HostEnvironment.BaseAddress.TrimEnd('/');
            
            // Set redirect URI based on environment
            var redirectUri = GetRedirectUri(baseUri2, builder.HostEnvironment.Environment);
            options.ProviderOptions.Authentication.RedirectUri = redirectUri;
            options.ProviderOptions.Authentication.PostLogoutRedirectUri = baseUri2;
            
            Console.WriteLine($"?? MSAL Configuration:");
            Console.WriteLine($"   - Client ID: {options.ProviderOptions.Authentication.ClientId}");
            Console.WriteLine($"   - Authority: {options.ProviderOptions.Authentication.Authority}");
            Console.WriteLine($"   - Redirect URI: {redirectUri}");
            Console.WriteLine($"   - Post Logout URI: {baseUri2}");
            
            // Log the exact redirect URI for copy/paste into Azure AD
            Console.WriteLine($"?? COPY THIS EXACT URL TO AZURE AD:");
            Console.WriteLine($"   {redirectUri}");
            
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

        Host = builder.Build();

        // ?? CRITICAL: Check sessionStorage for debug mode AFTER host is built
        // The JavaScript in index.html already parsed the URL and stored debug mode
        await ConfigureDebugFromUrl();

        Console.WriteLine("Blazor starting up...");
        Console.WriteLine($"?? Final debug mode state before run: {DebugHelper.IsDebugEnabled}");
        await Host.RunAsync();
    }

    private static string GetRedirectUri(string baseUri, string environment)
    {
        // Handle different environments
        var uri = new Uri(baseUri);
        var host = uri.Host.ToLower();
        
        Console.WriteLine($"?? Debug - Base URI: {baseUri}");
        Console.WriteLine($"?? Debug - Host: {host}");
        Console.WriteLine($"?? Debug - Environment: {environment}");
        
        // For localhost development
        if (host.Contains("localhost") || host == "127.0.0.1")
        {
            var redirectUri = $"{baseUri}/authentication/login-callback";
            Console.WriteLine($"?? Debug - Using localhost redirect URI: {redirectUri}");
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
            
            // PR environments are automatically handled by the .azurestaticapps.net fallback logic below
        };
        
        Console.WriteLine($"?? Debug - Available mappings: {string.Join(", ", redirectMappings.Keys)}");
        
        // Check for exact host match first
        if (redirectMappings.TryGetValue(host, out var exactRedirectUri))
        {
            Console.WriteLine($"?? Debug - Found exact match redirect URI: {exactRedirectUri}");
            return exactRedirectUri;
        }
        
        // Handle Azure Static Web Apps pattern matching for pull request environments
        if (host.Contains(".azurestaticapps.net"))
        {
            // For PR environments, you'll need to register each one or use a different approach
            // This is a fallback - you should register the specific URLs in Azure AD
            var fallbackUri = $"{baseUri}/authentication/login-callback";
            Console.WriteLine($"?? Warning: Using fallback redirect URI for host: {host} -> {fallbackUri}");
            return fallbackUri;
        }
        
        // Default fallback
        var defaultUri = $"{baseUri}/authentication/login-callback";
        Console.WriteLine($"?? Debug - Using default fallback redirect URI: {defaultUri}");
        return defaultUri;
    }

    private static async Task ConfigureDebugFromUrl()
    {
        try
        {
            var jsRuntime = Host!.Services.GetRequiredService<IJSRuntime>();
            
            // Check sessionStorage for debug mode (set by JavaScript in index.html)
            var debugModeFromStorage = await jsRuntime.InvokeAsync<string>("sessionStorage.getItem", "debugMode");
            Console.WriteLine($"?? sessionStorage.debugMode = '{debugModeFromStorage}'");
            
            if (debugModeFromStorage == "true")
            {
                DebugHelper.SetDebugMode(true);
                Console.WriteLine($"?? CRITICAL: Debug mode ENABLED from sessionStorage (set by index.html JavaScript)");
                
                // Reset RandomService to use fixed seed
                try
                {
                    var randomService = Host!.Services.GetRequiredService<RandomService>();
                    randomService.Reset();
                    Console.WriteLine($"?? RandomService reset to use fixed seed 1");
                }
                catch (Exception rsEx)
                {
                    Console.WriteLine($"?? Could not reset RandomService: {rsEx.Message}");
                }
            }
            else
            {
                DebugHelper.SetDebugMode(false);
                Console.WriteLine($"?? Debug mode DISABLED (sessionStorage check complete)");
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
                    Console.WriteLine($"?? Debug mode changed from URL query: {debugEnabled}");
                    
                    // Reset RandomService if debug mode changed
                    try
                    {
                        var randomService = Host!.Services.GetRequiredService<RandomService>();
                        randomService.Reset();
                        Console.WriteLine($"?? RandomService reset after URL check");
                    }
                    catch (Exception rsEx)
                    {
                        Console.WriteLine($"?? Could not reset RandomService after URL check: {rsEx.Message}");
                    }
                }
            }

            // Check for console parameter (enhanced debugging)
            if (query["console"] == "true" || query["console"] == "1")
            {
                if (!DebugHelper.IsDebugEnabled)
                {
                    DebugHelper.SetDebugMode(true);
                    Console.WriteLine("?? Enhanced console debugging enabled from URL");
                    
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
                    Console.WriteLine("?? Verbose debugging enabled from URL");
                    
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
            Console.WriteLine($"? Error configuring debug from URL: {ex.Message}");
            Console.WriteLine($"   Stack trace: {ex.StackTrace}");
        }
    }
}