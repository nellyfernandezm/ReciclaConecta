using System.ComponentModel.DataAnnotations;
using ReciclaConecta.Api.Domain;

namespace ReciclaConecta.Api.Endpoints;

// Error de validación de datos de entrada (400).
public class ValidacionException(string mensaje) : Exception(mensaje);

public static class Validacion
{
    // Valida los atributos [Required], [Range], [EmailAddress], etc. del DTO.
    public static void Validar(object dto)
    {
        var errores = new List<ValidationResult>();
        if (!Validator.TryValidateObject(dto, new ValidationContext(dto), errores, validateAllProperties: true))
            throw new ValidacionException(string.Join(" ", errores.Select(e => e.ErrorMessage)));
    }
}

// Convierte las excepciones de negocio en respuestas HTTP claras; nunca expone detalles internos.
public class ErroresMiddleware(RequestDelegate next, ILogger<ErroresMiddleware> log)
{
    public async Task Invoke(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (BadHttpRequestException ex)
        {
            await Escribir(ctx, ex.StatusCode, "La solicitud no tiene el formato esperado.");
        }
        catch (ValidacionException ex)
        {
            await Escribir(ctx, 400, ex.Message);
        }
        catch (ReglaNegocioException ex)
        {
            await Escribir(ctx, ex.Conflicto ? 409 : 400, ex.Message);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error no controlado");
            await Escribir(ctx, 500, "Ocurrió un error inesperado.");
        }
    }

    private static Task Escribir(HttpContext ctx, int status, string mensaje)
    {
        ctx.Response.StatusCode = status;
        return ctx.Response.WriteAsJsonAsync(new { error = mensaje });
    }
}
