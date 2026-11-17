using WisLicense.Web.Components;
using WisLicense.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Herramienta de operador con acceso a la clave privada: solo loopback, sin exposición en red.
builder.WebHost.UseUrls("http://127.0.0.1:5190");

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddScoped<LicenciasService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
