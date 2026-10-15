using System.Net;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using GDriveLight.Desktop.Models.Api;

namespace GDriveLight.Desktop.Services.Api;

public sealed class DriveApiService : IDriveApiService
{
    private readonly HttpClient _httpClient;
    private readonly IDeviceIdentifierService _deviceIdentifierService;
    private readonly object _sessionLock = new();
    private Uri _serverUri = new("https://localhost:7220/");
    private string? _authToken;

    public DriveApiService(HttpClient httpClient, IDeviceIdentifierService? deviceIdentifierService = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
        _deviceIdentifierService = deviceIdentifierService ?? new DeviceIdentifierService();
    }

    public Guid DeviceId => _deviceIdentifierService.GetDeviceId();
    public Guid? CurrentUserId => CurrentUser?.Id;
    public UserProfile? CurrentUser { get; private set; }

    public void SetAuthToken(string? token)
    {
        lock (_sessionLock)
        {
            _authToken = string.IsNullOrWhiteSpace(token) ? null : token;
            CurrentUser = _authToken is not null ? ExtractUserFromJwt(_authToken) : null;
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

    public Task<IEnumerable<FileTypeResponse>> GetFileTypesAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<FileTypeResponse>("api/files/types", cancellationToken);

    public async Task<StorageUsageResponse> GetStorageUsageAsync(CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, "api/storage/usage");
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<StorageUsageResponse>(cancellationToken).ConfigureAwait(false)
            ?? throw new JsonException("The server returned invalid storage usage.");
    }

    public Task<IEnumerable<FolderResponse>> GetFolderPathAsync(Guid id, CancellationToken cancellationToken = default) =>
        GetListAsync<FolderResponse>($"api/folders/{id}/path", cancellationToken);

    public Task<IEnumerable<FolderResponse>> SearchFoldersAsync(string name, CancellationToken cancellationToken = default) =>
        GetListAsync<FolderResponse>($"api/folders/search?name={Uri.EscapeDataString(name)}", cancellationToken);

    public Task<IEnumerable<FileResponse>> SearchFilesAsync(string name, CancellationToken cancellationToken = default) =>
        GetListAsync<FileResponse>($"api/files/search?name={Uri.EscapeDataString(name)}", cancellationToken);

    private async Task<IEnumerable<T>> GetListAsync<T>(string path, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Get, path);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<T[]>(cancellationToken).ConfigureAwait(false)
            ?? throw new JsonException("The server returned an invalid item list.");
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string path, bool authenticated = true, bool deviceOrigin = false)
    {
        lock (_sessionLock)
        {
            var request = new HttpRequestMessage(method, new Uri(_serverUri, path));
            if (deviceOrigin)
            {
                request.Headers.Add("X-Device-Id", DeviceId.ToString());
            }

            if (authenticated && _authToken is not null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authToken);
            }

            return request;
        }
    }

    public Task<Guid> CreateFolderAsync(string name, Guid? parentFolderId, CancellationToken cancellationToken = default)
        => CreateFolderAsync(name, parentFolderId, false, cancellationToken);

    public async Task<Guid> CreateFolderAsync(string name, Guid? parentFolderId, bool deviceOrigin, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "api/folders", deviceOrigin: deviceOrigin);
        request.Content = JsonContent.Create(new { Name = name, ParentFolderId = parentFolderId });
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        var result = await response.Content.ReadFromJsonAsync<CreatedFolderResponse>(cancellationToken).ConfigureAwait(false);
        return result is { FolderId: var id } && id != Guid.Empty
            ? id : throw new JsonException("The server returned an invalid folder ID.");
    }

    public Task<Guid> UploadFileAsync(string filePath, Guid? folderId, CancellationToken cancellationToken = default)
        => UploadFileAsync(filePath, folderId, false, cancellationToken);

    public async Task<Guid> UploadFileAsync(string filePath, Guid? folderId, bool deviceOrigin, CancellationToken cancellationToken = default)
    {
        await using var file = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            65536, FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var request = CreateRequest(HttpMethod.Post, "api/files", deviceOrigin: deviceOrigin);
        var content = new MultipartFormDataContent();
        request.Content = content;
        content.Add(new StreamContent(file), "file", Path.GetFileName(filePath));
        if (folderId.HasValue)
        {
            content.Add(new StringContent(folderId.Value.ToString()), "folderId");
        }

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        var result = await response.Content.ReadFromJsonAsync<CreatedFileResponse>(cancellationToken).ConfigureAwait(false);
        return result is { Id: var id } && id != Guid.Empty
            ? id : throw new JsonException("The server returned an invalid file ID.");
    }

    public async Task DownloadFileAsync(Guid fileId, string destination, CancellationToken cancellationToken = default)
    {
        var targetPath = Path.GetFullPath(destination);
        var temporaryPath = Path.Combine(Path.GetDirectoryName(targetPath)!, $".gdrivelight-{Guid.NewGuid():N}.tmp");
        try
        {
            using var request = CreateRequest(HttpMethod.Get, $"api/files/{fileId}/download");
            using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
            await using (var output = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                65536, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                await response.Content.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, targetPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public async Task RenameAsync(Guid id, bool isFolder, string name, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"api/{(isFolder ? "folders" : "files")}/{id}/rename");
        request.Content = JsonContent.Create(new { NewName = name });
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public Task DeleteAsync(Guid id, bool isFolder, CancellationToken cancellationToken = default)
        => DeleteAsync(id, isFolder, false, cancellationToken);

    public async Task DeleteAsync(Guid id, bool isFolder, bool deviceOrigin, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"api/{(isFolder ? "folders" : "files")}/{id}", deviceOrigin: deviceOrigin);
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public async Task MoveAsync(Guid id, bool isFolder, Guid? destinationFolderId, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"api/{(isFolder ? "folders" : "files")}/{id}/move");
        request.Content = isFolder
            ? JsonContent.Create(new { NewParentFolderId = destinationFolderId })
            : JsonContent.Create(new { NewFolderId = destinationFolderId });
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public async Task<SyncChangesResponse> GetChangesAsync(long cursor, int limit = 100, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Get, $"api/sync/changes?cursor={cursor}&limit={limit}");
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<SyncChangesResponse>(JsonOptions, cancellationToken).ConfigureAwait(false)
            ?? throw new JsonException("The server returned an invalid sync changes response.");
    }

    public async Task UpdateFileContentAsync(Guid fileId, Stream stream, string fileName, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"api/files/{fileId}/content", deviceOrigin: true);
        var content = new MultipartFormDataContent();
        request.Content = content;
        content.Add(new StreamContent(stream), "file", fileName);

        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    // Claims are used for display only; the API validates authentication and authorization.
    private static UserProfile? ExtractUserFromJwt(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3) return null;

            var payload = parts[1].Replace('-', '+').Replace('_', '/');
            payload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
            using var document = JsonDocument.Parse(Convert.FromBase64String(payload));
            var claims = document.RootElement;
            if (claims.ValueKind != JsonValueKind.Object) return null;

            var id = ReadStringClaim(claims, "sub");
            if (!Guid.TryParse(id, out var userId) &&
                !Guid.TryParse(ReadStringClaim(claims, "nameid"), out userId))
            {
                return null;
            }

            return new UserProfile(userId, ReadStringClaim(claims, "firstName"), ReadStringClaim(claims, "email"));
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            return null;
        }
    }

    private static string ReadStringClaim(JsonElement claims, string name) =>
        claims.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty : string.Empty;

    public async Task<Guid> RegisterDeviceAsync(Guid deviceId, string name, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Post, "api/devices");
        request.Content = JsonContent.Create(new { DeviceId = deviceId, Name = name });
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        var result = await response.Content.ReadFromJsonAsync<RegisterDeviceResponse>(cancellationToken).ConfigureAwait(false);
        return result is { DeviceId: var id } && id != Guid.Empty
            ? id : throw new JsonException("The server returned an invalid device ID.");
    }

    public Task<IEnumerable<DeviceResponse>> GetDevicesAsync(CancellationToken cancellationToken = default) =>
        GetListAsync<DeviceResponse>("api/devices", cancellationToken);

    public async Task<DeviceResponse> ConfigureSyncFolderAsync(string localPath, CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Put, $"api/devices/{DeviceId}/sync-folder");
        request.Content = JsonContent.Create(new { LocalPath = localPath });
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadFromJsonAsync<DeviceResponse>(cancellationToken).ConfigureAwait(false)
            ?? throw new JsonException("The server returned an invalid device configuration.");
    }

    public async Task DisconnectDeviceAsync(CancellationToken cancellationToken = default)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"api/devices/{DeviceId}");
        using var response = await SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    public Task<Guid> EnsureDeviceRegisteredAsync(CancellationToken cancellationToken = default) =>
        RegisterDeviceAsync(DeviceId, Environment.MachineName, cancellationToken);

    private sealed record CreatedFolderResponse(Guid FolderId);
    private sealed record CreatedFileResponse(Guid Id);
    private sealed record RegisterDeviceResponse(Guid DeviceId);

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
