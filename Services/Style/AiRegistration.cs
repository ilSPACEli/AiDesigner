namespace AiDesigner.Services.Style;

public static class AiRegistration
{
    public static IServiceCollection AddAiAnalysis(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(AiOptions.SectionName).Get<AiOptions>() ?? new AiOptions();
        services.AddSingleton(options);

        services.AddHttpClient<GeminiStyleAnalyzer>(client =>
        {
            client.BaseAddress = new Uri(GeminiStyleAnalyzer.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(60);
        });
        services.AddTransient<IStyleAnalyzer>(sp => sp.GetRequiredService<GeminiStyleAnalyzer>());

        return services;
    }
}
