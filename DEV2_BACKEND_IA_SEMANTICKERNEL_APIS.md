# 🤖 GUÍA TÉCNICA - DEV BACKEND 2
**Rol:** Desarrollador Backend 2  
**Módulos asignados:**
1. Inteligencia Artificial y Asistente Virtual con **Microsoft Semantic Kernel + Groq**.
2. Consumo de API Externa con **`HttpClient`** (Tipo de Cambio de Divisas o Feriados).

---

## 📌 Contexto del Proyecto
El docente implementó un asistente virtual con IA que usa **Groq** (API compatible con OpenAI pero ultrarrápida y con capa gratuita) junto con **Semantic Kernel** para *Function Calling* (herramientas).  
Tu objetivo es crear el asistente virtual **"ChambaBot"** para que los clientes puedan chatear en tiempo real y pedir recomendaciones o consultar el tipo de cambio del día.

---

## 🚀 PASO 1: Instalación de Paquetes NuGet

Abre la terminal en la carpeta del backend:
```bash
cd "Chambapoint-CP/ChambaPoint.Api"
dotnet add package Microsoft.SemanticKernel --version 1.81.0
dotnet add package Microsoft.SemanticKernel.Connectors.OpenAI --version 1.81.0
```

---

## 🔑 PASO 2: Configuración en `appsettings.json`

Abre `ChambaPoint.Api/appsettings.json` y agrega las siguientes secciones:
```json
"Groq": {
  "ApiKey": "gsk_...",
  "Model": "llama-3.3-70b-versatile",
  "BaseUrl": "https://api.groq.com/openai/v1"
},
"TipoCambio": {
  "BaseUrl": "https://api.frankfurter.dev/"
}
```
> **Nota:** Puedes obtener una API Key gratuita de Groq en [console.groq.com](https://console.groq.com) o utilizar una clave de prueba compartida para el curso.

---

## 🧠 PASO 3: Asistente Virtual con Semantic Kernel

### 3.1. Crear Carpeta de Agente y Plugins
Crea el directorio `ChambaPoint.Api/Services/Agent`.

### 3.2. Crear Plugin de Trabajadores `TrabajadoresPlugin.cs`
Este plugin le permite al modelo de IA consultar la base de datos real de trabajadores para responder consultas como: *"¿Tienes algún gasfitero con buena calificación?"*:

Crea `ChambaPoint.Api/Services/Agent/TrabajadoresPlugin.cs`:
```csharp
using System.ComponentModel;
using ChambaPoint.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;

namespace ChambaPoint.Api.Services.Agent;

public class TrabajadoresPlugin
{
    private readonly AppDbContext _db;

    public TrabajadoresPlugin(AppDbContext db)
    {
        _db = db;
    }

    [KernelFunction, Description("Busca trabajadores disponibles por su oficio o especialidad (electricista, gasfitero, pintor, carpintero)")]
    public async Task<string> BuscarTrabajadoresPorOficio(
        [Description("Nombre del oficio o profesión a buscar")] string oficio)
    {
        var trabajadores = await _db.Workers
            .Include(w => w.User)
            .Where(w => EF.Functions.Like(w.Profession, $"%{oficio}%"))
            .Take(3)
            .Select(w => new {
                w.Id,
                Nombre = w.User.Name,
                Oficio = w.Profession,
                Rating = w.RatingAvg,
                TrabajosCompletados = w.JobsCount,
                AñosExperiencia = w.ExperienceYears
            })
            .ToListAsync();

        if (!trabajadores.Any())
        {
            return $"No se encontraron trabajadores disponibles para el oficio de '{oficio}'.";
        }

        return System.Text.Json.JsonSerializer.Serialize(trabajadores);
    }
}
```

### 3.3. Crear el Servicio del Asistente `ChambaAssistantService.cs`
Crea `ChambaPoint.Api/Services/Agent/ChambaAssistantService.cs`:
```csharp
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

        builder.Plugins.AddFromType<TrabajadoresPlugin>(serviceProvider: serviceProvider);

        _kernel = builder.Build();
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
```

### 3.4. Crear el Controlador `AssistantController.cs`
Crea `ChambaPoint.Api/Controllers/AssistantController.cs`:
```csharp
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
```

---

## 🌐 PASO 4: Consumo de API Externa con `HttpClient`

Para cumplir con el requerimiento de integración externa (como el profesor hizo con `dummyjson` y `frankfurter`):

### 4.1. Crear Servicio de Tipo de Cambio `CurrencyService.cs`
Crea `ChambaPoint.Api/Services/External/CurrencyService.cs`:
```csharp
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
```

### 4.2. Crear Endpoint en `UtilityController.cs`
Crea `ChambaPoint.Api/Controllers/UtilityController.cs`:
```csharp
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
        return Ok(new {
            baseCurrency = "USD",
            targetCurrency = "PEN",
            rate = rate,
            updatedAt = DateTime.UtcNow
        });
    }
}
```

---

## ⚙️ PASO 5: Registro en `Program.cs`

En `ChambaPoint.Api/Program.cs`:
```csharp
// Inyecciones para el Asistente IA
builder.Services.AddScoped<TrabajadoresPlugin>();
builder.Services.AddScoped<IChambaAssistantService, ChambaAssistantService>();

// Inyección del cliente HTTP para la API externa
builder.Services.AddHttpClient<ICurrencyService, CurrencyService>(client =>
{
    var baseUrl = builder.Configuration["TipoCambio:BaseUrl"] ?? "https://api.frankfurter.dev/";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});
```

---

## ✅ Criterios de Aceptación (Pruebas)
1. **Chat Asistente:** Enviar un `POST` a `/api/assistant/chat` con `{"message": "Hola, necesito un electricista"}` y verificar que responde recomendando trabajadores desde la base de datos gracias a la función `TrabajadoresPlugin`.
2. **API Externa:** Enviar un `GET` a `/api/utility/exchange-rate` y verificar que devuelve el tipo de cambio oficial del día en JSON `{ "baseCurrency": "USD", "targetCurrency": "PEN", "rate": 3.7X }`.
