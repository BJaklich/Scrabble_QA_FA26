export async function api(path, options = {}) {
  const headers = { ...(options.headers || {}) };
  if (options.body !== undefined) headers['Content-Type'] = 'application/json';
  const response = await fetch(path, {
    headers,
    ...options
  });
  const body = response.status === 204 ? null : await response.json().catch(() => null);
  if (!response.ok) throw new Error(body?.error || body?.message || `Request failed (${response.status})`);
  return body;
}

export const createGame = (playerName, computerOpponent = false) => api('/api/games', {
  method: 'POST', body: JSON.stringify({ playerName, computerOpponent })
});

export const joinGame = (id, playerName) => api(`/api/games/${id}/join`, {
  method: 'POST', body: JSON.stringify({ playerName })
});

export const joinGameByCode = (joinCode, playerName) => api(`/api/games/join/${joinCode}`, {
  method: 'POST', body: JSON.stringify({ playerName })
});

export const getGame = (id, sessionToken) => api(`/api/games/${id}`, {
  headers: { 'X-Session-Token': sessionToken }
});

export const previewGame = (id, sessionToken, placements) => api(`/api/games/${id}/preview`, {
  method: 'POST', headers: { 'X-Session-Token': sessionToken, 'Content-Type': 'application/json' }, body: JSON.stringify(placements)
});

export const playGame = (id, sessionToken, placements) => api(`/api/games/${id}/play`, {
  method: 'POST',
  headers: { 'X-Session-Token': sessionToken, 'Content-Type': 'application/json' },
  body: JSON.stringify(placements)
});

export const playComputerTurn = (id, sessionToken) => api(`/api/games/${id}/computer-turn`, {
  method: 'POST', headers: { 'X-Session-Token': sessionToken }
});

export const exchangeGame = (id, sessionToken, tileIds) => api(`/api/games/${id}/exchange`, {
  method: 'POST',
  headers: { 'X-Session-Token': sessionToken },
  body: JSON.stringify({ tileIds })
});

export const passGame = (id, sessionToken) => api(`/api/games/${id}/pass`, {
  method: 'POST', headers: { 'X-Session-Token': sessionToken }
});
