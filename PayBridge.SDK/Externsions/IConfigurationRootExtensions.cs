using Microsoft.Extensions.Configuration;

namespace PayBridge.SDK;
public static class IConfigurationRootExtensions
{
    public static IConfigurationBuilder AddBasePath(this IConfigurationBuilder builder)
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        var startupProjectPath = Path.Combine(currentDirectory, "../PayBridge.SDK.Presentation");
        var basePathConfiguration = Directory.Exists(startupProjectPath) ? startupProjectPath : currentDirectory;

        return builder.SetBasePath(basePathConfiguration);
    }
}
