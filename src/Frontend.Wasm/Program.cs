using Frontend.Wasm;
using Frontend.Wasm.Services;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var backendBaseUrl = builder.Configuration["BackendBaseUrl"] ?? "http://localhost:5001";
var backendBaseUri = new Uri(backendBaseUrl, UriKind.Absolute);

builder.Services.AddSingleton(new BackendApiOptions(backendBaseUri));
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = backendBaseUri
});
builder.Services.AddSingleton<PulseStreamClient>();

await builder.Build().RunAsync();
