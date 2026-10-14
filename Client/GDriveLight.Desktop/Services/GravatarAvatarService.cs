using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace GDriveLight.Desktop.Services;

public sealed class GravatarAvatarService(HttpClient httpClient) : IAvatarService, IDisposable
{
    private const long MaximumImageBytes = 2 * 1024 * 1024;
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    public async Task<ImageSource?> GetAvatarAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant())));
        if (_cache.TryGetValue(hash, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return cached.Image;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        var requestToken = timeout.Token;
        ImageSource? image = null;
        try
        {
            // Use a separate HTTP client: Drive bearer tokens must never reach Gravatar.
            using var response = await httpClient.GetAsync(
                $"https://www.gravatar.com/avatar/{hash}?s=128&d=404&r=g",
                HttpCompletionOption.ResponseHeadersRead, requestToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                await response.Content.LoadIntoBufferAsync(MaximumImageBytes, requestToken).ConfigureAwait(false);
                await using var stream = await response.Content.ReadAsStreamAsync(requestToken).ConfigureAwait(false);
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                bitmap.DecodePixelWidth = 128;
                bitmap.StreamSource = stream;
                bitmap.EndInit();
                bitmap.Freeze();
                image = bitmap;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // A timeout leaves the local initial visible.
        }
        catch (Exception exception) when (exception is HttpRequestException or IOException or
            FileFormatException or NotSupportedException or ArgumentException or InvalidOperationException)
        {
            // Offline, missing, or invalid images must not interrupt the user's session.
        }

        cancellationToken.ThrowIfCancellationRequested();
        _cache[hash] = new CacheEntry(image, DateTimeOffset.UtcNow.Add(
            image is null ? TimeSpan.FromMinutes(5) : TimeSpan.FromHours(24)));
        return image;
    }

    public void Dispose() => httpClient.Dispose();

    private sealed record CacheEntry(ImageSource? Image, DateTimeOffset ExpiresAt);
}
