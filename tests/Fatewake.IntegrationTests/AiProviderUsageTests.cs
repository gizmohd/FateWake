using System.Net;
using System.Text;
using Fatewake.Infrastructure.Art;
using Fatewake.Infrastructure.Usage;
using Fatewake.Observability;
using Microsoft.Extensions.Options;
namespace Fatewake.IntegrationTests;
/// <summary>Checks accounting at provider-response boundaries without paid requests.</summary>
public sealed class AiProviderUsageTests
{
    /// <summary>Reported consumption survives HTTP errors and invalid image output.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OpenAI_usage_is_saved_before_failure(bool httpFailure)
    {
        var usage=new Recorder();
        using var client=new HttpClient(new Response(httpFailure?HttpStatusCode.ServiceUnavailable:HttpStatusCode.OK,
            "{\"usage\":{\"input_tokens\":20,\"output_tokens\":10,\"total_tokens\":30,\"input_tokens_details\":{\"cached_tokens\":4}},\"data\":[{\"b64_json\":\"invalid base64\"}]}")) {BaseAddress=new("http://test/")};
        var provider=new OpenAiArtMasterGenerator(client,Options.Create(new OpenAiArtGenerationOptions{Enabled=true,ApiKey="synthetic-test"}),new FileSystemArtBinaryStorage(Path.GetTempPath()),usage:usage);
        await Assert.ThrowsAnyAsync<Exception>(()=>provider.GenerateAsync(new("request",new("key","Background","fp","private prompt","OpenAI","test")),TestContext.Current.CancellationToken));
        Assert.Equal(1,usage.Begins);Assert.Equal(30,Assert.Single(usage.Reports).TotalTokens);Assert.Equal("failed",usage.Outcome);
    }
    /// <summary>Missing measurements stay null, while reported zero and token breakdowns remain valid.</summary>
    [Fact]
    public void Compatible_parser_preserves_unknown_zero_and_breakdowns()
    {
        Assert.Null(AiProviderUsage.Parse("{}").TotalTokens);
        var report=AiProviderUsage.Parse("{\"usage\":{\"prompt_tokens\":0,\"completion_tokens\":12,\"total_tokens\":12,\"prompt_tokens_details\":{\"text_tokens\":0,\"image_tokens\":0},\"output_tokens_details\":{\"image_tokens\":12},\"credits\":0,\"cost\":2,\"currency\":\"EUR\"}}", "request-id");
        Assert.Equal(0,report.InputTokens);Assert.Equal(12,report.ImageOutputTokens);Assert.Equal(0,report.Credits);Assert.Equal("EUR",report.Currency);
        Assert.Null(AiProviderUsage.Parse("{\"usage\":{\"total_tokens\":-3}}").TotalTokens);
    }
    /// <summary>ComfyUI persists submission identity immediately; polls and downloads add no attempts.</summary>
    [Fact]
    public async Task ComfyUI_polling_counts_one_attempt_and_retains_job_identity()
    {
        var usage=new Recorder();var path=Path.Combine(Path.GetTempPath(),"comfy-usage-"+Guid.NewGuid()+".json");
        await File.WriteAllTextAsync(path,"{\"1\":{\"class_type\":\"SaveImage\",\"inputs\":{}}}",TestContext.Current.CancellationToken);
        try
        {
            using var client=new HttpClient(new ComfyResponses(usage)){BaseAddress=new("http://test/")};
            var provider=new ComfyUiArtMasterGenerator(client,Options.Create(new ComfyUiArtGenerationOptions {Enabled=true,WorkflowPath=path,PollIntervalMilliseconds=1}),new FileSystemArtBinaryStorage(Path.GetTempPath()),usage:usage);
            var result=await provider.GenerateAsync(new("comfy",new("key","Background","fp","prompt","ComfyUI","workflow")),TestContext.Current.CancellationToken);
            Assert.Equal("prompt-id",result.ProviderJobId);Assert.Equal(1,usage.Begins);Assert.Equal("succeeded",usage.Outcome);
            Assert.Equal("prompt-id",usage.Reports[0].ProviderRequestId);Assert.Equal(2m,usage.Reports[^1].Credits);Assert.Null(usage.Reports[^1].TotalTokens);
        }
        finally {File.Delete(path);}
    }
    /// <summary>Compatible local endpoints report counters without inventing paid usage for unavailable fields.</summary>
    [Fact]
    public async Task Local_provider_captures_reported_tokens_on_success()
    {
        var usage=new Recorder();
        using var client=new HttpClient(new Response(HttpStatusCode.OK,"{\"usage\":{\"prompt_tokens\":2,\"completion_tokens\":3,\"total_tokens\":5},\"data\":[{\"b64_json\":\"AQID\"}]}")){BaseAddress=new("http://test/")};
        var provider=new LocalArtMasterGenerator(client,Options.Create(new LocalArtGenerationOptions {Enabled=true}),usage:usage);
        await provider.GenerateAsync(new("local",new("key","Background","fp","prompt","Local","test")),TestContext.Current.CancellationToken);
        Assert.Equal(5,Assert.Single(usage.Reports).TotalTokens);Assert.Equal("succeeded",usage.Outcome);Assert.Null(usage.Reports[0].Cost);
    }
    /// <summary>Cancellation and timeout close the attempt with different outcomes.</summary>
    [Theory]
    [InlineData(true,"cancelled")]
    [InlineData(false,"timed_out")]
    public async Task Interrupted_calls_keep_a_durable_outcome(bool cancelled,string outcome)
    {
        var usage=new Recorder();using var source=new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>TrackedAiCall.RunAsync<int>(usage,new("test","model","operation","attempt"),
            _=>{if(cancelled) source.Cancel();throw new OperationCanceledException();},source.Token));
        Assert.Equal(outcome,usage.Outcome);Assert.Equal(1,usage.Begins);
    }
    private sealed class ComfyResponses(Recorder usage) : HttpMessageHandler
    {
        private int polls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            var path=request.RequestUri!.AbsolutePath;
            if(path=="/history/prompt-id") Assert.Equal("prompt-id",usage.Reports[0].ProviderRequestId);
            var body=path=="/prompt"?"{\"prompt_id\":\"prompt-id\"}":path=="/view"?"binary":++polls==1?"{}":
                "{\"prompt-id\":{\"usage\":{\"credits\":2,\"compute_seconds\":3},\"outputs\":{\"1\":{\"images\":[{\"filename\":\"image.png\"}]}}}}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body)});
        }
    }
    private sealed class Response(HttpStatusCode status,string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {var result=new HttpResponseMessage(status){Content=new StringContent(body,Encoding.UTF8,"application/json")};result.Headers.Add("x-request-id","provider-id");return Task.FromResult(result);}
    }
    private sealed class Recorder : IAiUsageRecorder
    {
        public int Begins; public List<AiUsageReport> Reports=[]; public string? Outcome;
        public Task<Guid> BeginAsync(AiCallContext context,CancellationToken ct=default) {Begins++;return Task.FromResult(Guid.NewGuid());}
        public Task ReportAsync(Guid id,AiUsageReport report,CancellationToken ct=default) {Reports.Add(report);return Task.CompletedTask;}
        public Task FinishAsync(Guid id,string outcome,CancellationToken ct=default) {Outcome=outcome;return Task.CompletedTask;}
    }
}
