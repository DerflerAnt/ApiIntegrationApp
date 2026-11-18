namespace ApiIntegrationApp.Services;

/// <summary>
/// Підсумок збереження користувача та його постів.
/// </summary>
public sealed record SaveResult(
    bool UserInserted,
    bool UserUpdated,
    int PostsInserted,
    int PostsSkipped);
