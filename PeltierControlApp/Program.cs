using PeltierControlApp.Components;
using PeltierControlApp.Services;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.UseUrls("https://localhost:7073", "http://0.0.0.0:5186");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddSingleton<SettingsService>();
builder.Services.AddSingleton<PeltierService>();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// 앱 시작 후 브라우저 자동 실행
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
