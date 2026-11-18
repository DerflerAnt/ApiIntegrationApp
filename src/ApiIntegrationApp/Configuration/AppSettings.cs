using Microsoft.Extensions.Configuration;

namespace ApiIntegrationApp.Configuration;

/// <summary>
/// Завантажує та валідує налаштування застосунку.
/// Пріоритет джерел (наступне перекриває попереднє):
///   1. appsettings.json         — несекретні значення за замовчуванням;
///   2. User Secrets             — секрети для локальної розробки;
///   3. Змінні середовища        — напр. ConnectionStrings__FullStack, Api__ApiKey.
/// </summary>
public sealed class AppSettings
{
    public const string ConnectionStringName = "FullStack";

    public required ApiSettings Api { get; init; }
    public required string ConnectionString { get; init; }

    public static AppSettings Load()
    {
        IConfiguration configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddUserSecrets<AppSettings>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var api = configuration.GetSection(ApiSettings.SectionName).Get<ApiSettings>()
                  ?? throw new InvalidOperationException(
                      $"Секція '{ApiSettings.SectionName}' відсутня в конфігурації.");

        var connectionString = configuration.GetConnectionString(ConnectionStringName);

        var settings = new AppSettings
        {
            Api = api,
            ConnectionString = connectionString ?? string.Empty
        };

        settings.Validate();
        return settings;
    }

    private void Validate()
    {
        var errors = new List<string>();

        if (!Uri.TryCreate(Api.BaseUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("Api:BaseUrl має бути абсолютною HTTP/HTTPS адресою.");
        }

        if (Api.DefaultUserId <= 0)
            errors.Add("Api:DefaultUserId має бути додатним числом.");

        if (Api.TimeoutSeconds <= 0)
            errors.Add("Api:TimeoutSeconds має бути додатним числом.");

        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            errors.Add(
                $"Рядок підключення 'ConnectionStrings:{ConnectionStringName}' не задано. " +
                "Додайте його через User Secrets або змінну середовища " +
                $"'ConnectionStrings__{ConnectionStringName}' (див. README.md).");
        }

        if (errors.Count > 0)
            throw new InvalidOperationException(
                "Помилка конфігурації:" + Environment.NewLine + " - " +
                string.Join(Environment.NewLine + " - ", errors));
    }
}
