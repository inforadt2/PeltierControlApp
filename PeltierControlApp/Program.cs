var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(8085);
});

var app = builder.Build();

app.MapGet("/ping", () => "pong");

app.Run();
