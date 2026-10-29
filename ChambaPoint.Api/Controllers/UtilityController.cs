using ChambaPoint.Api.Services.External;
using Microsoft.AspNetCore.Mvc;

namespace ChambaPoint.Api.Controllers;

[ApiController]
[Route("api/utility")]
public class UtilityController : ControllerBase
{
    private readonly ICurrencyService _currencyService;

    public UtilityController(ICurrencyService currencyService)
    {
        _currencyService = currencyService;
    }

    [HttpGet("exchange-rate")]
    public async Task<IActionResult> GetExchangeRate()
    {
        var rate = await _currencyService.ObtenerTipoCambioUsdPenAsync();
        return Ok(new
        {
            baseCurrency = "USD",
            targetCurrency = "PEN",
            rate = rate,
            updatedAt = DateTime.UtcNow
        });
    }
}
