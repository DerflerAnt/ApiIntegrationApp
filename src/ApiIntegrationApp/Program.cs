using System.Text;
using ApiIntegrationApp.Configuration;
using Microsoft.Data.SqlClient;

Console.OutputEncoding = Encoding.UTF8;

AppSettings settings;
try
{
    settings = AppSettings.Load();
}
catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException or FormatException)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine(ex.Message);
    Console.ResetColor();
    return 1;
}

// Не виводимо повний рядок підключення — лише безпечні частини.
var csb = new SqlConnectionStringBuilder(settings.ConnectionString);

Console.WriteLine("=== Конфігурацію завантажено ===");
Console.WriteLine($"API BaseUrl     : {settings.Api.BaseUrl}");
Console.WriteLine($"Default userId  : {settings.Api.DefaultUserId}");
Console.WriteLine($"Timeout (сек)   : {settings.Api.TimeoutSeconds}");
Console.WriteLine($"API Key         : {(string.IsNullOrEmpty(settings.Api.ApiKey) ? "(не потрібен)" : "***")}");
Console.WriteLine($"SQL Server      : {csb.DataSource}");
Console.WriteLine($"Database        : {csb.InitialCatalog}");

return 0;
