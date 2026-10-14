using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BookInfoFinder.Services;

public sealed record GeminiResult(string Text, bool Success);

public sealed class GeminiClient(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiClient> logger)
{
    public async Task<string> ReplyAsync(string prompt) => (await GenerateAsync(prompt)).Text;

    public async Task<GeminiResult> GenerateAsync(string prompt)
    {
        var key = configuration["GEMINI:ApiKey"];
        if (string.IsNullOrWhiteSpace(key) || key == "dev_api_key")
            return new GeminiResult("Trợ lý AI chưa được cấu hình. Vui lòng thử lại sau.", false);

        var model = configuration["GEMINI:Model"] ?? "gemini-3.8-flash";
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post,
                    $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
                request.Headers.Add("x-goog-api-key", key);
                request.Content = JsonContent.Create(new
                {
                    contents = new[] { new { parts = new[] { new { text = prompt } } } },
                    generationConfig = new { temperature = 0.7, maxOutputTokens = 2048 }
                });

                using var response = await httpClient.SendAsync(request, deadline.Token);
                if (!response.IsSuccessStatusCode)
                {
                    // Do not log credentials, prompts or the provider's response body.
                    logger.LogWarning("Gemini request failed: HTTP {StatusCode}, model {Model}", (int)response.StatusCode, model);
                    if (attempt < 2 && response.StatusCode is HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout)
                    {
                        await Task.Delay(400 * (attempt + 1), deadline.Token);
                        continue;
                    }
                    return new GeminiResult(response.StatusCode switch
                    {
                        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Trợ lý AI đang gặp lỗi xác thực. Vui lòng thử lại sau.",
                        HttpStatusCode.NotFound => "Model AI hiện không khả dụng. Vui lòng thử lại sau.",
                        HttpStatusCode.TooManyRequests => "Trợ lý AI đã đạt hạn mức sử dụng. Vui lòng thử lại sau ít phút.",
                        _ => "Dịch vụ AI đang tạm gián đoạn. Vui lòng thử lại sau."
                    }, false);
                }

                using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(deadline.Token));
                if (document.RootElement.TryGetProperty("candidates", out var candidates)
                    && candidates.ValueKind == JsonValueKind.Array && candidates.GetArrayLength() > 0
                    && candidates[0].TryGetProperty("content", out var content)
                    && content.TryGetProperty("parts", out var parts) && parts.ValueKind == JsonValueKind.Array)
                {
                    var answer = string.Join("\n", parts.EnumerateArray()
                        .Where(part => !(part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True))
                        .Where(part => part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                        .Select(part => part.GetProperty("text").GetString()));
                    if (!string.IsNullOrWhiteSpace(answer)) return new GeminiResult(answer, true);
                }
                return new GeminiResult("AI chưa tạo được câu trả lời. Bạn thử diễn đạt lại yêu cầu nhé.", false);
            }
            return new GeminiResult("Dịch vụ AI đang tạm gián đoạn. Vui lòng thử lại sau.", false);
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Gemini request timed out, model {Model}", model);
            return new GeminiResult("AI phản hồi quá lâu. Vui lòng thử lại.", false);
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("Unable to connect to Gemini, model {Model}", model);
            return new GeminiResult("Chưa thể kết nối với dịch vụ AI. Vui lòng thử lại sau.", false);
        }
        catch (JsonException)
        {
            logger.LogWarning("Gemini returned invalid JSON, model {Model}", model);
            return new GeminiResult("AI trả về phản hồi không hợp lệ. Vui lòng thử lại.", false);
        }
    }
}
