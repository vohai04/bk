using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace BookInfoFinder.Services;

public sealed class GeminiClient(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiClient> logger)
{
    public async Task<string> ReplyAsync(string prompt)
    {
        var key = configuration["GEMINI:ApiKey"];
        if (string.IsNullOrWhiteSpace(key) || key == "dev_api_key")
            return "Trợ lý AI chưa được cấu hình. Vui lòng thử lại sau.";

        var model = configuration["GEMINI:Model"] ?? "gemini-3.8-flash";
        using var request = new HttpRequestMessage(HttpMethod.Post,
            $"https://generativelanguage.googleapis.com/v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
        request.Headers.Add("x-goog-api-key", key);
        request.Content = JsonContent.Create(new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { temperature = 0.7, maxOutputTokens = 2048 }
        });

        try
        {
            using var response = await httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                // Do not log credentials, prompts or the provider's response body.
                logger.LogWarning("Gemini request failed: HTTP {StatusCode}, model {Model}", (int)response.StatusCode, model);
                return response.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Trợ lý AI đang gặp lỗi xác thực. Vui lòng thử lại sau.",
                    HttpStatusCode.NotFound => "Model AI hiện không khả dụng. Vui lòng thử lại sau.",
                    HttpStatusCode.TooManyRequests => "Trợ lý AI đã đạt hạn mức sử dụng. Vui lòng thử lại sau ít phút.",
                    _ => "Dịch vụ AI đang tạm gián đoạn. Vui lòng thử lại sau."
                };
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            if (document.RootElement.TryGetProperty("candidates", out var candidates)
                && candidates.GetArrayLength() > 0
                && candidates[0].TryGetProperty("content", out var content)
                && content.TryGetProperty("parts", out var parts))
            {
                var answer = string.Join("\n", parts.EnumerateArray()
                    .Where(part => !(part.TryGetProperty("thought", out var thought) && thought.ValueKind == JsonValueKind.True))
                    .Where(part => part.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                    .Select(part => part.GetProperty("text").GetString()));
                if (!string.IsNullOrWhiteSpace(answer)) return answer;
            }
            return "AI chưa tạo được câu trả lời. Bạn thử diễn đạt lại yêu cầu nhé.";
        }
        catch (OperationCanceledException)
        {
            logger.LogWarning("Gemini request timed out, model {Model}", model);
            return "AI phản hồi quá lâu. Vui lòng thử lại.";
        }
        catch (HttpRequestException)
        {
            logger.LogWarning("Unable to connect to Gemini, model {Model}", model);
            return "Chưa thể kết nối với dịch vụ AI. Vui lòng thử lại sau.";
        }
        catch (JsonException)
        {
            logger.LogWarning("Gemini returned invalid JSON, model {Model}", model);
            return "AI trả về phản hồi không hợp lệ. Vui lòng thử lại.";
        }
    }
}
