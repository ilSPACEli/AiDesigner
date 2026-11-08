using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AiDesigner.Api.Dto;
using Microsoft.AspNetCore.Mvc;

namespace AiDesigner.Clients;

public sealed class GalleryClient : IGalleryClient
{
    public const string ResultsPath = "api/results";

    public const string GenericFailureMessage = "Не вдалося зберегти зображення. Спробуйте ще раз.";

    private readonly HttpClient _http;

    public GalleryClient(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
        _http = http;
    }

    public async Task<GallerySaveResult> SaveAsync(byte[] pngBytes, string catalogItemId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogItemId);

        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(pngBytes);   // форма сама звільнить додані частини
        file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "image", "result.png");
        form.Add(new StringContent(catalogItemId), "catalogItemId");

        using var response = await _http.PostAsync(ResultsPath, form, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            var dto = await response.Content.ReadFromJsonAsync<ResultDto>(cancellationToken);
            return dto is null ? GallerySaveResult.Fail(GenericFailureMessage) : GallerySaveResult.Success(dto);
        }

        return GallerySaveResult.Fail(await ReadProblemMessageAsync(response, cancellationToken));
    }

    public async Task<IReadOnlyList<ResultDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var results = await _http.GetFromJsonAsync<List<ResultDto>>(ResultsPath, cancellationToken);
        return results ?? new List<ResultDto>();
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        using var response = await _http.DeleteAsync($"{ResultsPath}/{id}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return false;

        response.EnsureSuccessStatusCode();
        return true;
    }


    private static async Task<string> ReadProblemMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken);
            var message = problem?.Detail ?? problem?.Title;
            if (!string.IsNullOrWhiteSpace(message))
                return message;
        }
        catch (JsonException)
        {
            // Тіло не JSON.
        }
        catch (NotSupportedException)
        {
            // Тип вмісту не JSON 
        }

        return $"{GenericFailureMessage} (код {(int)response.StatusCode})";
    }
}