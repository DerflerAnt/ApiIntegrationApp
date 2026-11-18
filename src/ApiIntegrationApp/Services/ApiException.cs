namespace ApiIntegrationApp.Services;

/// <summary>
/// Єдиний тип помилки, яку ApiService віддає назовні.
/// Оригінальний виняток зберігається в InnerException.
/// </summary>
public sealed class ApiException : Exception
{
    public ApiException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
