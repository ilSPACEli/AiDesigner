using System.Globalization;
using System.Text.Json;
using System.Threading.RateLimiting;
using AiDesigner.Services.Results;
using Microsoft.AspNetCore.RateLimiting;

namespace AiDesigner.Api;

public static class GalleryRateLimiting
{
    public const string SaveResultPolicy = "save-result";

    public const string TooManyRequestsTitle = "Забагато збережень";

    public static IServiceCollection AddGalleryRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options => options.RejectionStatusCode = StatusCodes.Status429TooManyRequests);

        services.AddOptions<RateLimiterOptions>().Configure<GalleryOptions>((options, gallery) =>
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(gallery.SavesPerMinute);

            options.AddPolicy(SaveResultPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    // Окремий лічильник на кожну адресу клієнта. Відома межа: за зворотним проксі
                    // або у виклику GalleryClient → API того самого сервера адреса буде спільною.
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = gallery.SavesPerMinute,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,          // зайві запити не чекають у черзі, а відхиляються одразу
                        AutoReplenishment = true
                    }));

            options.OnRejected = async (context, cancellationToken) =>
            {
                var response = context.HttpContext.Response;
                response.StatusCode = StatusCodes.Status429TooManyRequests;

                // Підказка клієнтові, через скільки секунд можна повторити.
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    response.Headers["Retry-After"] =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                }

                var problem = ApiProblem.Create(
                    StatusCodes.Status429TooManyRequests,
                    TooManyRequestsTitle,
                    $"Ліміт збережень — {gallery.SavesPerMinute} за хвилину. Спробуйте пізніше.");

                await response.WriteAsJsonAsync(problem, (JsonSerializerOptions?)null, ApiProblem.ContentType, cancellationToken);
            };
        });

        return services;
    }
}