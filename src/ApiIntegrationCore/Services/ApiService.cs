using System.Net.Http.Headers;
using System.Text.Json;
using ApiIntegrationApp.Configuration;
using ApiIntegrationApp.Models;

namespace ApiIntegrationApp.Services;

/// <summary>
/// Клієнт JSONPlaceholder API на базі HttpClient і System.Text.Json.
/// </summary>
public sealed class ApiService
{
    private readonly HttpClient _httpClient;

    public ApiService(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Створює налаштований HttpClient на основі конфігурації.
    /// </summary>
    public static HttpClient CreateHttpClient(ApiSettings settings)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(settings.BaseUrl),
            Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds)
        };

        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.UserAgent.ParseAdd("ApiIntegrationApp/1.0");

        // Ключ API береться лише з конфігурації (User Secrets / змінні середовища).
        if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            client.DefaultRequestHeaders.Add("x-api-key", settings.ApiKey);

        return client;
    }

    /// <summary>GET /users/{id}</summary>
    public async Task<User> GetUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);

        var user = await GetAsync<User>($"users/{userId}", cancellationToken);

        if (user.Id <= 0)
            throw new ApiException($"API повернуло порожні дані користувача з id={userId}.");

        return user;
    }

    /// <summary>GET /posts?userId={id} — query-параметр фільтрує пости за автором.</summary>
    public async Task<IReadOnlyList<Post>> GetPostsByUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        ValidateUserId(userId);

        var posts = await GetAsync<List<Post>>($"posts?userId={userId}", cancellationToken);

        if (posts.Count == 0)
            throw new ApiException($"API повернуло порожній список постів для userId={userId}.");

        return posts;
    }

    private async Task<T> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken) where T : class
    {
        var requestUri = new Uri(_httpClient.BaseAddress!, relativeUrl);

        try
        {
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);

            // Кидає HttpRequestException, якщо статус не 2xx (404, 500 тощо).
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(json))
                throw new ApiException($"API повернуло порожню відповідь: {requestUri}");

            var result = JsonSerializer.Deserialize<T>(json);

            return result ?? throw new ApiException($"API повернуло null замість даних: {requestUri}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode is not null)
        {
            throw new ApiException(
                $"API повернуло помилковий статус {(int)ex.StatusCode} ({ex.StatusCode}) для {requestUri}", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(
                $"Не вдалося з'єднатися з API ({requestUri.Host}). Перевірте підключення до мережі. Деталі: {ex.Message}", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ApiException(
                $"Перевищено час очікування відповіді від API ({_httpClient.Timeout.TotalSeconds} с): {requestUri}", ex);
        }
        catch (JsonException ex)
        {
            throw new ApiException($"Не вдалося розібрати JSON-відповідь від {requestUri}: {ex.Message}", ex);
        }
    }

    private static void ValidateUserId(int userId)
    {
        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId), userId, "userId має бути додатним числом.");
    }
}
