using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;

using Ignyos.LanPortal.Api;
using Ignyos.LanPortal.Api.Hubs;
using Ignyos.LanPortal.Api.Services;

// CRITICAL: Disable automatic claim type mapping to URI forms.
// By default, JwtSecurityTokenHandler maps short claim names like "role" to well-known URI forms.
// This breaks our custom short-name claims, so we disable it to preserve claim names as-is.
JwtSecurityTokenHandler.DefaultMapInboundClaims = false;

var builder = WebApplication.CreateBuilder(args);
var useHttpsRedirection = builder.Configuration.GetValue("Hosting:UseHttpsRedirection", false);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.Configure<BootstrapOptions>(builder.Configuration.GetSection(BootstrapOptions.SectionName));
builder.Services.Configure<UpdateChannelOptions>(builder.Configuration.GetSection(UpdateChannelOptions.SectionName));
builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit = 10L * 1024L * 1024L * 1024L;
});
builder.Services.AddHttpClient();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IValueProtector, DpapiValueProtector>();
builder.Services.AddSingleton<IAppSettingsStore, SqliteAppSettingsStore>();
builder.Services.AddSingleton<IDeviceLoginStore, InMemoryDeviceLoginStore>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();
builder.Services.AddSingleton<IUpdateManifestService, UpdateManifestService>();
builder.Services.AddSingleton<IFileEventPublisher, SignalRFileEventPublisher>();

JwtDatabaseConfig jwtConfig;
var bootstrapSection = builder.Configuration.GetSection(BootstrapOptions.SectionName).Get<BootstrapOptions>() ?? new BootstrapOptions();
var bootstrapStore = new SqliteAppSettingsStore(Options.Create(bootstrapSection), new DpapiValueProtector());
bootstrapStore.Initialize();
jwtConfig = bootstrapStore.GetJwtConfig();

var signingKeyBytes = Encoding.UTF8.GetBytes(jwtConfig.SigningKey);

if (signingKeyBytes.Length < 32)
{
    throw new InvalidOperationException("JWT signing key from SQLite must be at least 32 bytes.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"].ToString();
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrWhiteSpace(accessToken) &&
                    (path.StartsWithSegments("/api/files/download") || path.StartsWithSegments("/hubs/files")))
                {
                    context.Token = accessToken;
                }

                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;
                if (string.IsNullOrWhiteSpace(jti))
                {
                    context.Fail("Token missing jti.");
                    return Task.CompletedTask;
                }

                var settingsStore = context.HttpContext.RequestServices.GetRequiredService<IAppSettingsStore>();
                if (!settingsStore.IsAccessTokenActive(jti))
                {
                    context.Fail("Token revoked or inactive.");
                }

                return Task.CompletedTask;
            }
        };

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(signingKeyBytes),
            ValidateIssuer = true,
            ValidIssuer = jwtConfig.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtConfig.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            // Match the short "role" claim name written by JwtTokenService.
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (useHttpsRedirection)
{
    app.UseHttpsRedirection();
}
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<FileEventsHub>("/hubs/files");

app.Run();
