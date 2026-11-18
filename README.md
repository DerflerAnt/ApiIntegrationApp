# ApiIntegrationApp

C# Console Application (.NET 8): бере дані з публічного API
[JSONPlaceholder](https://jsonplaceholder.typicode.com), виводить їх у консоль,
зберігає в SQL Server і показує збережені дані через `JOIN`.

## Технології
- .NET 8, C#
- `HttpClient` — HTTP-запити
- `System.Text.Json` — десеріалізація JSON
- `Microsoft.Data.SqlClient` — робота з SQL Server
- `Microsoft.Extensions.Configuration` — appsettings / User Secrets / змінні середовища

## API
| Endpoint | Опис |
|----------|------|
| `GET /users/{id}` | Дані користувача |
| `GET /posts?userId={id}` | Пости користувача (query-параметр змінює відповідь) |

## База даних
- Сервер: `DESKTOP-9G9V9M7`
- База: `FullStack`
- Таблиці: `Users` (1) → `Posts` (багато), зв'язок через Foreign Key

## Структура проєкту
```
FullStack/
├── ApiIntegrationApp.sln
├── README.md
└── src/
    └── ApiIntegrationApp/
        ├── ApiIntegrationApp.csproj
        └── Program.cs
```

## Запуск
```powershell
dotnet build
dotnet run --project src/ApiIntegrationApp
```

## Стан розробки
- [x] Етап 1 — Ініціалізація проєкту
- [ ] Етап 2 — Конфігурація
- [ ] Етап 3 — API-клієнт
- [ ] Етап 4 — Збереження в SQL Server
- [ ] Етап 5 — Виведення даних через JOIN
