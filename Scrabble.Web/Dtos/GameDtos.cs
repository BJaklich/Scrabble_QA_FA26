namespace Scrabble.Web.Dtos;

public sealed record CreateGameRequest(string? PlayerName, bool ComputerOpponent = false);
public sealed record JoinGameRequest(string? PlayerName);
public sealed record PlacementRequest(int Row, int Column, string Letter, string? AssignedLetter = null);
public sealed record ExchangeRequest(IReadOnlyList<Guid> TileIds);

public sealed record TileDto(string Letter, int Points, bool IsBlank, Guid Id);
public sealed record PlayerDto(Guid Id, string Name, int Score, int RackCount);
public sealed record SquareDto(int Row, int Column, string Premium, string? Letter);
public sealed record MoveHistoryDto(string PlayerName, int Score, IReadOnlyList<string> Words);
public sealed record GameDto(Guid Id, string JoinCode, string Status, Guid CurrentPlayerId, IReadOnlyList<PlayerDto> Players, IReadOnlyList<SquareDto> Board, IReadOnlyList<TileDto>? Rack, IReadOnlyList<MoveHistoryDto> History);

public sealed record CreateGameResponse(Guid Id, string JoinCode, Guid PlayerId, Guid SessionToken);
public sealed record JoinGameResponse(Guid PlayerId, string Name, Guid SessionToken);
public sealed record JoinByCodeResponse(Guid GameId, Guid PlayerId, string Name, Guid SessionToken);
