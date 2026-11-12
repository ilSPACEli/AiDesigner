namespace AiDesigner.Api.Dto;

public class AnalysisRequestDto
{
    public string LightLevel { get; set; } = string.Empty;
    public List<string> DominantColors { get; set; } = new();
}