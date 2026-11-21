using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReciclaConecta.Api.Data;

namespace ReciclaConecta.Tests;

// Levanta la API completa en memoria con una base SQLite en memoria (datos demo incluidos).
public class ApiFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _conexion = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _conexion.Open();
        builder.UseEnvironment("Testing");
        builder.UseSetting("RateLimit:LoginPorMinuto", "1000");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(o => o.UseSqlite(_conexion));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _conexion.Dispose();
    }
}

internal static class ServiceCollectionExt
{
    public static void RemoveAll<T>(this IServiceCollection s)
    {
        foreach (var d in s.Where(x => x.ServiceType == typeof(T)).ToList()) s.Remove(d);
    }
}

// Pruebas de integración: HTTP real contra la API (autenticación, roles y flujo completo).
public class ApiTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Clave = "Demo2026!";

    private async Task<HttpClient> ClienteCon(string email)
    {
        var c = factory.CreateClient();
        var r = await c.PostAsJsonAsync("/api/auth/login", new { email, password = Clave });
        r.EnsureSuccessStatusCode();
        var token = (await r.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    [Fact]
    public async Task Health_responde_ok()
    {
        var r = await factory.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }

    [Fact]
    public async Task Respuestas_traen_cabeceras_de_seguridad()
    {
        var r = await factory.CreateClient().GetAsync("/api/materiales");
        Assert.Equal("nosniff", r.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", r.Headers.GetValues("X-Frame-Options").Single());
        Assert.Contains("default-src 'self'", r.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Registro_y_login_de_ciudadano()
    {
        var c = factory.CreateClient();
        var email = $"nuevo{Guid.NewGuid():N}@test.pe";
        var reg = await c.PostAsJsonAsync("/api/auth/registro",
            new { nombre = "Ana Test", email, password = "Clave1234", rol = "Ciudadano", distrito = "Lince" });
        Assert.Equal(HttpStatusCode.Created, reg.StatusCode);

        var dup = await c.PostAsJsonAsync("/api/auth/registro",
            new { nombre = "Ana Test", email, password = "Clave1234", rol = "Ciudadano", distrito = "Lince" });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        var login = await c.PostAsJsonAsync("/api/auth/login", new { email, password = "Clave1234" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task No_se_puede_registrar_como_admin_ni_con_clave_debil()
    {
        var c = factory.CreateClient();
        var admin = await c.PostAsJsonAsync("/api/auth/registro",
            new { nombre = "Hacker", email = "h@test.pe", password = "Clave1234", rol = "Admin", distrito = "" });
        Assert.Equal(HttpStatusCode.BadRequest, admin.StatusCode);
        var debil = await c.PostAsJsonAsync("/api/auth/registro",
            new { nombre = "Ana", email = "debil@test.pe", password = "123", rol = "Ciudadano", distrito = "" });
        Assert.Equal(HttpStatusCode.BadRequest, debil.StatusCode);
    }

    [Fact]
    public async Task Login_incorrecto_da_401()
    {
        var r = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "ciudadano@reciclaconecta.pe", password = "otraClave1" });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Endpoint_protegido_sin_token_da_401()
    {
        var r = await factory.CreateClient().GetAsync("/api/solicitudes/mias");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task Ciudadano_no_entra_al_panel_admin_403()
    {
        var c = await ClienteCon("ciudadano@reciclaconecta.pe");
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/admin/indicadores")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await c.GetAsync("/api/usuarios")).StatusCode);
    }

    [Fact]
    public async Task Filtro_de_puntos_por_material()
    {
        var puntos = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/puntos?material=RAEE");
        Assert.True(puntos.GetArrayLength() > 0);
        foreach (var p in puntos.EnumerateArray())
            Assert.Contains("RAEE", p.GetProperty("materiales").EnumerateArray().Select(m => m.GetString()));
    }

    [Fact]
    public async Task Puntos_cercanos_ordenados_por_distancia()
    {
        var puntos = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/puntos?lat=-12.12&lng=-77.03");
        var d = puntos.EnumerateArray().Select(p => p.GetProperty("distanciaKm").GetDouble()).ToList();
        Assert.Equal(d.OrderBy(x => x), d);
    }

    [Fact]
    public async Task Flujo_completo_crear_aceptar_recoger_calificar()
    {
        var ciudadano = await ClienteCon("ciudadano@reciclaconecta.pe");
        var recolector = await ClienteCon("recolector@reciclaconecta.pe");

        var crear = await ciudadano.PostAsJsonAsync("/api/solicitudes", new
        {
            material = "PLASTICO", cantidadEstimadaKg = 4, direccion = "Av. Pardo 123", distrito = "Miraflores",
            lat = -12.118, lng = -77.034, referencia = "Edificio azul"
        });
        Assert.Equal(HttpStatusCode.Created, crear.StatusCode);
        int id = (await crear.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        var disponibles = await recolector.GetFromJsonAsync<JsonElement>("/api/solicitudes/disponibles?lat=-12.12&lng=-77.03&radioKm=5");
        Assert.Contains(id, disponibles.EnumerateArray().Select(s => s.GetProperty("id").GetInt32()));

        Assert.Equal(HttpStatusCode.OK, (await recolector.PostAsync($"/api/solicitudes/{id}/aceptar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await recolector.PostAsJsonAsync($"/api/solicitudes/{id}/recoger", new { kgReales = 4.6 })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ciudadano.PostAsJsonAsync($"/api/solicitudes/{id}/calificar", new { calificacion = 5, comentario = "Gracias" })).StatusCode);

        var impacto = await ciudadano.GetFromJsonAsync<JsonElement>("/api/impacto/mio");
        Assert.True(impacto.GetProperty("totalKg").GetDouble() >= 4.6);
    }

    [Fact]
    public async Task Doble_aceptacion_da_409()
    {
        var ciudadano = await ClienteCon("ciudadano@reciclaconecta.pe");
        var recolector = await ClienteCon("recolector@reciclaconecta.pe");
        var crear = await ciudadano.PostAsJsonAsync("/api/solicitudes", new
        {
            material = "VIDRIO", cantidadEstimadaKg = 2, direccion = "Av. Pardo 123", distrito = "Miraflores", lat = -12.118, lng = -77.034
        });
        int id = (await crear.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();

        Assert.Equal(HttpStatusCode.OK, (await recolector.PostAsync($"/api/solicitudes/{id}/aceptar", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await recolector.PostAsync($"/api/solicitudes/{id}/aceptar", null)).StatusCode);
    }

    [Fact]
    public async Task Recolector_no_verificado_no_acepta()
    {
        var ciudadano = await ClienteCon("ciudadano@reciclaconecta.pe");
        var noVerificado = await ClienteCon("recolector2@reciclaconecta.pe");
        var crear = await ciudadano.PostAsJsonAsync("/api/solicitudes", new
        {
            material = "PAPEL", cantidadEstimadaKg = 2, direccion = "Av. Pardo 123", distrito = "Miraflores", lat = -12.118, lng = -77.034
        });
        int id = (await crear.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt32();
        Assert.Equal(HttpStatusCode.BadRequest, (await noVerificado.PostAsync($"/api/solicitudes/{id}/aceptar", null)).StatusCode);
    }

    [Fact]
    public async Task Admin_ve_indicadores()
    {
        var admin = await ClienteCon("admin@reciclaconecta.pe");
        var ind = await admin.GetFromJsonAsync<JsonElement>("/api/admin/indicadores");
        Assert.True(ind.GetProperty("puntosActivos").GetInt32() >= 10);
        Assert.True(ind.GetProperty("totalKg").GetDouble() > 0);
    }
}
