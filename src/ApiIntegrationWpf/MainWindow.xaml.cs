using System.IO;
using System.Windows;
using System.Windows.Media;
using ApiIntegrationApp.Configuration;
using ApiIntegrationApp.Services;
using Microsoft.Data.SqlClient;

namespace ApiIntegrationWpf;

public partial class MainWindow : Window
{
    private AppSettings? _settings;
    private DatabaseService? _databaseService;

    public MainWindow()
    {
        InitializeComponent();
        Loaded += MainWindow_Loaded;
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _settings = AppSettings.Load();
            _databaseService = new DatabaseService(_settings.ConnectionString);

            UserIdTextBox.Text = _settings.Api.DefaultUserId.ToString();

            var csb = new SqlConnectionStringBuilder(_settings.ConnectionString);
            ConnectionInfoText.Text =
                $"API: {_settings.Api.BaseUrl}   |   SQL Server: {csb.DataSource}   |   База: {csb.InitialCatalog}";

            await _databaseService.EnsureSchemaAsync();
            await LoadJoinedDataFromDatabaseAsync(showSuccessStatus: false);
        }
        catch (Exception ex)
        {
            SetStatus($"Помилка ініціалізації: {ex.Message}", isError: true);
            MessageBox.Show(this, ex.Message, "Помилка конфігурації або БД",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void FetchAndSaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (_settings is null || _databaseService is null)
        {
            SetStatus("Конфігурацію не завантажено. Перевірте User Secrets.", isError: true);
            return;
        }

        if (!TryParseUserId(out int userId))
            return;

        SetButtonsEnabled(false);
        SetStatus($"Запит до API для userId={userId} (GET /users/{userId} та GET /posts?userId={userId})...", isError: false);

        try
        {
            using var httpClient = ApiService.CreateHttpClient(_settings.Api);
            var apiService = new ApiService(httpClient);

            var user = await apiService.GetUserAsync(userId);
            var posts = await apiService.GetPostsByUserAsync(userId);

            await _databaseService.EnsureSchemaAsync();
            var saveResult = await _databaseService.SaveUserWithPostsAsync(user, posts);

            var userAction = saveResult.UserInserted ? "додано нового користувача"
                           : saveResult.UserUpdated ? "оновлено дані користувача"
                           : "користувач вже існує без змін";

            await LoadJoinedDataFromDatabaseAsync(showSuccessStatus: false);

            SetStatus(
                $"Успішно! Користувач #{user.Id} ({user.Name}, @{user.Username}): {userAction}. " +
                $"Пости: додано {saveResult.PostsInserted}, пропущено як дублікати {saveResult.PostsSkipped}.",
                isError: false, isSuccess: true);
        }
        catch (ApiException ex)
        {
            SetStatus($"Помилка API: {ex.Message}", isError: true);
            MessageBox.Show(this, ex.Message, "Помилка запиту до API",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (SqlException ex)
        {
            var msg = DatabaseService.DescribeSqlError(ex);
            SetStatus(msg, isError: true);
            MessageBox.Show(this, msg, "Помилка SQL Server",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (FileNotFoundException ex)
        {
            var msg = $"{ex.Message} ({ex.FileName})";
            SetStatus(msg, isError: true);
            MessageBox.Show(this, msg, "Файл не знайдено",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            SetStatus($"Непередбачена помилка: {ex.Message}", isError: true);
            MessageBox.Show(this, ex.Message, "Помилка",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private async void RefreshFromDbButton_Click(object sender, RoutedEventArgs e)
    {
        await LoadJoinedDataFromDatabaseAsync(showSuccessStatus: true);
    }

    private async void FilterByUserIdCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || _databaseService is null)
            return;

        await LoadJoinedDataFromDatabaseAsync(showSuccessStatus: true);
    }

    private async Task LoadJoinedDataFromDatabaseAsync(bool showSuccessStatus)
    {
        if (_databaseService is null)
            return;

        int? filterUserId = null;
        if (FilterByUserIdCheckBox.IsChecked == true)
        {
            if (!TryParseUserId(out int parsedId))
                return;
            filterUserId = parsedId;
        }

        SetButtonsEnabled(false);
        try
        {
            var rows = await _databaseService.GetUserPostsJoinedAsync(filterUserId);
            PostsDataGrid.ItemsSource = rows;
            RowCountBadge.Text = $"Рядків у таблиці: {rows.Count}";

            if (showSuccessStatus)
            {
                var scope = filterUserId.HasValue ? $"для userId={filterUserId.Value}" : "для всіх користувачів";
                SetStatus($"Дані оновлено з БД через INNER JOIN ({scope}): знайдено {rows.Count} записів.",
                    isError: false, isSuccess: true);
            }
        }
        catch (SqlException ex)
        {
            var msg = DatabaseService.DescribeSqlError(ex);
            SetStatus(msg, isError: true);
            MessageBox.Show(this, msg, "Помилка читання з SQL Server",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private bool TryParseUserId(out int userId)
    {
        var raw = UserIdTextBox.Text?.Trim() ?? string.Empty;
        if (!int.TryParse(raw, out userId) || userId <= 0)
        {
            var msg = $"Некоректний User ID '{raw}'. Введіть додатне ціле число (наприклад, від 1 до 10).";
            SetStatus(msg, isError: true);
            MessageBox.Show(this, msg, "Некоректний ввід",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        return true;
    }

    private void SetButtonsEnabled(bool isEnabled)
    {
        FetchAndSaveButton.IsEnabled = isEnabled;
        RefreshFromDbButton.IsEnabled = isEnabled;
        FilterByUserIdCheckBox.IsEnabled = isEnabled;
    }

    private void SetStatus(string message, bool isError, bool isSuccess = false)
    {
        StatusTextBlock.Text = message;

        if (isError)
        {
            StatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF2F2"));
            StatusBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FECACA"));
            StatusTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B91C1C"));
        }
        else if (isSuccess)
        {
            StatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0FDF4"));
            StatusBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BBF7D0"));
            StatusTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15803D"));
        }
        else
        {
            StatusBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#EFF6FF"));
            StatusBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#BFDBFE"));
            StatusTextBlock.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E40AF"));
        }
    }
}
