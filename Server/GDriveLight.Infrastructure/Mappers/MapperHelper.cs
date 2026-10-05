using System.Reflection;

namespace GDriveLight.Infrastructure.Mappers;

internal static class MapperHelper
{
    public static void SetPropertyValue<T>(this T obj, string propertyName, object? value)
    {
        var property = typeof(T).GetProperty(propertyName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        property?.SetValue(obj, value);
    }
}
