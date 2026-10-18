using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace GDriveLight.Infrastructure.Storage;

internal static class StorageRegistration
{
    public static void AddFileStorage(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration["Storage:Provider"] ?? "Local";
        if (string.Equals(provider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            services.AddOptions<LocalFileStorageOptions>()
                .Bind(configuration.GetSection(LocalFileStorageOptions.SectionName))
                .Validate(options => !string.IsNullOrWhiteSpace(options.Path), "Storage:Path must not be empty.")
                .ValidateOnStart();
            services.AddSingleton<IFileStorageService, LocalFileStorageService>();
            return;
        }

        if (!string.Equals(provider, "S3", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"Storage provider '{provider}' is not supported. Supported providers are 'Local' and 'S3'.");

        services.AddOptions<S3StorageOptions>()
            .Bind(configuration.GetSection(S3StorageOptions.SectionName))
            .PostConfigure(options =>
            {
                if (string.IsNullOrWhiteSpace(options.Region))
                    options.Region = configuration["AWS:Region"] ?? string.Empty;
            })
            .Validate(options => IsConfigured(options.BucketName), "Storage:S3:BucketName must be configured; replace the placeholder.")
            .Validate(options => IsConfigured(options.Region), "AWS:Region or Storage:S3:Region must be configured; replace the placeholder.")
            .Validate(options => string.IsNullOrWhiteSpace(options.ServiceUrl) ||
                (IsConfigured(options.ServiceUrl) && Uri.TryCreate(options.ServiceUrl, UriKind.Absolute, out var uri) &&
                 (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) && string.IsNullOrEmpty(uri.UserInfo)),
                "Storage:S3:ServiceUrl must be empty for AWS or an absolute HTTP/HTTPS endpoint without credentials.")
            .Validate(options =>
                (string.IsNullOrWhiteSpace(options.AccessKey) && string.IsNullOrWhiteSpace(options.SecretKey)) ||
                (IsConfigured(options.AccessKey) && IsConfigured(options.SecretKey)),
                "Storage:S3:AccessKey and SecretKey must both be configured, or both empty to use the AWS credential chain.")
            .Validate(options => string.IsNullOrWhiteSpace(options.SessionToken) ||
                (IsConfigured(options.SessionToken) && IsConfigured(options.AccessKey) && IsConfigured(options.SecretKey)),
                "Storage:S3:SessionToken requires configured AccessKey and SecretKey.")
            .ValidateOnStart();

        services.AddDefaultAWSOptions(serviceProvider =>
        {
            var settings = serviceProvider.GetRequiredService<IOptions<S3StorageOptions>>().Value;
            var options = configuration.GetAWSOptions();
            options.Region = RegionEndpoint.GetBySystemName(settings.Region);
            if (!string.IsNullOrWhiteSpace(settings.ServiceUrl))
            {
                options.DefaultClientConfig.ServiceURL = settings.ServiceUrl;
                options.DefaultClientConfig.AuthenticationRegion = settings.Region;
            }
            if (!string.IsNullOrWhiteSpace(settings.AccessKey))
                options.Credentials = string.IsNullOrWhiteSpace(settings.SessionToken)
                    ? new BasicAWSCredentials(settings.AccessKey, settings.SecretKey)
                    : new SessionAWSCredentials(settings.AccessKey, settings.SecretKey, settings.SessionToken);
            return options;
        });
        services.AddAWSService<IAmazonS3>((clientConfig, serviceProvider) =>
            ((AmazonS3Config)clientConfig).ForcePathStyle = serviceProvider.GetRequiredService<IOptions<S3StorageOptions>>().Value.ForcePathStyle);
        services.AddSingleton<IFileStorageService, S3FileStorageService>();
    }

    private static bool IsConfigured(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !value.Contains('<') && !value.Contains('>');
}
