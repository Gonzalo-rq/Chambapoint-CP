using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using ChambaPoint.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace ChambaPoint.Api.Services.Agent;

public interface IChambaAssistantService
{
    Task<string> ResponderPreguntaAsync(string pregunta, List<string>? historial = null);
}

public class ChambaAssistantService : IChambaAssistantService
{
    private readonly Kernel? _kernel;
    private readonly IChatCompletionService? _chatService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChambaAssistantService> _logger;

    public ChambaAssistantService(
        IConfiguration config,
        IServiceProvider serviceProvider,
        ILogger<ChambaAssistantService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;

        var apiKey = config["Groq:ApiKey"] ?? string.Empty;
        var model = config["Groq:Model"] ?? "llama-3.3-70b-versatile";
        var endpoint = new Uri(config["Groq:BaseUrl"] ?? "https://api.groq.com/openai/v1");

        try
        {
            if (!string.IsNullOrWhiteSpace(apiKey) && apiKey.StartsWith("gsk_") && apiKey.Length > 15)
            {
                var builder = Kernel.CreateBuilder();
                builder.AddOpenAIChatCompletion(modelId: model, apiKey: apiKey, endpoint: endpoint);
                _kernel = builder.Build();
                _kernel.Plugins.AddFromType<TrabajadoresPlugin>(serviceProvider: serviceProvider);
                _chatService = _kernel.GetRequiredService<IChatCompletionService>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo inicializar Groq Kernel. Se activará el motor de fallback local.");
        }
    }

    public async Task<string> ResponderPreguntaAsync(string pregunta, List<string>? historial = null)
    {
        if (string.IsNullOrWhiteSpace(pregunta))
        {
            return "¡Hola! ¿En qué te puedo ayudar hoy? Puedes preguntarme por gasfiteros, electricistas, carpinteros o pintores.";
        }

        // Intento 1: Llamar al modelo de Groq con Function Calling
        if (_chatService != null && _kernel != null)
        {
            try
            {
                var chatHistory = new ChatHistory();
                chatHistory.AddSystemMessage("Eres ChambaBot, el asistente inteligente de ChambaPoint Perú. " +
                    "Tu objetivo es recomendar técnicos locales y responder dudas de los usuarios. " +
                    "Si te piden un oficio, busca en la base de datos con la herramienta y responde amigablemente.");

                if (historial != null)
                {
                    foreach (var m in historial) chatHistory.AddUserMessage(m);
                }
                chatHistory.AddUserMessage(pregunta);

                var settings = new PromptExecutionSettings
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                };

                var respuesta = await _chatService.GetChatMessageContentAsync(chatHistory, settings, _kernel);
                if (!string.IsNullOrWhiteSpace(respuesta.Content))
                {
                    return respuesta.Content;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning("Groq API falló o no tiene saldo. Ejecutando motor de contingencia local: {Msg}", ex.Message);
            }
        }

        // Intento 2 (Fallback Local Resiliente): Consulta directa a SQLite
        return await GenerarRespuestaLocalAsync(pregunta);
    }

    private async Task<string> GenerarRespuestaLocalAsync(string pregunta)
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var q = pregunta.ToLowerInvariant();
        string? oficioDetectado = null;
        string? categoriaAlternativa = null;

        if (q.Contains("electr"))
        {
            oficioDetectado = "Electricista";
            categoriaAlternativa = "Electric";
        }
        else if (q.Contains("gasfit") || q.Contains("plomer"))
        {
            oficioDetectado = "Gasfitero";
            categoriaAlternativa = "Plomer";
        }
        else if (q.Contains("carpint"))
        {
            oficioDetectado = "Carpintero";
            categoriaAlternativa = "Carpint";
        }
        else if (q.Contains("pint"))
        {
            oficioDetectado = "Pintor";
            categoriaAlternativa = "Pint";
        }

        if (oficioDetectado != null)
        {
            var workers = await db.Workers
                .Include(w => w.User)
                .Where(w => EF.Functions.Like(w.Profession, $"%{oficioDetectado}%") ||
                            (categoriaAlternativa != null && EF.Functions.Like(w.Profession, $"%{categoriaAlternativa}%")))
                .Take(2)
                .ToListAsync();

            if (workers.Count > 0)
            {
                var nombres = string.Join(" y ", workers.Select(w => $"**{w.User.Name}** ({w.Profession}, {w.ExperienceYears} años de experiencia)"));
                return $"¡Claro que sí! En ChambaPoint tenemos disponibles para ese oficio a: {nombres}. Puedes encontrarlos en el mapa o ver su perfil para agendar una cita de inmediato.";
            }

            return $"Por el momento no encontré técnicos disponibles exactamente con el oficio '{oficioDetectado}', pero puedes revisar la sección de Explorar para ver todos los especialistas cercanos.";
        }

        if (q.Contains("hola") || q.Contains("buenos") || q.Contains("buenas"))
        {
            return "¡Hola! Soy ChambaBot 🤖. ¿Qué servicio estás necesitando hoy? Dime si buscas un electricista, gasfitero, carpintero o pintor.";
        }

        return "Entiendo tu consulta. Puedes explorar técnicos disponibles en el mapa interactivo o solicitar un servicio directamente desde el menú de inicio. ¿Deseas que te busque un especialista en particular?";
    }
}
