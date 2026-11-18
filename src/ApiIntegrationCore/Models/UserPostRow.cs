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
    DateTime CreatedAt);
