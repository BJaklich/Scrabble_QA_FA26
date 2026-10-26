const storedSession = localStorage.getItem('word-square-session');
if (!storedSession) {
  localStorage.removeItem('word-square-game');
  localStorage.removeItem('word-square-player');
}

export const state = {
  gameId: localStorage.getItem('word-square-game') || null,
  playerId: localStorage.getItem('word-square-player') || null,
  sessionToken: localStorage.getItem('word-square-session') || null,
  game: null,
  pending: new Map(),
  selectedRackId: null,
  exchangeIds: new Set()
};

export function clearSession() {
  state.gameId = null;
  state.playerId = null;
  state.sessionToken = null;
  localStorage.removeItem('word-square-game');
  localStorage.removeItem('word-square-player');
  localStorage.removeItem('word-square-session');
}

export function saveSession() {
  if (state.gameId) localStorage.setItem('word-square-game', state.gameId);
  if (state.playerId) localStorage.setItem('word-square-player', state.playerId);
  if (state.sessionToken) localStorage.setItem('word-square-session', state.sessionToken);
}
