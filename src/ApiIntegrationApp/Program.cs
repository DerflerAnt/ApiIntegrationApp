using System.Text;
using ApiIntegrationApp.Configuration;
using ApiIntegrationApp.Models;
using ApiIntegrationApp.Services;
using Microsoft.Data.SqlClient;

Console.OutputEncoding = Encoding.UTF8;

// ---------- 1. Конфігурація ----------
AppSettings settings;
try
{
    settings = AppSettings.Load();
}
catch (Exception ex) when (ex is InvalidOperationException or FileNotFoundException or FormatException)
{
    WriteError(ex.Message);
    return 1;
}

// userId можна передати аргументом: dotnet run --project src/ApiIntegrationApp -- 3
int userId = settings.Api.DefaultUserId;
if (args.Length > 0)
{
    if (!int.TryParse(args[0], out userId) || userId <= 0)
    {
        WriteError($"Некоректний userId '{args[0]}'. Очікується додатне ціле число.");
        return 1;
    }
}

var csb = new SqlConnectionStringBuilder(settings.ConnectionString);
Console.WriteLine($"API: {settings.Api.BaseUrl} | DB: {csb.DataSource}/{csb.InitialCatalog} | userId={userId}");
Console.WriteLine();

// ---------- 2. Запит до API ----------
User user;
IReadOnlyList<Post> posts;

using (var httpClient = ApiService.CreateHttpClient(settings.Api))
{
    var apiService = new ApiService(httpClient);
    try
    {
        user = await apiService.GetUserAsync(userId);
        posts = await apiService.GetPostsByUserAsync(userId);
    }
    catch (ApiException ex)
    {
        WriteError(ex.Message);
        return 2;
    }
}

// ---------- 3. Виведення результату ----------
PrintUser(user);
PrintPosts(posts);

return 0;

// ---------- Допоміжні методи ----------
static void PrintUser(User user)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"=== Користувач (GET /users/{user.Id}) ===");
    Console.ResetColor();
    Console.WriteLine($"Id       : {user.Id}");
    Console.WriteLine($"Ім'я     : {user.Name}");
    Console.WriteLine($"Username : {user.Username}");
    Console.WriteLine($"Email    : {user.Email}");
    Console.WriteLine($"Телефон  : {user.Phone}");
    Console.WriteLine($"Сайт     : {user.Website}");
    Console.WriteLine($"Місто    : {user.Address?.City}");
    Console.WriteLine($"Компанія : {user.Company?.Name}");
    Console.WriteLine();
}

static void PrintPosts(IReadOnlyList<Post> posts)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"=== Пости (GET /posts?userId={posts[0].UserId}) — {posts.Count} шт. ===");
    Console.ResetColor();
    foreach (var post in posts)
        Console.WriteLine($"[{post.Id,3}] {post.Title}");
    Console.WriteLine();
}

static void WriteError(string message)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"Помилка: {message}");
    Console.ResetColor();
}
