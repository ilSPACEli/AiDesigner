namespace AiDesigner.Clients;

public static class ApiClientRegistration
{
    public static IServiceCollection AddApiClients(this IServiceCollection services)
    {
        services.AddHttpClient<IGalleryClient, GalleryClient>(ConfigureBaseAddress);
        services.AddHttpClient<ICatalogClient, CatalogClient>(ConfigureBaseAddress);
        services.AddHttpClient<ISuggestionClient, SuggestionClient>(ConfigureBaseAddress);
        return services;
    }

    private static void ConfigureBaseAddress(IServiceProvider services, HttpClient client)
    {
        var baseUrl = services.GetRequiredService<IConfiguration>()["ApiBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new InvalidOperationException("Не задано ApiBaseUrl. Створіть .env за зразком .env.example.");
        client.BaseAddress = new Uri(baseUrl);
    }
}
