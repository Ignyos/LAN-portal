using Ignyos.LanPortal.Web.Components;
using Ignyos.LanPortal.Web.Services;

var builder = WebApplication.CreateBuilder(args);
var useHttpsRedirection = builder.Configuration.GetValue("Hosting:UseHttpsRedirection", false);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<AuthSession>();
builder.Services.AddScoped<FileEventsClient>();

builder.Services.AddHttpClient<FileApiClient>(client =>
{
    var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5212/";
    client.BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute);
});

builder.Services.AddHttpClient<AuthApiClient>(client =>
{
    var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5212/";
    client.BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute);
});

builder.Services.AddHttpClient<PortalConfigClient>(client =>
{
    var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5212/";
    client.BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute);
});

builder.Services.AddHttpClient<AdminApiClient>(client =>
{
    var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5212/";
    client.BaseAddress = new Uri(apiBaseUrl, UriKind.Absolute);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);

    if (useHttpsRedirection)
    {
        app.UseHsts();
    }
}

if (useHttpsRedirection)
{
    app.UseHttpsRedirection();
}


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
