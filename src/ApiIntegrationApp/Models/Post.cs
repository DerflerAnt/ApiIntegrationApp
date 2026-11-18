using System.Text.Json.Serialization;

namespace ApiIntegrationApp.Models;

/// <summary>
/// Пост з JSONPlaceholder: GET /posts?userId={id}.
/// </summary>
public sealed class Post
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("userId")]
    public int UserId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("body")]
    public string Body { get; set; } = string.Empty;
}
