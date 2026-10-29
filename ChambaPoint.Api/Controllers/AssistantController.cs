using ChambaPoint.Api.Services.Agent;
using Microsoft.AspNetCore.Mvc;

namespace ChambaPoint.Api.Controllers;

[ApiController]
[Route("api/assistant")]
public class AssistantController : ControllerBase
{
    private readonly IChambaAssistantService _assistant;

    public AssistantController(IChambaAssistantService assistant)
    {
        _assistant = assistant;
    }

    public record ChatRequest(string Message);

    [HttpPost("chat")]
    public async Task<IActionResult> Chat([FromBody] ChatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Message))
        {
            return BadRequest(new { error = "El mensaje no puede estar vacío." });
        }

        var response = await _assistant.ResponderPreguntaAsync(request.Message);
        return Ok(new { reply = response });
    }
}
