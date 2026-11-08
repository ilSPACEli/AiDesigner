using Microsoft.Extensions.Options;

namespace AiDesigner.Services.Results;

public static class GalleryServiceCollectionExtensions
{

    public static IServiceCollection AddGalleryOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GalleryOptions>()
            .Bind(configuration.GetSection(GalleryOptions.SectionName))
            .Validate(o => o.MaxItems > 0, "Gallery:MaxItems має бути більше 0.")
            .Validate(o => o.MaxImageBytes > 0, "Gallery:MaxImageBytes має бути більше 0.")
            .Validate(o => o.SavesPerMinute > 0, "Gallery:SavesPerMinute має бути більше 0.")
            .ValidateOnStart();

        services.AddSingleton(provider => provider.GetRequiredService<IOptions<GalleryOptions>>().Value);

        return services;
    }
}
