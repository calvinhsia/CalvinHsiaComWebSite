using DictionaryLib;
using Client;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

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
        //        builder.Services.AddAuthorizationCore();
        builder.Services.AddMsalAuthentication(options =>
        {
            builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
            
            // Add explicit redirect URIs for better mobile support
            options.ProviderOptions.Authentication.RedirectUri = builder.HostEnvironment.BaseAddress + "authentication/login-callback";
            options.ProviderOptions.Authentication.PostLogoutRedirectUri = builder.HostEnvironment.BaseAddress;
            
            // Add scopes
            options.ProviderOptions.DefaultAccessTokenScopes.Add("User.Read");
            options.ProviderOptions.DefaultAccessTokenScopes.Add("Mail.Read");
            options.ProviderOptions.DefaultAccessTokenScopes.Add("Files.Read.All");
            options.ProviderOptions.DefaultAccessTokenScopes.Add("Files.ReadWrite");
            options.ProviderOptions.DefaultAccessTokenScopes.Add("Files.ReadWrite.All"); // Required for creating albums/bundles
            
            // Add cache options for better mobile support
            options.ProviderOptions.Cache.CacheLocation = "localStorage";
            options.ProviderOptions.Cache.StoreAuthStateInCookie = true; // Helps with mobile browsers
        });


        //builder.Services.AddScoped<AuthenticationStateProvider>(sp=>
        //{
        //    return new AuthenticationStateProvider();
        //});
        Host = builder.Build();
        //var cl = Program.Host!.Services.GetService<HttpClient>();
        //var addr = "https://calvinhvscode.azurewebsites.net/api/GetWordData";
        //addr = "https://msn.com";
        //var res = await cl!.GetStringAsync(addr);

        await Host.RunAsync();
    }
}