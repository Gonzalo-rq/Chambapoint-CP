using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace ChambaPoint.Api.Services.Agent;

public interface IChambaAssistantService
{
    Task<string> ResponderPreguntaAsync(string pregunta, List<string>? historial = null);
}

public class ChambaAssistantService : IChambaAssistantService
{
    private readonly Kernel _kernel;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<ChambaAssistantService> _logger;

    public ChambaAssistantService(
        IConfiguration config,
        IServiceProvider serviceProvider,
        ILogger<ChambaAssistantService> logger)
    {
        _logger = logger;
        var apiKey = config["Groq:ApiKey"] ?? string.Empty;
        var model = config["Groq:Model"] ?? "llama-3.3-70b-versatile";
        var endpoint = new Uri(config["Groq:BaseUrl"] ?? "https://api.groq.com/openai/v1");

        // Construir el Kernel de Semantic Kernel con el conector de Groq/OpenAI
        var builder = Kernel.CreateBuilder();
        builder.AddOpenAIChatCompletion(
            modelId: model,
            apiKey: apiKey,
            endpoint: endpoint
        );

        _kernel = builder.Build();
        _kernel.Plugins.AddFromType<TrabajadoresPlugin>(serviceProvider: serviceProvider);
        _chatService = _kernel.GetRequiredService<IChatCompletionService>();
    }

    public async Task<string> ResponderPreguntaAsync(string pregunta, List<string>? historial = null)
    {
        try
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage("Eres ChambaBot, el asistente inteligente y amigable de ChambaPoint Perú. " +
                "Tu objetivo es ayudar a clientes a encontrar electricistas, gasfiteros, carpinteros y pintores de confianza. " +
                "Si te preguntan por trabajadores, utiliza la función de búsqueda disponible.");

            if (historial != null)
            {
                foreach (var item in historial)
                {
                    chatHistory.AddUserMessage(item);
                }
            }

            chatHistory.AddUserMessage(pregunta);

            var settings = new PromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };

            var respuesta = await _chatService.GetChatMessageContentAsync(chatHistory, settings, _kernel);
            return respuesta.Content ?? "No pude generar una respuesta en este momento.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error comunicando con Groq/Semantic Kernel");
            return "Lo siento, tuve un problema al procesar tu solicitud. Por favor intenta nuevamente.";
        }
    }
}
