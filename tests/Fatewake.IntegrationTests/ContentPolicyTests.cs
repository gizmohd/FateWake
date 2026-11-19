using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Fatewake.AI.Intent;
using Fatewake.GameEngine;
using Fatewake.Infrastructure.Authentication;
using Fatewake.Infrastructure.Story;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fatewake.IntegrationTests;

/// <summary>Verifies age transitions and language checks without a provider or database.</summary>
public sealed class ContentPolicyTests
{
    private static readonly DateOnly Today = new(2026, 11, 19);

    /// <summary>Birthday boundaries, missing dates, and adult opt-in use a conservative effective level.</summary>
    [Theory]
    [InlineData(2012, 11, 20, ContentLevel.Under14, ContentLevel.Under14)]
    [InlineData(2012, 11, 19, ContentLevel.Teen, ContentLevel.Teen)]
    [InlineData(2008, 11, 20, ContentLevel.Teen, ContentLevel.Teen)]
    [InlineData(2008, 11, 19, ContentLevel.AdultLanguage, ContentLevel.AdultClean)]
    [InlineData(2026, 11, 20, ContentLevel.Under14, ContentLevel.Under14)]
    public void Resolves_age_cap_and_clean_default(int year, int month, int day, ContentLevel maximum, ContentLevel expected)
    {
        var settings = ContentAgePolicy.Resolve(new DateOnly(year, month, day), null, Today);
        Assert.Equal(maximum, settings.MaximumLevel);
        Assert.Equal(expected, settings.Level);
    }

    /// <summary>Players may select a stricter rating, and missing birth dates never unlock older content.</summary>
    [Fact]
    public void Rejects_above_age_caps_and_allows_lower_levels()
    {
        Assert.Equal(ContentLevel.Under14, ContentAgePolicy.Resolve(null, ContentLevel.AdultLanguage, Today).Level);
        Assert.Equal(ContentLevel.Teen, ContentAgePolicy.Resolve(Today.AddYears(-15), ContentLevel.AdultLanguage, Today).Level);
        Assert.Equal(ContentLevel.Under14, ContentAgePolicy.Resolve(Today.AddYears(-20), ContentLevel.Under14, Today).Level);
        Assert.Equal(ContentLevel.AdultLanguage, ContentAgePolicy.Resolve(Today.AddYears(-20), ContentLevel.AdultLanguage, Today).Level);
    }

    /// <summary>Prohibited story topics stay blocked at every level, even when dialogue permits profanity.</summary>
    [Fact]
    public void Applies_the_topic_ban_and_language_preference()
    {
        foreach (var level in Enum.GetValues<ContentLevel>())
        {
            Assert.False(StoryContentPolicy.Allows(level, "A note mentions s u i c i d e."));
            Assert.False(StoryContentPolicy.Allows(level, "A note says they will end their own life."));
            Assert.True(StoryContentPolicy.Allows(level, "I ask Michelle to bring him inside."));
        }

        Assert.False(StoryContentPolicy.Allows(ContentLevel.AdultClean, "What the hell? This is bullshit."));
        Assert.True(StoryContentPolicy.Allows(ContentLevel.AdultLanguage, "What the hell? This is bullshit."));
        Assert.False(StoryContentPolicy.Allows(ContentLevel.Under14, "The detective investigates a murder."));
    }

    /// <summary>The generation path releases a scene only after a second review accepts it.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Narrator_requires_an_independent_review(bool approved)
    {
        var ct = TestContext.Current.CancellationToken;
        var handler = new NarratorResponses(approved);
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
        var narrator = new OpenAiSceneAuthor(client, new SceneAuthoringOptions { ApiKey = "test-key" },
            null!, NullLogger<OpenAiSceneAuthor>.Instance);
        var context = new SceneAuthoringContext("Sam", 1, 1, false, [], [], null, Guid.NewGuid(),
            ContentLevel: ContentLevel.Teen);
        if (approved)
            Assert.Equal("Dawn", (await narrator.AuthorAsync(context, Guid.NewGuid(), 1, ct)).Title);
        else
            await Assert.ThrowsAsync<InvalidDataException>(() => narrator.AuthorAsync(context, Guid.NewGuid(), 1, ct));
        Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, body => Assert.Contains("Use clean language", body));
    }

    /// <summary>Calling from safety and declining to act must never be read as approaching the stranger.</summary>
    [Theory]
    [InlineData("I call for help from the doorway.", "call_from_safety")]
    [InlineData("I don't help him.", "unrecognized_intent")]
    [InlineData("I check the radio.", "investigate_radio")]
    [InlineData("I step out to help Michelle.", "help_injured_stranger")]
    public async Task Interprets_the_attempted_action_without_reversing_it(string input, string expected)
    {
        var snapshot = new GameSnapshot(Guid.NewGuid(), Guid.NewGuid(), 1, "day-001-injured-stranger",
            new Dictionary<string, string>());
        var action = await new BaselineIntentInterpreter().InterpretAsync(snapshot, input, TestContext.Current.CancellationToken);
        Assert.Equal(expected, action.ActionType);
    }

    private sealed class NarratorResponses(bool approved) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(await request.Content!.ReadAsStringAsync(ct));
            var output = Requests.Count == 1
                ? """{"title":"Dawn","summary":"Light returns.","time_label":"6:45 AM","beats":[{"kind":"Narration","narration":"Light returns.","art_role":"background","art_subject":"A street at dawn."}],"choices":[]}"""
                : JsonSerializer.Serialize(new { allowed = approved });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { output = new[] { new { content = new[] { new { type = "output_text", text = output } } } } })
            };
        }
    }
}
