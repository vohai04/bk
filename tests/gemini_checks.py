"""Exercise Gemini HTTP handling without contacting Google or using real credentials."""
import subprocess
import tempfile
from pathlib import Path

root = Path(__file__).resolve().parents[1]
with tempfile.TemporaryDirectory(prefix='bookinfo-gemini-') as directory:
    folder = Path(directory)
    (folder / 'Checks.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
<PropertyGroup><TargetFramework>net9.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
<ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" /><Compile Include="{root / 'Services/GeminiClient.cs'}" Link="GeminiClient.cs" /></ItemGroup>
<ItemGroup><Using Include="Microsoft.Extensions.Configuration" /><Using Include="Microsoft.Extensions.Logging" /></ItemGroup>
</Project>''', encoding='utf-8')
    (folder / 'Program.cs').write_text(r'''
using System.Net;
using System.Text;
using BookInfoFinder.Services;
using Microsoft.Extensions.Logging.Abstractions;

var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
    ["GEMINI:ApiKey"] = "test-secret", ["GEMINI:Model"] = "gemini-3.8-flash"
}).Build();
var handler = new FakeHandler();
var client = new GeminiClient(new HttpClient(handler), settings, NullLogger<GeminiClient>.Instance);
async Task Check(string name, string expected) {
    var result = await client.ReplyAsync("test prompt");
    if (!result.Contains(expected) || result.Contains("test-secret")) throw new Exception(name + ": " + result);
    Console.WriteLine("PASS: " + name);
}
handler.Body = """{"candidates":[{"content":{"parts":[{"text":"private reasoning","thought":true},{"text":"Xin chào"},{"text":"Bạn tìm sách gì?"}]}}]}""";
await Check("text parts combined, thought excluded", "Xin chào\nBạn tìm sách gì?");
handler.Body = "{}";
await Check("empty response", "chưa tạo được");
handler.Body = "not JSON";
await Check("invalid JSON", "không hợp lệ");
foreach (var pair in new[] { (401,"xác thực"), (403,"xác thực"), (404,"Model AI"), (429,"hạn mức"), (500,"gián đoạn") }) {
    handler.Status = pair.Item1;
    await Check("HTTP " + pair.Item1, pair.Item2);
}
handler.Body = """{"candidates":[{"content":{"parts":[{"text":"Recovered"}]}}]}""";
handler.Statuses = new Queue<int>(new[] {503, 200});
var previousCalls = handler.Calls;
await Check("temporary 503 is retried and recovers", "Recovered");
if (handler.Calls != previousCalls + 2) throw new Exception("Unexpected retry count");
handler.Statuses = new Queue<int>(new[] {503, 503, 503, 200});
previousCalls = handler.Calls;
await Check("persistent 503 stops after three attempts", "gián đoạn");
if (handler.Calls != previousCalls + 3) throw new Exception("Unbounded retries");
handler.Statuses.Clear();
handler.Failure = new HttpRequestException("test-secret");
await Check("network failure sanitized", "Chưa thể kết nối");
handler.Failure = new TaskCanceledException("test-secret");
await Check("timeout sanitized", "quá lâu");
settings["GEMINI:ApiKey"] = "";
await Check("missing configuration", "chưa được cấu hình");

sealed class FakeHandler : HttpMessageHandler {
    public int Calls;
    public Queue<int> Statuses = new();
    public int Status = 200;
    public string Body = "{}";
    public Exception? Failure;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        Calls++;
        if (request.RequestUri!.Query.Length != 0 || request.RequestUri.ToString().Contains("test-secret")) throw new Exception("Secret in URI");
        if (request.Headers.GetValues("x-goog-api-key").Single() != "test-secret") throw new Exception("Missing API key header");
        if (!request.RequestUri.AbsolutePath.Contains("gemini-3.8-flash")) throw new Exception("Incorrect model");
        if (Failure != null) throw Failure;
        return Task.FromResult(new HttpResponseMessage((HttpStatusCode)(Statuses.Count > 0 ? Statuses.Dequeue() : Status)) { Content = new StringContent(Body, Encoding.UTF8, "application/json") });
    }
}
''', encoding='utf-8')
    subprocess.run(['dotnet', 'run', '--project', str(folder / 'Checks.csproj')], check=True)
