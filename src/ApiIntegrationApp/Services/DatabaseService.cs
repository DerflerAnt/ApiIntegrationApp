using System.Data;
using ApiIntegrationApp.Models;
using Microsoft.Data.SqlClient;

namespace ApiIntegrationApp.Services;

/// <summary>
/// Робота з SQL Server через Microsoft.Data.SqlClient.
/// Усі запити з даними параметризовані (@Id, @Name, ...), конкатенації рядків SQL немає.
/// </summary>
public sealed class DatabaseService
{
    private const string SchemaScriptRelativePath = "sql/init.sql";

    private readonly string _connectionString;

    public DatabaseService(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Рядок підключення не може бути порожнім.", nameof(connectionString));

        _connectionString = connectionString;
    }

    /// <summary>
    /// Створює таблиці Users/Posts, якщо їх ще немає (виконує ідемпотентний sql/init.sql).
    /// </summary>
    public async Task EnsureSchemaAsync(CancellationToken cancellationToken = default)
    {
        var scriptPath = Path.Combine(AppContext.BaseDirectory, SchemaScriptRelativePath);
        if (!File.Exists(scriptPath))
            throw new FileNotFoundException("Не знайдено SQL-скрипт ініціалізації схеми.", scriptPath);

        var script = await File.ReadAllTextAsync(scriptPath, cancellationToken);

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(script, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Зберігає користувача та його пости в одній транзакції без створення дублікатів:
    ///  - Users: IF NOT EXISTS → INSERT, інакше UPDATE лише якщо дані змінилися;
    ///  - Posts: IF NOT EXISTS → INSERT, інакше пропуск.
    /// </summary>
    public async Task<SaveResult> SaveUserWithPostsAsync(
        User user, IReadOnlyCollection<Post> posts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(posts);

        var foreignPost = posts.FirstOrDefault(p => p.UserId != user.Id);
        if (foreignPost is not null)
            throw new ArgumentException(
                $"Пост {foreignPost.Id} належить userId={foreignPost.UserId}, а не {user.Id}.", nameof(posts));

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var userAction = await UpsertUserAsync(connection, transaction, user, cancellationToken);

            int inserted = 0, skipped = 0;
            foreach (var post in posts)
            {
                if (await InsertPostIfNotExistsAsync(connection, transaction, post, cancellationToken))
                    inserted++;
                else
                    skipped++;
            }

            await transaction.CommitAsync(cancellationToken);

            return new SaveResult(
                UserInserted: userAction == UserAction.Inserted,
                UserUpdated: userAction == UserAction.Updated,
                PostsInserted: inserted,
                PostsSkipped: skipped);
        }
        catch
        {
            // Жодних "напівзбережених" даних: або все, або нічого.
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private enum UserAction { Unchanged = 0, Inserted = 1, Updated = 2 }

    private static async Task<UserAction> UpsertUserAsync(
        SqlConnection connection, SqlTransaction transaction, User user, CancellationToken cancellationToken)
    {
        // UPDLOCK + HOLDLOCK захищають від гонки, якщо дві копії програми працюють одночасно.
        // EXCEPT коректно порівнює NULL-значення — UPDATE лише за реальних змін.
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.Users WITH (UPDLOCK, HOLDLOCK) WHERE Id = @Id)
            BEGIN
                INSERT INTO dbo.Users (Id, Name, Username, Email, Phone, Website, City, CompanyName)
                VALUES (@Id, @Name, @Username, @Email, @Phone, @Website, @City, @CompanyName);
                SELECT 1;
            END
            ELSE IF EXISTS (
                SELECT @Name, @Username, @Email, @Phone, @Website, @City, @CompanyName
                EXCEPT
                SELECT Name, Username, Email, Phone, Website, City, CompanyName
                FROM dbo.Users WHERE Id = @Id)
            BEGIN
                UPDATE dbo.Users
                SET Name        = @Name,
                    Username    = @Username,
                    Email       = @Email,
                    Phone       = @Phone,
                    Website     = @Website,
                    City        = @City,
                    CompanyName = @CompanyName,
                    UpdatedAt   = SYSUTCDATETIME()
                WHERE Id = @Id;
                SELECT 2;
            END
            ELSE
                SELECT 0;
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = user.Id;
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 100).Value = user.Name;
        command.Parameters.Add("@Username", SqlDbType.NVarChar, 50).Value = user.Username;
        command.Parameters.Add("@Email", SqlDbType.NVarChar, 100).Value = user.Email;
        command.Parameters.Add("@Phone", SqlDbType.NVarChar, 50).Value = DbValue(user.Phone);
        command.Parameters.Add("@Website", SqlDbType.NVarChar, 100).Value = DbValue(user.Website);
        command.Parameters.Add("@City", SqlDbType.NVarChar, 100).Value = DbValue(user.Address?.City);
        command.Parameters.Add("@CompanyName", SqlDbType.NVarChar, 100).Value = DbValue(user.Company?.Name);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return (UserAction)Convert.ToInt32(result);
    }

    private static async Task<bool> InsertPostIfNotExistsAsync(
        SqlConnection connection, SqlTransaction transaction, Post post, CancellationToken cancellationToken)
    {
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.Posts WITH (UPDLOCK, HOLDLOCK) WHERE Id = @Id)
            BEGIN
                INSERT INTO dbo.Posts (Id, UserId, Title, Body)
                VALUES (@Id, @UserId, @Title, @Body);
                SELECT 1;
            END
            ELSE
                SELECT 0;
            """;

        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.Add("@Id", SqlDbType.Int).Value = post.Id;
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = post.UserId;
        command.Parameters.Add("@Title", SqlDbType.NVarChar, 300).Value = post.Title;
        command.Parameters.Add("@Body", SqlDbType.NVarChar, -1).Value = post.Body; // -1 = NVARCHAR(MAX)

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(result) == 1;
    }

    private static object DbValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;

    /// <summary>
    /// Читає збережені дані через параметризований INNER JOIN між Users та Posts.
    /// Якщо userId == null, повертає пости всіх користувачів у БД.
    /// </summary>
    public async Task<IReadOnlyList<UserPostRow>> GetUserPostsJoinedAsync(
        int? userId = null, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                u.Id          AS UserId,
                u.Name        AS UserName,
                u.Username    AS Username,
                u.Email       AS Email,
                u.City        AS City,
                u.CompanyName AS CompanyName,
                p.Id          AS PostId,
                p.Title       AS PostTitle,
                p.Body        AS PostBody,
                p.CreatedAt   AS CreatedAt
            FROM dbo.Users u
            INNER JOIN dbo.Posts p ON p.UserId = u.Id
            WHERE (@UserId IS NULL OR u.Id = @UserId)
            ORDER BY u.Id, p.Id;
            """;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add("@UserId", SqlDbType.Int).Value = userId.HasValue ? userId.Value : DBNull.Value;

        var rows = new List<UserPostRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new UserPostRow(
                UserId: reader.GetInt32(0),
                UserName: reader.GetString(1),
                Username: reader.GetString(2),
                Email: reader.GetString(3),
                City: reader.IsDBNull(4) ? null : reader.GetString(4),
                CompanyName: reader.IsDBNull(5) ? null : reader.GetString(5),
                PostId: reader.GetInt32(6),
                PostTitle: reader.GetString(7),
                PostBody: reader.GetString(8),
                CreatedAt: reader.GetDateTime(9)));
        }

        return rows;
    }

    /// <summary>
    /// Людиночитний опис типових помилок SQL Server.
    /// </summary>
    public static string DescribeSqlError(SqlException ex) => ex.Number switch
    {
        -2 => "Перевищено час очікування відповіді від SQL Server.",
        2 or 53 or -1 => "SQL Server недоступний. Перевірте назву сервера та чи запущена служба SQL Server.",
        4060 => "Не вдалося відкрити базу даних. Перевірте назву бази в рядку підключення та чи вона існує.",
        18456 => "Помилка автентифікації. Перевірте облікові дані в рядку підключення.",
        547 => "Порушення обмеження (наприклад, зовнішнього ключа FK_Posts_Users).",
        2627 or 2601 => "Спроба вставити дублікат первинного ключа.",
        _ => "Помилка бази даних."
    } + $" [SQL {ex.Number}] {ex.Message}";
}
