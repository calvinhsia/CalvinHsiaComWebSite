using DictionaryLib;
using BlazorBasic;
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

        builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
        builder.Services.AddOptions();
        builder.Services.AddAuthorizationCore();
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