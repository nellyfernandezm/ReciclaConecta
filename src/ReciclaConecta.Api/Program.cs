using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ReciclaConecta.Api.Data;
using ReciclaConecta.Api.Domain;
using ReciclaConecta.Api.Endpoints;
using ReciclaConecta.Api.Security;

var builder = WebApplication.CreateBuilder(args);

// En Render la plataforma indica el puerto con la variable PORT.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port)) builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// Base de datos SQLite (archivo local; se crea y se siembra al arrancar).
var cadena = builder.Configuration.GetConnectionString("Default") ?? "Data Source=db/reciclaconecta.db";
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(cadena));
builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

// Autenticación JWT + autorización por rol.
var jwt = JwtConfig.Desde(builder.Configuration);
builder.Services.AddSingleton(jwt);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o => o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = jwt.Emisor,
        ValidateAudience = true, ValidAudience = jwt.Emisor,
        ValidateLifetime = true, ValidateIssuerSigningKey = true,
        IssuerSigningKey = jwt.Clave,
        ClockSkew = TimeSpan.FromMinutes(1)
    });
builder.Services.AddAuthorization();

// Rate limiting: frena ataques de fuerza bruta al login (N intentos por minuto por IP).
int loginPorMinuto = builder.Configuration.GetValue("RateLimit:LoginPorMinuto", 10);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anonimo",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = loginPorMinuto, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();

// Crea el esquema y carga datos demo si la base está vacía.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (cadena.Contains("db/")) Directory.CreateDirectory("db");
    db.Database.EnsureCreated();
    Seed.Cargar(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher<Usuario>>());
}

if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseMiddleware<CabecerasSeguridadMiddleware>();
app.UseMiddleware<ErroresMiddleware>();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapAuth();
app.MapPuntos();
app.MapSolicitudes();
app.MapAdmin();

app.Run();

// Necesario para las pruebas de integración (WebApplicationFactory<Program>).
public partial class Program { }
