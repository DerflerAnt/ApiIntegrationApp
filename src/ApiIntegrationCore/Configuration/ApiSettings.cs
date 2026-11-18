namespace ApiIntegrationApp.Configuration;

/// <summary>
/// Налаштування публічного API (секція "Api" у конфігурації).
/// </summary>
public sealed class ApiSettings
{
    public const string SectionName = "Api";

    /// <summary>Базова адреса API, обов'язково з кінцевим "/".</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>Значення query-параметра userId за замовчуванням.</summary>
    public int DefaultUserId { get; set; } = 1;

    /// <summary>Таймаут HTTP-запиту в секундах.</summary>
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Ключ API (JSONPlaceholder його не потребує, але для інших API
    /// його треба зберігати в User Secrets або змінній середовища).
    /// </summary>
    public string? ApiKey { get; set; }
}
