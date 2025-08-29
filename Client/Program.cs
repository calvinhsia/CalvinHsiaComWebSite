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
        
        // Add WordScape game services
        builder.Services.AddScoped<WordScapeGameService>();
        builder.Services.AddScoped<GameSettingsService>();
        builder.Services.AddScoped<DebugHelper>();
        
        builder.Services.AddMsalAuthentication(options =>
        {
            builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
            
            var baseUri = builder.HostEnvironment.BaseAddress.TrimEnd('/');
            options.ProviderOptions.Authentication.RedirectUri = $"{baseUri}/authentication/login-callback";
            options.ProviderOptions.Authentication.PostLogoutRedirectUri = baseUri;
            
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

        // Check for URL-based debug settings
        await ConfigureDebugFromUrl();

        // Enable debug mode for development
        //#if DEBUG
        //if (!DebugHelper.IsDebugEnabled) // Don't override URL setting
        //{
        //    DebugHelper.SetDebugMode(true);
        //    Console.WriteLine("?? Debug mode enabled for development build");
        //}
        //#endif

        Console.WriteLine("Blazor starting up...");
        await Host.RunAsync();
    }

    private static async Task ConfigureDebugFromUrl()
    {
        try
        {
            var navigationManager = Host!.Services.GetRequiredService<NavigationManager>();
            var uri = new Uri(navigationManager.Uri);
            var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

            // Check for debug parameter
            if (query["debug"] != null)
            {
                bool debugEnabled = query["debug"] == "true" || query["debug"] == "1";
                DebugHelper.SetDebugMode(debugEnabled);
                Console.WriteLine($"?? Debug mode set from URL: {debugEnabled}");
            }

            // Check for console parameter (enhanced debugging)
            if (query["console"] == "true" || query["console"] == "1")
            {
                DebugHelper.SetDebugMode(true);
                Console.WriteLine("?? Enhanced console debugging enabled from URL");
            }

            // Check for verbose parameter
            if (query["verbose"] == "true" || query["verbose"] == "1")
            {
                DebugHelper.SetDebugMode(true);
                Console.WriteLine("?? Verbose debugging enabled from URL");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"? Error configuring debug from URL: {ex.Message}");
        }
    }
}