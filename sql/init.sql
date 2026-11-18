/*
    init.sql — схема бази даних FullStack для ApiIntegrationApp.

    Скрипт ідемпотентний: його можна запускати скільки завгодно разів.
    Застосунок виконує його автоматично при старті (DatabaseService.EnsureSchemaAsync),
    тому тут немає "USE" і "GO" — база береться з рядка підключення.

    Ручний запуск:
        sqlcmd -S localhost -d FullStack -E -C -i sql/init.sql

    Зв'язок: Users (1) ──< Posts (багато), Posts.UserId → Users.Id
*/

SET NOCOUNT ON;

-- ============================================================
-- Таблиця Users (сторона "1")
-- Id береться з API (JSONPlaceholder), тому без IDENTITY.
-- ============================================================
IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Users
    (
        Id          INT             NOT NULL,
        Name        NVARCHAR(100)   NOT NULL,
        Username    NVARCHAR(50)    NOT NULL,
        Email       NVARCHAR(100)   NOT NULL,
        Phone       NVARCHAR(50)    NULL,
        Website     NVARCHAR(100)   NULL,
        City        NVARCHAR(100)   NULL,
        CompanyName NVARCHAR(100)   NULL,
        CreatedAt   DATETIME2(0)    NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME(),
        UpdatedAt   DATETIME2(0)    NULL,

        CONSTRAINT PK_Users PRIMARY KEY CLUSTERED (Id)
    );
END;

-- ============================================================
-- Таблиця Posts (сторона "багато")
-- ============================================================
IF OBJECT_ID(N'dbo.Posts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Posts
    (
        Id          INT             NOT NULL,
        UserId      INT             NOT NULL,
        Title       NVARCHAR(300)   NOT NULL,
        Body        NVARCHAR(MAX)   NOT NULL,
        CreatedAt   DATETIME2(0)    NOT NULL CONSTRAINT DF_Posts_CreatedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_Posts PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Posts_Users FOREIGN KEY (UserId)
            REFERENCES dbo.Users (Id)
            ON DELETE CASCADE
    );
END;

-- Індекс на зовнішньому ключі пришвидшує JOIN Users ↔ Posts.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Posts_UserId' AND object_id = OBJECT_ID(N'dbo.Posts'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Posts_UserId ON dbo.Posts (UserId);
END;
