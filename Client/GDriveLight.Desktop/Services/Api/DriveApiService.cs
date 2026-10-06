using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using GDriveLight.Desktop.Models.Api;

namespace GDriveLight.Desktop.Services.Api;

public class DriveApiService : IDriveApiService
{
    private readonly HttpClient _httpClient;
    private string _baseUrl = "https://localhost:7220";

    public DriveApiService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public void SetServerUrl(string url)
    {
        _baseUrl = url.TrimEnd('/');
    }

    public string GetServerUrl() => _baseUrl;

    public void SetAuthToken(string? token)
    {
        if (string.IsNullOrEmpty(token))
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }

    public void Logout()
    {
        SetAuthToken(null);
    }

    public async Task<string?> LoginAsync(string email, string password)
    {
        var request = new LoginRequest(email, password);
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/auth/login", request);
        
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            return result?.Token;
        }
        return null;
    }

    public async Task<string?> RegisterAsync(string email, string password, string firstName)
    {
        var request = new { Email = email, Password = password, FirstName = firstName };
        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/api/auth/register", request);
        
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            return result?.Token;
        }
        return null;
    }

    public async Task<IEnumerable<FolderResponse>> GetFolderContentAsync(Guid? parentFolderId)
    {
        var url = $"{_baseUrl}/api/folders";
        if (parentFolderId.HasValue)
        {
            url += $"?parentFolderId={parentFolderId.Value}";
        }

        var response = await _httpClient.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var folders = await response.Content.ReadFromJsonAsync<IEnumerable<FolderResponse>>();
            return folders ?? Array.Empty<FolderResponse>();
        }
        return Array.Empty<FolderResponse>();
    }

    public async Task<IEnumerable<FileResponse>> GetFilesAsync(Guid? folderId)
    {
        var url = $"{_baseUrl}/api/files";
        if (folderId.HasValue)
        {
            url += $"?folderId={folderId.Value}";
        }

        var response = await _httpClient.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            var files = await response.Content.ReadFromJsonAsync<IEnumerable<FileResponse>>();
            return files ?? Array.Empty<FileResponse>();
        }
        return Array.Empty<FileResponse>();
    }
}
