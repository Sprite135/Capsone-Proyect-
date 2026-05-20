using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Net;
using LicitIA.Api.Configuration;
using LicitIA.Api.Models;
using Microsoft.Extensions.Options;

namespace LicitIA.Api.Services;

public sealed class GeminiAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;

    public GeminiAnalysisService(HttpClient httpClient, IOptions<GeminiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.ApiKey);

    public string Model => string.IsNullOrWhiteSpace(_options.Model)
        ? "gemini-2.5-flash-lite"
        : _options.Model;

    public int DailyLimitPerUser => Math.Max(1, _options.DailyLimitPerUser);

    public async Task<OpportunityAiAnalysis> AnalyzeAsync(
        Guid userId,
        Opportunity opportunity,
        RecommendationAnalysis recommendation,
        SeaceDocumentAnalysisContext? documentContext,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Gemini no esta configurado. Agrega Gemini:ApiKey en appsettings o variable de entorno.");
        }

        var prompt = BuildPrompt(opportunity, recommendation, documentContext);
        var parts = new List<GeminiPart>
        {
            GeminiPart.FromText(prompt)
        };

        if (documentContext is not null)
        {
            parts.AddRange(documentContext.InlineDocuments.Select(document =>
                GeminiPart.FromInlineData(document.MimeType, Convert.ToBase64String(document.Content))));
        }

        var request = new GeminiRequest(new[]
        {
            new GeminiContent(parts)
        });

        var body = await PostToGeminiAsync(request, cancellationToken);
        var text = ExtractText(body);
        var parsed = ParseAnalysis(text);

        return new OpportunityAiAnalysis
        {
            UserId = userId,
            OpportunityId = opportunity.OpportunityId,
            ModelName = Model,
            Recommendation = parsed.Recommendation,
            Summary = parsed.Summary,
            Risks = parsed.Risks,
            Requirements = parsed.Requirements,
            NextSteps = parsed.NextSteps,
            RawResponse = text
        };
    }

    public Task<OpportunityAiAnalysis> AnalyzeAsync(
        Guid userId,
        Opportunity opportunity,
        RecommendationAnalysis recommendation,
        CancellationToken cancellationToken) =>
        AnalyzeAsync(userId, opportunity, recommendation, null, cancellationToken);

    public async Task<string> AnswerDocumentQuestionAsync(
        Opportunity opportunity,
        SeaceDocumentAnalysisContext documentContext,
        string question,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Gemini no esta configurado. Agrega Gemini:ApiKey en appsettings o variable de entorno.");
        }

        var parts = new List<GeminiPart>
        {
            GeminiPart.FromText(BuildDocumentQuestionPrompt(opportunity, documentContext, question))
        };
        parts.AddRange(documentContext.InlineDocuments.Select(document =>
            GeminiPart.FromInlineData(document.MimeType, Convert.ToBase64String(document.Content))));

        var request = new GeminiRequest(new[]
        {
            new GeminiContent(parts)
        });

        var body = await PostToGeminiAsync(request, cancellationToken);
        return ExtractText(body).Trim();
    }

    public async Task<string> ExtractDocumentTextAsync(
        Opportunity opportunity,
        SeaceDocumentAnalysisContext documentContext,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Gemini no esta configurado. Agrega Gemini:ApiKey en appsettings o variable de entorno.");
        }

        var parts = new List<GeminiPart>
        {
            GeminiPart.FromText(BuildDocumentExtractionPrompt(opportunity, documentContext))
        };
        parts.AddRange(documentContext.InlineDocuments.Select(document =>
            GeminiPart.FromInlineData(document.MimeType, Convert.ToBase64String(document.Content))));

        var request = new GeminiRequest(new[]
        {
            new GeminiContent(parts)
        });

        var body = await PostToGeminiAsync(request, cancellationToken);
        return ExtractText(body).Trim();
    }

    public async Task<string> AnswerDocumentQuestionFromExtractedTextAsync(
        Opportunity opportunity,
        string extractedDocumentText,
        string question,
        CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Gemini no esta configurado. Agrega Gemini:ApiKey en appsettings o variable de entorno.");
        }

        var request = new GeminiRequest(new[]
        {
            new GeminiContent(new[]
            {
                GeminiPart.FromText(BuildDocumentQuestionFromTextPrompt(opportunity, extractedDocumentText, question))
            })
        });

        var body = await PostToGeminiAsync(request, cancellationToken);
        return ExtractText(body).Trim();
    }

    private async Task<string> PostToGeminiAsync(GeminiRequest request, CancellationToken cancellationToken)
    {
        var endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(Model)}:generateContent?key={Uri.EscapeDataString(_options.ApiKey)}";

        var retryDelays = new[]
        {
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(10)
        };

        for (var attempt = 1; attempt <= retryDelays.Length + 1; attempt++)
        {
            try
            {
                using var response = await _httpClient.PostAsJsonAsync(endpoint, request, cancellationToken);
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    return body;
                }

                if (attempt <= retryDelays.Length && IsTransientGeminiError(response.StatusCode))
                {
                    await Task.Delay(retryDelays[attempt - 1], cancellationToken);
                    continue;
                }

                throw new InvalidOperationException(BuildFriendlyGeminiError(response.StatusCode));
            }
            catch (HttpRequestException) when (attempt <= retryDelays.Length)
            {
                await Task.Delay(retryDelays[attempt - 1], cancellationToken);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested && attempt <= retryDelays.Length)
            {
                await Task.Delay(retryDelays[attempt - 1], cancellationToken);
            }
        }

        throw new InvalidOperationException("Gemini no pudo responder. Intenta nuevamente en unos minutos.");
    }

    private static bool IsTransientGeminiError(HttpStatusCode statusCode) =>
        statusCode is HttpStatusCode.TooManyRequests or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout;

    private static string BuildFriendlyGeminiError(HttpStatusCode statusCode) =>
        statusCode switch
        {
            HttpStatusCode.ServiceUnavailable =>
                "Gemini esta con alta demanda en este momento. Intenta nuevamente en unos minutos.",
            HttpStatusCode.TooManyRequests =>
                "Se alcanzo el limite temporal de uso de Gemini. Espera un momento y vuelve a intentar.",
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden =>
                "Gemini rechazo la solicitud. Revisa que la API key este activa y tenga permisos.",
            HttpStatusCode.RequestEntityTooLarge =>
                "El documento es demasiado pesado para enviarlo completo a Gemini. Prueba con otro documento o una version mas ligera.",
            _ =>
                $"Gemini no pudo responder correctamente (codigo {(int)statusCode}). Intenta nuevamente."
        };

    private static string BuildPrompt(
        Opportunity opportunity,
        RecommendationAnalysis recommendation,
        SeaceDocumentAnalysisContext? documentContext) =>
        $$"""
        Eres un analista peruano senior de licitaciones publicas peruanas. Analiza la oportunidad usando solo la ficha, cronograma y documentos SEACE adjuntos.
        Si hay PDFs adjuntos, leelos como documentos oficiales, incluso si son escaneados o imagenes. Extrae datos visibles de las bases; no hagas un resumen generico.
        Devuelve exclusivamente JSON valido, sin markdown, con estas propiedades:
        recommendation, summary, risks, requirements, nextSteps.

        Formato obligatorio del contenido:
        - recommendation: una frase corta: "Postular", "Revisar antes de postular" o "Descartar por ahora".
        - summary: 4 a 7 lineas con objeto, entidad, estado/etapa, monto si existe, fechas clave y conclusion.
        - requirements: lista textual con requisitos concretos encontrados en bases/documentos. Incluye certificados, registros, experiencia, habilitaciones, documentos de oferta, garantias, plazos, lugar de entrega y condiciones tecnicas. Si un dato no aparece, escribe "No encontrado" para ese dato.
        - risks: lista textual de riesgos reales detectados: vencimiento, proceso desierto, requisitos restrictivos, certificados faltantes, entrega compleja, penalidades, monto no definido, documentos ilegibles. Si no detectas riesgo, escribe "No encontrado".
        - nextSteps: acciones concretas para el equipo comercial/legal/tecnico antes de postular.

        Datos:
        Codigo: {{opportunity.ProcessCode}}
        Titulo: {{opportunity.Title}}
        Entidad: {{opportunity.EntityName}}
        Categoria: {{opportunity.Category}}
        Modalidad: {{opportunity.Modality}}
        Monto estimado: {{opportunity.EstimatedAmount}}
        Fecha de cierre: {{opportunity.ClosingDate:yyyy-MM-dd HH:mm}}
        Ubicacion: {{opportunity.Location}}
        Descripcion: {{opportunity.Summary}}

        Afinidad local: {{recommendation.Score}}%
        Etiqueta local: {{recommendation.Label}}
        Motivo local: {{recommendation.Reason}}
        Keywords encontradas: {{string.Join(", ", recommendation.MatchedKeywords)}}

        Ficha SEACE JSON:
        {{opportunity.SeaceDetailJson}}

        Cronograma SEACE JSON:
        {{opportunity.SeaceScheduleJson}}

        Documentos SEACE JSON:
        {{opportunity.SeaceDocumentsJson}}

        Contexto documental extraido:
        {{documentContext?.Summary ?? "No se analizaron bases/documentos oficiales en esta ejecucion."}}

        Reglas:
        - Escribe en espanol claro y breve.
        - No inventes requisitos que no esten en la ficha o documentos.
        - Si hay documentos adjuntos, prioriza sus requisitos sobre la descripcion resumida.
        - Si un ZIP no pudo leerse completamente o faltan bases, indicalo como riesgo o pendiente.
        - Distingue entre convocatoria activa, desierta, segunda convocatoria u otorgamiento si el documento lo indica.
        - Cuando cites un requisito, menciona de donde sale si puedes: bases, anexo, especificaciones tecnicas, cronograma o ficha SEACE.
        - Si el PDF escaneado no se puede leer con claridad, dilo explicitamente y no inventes.
        - Evita frases genericas como "revisar bases" salvo como accion secundaria; primero entrega hallazgos concretos.
        """;

    private static string BuildDocumentQuestionPrompt(
        Opportunity opportunity,
        SeaceDocumentAnalysisContext documentContext,
        string question) =>
        $$"""
        Eres Gemini, asistente de analisis documental para licitaciones publicas peruanas dentro de LicitIA.
        Responde usando solo el documento SEACE actualmente abierto y el contexto del proceso.
        El usuario puede hacer varias preguntas seguidas sobre el mismo documento: no asumas que se perdio el archivo.
        Tu tarea principal es explicar TODO lo que el documento abierto contenga sobre la pregunta: capitulos, secciones, tablas, anexos, requisitos, plazos, penalidades, procedimientos, montos, criterios, documentos obligatorios, definiciones o cualquier tema consultado.
        Primero busca una respuesta directa. Si no encuentras el termino literal, busca informacion relacionada por sinonimos, titulos cercanos, anexos, tablas, subtitulos y secciones del mismo tema.
        Si encuentras una seccion, capitulo, titulo, tabla o parrafo relacionado, responde directamente con esa informacion. No empieces diciendo que no aparece.
        Solo di que no encontraste informacion cuando no haya ninguna evidencia relacionada en el documento abierto.
        Si el documento abierto es un informe, acta, declaratoria de desierto u otorgamiento, responde igualmente sobre lo que ese documento contenga; solo aclara sus limites si el usuario pide criterios, requisitos o puntajes que no estan alli.
        Si el documento abierto es Bases Administrativas o Bases Integradas, revisa con especial cuidado los capitulos, indices, anexos y secciones numeradas.
        Si el usuario pregunta por una seccion como "2.2.1", tambien revisa subsecciones cercanas como "2.2.1.1", "2.2.1.2" y encabezados relacionados. No digas que no aparece si existe una subseccion visible relacionada.

        Proceso:
        Codigo: {{opportunity.ProcessCode}}
        Titulo: {{opportunity.Title}}
        Entidad: {{opportunity.EntityName}}
        Objeto: {{opportunity.ContractObject}}
        Descripcion: {{opportunity.Summary}}
        Fecha de cierre: {{opportunity.ClosingDate:yyyy-MM-dd HH:mm}}

        Ficha SEACE JSON:
        {{opportunity.SeaceDetailJson}}

        Cronograma SEACE JSON:
        {{opportunity.SeaceScheduleJson}}

        Contexto extraido del documento:
        {{documentContext.Summary}}

        Documentos adjuntos enviados en esta consulta:
        {{string.Join(", ", documentContext.InlineDocuments.Select(document => document.Name))}}

        Pregunta:
        {{question}}

        Reglas:
        - Responde en espanol claro, breve y accionable.
        - No inventes datos. Puedes inferir solo relaciones obvias entre secciones visibles, pero no crear requisitos, puntajes, montos ni fechas que no esten en el documento.
        - Responde sobre cualquier tema del documento, no solo sobre requisitos o criterios.
        - Si encuentras informacion relacionada, usa un encabezado natural como "Segun el documento" o "En el capitulo/seccion..." y resume los puntos encontrados.
        - Si preguntas por requisitos, separa requisitos tecnicos, legales/documentarios, experiencia, garantias, plazos y entregables cuando existan.
        - Si preguntas por criterios de evaluacion, menciona factores, puntajes y como mejorar el puntaje si aparece.
        - Si preguntas por una seccion numerada, responde con el titulo de la seccion, los puntos encontrados y las subsecciones relacionadas.
        - Si preguntas por multas o sanciones, lista penalidades, causas y porcentajes/montos si aparecen.
        - Si el PDF esta escaneado y una parte no se lee con claridad, dilo explicitamente.
        - Si no hay ninguna informacion relacionada, di "No encontre informacion sobre ese punto en el documento abierto" y sugiere que documento podria contenerla.
        - No recomiendes "revisar el documento adjunto" como respuesta principal, porque ya lo estas revisando.
        - Termina con un siguiente paso recomendado solo si aporta valor.
        """;

    private static string BuildDocumentExtractionPrompt(
        Opportunity opportunity,
        SeaceDocumentAnalysisContext documentContext) =>
        $$"""
        Eres Gemini, motor de OCR y extraccion documental para LicitIA.
        Lee el documento SEACE adjunto una sola vez y genera un contexto textual amplio, ordenado y reutilizable para responder preguntas posteriores sin reenviar el PDF.

        Proceso:
        Codigo: {{opportunity.ProcessCode}}
        Titulo: {{opportunity.Title}}
        Entidad: {{opportunity.EntityName}}
        Objeto: {{opportunity.ContractObject}}
        Descripcion: {{opportunity.Summary}}

        Contexto ya conocido:
        {{documentContext.Summary}}

        Documentos enviados:
        {{string.Join(", ", documentContext.InlineDocuments.Select(document => document.Name))}}

        Extrae en espanol, con el mayor detalle posible y sin inventar:
        1. Nombre/tipo del documento, procedimiento, entidad, objeto y estado si aparecen.
        2. Indice, capitulos, secciones y subsecciones relevantes, conservando numeracion visible.
        3. Cronograma, plazos, fechas, registro de participantes, consultas, presentacion de ofertas, evaluacion, buena pro y perfeccionamiento del contrato.
        4. Requisitos de admision, documentos obligatorios, anexos, certificados, declaraciones juradas, registros, experiencia, garantias y poderes.
        5. Especificaciones tecnicas, cantidades, lugar de entrega, condiciones de entrega, conformidad, pagos y penalidades.
        6. Criterios de evaluacion, factores, puntajes, bonificaciones y criterios de desempate.
        7. Recursos, impugnaciones, recurso de apelacion, acceso al expediente y consentimiento de buena pro.
        8. Riesgos o datos que se vean incompletos, ilegibles o escaneados.

        Formato:
        - Usa encabezados por capitulo/seccion.
        - Incluye numero de pagina aproximado si lo detectas.
        - Copia frases clave cortas cuando sean importantes.
        - Si una parte no se lee por escaneo, indicalo.
        - No respondas a una pregunta todavia; solo genera el contexto textual reutilizable.
        """;

    private static string BuildDocumentQuestionFromTextPrompt(
        Opportunity opportunity,
        string extractedDocumentText,
        string question) =>
        $$"""
        Eres Gemini, asistente documental de LicitIA.
        Responde usando exclusivamente el contexto textual extraido del documento SEACE abierto. No digas que no tienes el documento: el texto extraido es el documento disponible.

        Proceso:
        Codigo: {{opportunity.ProcessCode}}
        Titulo: {{opportunity.Title}}
        Entidad: {{opportunity.EntityName}}
        Objeto: {{opportunity.ContractObject}}
        Descripcion: {{opportunity.Summary}}
        Fecha de cierre: {{opportunity.ClosingDate:yyyy-MM-dd HH:mm}}

        Texto/contexto extraido del documento:
        {{extractedDocumentText}}

        Pregunta del usuario:
        {{question}}

        Reglas:
        - Responde en espanol claro y accionable.
        - Busca por terminos literales y relacionados. Si preguntan por "capitulo 4", revisa tambien secciones 4.1, 4.2, 4.3, etc.
        - Si encuentras informacion relacionada, responde directamente y menciona capitulo/seccion/pagina aproximada si aparece.
        - Si el usuario pregunta por requisitos, separa documentos, experiencia, garantias, plazos y condiciones cuando existan.
        - Si pregunta por criterios de evaluacion, menciona factores, puntajes y como obtener mejor puntaje si aparece.
        - Si pregunta por penalidades o sanciones, lista causas, porcentajes/montos y condiciones si aparecen.
        - No inventes datos que no esten en el contexto extraido.
        - Solo di "No encontre informacion sobre ese punto en el documento abierto" si el contexto no contiene nada relacionado.
        """;

    private static string ExtractText(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            return body;
        }

        var parts = candidates[0].GetProperty("content").GetProperty("parts");
        return parts.EnumerateArray()
            .Select(part => part.TryGetProperty("text", out var text) ? text.GetString() : null)
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Aggregate(string.Empty, (current, next) => current + next);
    }

    private static ParsedAiAnalysis ParseAnalysis(string text)
    {
        var json = StripCodeFence(text);
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            return new ParsedAiAnalysis(
                ReadString(root, "recommendation", "Revisar antes de postular"),
                ReadString(root, "summary", text),
                ReadStringOrList(root, "risks", "Revisar bases y requisitos antes de postular."),
                ReadStringOrList(root, "requirements", "No se identificaron requisitos especificos en la informacion disponible."),
                ReadStringOrList(root, "nextSteps", "Revisar documentos oficiales y validar experiencia requerida."));
        }
        catch (JsonException)
        {
            return new ParsedAiAnalysis(
                "Revisar antes de postular",
                text,
                "La respuesta IA no vino en formato estructurado; revisar manualmente.",
                "Revisar bases oficiales.",
                "Validar requisitos y fechas antes de preparar propuesta.");
        }
    }

    private static string StripCodeFence(string text)
    {
        var cleaned = text.Trim();
        if (cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            var firstBreak = cleaned.IndexOf('\n');
            var lastFence = cleaned.LastIndexOf("```", StringComparison.Ordinal);
            if (firstBreak >= 0 && lastFence > firstBreak)
            {
                cleaned = cleaned[(firstBreak + 1)..lastFence].Trim();
            }
        }

        return cleaned;
    }

    private static string ReadString(JsonElement root, string name, string fallback) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? fallback
            : fallback;

    private static string ReadStringOrList(JsonElement root, string name, string fallback)
    {
        if (!root.TryGetProperty(name, out var value))
        {
            return fallback;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? fallback;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            var items = value.EnumerateArray()
                .Select(item => item.ValueKind switch
                {
                    JsonValueKind.String => item.GetString(),
                    JsonValueKind.Object => string.Join(": ", item.EnumerateObject()
                        .Select(property => property.Value.ValueKind == JsonValueKind.String
                            ? property.Value.GetString()
                            : property.Value.GetRawText())
                        .Where(text => !string.IsNullOrWhiteSpace(text))),
                    _ => item.GetRawText()
                })
                .Where(text => !string.IsNullOrWhiteSpace(text))
                .Select(text => $"- {text}");

            var result = string.Join("\n", items);
            return string.IsNullOrWhiteSpace(result) ? fallback : result;
        }

        return fallback;
    }

    private sealed record GeminiRequest(IEnumerable<GeminiContent> Contents);

    private sealed record GeminiContent(IEnumerable<GeminiPart> Parts);

    private sealed class GeminiPart
    {
        [JsonPropertyName("text")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Text { get; init; }

        [JsonPropertyName("inline_data")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public GeminiInlineData? InlineData { get; init; }

        public static GeminiPart FromText(string text) => new() { Text = text };

        public static GeminiPart FromInlineData(string mimeType, string data) => new()
        {
            InlineData = new GeminiInlineData(mimeType, data)
        };
    }

    private sealed record GeminiInlineData(
        [property: JsonPropertyName("mime_type")] string MimeType,
        [property: JsonPropertyName("data")] string Data);

    private sealed record ParsedAiAnalysis(
        string Recommendation,
        string Summary,
        string Risks,
        string Requirements,
        string NextSteps);
}
