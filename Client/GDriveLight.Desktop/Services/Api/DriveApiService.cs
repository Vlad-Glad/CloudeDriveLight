using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GDriveLight.Desktop.Models.Api;

namespace GDriveLight.Desktop.Services.Api;

public sealed class DriveApiService : IDriveApiService
{
    private readonly HttpClient _httpClient;
    private readonly object _sessionLock = new();
    private Uri _serverUri = new("https://localhost:7220/");
    private string? _authToken;

    public DriveApiService(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    public void SetServerUrl(string url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new ArgumentException("Enter an absolute HTTP or HTTPS server address without credentials, query or fragment.", nameof(url));
        }

        var normalized = new Uri(uri.AbsoluteUri.TrimEnd('/') + "/");
        lock (_sessionLock)
        {
            if (_serverUri != normalized)
            {
                _authToken = null;
                _serverUri = normalized;
            }
        }
    }

    public string GetServerUrl()
    {
        lock (_sessionLock)
        {
            return _serverUri.AbsoluteUri.TrimEnd('/');
        }
    }

    public void SetAuthToken(string? token)
    {
        lock (_sessionLock)
        {
            _authToken = string.IsNullOrWhiteSpace(token) ? null : token;
        }
    }

    public void Logout() => SetAuthToken(null);

    public Task<string?> LoginAsync(string email, string password, CancellationToken cancellationToken = default) =>
        AuthenticateAsync("api/auth/login", new LoginRequest(email, password), cancellationToken);

    public Task<string?> RegisterAsync(string email, string password, string firstName, CancellationToken cancellationToken = default) =>
        AuthenticateAsync("api/auth/register", new RegisterRequest(email, password, firstName), cancellationToken);

    private async Task<string?> AuthenticateAsync<T>(string path, T body, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Post, path, authenticated: false);
        request.Content = JsonContent.Create(body);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(result?.Token))
        {
            throw new JsonException("The server returned an empty authentication token.");
        }

        return result.Token;
    }

    public Task<IEnumerable<FolderResponse>> GetFolderContentAsync(Guid? parentFolderId, CancellationToken cancellationToken = default) =>
        GetListAsync<FolderResponse>(parentFolderId.HasValue
            ? $"api/folders?parentFolderId={parentFolderId.Value}"
            : "api/folders", cancellationToken);

    public Task<IEnumerable<FileResponse>> GetFilesAsync(Guid? folderId, CancellationToken cancellationToken = default) =>
        GetListAsync<FileResponse>(folderId.HasValue ? $"api/files?folderId={folderId.Value}" : "api/files", cancellationToken);

    private async Task<IEnumerable<T>> GetListAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, path);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<T[]>(cancellationToken).ConfigureAwait(false)
            ?? throw new JsonException("The server returned an invalid item list.");
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, bool authenticated = true)
    {
        lock (_sessionLock)
        {
            var request = new HttpRequestMessage(method, new Uri(_serverUri, path));
            if (authenticated && _authToken is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            }

            return request;
        }
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        try
        {
            var message = await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
            throw new DriveApiException(response.StatusCode, message);
        }
        finally
        {
            response.Dispose();
        }
    }

    private static async Task<string> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return "Your session has expired. Please sign in again.";
        }

        if ((int)response.StatusCode >= 500)
        {
            return "The server could not complete the request. Please try again.";
        }

        var fallback = $"The request failed (HTTP {(int)response.StatusCode}).";
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false),
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return fallback;
            }

            var messages = new List<string>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if ((property.Name.Equals("validationErrors", StringComparison.OrdinalIgnoreCase) ||
                     property.Name.Equals("errors", StringComparison.OrdinalIgnoreCase)) &&
                    property.Value.ValueKind == JsonValueKind.Object)
                {
                    foreach (var field in property.Value.EnumerateObject())
                    {
                        if (field.Value.ValueKind == JsonValueKind.Array)
                        {
                            messages.AddRange(field.Value.EnumerateArray()
                                .Where(value => value.ValueKind == JsonValueKind.String)
                                .Select(value => value.GetString()!));
                        }
                    }
                }
            }

            if (messages.Count > 0)
            {
                return string.Join(Environment.NewLine, messages);
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                if ((property.Name.Equals("error", StringComparison.OrdinalIgnoreCase) ||
                     property.Name.Equals("title", StringComparison.OrdinalIgnoreCase)) &&
                    property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString() ?? fallback;
                }
            }
        }
        catch (JsonException)
        {
        }

        return fallback;
    }
}
