using System.Net;
using System.Security.Claims;
using Fatewake.Api;
using Fatewake.Infrastructure.Persistence;
using Fatewake.Infrastructure.Story;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Fatewake.IntegrationTests;
/// <summary>Tests novel history audience ownership at the HTTP boundary.</summary>
public sealed class StoryAccessTests
{
    /// <summary>A signed-in player cannot read another account's journal; anonymous access cannot expose it either.</summary>
    [Fact]
    public async Task Account_owned_history_requires_matching_owner()
    {
        var owner = Guid.NewGuid(); var survivor = Guid.NewGuid();
        var builder = WebApplication.CreateBuilder(); builder.WebHost.UseTestServer();
        // One fixed database across request scopes.
        var database = Guid.NewGuid().ToString();
        builder.Services.AddDbContext<FatewakeDbContext>(x => x.UseInMemoryDatabase(database));
        builder.Services.AddScoped<StoryJournalService>();
        await using var app = builder.Build();
        app.Use(async (context, next) =>
        {
            if (context.Request.Headers.TryGetValue("TestOwner", out var id))
                context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, id.ToString())], "test"));
            await next(context);
        });
        app.MapStory(); await app.StartAsync(TestContext.Current.CancellationToken);
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FatewakeDbContext>();
            db.Survivors.Add(new SurvivorRecord { Id = survivor, AccountId = owner, TimelineId = Guid.NewGuid(), DisplayName = "Test", IdentityMode = "test", BroadRegion = "test", Status = SurvivorStatus.Active });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        using var client = app.GetTestClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/story/{survivor}", TestContext.Current.CancellationToken)).StatusCode);
        client.DefaultRequestHeaders.Add("TestOwner", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/story/{survivor}", TestContext.Current.CancellationToken)).StatusCode);
        client.DefaultRequestHeaders.Remove("TestOwner"); client.DefaultRequestHeaders.Add("TestOwner", owner.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/story/{survivor}", TestContext.Current.CancellationToken)).StatusCode);
    }
}
