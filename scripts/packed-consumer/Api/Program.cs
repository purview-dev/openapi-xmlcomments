using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;

// Serves /openapi/v1.json to itself and exits 0 only on a success status. Without the package, the first schema
// transform throws "An item with the same key has already been added" and the request answers 500.
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Services.AddOpenApi();

var app = builder.Build();
app.MapOpenApi();
app.MapGet("/a", () => new Lib.A.WidgetA());
app.MapGet("/b", () => new Lib.B.WidgetB());

await app.StartAsync();
var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
using var client = new HttpClient { BaseAddress = new Uri(address) };
using var response = await client.GetAsync("/openapi/v1.json");
Console.WriteLine($"STATUS {(int)response.StatusCode}");
Console.WriteLine(await response.Content.ReadAsStringAsync());
await app.StopAsync();

return response.IsSuccessStatusCode ? 0 : 1;
