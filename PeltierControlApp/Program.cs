using Blazorise;
using Blazorise.Bootstrap;
using Blazorise.Bootstrap;
using Blazorise.Icons.FontAwesome;
using PeltierControlApp.Components;
using PeltierControlApp.Services;

var builder = WebApplication.CreateBuilder(args);

// .NET 8 대화형 서버 서비스 등록 (매우 중요)
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Blazorise 및 펠티어 서비스 등록
builder.Services.AddBlazorise(options => { options.Immediate = true; })
    .AddBootstrapProviders()
    .AddFontAwesomeIcons();

builder.Services.AddSingleton<PeltierService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

// 엔드포인트 설정 (매우 중요)
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();