# Scrabble Web App Plan (C# + HTML/JS)

## 1. Architecture

**Recommended stack**

- **Backend:** ASP.NET Core (.NET 8) Web API + **SignalR** for real-time multiplayer
- **Frontend:** Vanilla HTML/CSS/JS (ES modules), served as static files from `wwwroot`, so there's no separate build step
- **Storage:** In-memory for the MVP, then SQLite via EF Core for persistence
- **Tests:** xUnit for game logic

**Key principle:** the server is authoritative. The client only sends *intents* ("place these tiles here"), and the server validates, scores, and broadcasts the new state. This prevents cheating and keeps the rules in one place.

## 2. Solution Structure

```
Scrabble.sln
├── Scrabble.Core/            # Pure C# class library, no web dependencies
│   ├── Models/               # Tile, Square, Board, Rack, Player, Game
│   ├── Rules/                # MoveValidator, Scorer, TileBag, EndGameRules
│   ├── Dictionary/           # IWordDictionary, HashSetDictionary (or GADDAG/Trie)
│   └── Ai/                   # Optional: MoveGenerator for a computer opponent
├── Scrabble.Web/             # ASP.NET Core host
│   ├── Controllers/          # GamesController (REST)
│   ├── Hubs/                 # GameHub (SignalR)
│   ├── Services/             # GameManager, GameRepository
│   ├── Dtos/                 # Request/response contracts
│   └── wwwroot/              # index.html, css/, js/
└── Scrabble.Tests/           # Unit + integration tests
```

Keeping `Scrabble.Core` free of web dependencies makes it easy to test and reuse.

## 3. Domain Model

| Class | Responsibility |
|---|---|
| `Tile` | Letter, point value, `IsBlank`, `AssignedLetter` |
| `Square` | Premium type (`Normal`, `DL`, `TL`, `DW`, `TW`, `Center`), optional placed `Tile` |
| `Board` | 15×15 grid, with premium layout defined as data |
| `TileBag` | 100 tiles with standard distribution, `Draw(n)`, `Exchange(tiles)` |
| `Rack` | Up to 7 tiles per player |
| `Player` | Id, name, rack, score |
| `Game` | Board, bag, players, current turn, move history, status |
| `Move` | List of `(row, col, tile)` placements plus the move type (Play, Exchange, Pass) |

Store the premium square layout and tile distribution as constants or JSON so variants (e.g., Super Scrabble) can be added later.

## 4. Game Logic (the core work)

### Move validation

1. All tiles lie in a **single row or column**
2. Tiles land on **empty squares** within bounds
3. Placement is **contiguous**, counting existing tiles between new ones
4. First move covers the **center square**; later moves **connect** to existing tiles
5. All **words formed are valid**: the main word plus every cross-word
6. Player actually **owns** the tiles (checked against the server-side rack)

### Scoring

- Premium squares apply **only to newly placed tiles**
- Letter multipliers first, then word multipliers (which stack, e.g., DW×DW = ×4)
- Blank tiles score 0
- Add every cross-word's score
- **50-point bonus** for using all 7 tiles

### Turn flow and game end

- Play, pass, or exchange (exchange only if the bag has at least 7 tiles)
- Refill rack after each play
- Game ends when the bag is empty and a player empties their rack, or after several consecutive passes
- Final adjustment: subtract remaining tile values from each player, and give the sum to the player who went out

### Dictionary

- Load a word list (e.g., TWL/NWL or SOWPODS/CSW) into a `HashSet<string>` at startup. This is fast enough for validation.
- Check licensing for whichever word list you pick.
- Upgrade to a **GADDAG or Trie** only if you build an AI that generates moves.

## 5. API Design

**REST (setup and queries)**

- `POST /api/games` creates a game and returns a join code
- `POST /api/games/{id}/join` joins a game
- `GET /api/games/{id}` returns the state visible to the requesting player (own rack only)

**SignalR hub (gameplay)**

- Client → Server: `PlayMove(gameId, placements)`, `ExchangeTiles(gameId, tiles)`, `Pass(gameId)`, `PreviewMove(...)` (optional live score)
- Server → Client: `GameStateUpdated`, `MoveRejected(reason)`, `PlayerJoined`, `GameEnded`

**Important:** never send opponents' racks or the bag contents to clients. Build a per-player `GameStateDto`.

## 6. Frontend Plan

### Layout

- 15×15 CSS grid board with colored premium squares and labels
- Rack below the board, plus a score panel, turn indicator, and move history
- Buttons: **Play**, **Recall**, **Shuffle**, **Exchange**, **Pass**

### Interactions

- **Drag-and-drop** tiles from rack to board (Pointer Events so it works on touch), with click-to-select-then-click-to-place as a fallback
- Pending tiles show visually distinct from committed ones, and can be dragged back
- Blank tile prompts for a letter
- Live move preview: highlight the word and show the potential score (client-side estimate or server `PreviewMove`)
- Error toasts for rejected moves, with clear reasons

### JS module layout

```
js/
├── main.js          # bootstrap
├── api.js           # fetch + SignalR connection
├── state.js         # client game state store
├── board.js         # render board, handle drops
├── rack.js          # render and reorder rack
└── ui.js            # modals, toasts, scoreboard
```

### Accessibility

- Keyboard placement (arrow keys + letter keys), ARIA labels on squares, and sufficient color contrast beyond premium-square colors alone

## 7. Milestones

1. **Core rules (1–2 weeks):** Board, TileBag, Scorer, MoveValidator, dictionary, with a solid xUnit suite (premium squares, cross-words, blanks, bingos)
2. **Single-session hotseat (1 week):** REST API + static UI; two players share one browser
3. **Real-time multiplayer (1 week):** SignalR, join codes, per-player state, reconnect handling
4. **Polish (1 week):** Drag-and-drop refinements, animations, move history, mobile layout, end-game flow
5. **Persistence and accounts (optional):** EF Core + SQLite, resume games, simple identity
6. **AI opponent (optional):** Move generation with a GADDAG, difficulty levels by choosing from ranked moves

## 8. Testing Strategy

- **Unit tests** for scoring edge cases, such as a tile on a DW completing two words, blank on a premium square, and a 7-tile bingo
- **Validation tests**: gaps, disconnected plays, invalid cross-words, wrong first move
- **Integration tests** with `WebApplicationFactory` for the API and hub
- **Property/simulation tests:** play random legal games to completion and assert invariants (100 tiles always accounted for, scores non-negative)

## 9. Risks and Decisions to Make Early

| Topic | Decision |
|---|---|
| Word list | Which dictionary, and its licensing |
| Branding | "Scrabble" is a trademark, so consider a different name if you'll publish |
| Persistence | In-memory loses games on restart; decide when to add a database |
| Concurrency | Lock per game (or use a per-game actor/channel) to avoid races |
| Reconnects | Use stable player tokens rather than connection IDs |
| Scale | A single server instance is fine to start; SignalR backplane (Redis) if you scale out |

## 10. Suggested First Steps

1. Create the solution and `Scrabble.Core` project
2. Implement `Board` with the premium layout and `TileBag` with the standard distribution
3. Write `MoveValidator` and `Scorer` test-first
4. Stand up the Web API with an in-memory `GameManager`
5. Build the static board UI against a hardcoded game state, then wire it up