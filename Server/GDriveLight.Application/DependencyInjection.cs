using System.Reflection;
using FluentValidation;
using GDriveLight.Application.Common.Behaviors;
using Microsoft.Extensions.DependencyInjection;
using MediatR;
using GDriveLight.Application.Abstractions.Services;
using GDriveLight.Application.Storage;

namespace GDriveLight.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IStorageQuotaPolicy, DefaultStorageQuotaPolicy>();
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(Assembly.GetExecutingAssembly());
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        return services;
    }
}
