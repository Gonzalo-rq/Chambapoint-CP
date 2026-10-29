using System.Text.Json;

namespace ChambaPoint.Api.Services.External;

public interface ICurrencyService
{
    Task<decimal> ObtenerTipoCambioUsdPenAsync(CancellationToken ct = default);
}

public class CurrencyService : ICurrencyService
{
    private readonly HttpClient _client;
    private readonly ILogger<CurrencyService> _logger;

    public CurrencyService(HttpClient client, ILogger<CurrencyService> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<decimal> ObtenerTipoCambioUsdPenAsync(CancellationToken ct = default)
    {
        try
        {
            // Consulta la API pública de tipos de cambio
            var response = await _client.GetAsync("v1/latest?base=USD&symbols=PEN", ct);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (doc.RootElement.TryGetProperty("rates", out var rates) &&
                rates.TryGetProperty("PEN", out var penRate))
            {
                return penRate.GetDecimal();
            }

            return 3.75m; // Fallback promedio
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error al consultar API de tipo de cambio. Retornando valor estimado.");
            return 3.75m;
        }
    }
}
