using Microsoft.Extensions.Logging;

namespace BlazorApp.Services;

/// <summary>
/// Servicio para formatear respuestas con Markdown y estructura mejorada
/// </summary>
public class ResponseFormatter
{
    private readonly ILogger<ResponseFormatter> _logger;

    public ResponseFormatter(ILogger<ResponseFormatter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Formatea una respuesta del LLM para mejor presentación
    /// </summary>
    public string FormatResponse(string response, ResponseFormatOptions? options = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(response))
                return response;

            options ??= new ResponseFormatOptions();

            var formattedResponse = response;

            // Paso 1: Limpiar respuesta básica
            formattedResponse = CleanResponse(formattedResponse);

            // Paso 2: Formatear según opciones
            if (options.FormatMarkdown)
            {
                formattedResponse = FormatMarkdown(formattedResponse);
            }

            if (options.HighlightCode)
            {
                formattedResponse = HighlightCodeBlocks(formattedResponse);
            }

            if (options.NumberSteps)
            {
                formattedResponse = FormatNumberedSteps(formattedResponse);
            }

            if (options.AddSources && options.Sources != null && options.Sources.Any())
            {
                formattedResponse = AddSourcesSection(formattedResponse, options.Sources);
            }

            // Paso 3: Aplicar estructura final
            if (options.WrapInContainer)
            {
                formattedResponse = WrapInContainer(formattedResponse, options);
            }

            return formattedResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al formatear respuesta");
            return response; // Devolver respuesta original en caso de error
        }
    }

    /// <summary>
    /// Limpia la respuesta de artefactos comunes
    /// </summary>
    private string CleanResponse(string response)
    {
        var cleaned = response;

        // Remover frases comunes de introducción
        var cleanupPhrases = new[]
        {
            "Basándome en el contexto proporcionado,",
            "Según la información en el contexto,",
            "De acuerdo con el contexto,",
            "La información en el contexto indica que",
            "El contexto menciona que",
            "Aquí tienes la respuesta:",
            "La respuesta es:",
            "Respuesta:"
        };

        foreach (var phrase in cleanupPhrases)
        {
            if (cleaned.StartsWith(phrase, StringComparison.OrdinalIgnoreCase))
            {
                cleaned = cleaned.Substring(phrase.Length).TrimStart();
            }
        }

        // Normalizar espacios en blanco
        cleaned = System.Text.RegularExpressions.Regex.Replace(cleaned, @"\s+", " ");
        cleaned = cleaned.Trim();

        // Asegurar que termine con puntuación adecuada
        if (!cleaned.EndsWith(".") && !cleaned.EndsWith("!") && !cleaned.EndsWith("?"))
        {
            cleaned += ".";
        }

        return cleaned;
    }

    /// <summary>
    /// Formatea texto Markdown básico
    /// </summary>
    private string FormatMarkdown(string text)
    {
        var formatted = text;

        // Encabezados
        formatted = System.Text.RegularExpressions.Regex.Replace(
            formatted,
            @"^(#+)\s+(.+)$",
            match => $"<h{match.Groups[1].Value.Length}>{match.Groups[2].Value}</h{match.Groups[1].Value.Length}>",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        // Negrita
        formatted = System.Text.RegularExpressions.Regex.Replace(
            formatted,
            @"\*\*(.+?)\*\*",
            "<strong>$1</strong>");

        // Cursiva
        formatted = System.Text.RegularExpressions.Regex.Replace(
            formatted,
            @"\*(.+?)\*",
            "<em>$1</em>");

        // Listas no ordenadas
        formatted = System.Text.RegularExpressions.Regex.Replace(
            formatted,
            @"^\s*[-*]\s+(.+)$",
            "<li>$1</li>",
            System.Text.RegularExpressions.RegexOptions.Multiline);

        // Bloque de código
        formatted = System.Text.RegularExpressions.Regex.Replace(
            formatted,
            @"```(\w+)?\s*\n([\s\S]*?)\n```",
            match =>
            {
                var language = match.Groups[1].Success ? match.Groups[1].Value : "text";
                var code = match.Groups[2].Value;
                return $"<pre class=\"code-block language-{language}\"><code>{code}</code></pre>";
            });

        // Código en línea
        formatted = System.Text.RegularExpressions.Regex.Replace(
            formatted,
            @"`([^`]+)`",
            "<code>$1</code>");

        return formatted;
    }

    /// <summary>
    /// Resalta bloques de código
    /// </summary>
    private string HighlightCodeBlocks(string text)
    {
        var formatted = text;

        formatted = System.Text.RegularExpressions.Regex.Replace(
            formatted,
            @"<pre class=""code-block language-(\w+)""><code>([\s\S]*?)</code></pre>",
            match =>
            {
                var language = match.Groups[1].Value;
                var code = System.Web.HttpUtility.HtmlEncode(match.Groups[2].Value);
                
                return $@"
                <div class=""code-container"">
                    <div class=""code-header"">
                        <span class=""language-label"">{language}</span>
                        <button class=""copy-code-btn"" onclick=""copyCode(this)"">
                            <i class=""bi bi-clipboard""></i>
                        </button>
                    </div>
                    <pre class=""code-block highlighted""><code class=""language-{language}"">{code}</code></pre>
                </div>";
            });

        return formatted;
    }

    /// <summary>
    /// Formatea pasos numerados
    /// </summary>
    private string FormatNumberedSteps(string text)
    {
        var formatted = text;

        // Detectar secuencias de pasos
        var stepPatterns = new[]
        {
            @"(\d+)\.\s+(.+?)(?=\n\d+\.|\n\n|$)",
            @"Paso\s+(\d+):\s+(.+?)(?=\nPaso\s+\d+:|$)",
            @"Step\s+(\d+):\s+(.+?)(?=\nStep\s+\d+:|$)"
        };

        foreach (var pattern in stepPatterns)
        {
            formatted = System.Text.RegularExpressions.Regex.Replace(
                formatted,
                pattern,
                match =>
                {
                    var number = match.Groups[1].Value;
                    var content = match.Groups[2].Value;
                    return $"<div class=\"step\"><span class=\"step-number\">{number}</span><span class=\"step-content\">{content}</span></div>";
                },
                System.Text.RegularExpressions.RegexOptions.Multiline | System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        }

        return formatted;
    }

    /// <summary>
    /// Agrega sección de fuentes
    /// </summary>
    private string AddSourcesSection(string text, List<string> sources)
    {
        if (!sources.Any())
            return text;

        var sourcesHtml = @"
            <div class=""sources-section"">
                <div class=""sources-header"">
                    <i class=""bi bi-file-text""></i>
                    <span>Fuentes utilizadas:</span>
                </div>
                <div class=""sources-list"">";

        foreach (var source in sources.Distinct().Take(5)) // Limitar a 5 fuentes
        {
            sourcesHtml += $@"<div class=""source-item"">
                <i class=""bi bi-file-earmark""></i>
                <span>{source}</span>
            </div>";
        }

        sourcesHtml += "</div></div>";

        return text + sourcesHtml;
    }

    /// <summary>
    /// Envuelve la respuesta en un contenedor estilizado
    /// </summary>
    private string WrapInContainer(string text, ResponseFormatOptions options)
    {
        var containerClass = "formatted-response";
        
        if (options.ShowBorder)
        {
            containerClass += " bordered";
        }
        
        if (options.RoundedCorners)
        {
            containerClass += " rounded";
        }
        
        if (options.AddShadow)
        {
            containerClass += " shadow";
        }

        return $@"
            <div class=""{containerClass}"">
                <div class=""response-content"">
                    {text}
                </div>
                {GetResponseFooter(options)}
            </div>";
    }

    /// <summary>
    /// Genera el pie de página de la respuesta
    /// </summary>
    private string GetResponseFooter(ResponseFormatOptions options)
    {
        if (!options.ShowFooter)
            return string.Empty;

        var footerItems = new List<string>();

        if (options.ProcessingTimeMs > 0)
        {
            footerItems.Add($"Tiempo: {options.ProcessingTimeMs}ms");
        }

        if (options.ContextChunksUsed > 0)
        {
            footerItems.Add($"Chunks: {options.ContextChunksUsed}");
        }

        if (options.AverageSimilarity > 0)
        {
            footerItems.Add($"Relevancia: {options.AverageSimilarity:P1}");
        }

        if (!footerItems.Any())
            return string.Empty;

        return $@"
            <div class=""response-footer"">
                <div class=""footer-stats"">
                    {string.Join(" • ", footerItems)}
                </div>
            </div>";
    }

    /// <summary>
    /// Extrae información importante de la respuesta
    /// </summary>
    public ResponseInfo ExtractResponseInfo(string response)
    {
        try
        {
            var info = new ResponseInfo
            {
                OriginalLength = response.Length,
                WordCount = CountWords(response),
                HasSteps = ContainsSteps(response),
                HasCode = ContainsCode(response),
                HasLinks = ContainsLinks(response),
                HasLists = ContainsLists(response),
                EstimatedReadingTime = CalculateReadingTime(response)
            };

            // Extraer palabras clave
            info.Keywords = ExtractKeywords(response);

            return info;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al extraer información de respuesta");
            return new ResponseInfo { OriginalLength = response.Length };
        }
    }

    /// <summary>
    /// Cuenta palabras en el texto
    /// </summary>
    private int CountWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        var words = System.Text.RegularExpressions.Regex.Matches(text, @"\b\w+\b");
        return words.Count;
    }

    /// <summary>
    /// Verifica si contiene pasos numerados
    /// </summary>
    private bool ContainsSteps(string text)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(text, @"\d+\.\s+\w+|Paso\s+\d+:|Step\s+\d+:", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Verifica si contiene código
    /// </summary>
    private bool ContainsCode(string text)
    {
        return text.Contains("```") || text.Contains("`");
    }

    /// <summary>
    /// Verifica si contiene enlaces
    /// </summary>
    private bool ContainsLinks(string text)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(text, @"https?://\S+|www\.\S+");
    }

    /// <summary>
    /// Verifica si contiene listas
    /// </summary>
    private bool ContainsLists(string text)
    {
        return System.Text.RegularExpressions.Regex.IsMatch(text, @"^\s*[-*]\s+\w+", 
            System.Text.RegularExpressions.RegexOptions.Multiline);
    }

    /// <summary>
    /// Calcula tiempo estimado de lectura
    /// </summary>
    private TimeSpan CalculateReadingTime(string text)
    {
        var words = CountWords(text);
        var minutes = words / 200.0; // 200 palabras por minuto
        return TimeSpan.FromMinutes(Math.Max(0.5, minutes)); // Mínimo 30 segundos
    }

    /// <summary>
    /// Extrae palabras clave del texto
    /// </summary>
    private List<string> ExtractKeywords(string text)
    {
        try
        {
            // Lista de stop words en español
            var stopWords = new HashSet<string>
            {
                "el", "la", "los", "las", "un", "una", "unos", "unas",
                "de", "del", "al", "y", "o", "pero", "porque", "como",
                "que", "qué", "cual", "cuales", "donde", "cuando",
                "este", "esta", "estos", "estas", "aquel", "aquella",
                "es", "son", "era", "fueron", "ser", "estar", "tener"
            };

            var words = System.Text.RegularExpressions.Regex.Matches(text.ToLower(), @"\b\w+\b")
                .Cast<System.Text.RegularExpressions.Match>()
                .Select(m => m.Value)
                .Where(w => w.Length > 3 && !stopWords.Contains(w))
                .GroupBy(w => w)
                .OrderByDescending(g => g.Count())
                .Take(10)
                .Select(g => g.Key)
                .ToList();

            return words;
        }
        catch
        {
            return new List<string>();
        }
    }
}

/// <summary>
/// Opciones de formateo de respuesta
/// </summary>
public class ResponseFormatOptions
{
    /// <summary>
    /// Formatear Markdown
    /// </summary>
    public bool FormatMarkdown { get; set; } = true;

    /// <summary>
    /// Resaltar código
    /// </summary>
    public bool HighlightCode { get; set; } = true;

    /// <summary>
    /// Numerar pasos automáticamente
    /// </summary>
    public bool NumberSteps { get; set; } = true;

    /// <summary>
    /// Agregar sección de fuentes
    /// </summary>
    public bool AddSources { get; set; } = true;

    /// <summary>
    /// Fuentes a mostrar
    /// </summary>
    public List<string>? Sources { get; set; }

    /// <summary>
    /// Envolver en contenedor estilizado
    /// </summary>
    public bool WrapInContainer { get; set; } = true;

    /// <summary>
    /// Mostrar borde
    /// </summary>
    public bool ShowBorder { get; set; } = true;

    /// <summary>
    /// Esquinas redondeadas
    /// </summary>
    public bool RoundedCorners { get; set; } = true;

    /// <summary>
    /// Agregar sombra
    /// </summary>
    public bool AddShadow { get; set; } = true;

    /// <summary>
    /// Mostrar pie de página
    /// </summary>
    public bool ShowFooter { get; set; } = true;

    /// <summary>
    /// Tiempo de procesamiento en ms
    /// </summary>
    public long ProcessingTimeMs { get; set; }

    /// <summary>
    /// Número de chunks de contexto utilizados
    /// </summary>
    public int ContextChunksUsed { get; set; }

    /// <summary>
    /// Similaridad promedio del contexto
    /// </summary>
    public double AverageSimilarity { get; set; }
}

/// <summary>
/// Información extraída de la respuesta
/// </summary>
public class ResponseInfo
{
    /// <summary>
    /// Longitud original
    /// </summary>
    public int OriginalLength { get; set; }

    /// <summary>
    /// Número de palabras
    /// </summary>
    public int WordCount { get; set; }

    /// <summary>
    /// Indica si contiene pasos
    /// </summary>
    public bool HasSteps { get; set; }

    /// <summary>
    /// Indica si contiene código
    /// </summary>
    public bool HasCode { get; set; }

    /// <summary>
    /// Indica si contiene enlaces
    /// </summary>
    public bool HasLinks { get; set; }

    /// <summary>
    /// Indica si contiene listas
    /// </summary>
    public bool HasLists { get; set; }

    /// <summary>
    /// Tiempo estimado de lectura
    /// </summary>
    public TimeSpan EstimatedReadingTime { get; set; }

    /// <summary>
    /// Palabras clave extraídas
    /// </summary>
    public List<string> Keywords { get; set; } = new();

    /// <summary>
    /// Formatea el tiempo de lectura
    /// </summary>
    public string ReadingTimeFormatted
    {
        get
        {
            if (EstimatedReadingTime.TotalMinutes < 1)
                return "Menos de 1 min";
            
            if (EstimatedReadingTime.TotalMinutes < 2)
                return "1 min";
            
            return $"{(int)EstimatedReadingTime.TotalMinutes} mins";
        }
    }
}