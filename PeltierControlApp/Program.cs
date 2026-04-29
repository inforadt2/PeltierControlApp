using PeltierControlApp.Components;
using PeltierControlApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Listen(System.Net.IPAddress.Any, 8085);                          // HTTP (네트워크)
    options.Listen(System.Net.IPAddress.Loopback, 7073, o => o.UseHttps()); // HTTPS (로컬)
});

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<PeltierService>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Lifetime.ApplicationStarted.Register(() =>
{
    Task.Run(async () =>
    {
        await Task.Delay(500);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "https://localhost:7073",
            UseShellExecute = true
        });
    });
});

app.Run();
