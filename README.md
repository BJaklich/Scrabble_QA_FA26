# Scrabble

Scrabble is a server-authoritative Scrabble web game built with ASP.NET Core, C#, SignalR, HTML, CSS, and JavaScript. The game enforces tile ownership, board placement, dictionary validation, turn order, premiums, scoring, and game end rules on the server.

The workspace currently uses `.NET 9` because that is the installed SDK. The application is an MVP and stores games in memory; restarting the server removes active games.

## Features

- 15 x 15 Scrabble-style board with premium squares
- Standard tile distribution and seven-tile racks
- First-word and connection validation
- Main-word and cross-word validation
- Letter, word, bingo, and blank-tile scoring
- Pass, exchange, and rack refill actions
- Server-generated move previews
- Optional computer opponent with automatic turns
- Join codes and session-based player state
- SignalR updates for multiplayer state changes
- Mouse/drag placement, click-to-place, and keyboard controls
- Move history and player score panel
- Embedded fallback dictionary plus external dictionary import and diff support

## Requirements

- .NET 9 SDK
- A modern browser

Verify the SDK with:

```bash
dotnet --version
```

## Run the Application

From the solution directory:

```bash
dotnet restore Scrabble.sln
dotnet run --project Scrabble.Web/Scrabble.Web.csproj
```

Open:

```text
http://localhost:5086
```

The HTTPS profile is also available in `Scrabble.Web/Properties/launchSettings.json` when needed.

## Play a Game

### Create a game

1. Click the menu button in the upper-right corner.
2. Enter a player name.
3. Optionally enable **Computer opponent**.
4. Click **Create game**.

The game screen displays a six-character join code and the player's rack.

### Join a multiplayer game

1. Enter a player name and the six-character join code.
2. Click **Join game**.

Two-player hotseat/multiplayer turn state is server-enforced by session tokens. Games can support up to four players.

### Place tiles

Choose a rack tile, then click or drag it onto the board. For keyboard play:

- Use letter keys to select a matching rack tile.
- Use arrow keys to move between board squares.
- Press Enter or Space to place the selected tile.

Pending tiles are visually distinct until the **Play** button is clicked. Use **Recall** to return pending tiles to the rack.

Blank tiles open a letter-selection dialog. Choose the letter the blank should represent before submission.

### Other actions

- **Shuffle:** Randomizes the visible rack order.
- **Recall:** Removes all pending tiles from the board.
- **Exchange:** Selects rack tiles with Shift-click and exchanges them when the bag has enough tiles.
- **Pass:** Ends the player's current turn without a play.
- **Play:** Sends the complete move to the server for validation and scoring.

## Dictionary Configuration

A small embedded fallback dictionary is included in `Scrabble.Core/Dictionary/words.txt`.

To use a full word list, configure an external plain-text file in `Scrabble.Web/appsettings.json`:

```json
{
  "Dictionary": {
    "FilePath": "C:/path/to/words.txt"
  }
}
```

The import formats are:

- Plain word list: replaces the fallback dictionary with that file.
- Diff list: uses the fallback plus additions/removals, where additions begin with `+` and removals begin with `-`.

Diff example:

```text
+CUP
+BITE
-XXZZZ
```

Lines beginning with `#` are comments. Words must use ASCII letters and contain no more than 15 letters.

Check the dictionary's license and compatibility before importing a published Scrabble word list.

## Tests

Run the complete test suite:

```bash
dotnet test Scrabble.Tests/Scrabble.Tests.csproj
```

Build the solution without running tests:

```bash
dotnet build Scrabble.sln
```

The test project includes xUnit unit tests, API integration tests with `WebApplicationFactory`, dictionary tests, scoring and validation tests, and tile-bag property/invariant coverage.

## Project Layout

```text
Scrabble.Core/
  Models/       Tile, board, rack, player, and move types
  Rules/        Validation, scoring, and tile bag
  Dictionary/   Dictionary interface, loader, and fallback words
  Ai/           Computer move generator

Scrabble.Web/
  Controllers/  REST game endpoints
  Hubs/         SignalR game hub
  Services/     In-memory game manager
  Dtos/         API request and response contracts
  wwwroot/      Browser UI, CSS, and JavaScript

Scrabble.Tests/
  CoreRulesTests.cs
  WebIntegrationTests.cs
```

## Known Limitations

- Games are in memory and disappear when the server restarts.
- Joining and game state are designed for a single server instance.
- There are no user accounts or persistent identity records.
- Multiplayer reconnection uses the stored session token, not a database-backed account.
- Real-world distribution is small only for the embedded fallback; production dictionary licensing must be handled separately.
