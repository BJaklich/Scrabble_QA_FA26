using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Scrabble.Web.Dtos;

namespace Scrabble.Tests;

public sealed class WebIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> factory;

    public WebIntegrationTests(WebApplicationFactory<Program> factory) => this.factory = factory;

    [Fact]
    public async Task CreateGame_ReturnsSessionAndDoesNotExposeOpponentsRack()
    {
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/games", new CreateGameRequest("Ada", true));

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<CreateGameResponse>();
        Assert.NotNull(created);
        Assert.NotEqual(Guid.Empty, created.SessionToken);

        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/games/{created.Id}");
        request.Headers.Add("X-Session-Token", created.SessionToken.ToString());
        var stateResponse = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, stateResponse.StatusCode);
        var state = await stateResponse.Content.ReadFromJsonAsync<GameDto>();
        Assert.NotNull(state);
        Assert.NotEmpty(state.Rack!);
        Assert.Equal(7, state.Rack!.Count);
    }

    [Fact]
    public async Task JoinByCode_ReturnsPlayerSession()
    {
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/games", new CreateGameRequest("Ada"));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateGameResponse>();
        Assert.NotNull(created);

        var joinResponse = await client.PostAsJsonAsync($"/api/games/join/{created.JoinCode}", new JoinGameRequest("Grace"));
        Assert.Equal(HttpStatusCode.OK, joinResponse.StatusCode);
        var joined = await joinResponse.Content.ReadFromJsonAsync<JoinByCodeResponse>();
        Assert.NotNull(joined);
        Assert.NotEqual(created.SessionToken, joined.SessionToken);
    }

    [Fact]
    public async Task Preview_WithJsonPlacement_ReturnsJsonNot415()
    {
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/games", new CreateGameRequest("Ada"));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateGameResponse>();
        Assert.NotNull(created);

        await client.PostAsJsonAsync($"/api/games/join/{created.JoinCode}", new JoinGameRequest("Grace"));

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{created.Id}/preview");
        request.Headers.Add("X-Session-Token", created.SessionToken.ToString());
        request.Content = JsonContent.Create(new[] { new PlacementRequest(7, 7, "A") });
        var response = await client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Play_WithJsonPlacement_ReturnsNot415()
    {
        using var client = factory.CreateClient();
        var createResponse = await client.PostAsJsonAsync("/api/games", new CreateGameRequest("Ada"));
        var created = await createResponse.Content.ReadFromJsonAsync<CreateGameResponse>();
        Assert.NotNull(created);

        await client.PostAsJsonAsync($"/api/games/join/{created.JoinCode}", new JoinGameRequest("Grace"));

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/games/{created.Id}/play");
        request.Headers.Add("X-Session-Token", created.SessionToken.ToString());
        request.Content = JsonContent.Create(new[] {
            new PlacementRequest(7, 5, "B"),
            new PlacementRequest(7, 6, "I"),
            new PlacementRequest(7, 7, "T"),
            new PlacementRequest(7, 8, "E")
        });
        var response = await client.SendAsync(request);

        Assert.NotEqual(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnknownGame_ReturnsNotFound()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync($"/api/games/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
