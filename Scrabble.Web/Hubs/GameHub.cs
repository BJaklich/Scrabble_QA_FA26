using Microsoft.AspNetCore.SignalR;
using Scrabble.Web.Services;

namespace Scrabble.Web.Hubs;

public sealed class GameHub(GameManager games) : Hub
{
    public async Task JoinGame(Guid gameId, Guid sessionToken)
    {
        var game = games.Get(gameId);
        Guid publicPlayerId;
        lock (game.Lock)
        {
            var player = game.Players.SingleOrDefault(candidate => candidate.SessionToken == sessionToken);
            if (player is null) throw new HubException("Invalid player session.");
            publicPlayerId = player.Id;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, gameId.ToString());
        await Clients.OthersInGroup(gameId.ToString()).SendAsync("PlayerJoined", publicPlayerId);
    }
}
