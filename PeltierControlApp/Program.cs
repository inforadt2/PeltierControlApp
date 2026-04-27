using PeltierControlApp.Components;
using PeltierControlApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(System.Net.IPAddress.Any, 8085);
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<PeltierService>();

var app = builder.Build();

app.Use(async (context, next) =>
{
    Console.WriteLine($"[REQ] {context.Connection.RemoteIpAddress} → {context.Request.Method} {context.Request.Path}");
    await next();
    Console.WriteLine($"[RES] {context.Connection.RemoteIpAddress} ← {context.Response.StatusCode}");
});

app.UseStaticFiles();
app.UseAntiforgery();

app.MapGet("/ping", () => "pong");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Lifetime.ApplicationStarted.Register(() =>
{
    Task.Run(async () =>
    {
        await Task.Delay(500);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "http://localhost:8085",
            UseShellExecute = true
        });
    });
});

app.Run();
