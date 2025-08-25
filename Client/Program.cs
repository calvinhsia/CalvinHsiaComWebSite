using DictionaryLib;
using Client;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.JSInterop;
using WordScapeBlazorWasm.Services; // Add this using statement

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

        //        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
        var apipref = builder.Configuration["API_Prefix"];
        var uri = new Uri(apipref ?? builder.HostEnvironment.BaseAddress);
        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = uri });
        builder.Services.AddOptions();
        
        // Add WordScape game services
        builder.Services.AddScoped<WordScapeGameService>();
        builder.Services.AddScoped<GameSettingsService>();
        
        //        builder.Services.AddAuthorizationCore();
        builder.Services.AddMsalAuthentication(options =>
        {
            builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
            
            // Better mobile support with explicit redirect URIs
            var baseUri = builder.HostEnvironment.BaseAddress.TrimEnd('/');
            options.ProviderOptions.Authentication.RedirectUri = $"{baseUri}/authentication/login-callback";
            options.ProviderOptions.Authentication.PostLogoutRedirectUri = baseUri;
            
            // Add scopes
            options.ProviderOptions.DefaultAccessTokenScopes.Add("User.Read");
            options.ProviderOptions.DefaultAccessTokenScopes.Add("Mail.Read");
            options.ProviderOptions.DefaultAccessTokenScopes.Add("Files.Read.All");
            options.ProviderOptions.DefaultAccessTokenScopes.Add("Files.ReadWrite");
            options.ProviderOptions.DefaultAccessTokenScopes.Add("Files.ReadWrite.All");
            
            // Enhanced mobile browser support
            options.ProviderOptions.Cache.CacheLocation = "localStorage";
            options.ProviderOptions.Cache.StoreAuthStateInCookie = true;
            
            // Add mobile-specific settings
            options.ProviderOptions.LoginMode = "redirect"; // Force redirect mode instead of popup
            options.ProviderOptions.Authentication.NavigateToLoginRequestUrl = true;
        });


        //builder.Services.AddScoped<AuthenticationStateProvider>(sp=>
        //{
        //    return new AuthenticationStateProvider();
        //});
        Host = builder.Build();

        // Remove the problematic JavaScript calls from here
        // JavaScript interop should be called after Blazor has fully started
        Console.WriteLine("Blazor starting up...");

        //var cl = Program.Host!.Services.GetService<HttpClient>();
        //var addr = "https://calvinhvscode.azurewebsites.net/api/GetWordData";
        //addr = "https://msn.com";
        //var res = await cl!.GetStringAsync(addr);

        await Host.RunAsync();
    }
}