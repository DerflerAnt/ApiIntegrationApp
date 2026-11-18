namespace ApiIntegrationApp.Models;

/// <summary>
/// Рядок об'єднаного результату INNER JOIN таблиць Users та Posts.
/// </summary>
public sealed record UserPostRow(
    int UserId,
    string UserName,
    string Username,
    string Email,
    string? City,
    string? CompanyName,
    int PostId,
    string PostTitle,
    string PostBody,
    DateTime CreatedAt)
{
    /// <summary>
    /// Однорядкове представлення тексту поста (без переносів рядків) для швидкого відображення у таблиці DataGrid.
    /// </summary>
    public string SingleLineBody =>
        string.IsNullOrEmpty(PostBody)
            ? string.Empty
            : PostBody.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ');
}
