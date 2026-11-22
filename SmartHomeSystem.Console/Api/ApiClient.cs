using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HomeConsole.Api;

/// <summary>Помилка, яку повернув веб-API (400, 404, 409 ...).</summary>
public sealed class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    public ApiException(HttpStatusCode statusCode, string message) : base(message)
        => StatusCode = statusCode;
}

/// <summary>Тонка обгортка над HttpClient: JSON як у API (snake_case, enum рядками).</summary>
public sealed class ApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HttpClient _http;

    public ApiClient(HttpClient http) => _http = http;

    /// <summary>GET; повертає null, якщо API відповіло 404.</summary>
    public async Task<T?> GetOrNullAsync<T>(string url) where T : class
    {
        using var response = await _http.GetAsync(url);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        await EnsureSuccessAsync(response);
        return await ReadAsync<T>(response);
    }

    public async Task<T> SendAsync<T>(HttpMethod method, string url, object? body = null)
    {
        using var response = await SendRawAsync(method, url, body);
        return await ReadAsync<T>(response)
               ?? throw new ApiException(response.StatusCode, "API повернуло порожню відповідь.");
    }

    public async Task SendAsync(HttpMethod method, string url, object? body = null)
    {
        using var response = await SendRawAsync(method, url, body);
    }

    private async Task<HttpResponseMessage> SendRawAsync(HttpMethod method, string url, object? body)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
        {
            var json = JsonSerializer.Serialize(body, body.GetType(), JsonOptions);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        var response = await _http.SendAsync(request);
        try
        {
            await EnsureSuccessAsync(response);
            return response;
        }
        catch
        {
            response.Dispose();
            throw;
        }
    }

    private static async Task<T?> ReadAsync<T>(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        return string.IsNullOrWhiteSpace(text) ? default : JsonSerializer.Deserialize<T>(text, JsonOptions);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var text = await response.Content.ReadAsStringAsync();
        throw new ApiException(response.StatusCode, ExtractMessage(text, response.StatusCode));
    }

    // ProblemDetails (наш middleware): detail; стандартна валідація [ApiController]: errors.
    private static string ExtractMessage(string text, HttpStatusCode status)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            if (root.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
            {
                return detail.GetString()!;
            }

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
            {
                var messages = errors.EnumerateObject()
                    .SelectMany(p => p.Value.EnumerateArray().Select(e => e.GetString()));
                return string.Join(" ", messages);
            }

            if (root.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
            {
                return title.GetString()!;
            }
        }
        catch (JsonException)
        {
            // тіло не JSON — повертаємо загальний текст нижче
        }

        return $"API відповіло {(int)status} {status}.";
    }
}
