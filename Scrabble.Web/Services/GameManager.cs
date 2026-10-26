using System.Collections.Concurrent;
using Scrabble.Core.Ai;
using Scrabble.Core.Dictionary;
using Scrabble.Core.Models;
using Scrabble.Core.Rules;

namespace Scrabble.Web.Services;

public sealed class PlayerState
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid SessionToken { get; init; } = Guid.NewGuid();
    public required string Name { get; init; }
    public Rack Rack { get; } = new();
    public int Score { get; set; }
}

public sealed record MoveHistoryEntry(string PlayerName, int Score, IReadOnlyList<string> Words);

public sealed class GameState
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string JoinCode { get; set; } = string.Empty;
    public Board Board { get; } = new();
    public TileBag Bag { get; } = new();
    public List<PlayerState> Players { get; } = [];
    public int CurrentPlayerIndex { get; set; }
    public int ConsecutivePasses { get; set; }
    public List<MoveHistoryEntry> MoveHistory { get; } = [];
    public string Status { get; set; } = "Waiting";
    public object Lock { get; } = new();
}

public sealed class GameManager
{
    private readonly ConcurrentDictionary<Guid, GameState> games = new();
    private readonly ConcurrentDictionary<string, Guid> joinCodes = new(StringComparer.OrdinalIgnoreCase);
    private readonly IWordDictionary dictionary;
    private readonly Random random;

    public GameManager(IWordDictionary dictionary, Random? random = null)
    {
        this.dictionary = dictionary;
        this.random = random ?? Random.Shared;
    }

    public GameState Create(string? playerName, bool computerOpponent = false)
    {
        if (string.IsNullOrWhiteSpace(playerName)) throw new ArgumentException("Player name is required.", nameof(playerName));

        var game = new GameState();
        var joinCode = GenerateJoinCode(game.Id);
        game.JoinCode = joinCode;
        var player = new PlayerState { Name = playerName.Trim() };
        FillRack(player.Rack, game.Bag);
        game.Players.Add(player);
        if (computerOpponent)
        {
            var computer = new PlayerState { Name = "Computer" };
            FillRack(computer.Rack, game.Bag);
            game.Players.Add(computer);
            game.Status = "Playing";
        }
        games[game.Id] = game;
        return game;
    }

    public GameState Get(Guid id)
    {
        if (!games.TryGetValue(id, out var game)) throw new KeyNotFoundException("Game not found.");
        return game;
    }

    public GameState GetByJoinCode(string? joinCode)
    {
        if (string.IsNullOrWhiteSpace(joinCode) || !joinCodes.TryGetValue(joinCode.Trim(), out var id))
            throw new KeyNotFoundException("Game not found.");
        return Get(id);
    }

    public PlayerState Join(Guid gameId, string? playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName)) throw new ArgumentException("Player name is required.", nameof(playerName));
        if (!games.TryGetValue(gameId, out var game)) throw new KeyNotFoundException("Game not found.");
        lock (game.Lock)
        {
            if (game.Players.Count >= 4) throw new InvalidOperationException("The game is full.");
            if (game.Status == "Finished") throw new InvalidOperationException("The game has finished.");
            var player = new PlayerState { Name = playerName.Trim() };
            FillRack(player.Rack, game.Bag);
            game.Players.Add(player);
            if (game.Players.Count == 2) game.Status = "Playing";
            return player;
        }
    }

    public ValidationResult Play(GameState game, Guid sessionToken, IReadOnlyList<(int Row, int Column, string Letter, string? AssignedLetter)> requests)
    {
        lock (game.Lock)
        {
            if (game.Status == "Waiting") return new ValidationResult(false, "Wait for another player before playing.");
            if (game.Status == "Finished") return new ValidationResult(false, "The game has finished.");
            var player = game.Players.SingleOrDefault(p => p.SessionToken == sessionToken);
            if (player is null) return new ValidationResult(false, "Player is not in this game.");
            if (game.Players.Count == 0 || game.Players[game.CurrentPlayerIndex].SessionToken != sessionToken)
                return new ValidationResult(false, "It is not your turn.");
            if (requests.Count == 0) return new ValidationResult(false, "A play must contain at least one tile.");

            var placements = new List<Placement>();
            var reservedTileIds = new HashSet<Guid>();
            foreach (var request in requests)
            {
                var isBlank = request.Letter.Equals("?", StringComparison.Ordinal) || request.AssignedLetter is not null;
                var rackTile = player.Rack.FindMatch(request.Letter, isBlank, request.AssignedLetter, reservedTileIds);
                if (rackTile is null || !reservedTileIds.Add(rackTile.Id))
                    return new ValidationResult(false, "Tile is not in the player's rack or was used twice.");

                Tile playedTile;
                if (isBlank)
                {
                    if (string.IsNullOrWhiteSpace(request.AssignedLetter) || request.AssignedLetter.Trim().Length != 1)
                        return new ValidationResult(false, "A blank tile requires one assigned letter.");
                    playedTile = new Tile { Letter = "?", PointValue = 0, IsBlank = true, AssignedLetter = request.AssignedLetter.Trim().ToUpperInvariant(), Id = rackTile.Id };
                }
                else
                {
                    playedTile = rackTile.Clone();
                }

                placements.Add(new Placement(request.Row, request.Column, playedTile));
            }

            var result = new MoveValidator(dictionary).Validate(game.Board, new Move { Placements = placements });
            if (!result.IsValid) return result;

            var score = new Scorer().Score(game.Board, placements);
            foreach (var placement in placements)
            {
                game.Board.Squares[placement.Row, placement.Column].Tile = placement.Tile;
                player.Rack.Remove(placement.Tile);
            }

            player.Score += score.Score;
            game.MoveHistory.Add(new MoveHistoryEntry(player.Name, score.Score, score.Words));
            game.ConsecutivePasses = 0;
            FillRack(player.Rack, game.Bag);
            FinishIfRackEmpty(game, player);
            if (game.Status != "Finished") NextTurn(game);
            return new ValidationResult(true, $"Scored {score.Score}");
        }
    }

    public ValidationResult Exchange(GameState game, Guid sessionToken, IReadOnlyList<Guid> tileIds)
    {
        lock (game.Lock)
        {
            if (game.Status == "Waiting") return new ValidationResult(false, "Wait for another player before exchanging.");
            if (game.Status == "Finished") return new ValidationResult(false, "The game has finished.");
            var player = game.Players.SingleOrDefault(p => p.SessionToken == sessionToken);
            if (player is null) return new ValidationResult(false, "Player is not in this game.");
            if (game.Players[game.CurrentPlayerIndex].SessionToken != sessionToken) return new ValidationResult(false, "It is not your turn.");
            if (game.Bag.Remaining < 7) return new ValidationResult(false, "Exchange requires at least 7 tiles in the bag.");
            if (tileIds.Count == 0 || tileIds.Count != tileIds.Distinct().Count()) return new ValidationResult(false, "Choose one or more unique tiles.");

            var returned = player.Rack.Tiles.Where(tile => tileIds.Contains(tile.Id)).ToList();
            if (returned.Count != tileIds.Count) return new ValidationResult(false, "Tile is not in the player's rack.");

            foreach (var tile in returned) player.Rack.Remove(tile);
            game.Bag.Return(returned);
            FillRack(player.Rack, game.Bag);
            game.ConsecutivePasses = 0;
            game.MoveHistory.Add(new MoveHistoryEntry(player.Name, 0, [$"Exchanged {returned.Count}"]));
            NextTurn(game);
            return new ValidationResult(true);
        }
    }

    public ValidationResult Pass(GameState game, Guid sessionToken)
    {
        lock (game.Lock)
        {
            if (game.Status == "Waiting") return new ValidationResult(false, "Wait for another player before passing.");
            if (game.Status == "Finished") return new ValidationResult(false, "The game has finished.");
            var player = game.Players.SingleOrDefault(p => p.SessionToken == sessionToken);
            if (player is null) return new ValidationResult(false, "Player is not in this game.");
            if (game.Players[game.CurrentPlayerIndex].SessionToken != sessionToken) return new ValidationResult(false, "It is not your turn.");

            game.ConsecutivePasses++;
            game.MoveHistory.Add(new MoveHistoryEntry(player.Name, 0, []));
            if (game.ConsecutivePasses >= game.Players.Count * 2)
            {
                FinishByPasses(game);
            }
            NextTurn(game);
            return new ValidationResult(true);
        }
    }

    public ValidationResult PlayComputerTurn(GameState game)
    {
        lock (game.Lock)
        {
            if (game.Status == "Waiting") return new ValidationResult(false, "Wait for another player before playing.");
            if (game.Status == "Finished") return new ValidationResult(false, "The game has finished.");
            var computer = game.Players[game.CurrentPlayerIndex];
            if (!computer.Name.Equals("Computer", StringComparison.Ordinal))
                return new ValidationResult(false, "It is your turn.");

            var generated = new MoveGenerator(dictionary).FindBestMove(game.Board, computer.Rack);
            if (generated is null)
            {
                game.ConsecutivePasses++;
                NextTurn(game);
                if (game.ConsecutivePasses >= game.Players.Count * 2) game.Status = "Finished";
                return new ValidationResult(true, "Computer passed");
            }

            var validation = new MoveValidator(dictionary).Validate(game.Board, new Move { Placements = generated.Placements });
            if (!validation.IsValid) return validation;

            foreach (var placement in generated.Placements)
            {
                game.Board.Squares[placement.Row, placement.Column].Tile = placement.Tile;
                computer.Rack.Remove(placement.Tile);
            }
            computer.Score += generated.Score;
            game.ConsecutivePasses = 0;
            FillRack(computer.Rack, game.Bag);
            NextTurn(game);
            return new ValidationResult(true, $"Computer scored {generated.Score}");
        }
    }

    public ValidationResult Preview(GameState game, Guid sessionToken, IReadOnlyList<(int Row, int Column, string Letter, string? AssignedLetter)> requests)
    {
        lock (game.Lock)
        {
            var player = game.Players.SingleOrDefault(p => p.SessionToken == sessionToken);
            if (player is null) return new ValidationResult(false, "Player is not in this game.");
            if (requests.Count == 0) return new ValidationResult(true, "0|0");

            var placements = new List<Placement>();
            var reservedTileIds = new HashSet<Guid>();
            foreach (var request in requests)
            {
                var isBlank = request.Letter.Equals("?", StringComparison.Ordinal) || request.AssignedLetter is not null;
                var rackTile = player.Rack.FindMatch(request.Letter, isBlank, request.AssignedLetter, reservedTileIds);
                if (rackTile is null) return new ValidationResult(false, "Tile is not in the player's rack.");
                var tile = isBlank
                    ? new Tile { Id = rackTile.Id, Letter = "?", PointValue = 0, IsBlank = true, AssignedLetter = request.AssignedLetter?.Trim().ToUpperInvariant() }
                    : rackTile.Clone();
                placements.Add(new Placement(request.Row, request.Column, tile));
            }

            var validation = new MoveValidator(dictionary).Validate(game.Board, new Move { Placements = placements });
            if (!validation.IsValid) return validation;
            var score = new Scorer().Score(game.Board, placements);
            return new ValidationResult(true, $"{score.Score}|{string.Join(",", score.Words)}");
        }
    }

    private static void FinishIfRackEmpty(GameState game, PlayerState playerWhoWentOut)
    {
        if (game.Bag.Remaining != 0 || playerWhoWentOut.Rack.Tiles.Count != 0) return;

        var playerTiles = playerWhoWentOut.Rack.Tiles.Sum(tile => tile.IsBlank ? 0 : tile.PointValue);
        var carried = 0;
        foreach (var player in game.Players.Where(player => player.Id != playerWhoWentOut.Id))
        {
            var remaining = player.Rack.Tiles.Sum(tile => tile.IsBlank ? 0 : tile.PointValue);
            player.Score -= remaining;
            carried += remaining;
        }
        playerWhoWentOut.Score += carried;
        game.Status = "Finished";
    }

    private static void FinishByPasses(GameState game)
    {
        foreach (var player in game.Players)
            player.Score -= player.Rack.Tiles.Sum(tile => tile.IsBlank ? 0 : tile.PointValue);
        game.Status = "Finished";
    }

    private void FillRack(Rack rack, TileBag bag)
    {
        var count = Rack.Capacity - rack.Tiles.Count;
        if (count > 0) foreach (var tile in bag.Draw(Math.Min(count, bag.Remaining))) rack.Add(tile);
    }

    private static void NextTurn(GameState game) => game.CurrentPlayerIndex = (game.CurrentPlayerIndex + 1) % game.Players.Count;

    private string GenerateJoinCode(Guid gameId)
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        while (true)
        {
            var code = new string(Enumerable.Range(0, 6).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
            if (joinCodes.TryAdd(code, gameId)) return code;
        }
    }
}
