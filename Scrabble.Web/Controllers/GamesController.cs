using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Scrabble.Core.Models;
using Scrabble.Web.Hubs;
using Scrabble.Web.Dtos;
using Scrabble.Web.Services;

namespace Scrabble.Web.Controllers;

[ApiController]
[Route("api/games")]
public sealed class GamesController : ControllerBase
{
    private readonly GameManager games;
    private readonly IHubContext<GameHub> hub;

    public GamesController(GameManager games, IHubContext<GameHub> hub)
    {
        this.games = games;
        this.hub = hub;
    }

    [HttpPost]
    public ActionResult<CreateGameResponse> Create([FromBody] CreateGameRequest request)
    {
        try
        {
            var game = games.Create(request.PlayerName, request.ComputerOpponent);
            var player = game.Players[0];
            return CreatedAtAction(nameof(Get), new { id = game.Id }, new CreateGameResponse(game.Id, game.JoinCode, player.Id, player.SessionToken));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("join/{joinCode}")]
    public async Task<ActionResult<JoinGameResponse>> JoinByCode(string joinCode, [FromBody] JoinGameRequest request)
    {
        try
        {
            var game = games.GetByJoinCode(joinCode);
            var player = games.Join(game.Id, request.PlayerName);
            await hub.Clients.Group(game.Id.ToString()).SendAsync("PlayerJoined", player.Id);
            return Ok(new JoinByCodeResponse(game.Id, player.Id, player.Name, player.SessionToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpPost("{id:guid}/join")]
    public async Task<ActionResult<JoinGameResponse>> Join(Guid id, [FromBody] JoinGameRequest request)
    {
        try
        {
            var player = games.Join(id, request.PlayerName);
            await hub.Clients.Group(id.ToString()).SendAsync("PlayerJoined", player.Id);
            return Ok(new JoinGameResponse(player.Id, player.Name, player.SessionToken));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
    }

    [HttpGet("{id:guid}")]
    public ActionResult<GameDto> Get(Guid id, [FromHeader(Name = "X-Session-Token")] Guid sessionToken)
    {
        try
        {
            var game = games.Get(id);
            lock (game.Lock)
            {
                var player = game.Players.SingleOrDefault(candidate => candidate.SessionToken == sessionToken);
                if (player is null) return NotFound();
                return Ok(ToDto(game, player));
            }
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/play")]
    public async Task<IActionResult> Play(Guid id, [FromHeader(Name = "X-Session-Token")] Guid sessionToken, [FromBody] IReadOnlyList<PlacementRequest> requests)
    {
        try
        {
            var game = games.Get(id);
            var placements = requests.Select(request =>
                (request.Row, request.Column, request.Letter, request.AssignedLetter)).ToList();
            var result = games.Play(game, sessionToken, placements);
            if (result.IsValid) await hub.Clients.Group(id.ToString()).SendAsync("GameStateUpdated", id);
            return result.IsValid ? Ok(new { message = result.Error }) : BadRequest(new { error = result.Error });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/computer-turn")]
    public async Task<IActionResult> ComputerTurn(Guid id, [FromHeader(Name = "X-Session-Token")] Guid sessionToken)
    {
        try
        {
            var game = games.Get(id);
            lock (game.Lock)
            {
                var requester = game.Players.SingleOrDefault(player => player.SessionToken == sessionToken);
                if (requester is null) return Unauthorized(new { error = "Invalid player session." });
            }
            var result = games.PlayComputerTurn(game);
            if (result.IsValid) await hub.Clients.Group(id.ToString()).SendAsync("GameStateUpdated", id);
            return result.IsValid ? Ok(new { message = result.Error }) : BadRequest(new { error = result.Error });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/preview")]
    public IActionResult Preview(Guid id, [FromHeader(Name = "X-Session-Token")] Guid sessionToken, [FromBody] IReadOnlyList<PlacementRequest> requests)
    {
        try
        {
            var game = games.Get(id);
            var placements = requests.Select(request => (request.Row, request.Column, request.Letter, request.AssignedLetter)).ToList();
            var result = games.Preview(game, sessionToken, placements);
            if (!result.IsValid)
                return Ok(new { score = 0, words = Array.Empty<string>(), valid = false });
            var parts = result.Error!.Split('|', 2);
            var words = parts.Length > 1 && !string.IsNullOrEmpty(parts[1]) ? parts[1].Split(',') : [];
            return Ok(new { score = int.Parse(parts[0]), words, valid = true });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/exchange")]
    public async Task<IActionResult> Exchange(Guid id, [FromHeader(Name = "X-Session-Token")] Guid sessionToken, [FromBody] ExchangeRequest request)
    {
        try
        {
            var game = games.Get(id);
            var result = games.Exchange(game, sessionToken, request.TileIds);
            if (result.IsValid) await hub.Clients.Group(id.ToString()).SendAsync("GameStateUpdated", id);
            return result.IsValid ? Ok() : BadRequest(new { error = result.Error });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{id:guid}/pass")]
    public async Task<IActionResult> Pass(Guid id, [FromHeader(Name = "X-Session-Token")] Guid sessionToken)
    {
        try
        {
            var game = games.Get(id);
            var result = games.Pass(game, sessionToken);
            if (result.IsValid) await hub.Clients.Group(id.ToString()).SendAsync("GameStateUpdated", id);
            return result.IsValid ? Ok() : BadRequest(new { error = result.Error });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private static string PremiumCode(PremiumType premium) => premium switch
    {
        PremiumType.TripleWord => "TW",
        PremiumType.DoubleWord or PremiumType.Center => "DW",
        PremiumType.TripleLetter => "TL",
        PremiumType.DoubleLetter => "DL",
        _ => "Normal"
    };

    private static GameDto ToDto(GameState game, PlayerState requestingPlayer)
    {
        var squares = game.Board.Squares.Cast<Square>()
            .Select(square => new SquareDto(square.Row, square.Column, PremiumCode(square.Premium), square.Tile?.EffectiveLetter))
            .ToList();
        var players = game.Players.Select(player => new PlayerDto(player.Id, player.Name, player.Score, player.Rack.Tiles.Count)).ToList();
        var rack = requestingPlayer.Rack.Tiles.Select(tile => new TileDto(tile.EffectiveLetter, tile.PointValue, tile.IsBlank, tile.Id)).ToList();

        var history = game.MoveHistory.Select(entry => new MoveHistoryDto(entry.PlayerName, entry.Score, entry.Words)).Reverse().ToList();
        return new GameDto(game.Id, game.JoinCode, game.Status,
            game.Players[game.CurrentPlayerIndex].Id, players, squares, rack, history);
    }
}
