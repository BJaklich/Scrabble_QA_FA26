import { createGame, joinGameByCode, getGame, previewGame, playGame, exchangeGame, passGame, playComputerTurn } from './api.js?v=2';
import { state, saveSession, clearSession } from './state.js?v=2';

const boardElement = document.querySelector('#board');
const rackElement = document.querySelector('#rack');
const playersElement = document.querySelector('#players');
const blankDialog = document.querySelector('#blankDialog');
const blankLetters = document.querySelector('#blankLetters');
const playButton = document.querySelector('#playButton');
const recallButton = document.querySelector('#recallButton');
const exchangeButton = document.querySelector('#exchangeButton');
const passButton = document.querySelector('#passButton');
const shuffleButton = document.querySelector('#shuffleButton');
const menuButton = document.querySelector('#menuButton');
const gameMenu = document.querySelector('#gameMenu');
const toastElement = document.querySelector('#toast');

let pendingBlank = null;
let selectedSquare = { row: 7, column: 7 };
let previewTimer = null;
let gameConnection = null;
let computerTurnPending = false;

function showToast(message, isError = false) {
  toastElement.textContent = message;
  toastElement.className = `toast show${isError ? ' error' : ''}`;
  clearTimeout(showToast.timer);
  showToast.timer = setTimeout(() => { toastElement.className = 'toast'; }, 2600);
}

function premiumLabel(square) {
  if (square.premium === 'TW') return 'TW';
  if (square.premium === 'DW') return 'DW';
  if (square.premium === 'TL') return 'TL';
  if (square.premium === 'DL') return 'DL';
  return '';
}

function renderBoard() {
  boardElement.replaceChildren();
  if (!state.game) {
    for (let index = 0; index < 225; index++) {
      const square = document.createElement('div');
      square.className = 'square';
      boardElement.append(square);
    }
    return;
  }

  for (const square of state.game.board) {
    const key = `${square.row},${square.column}`;
    const element = document.createElement('div');
    element.className = 'square';
    element.dataset.premium = square.premium;
    element.dataset.row = square.row;
    element.dataset.column = square.column;
    element.setAttribute('role', 'gridcell');
    element.setAttribute('aria-label', `Row ${square.row + 1}, column ${square.column + 1}${square.letter ? ', tile ' + square.letter : ''}`);

    if (!square.letter) {
      const label = premiumLabel(square);
      if (label) { const span = document.createElement('span'); span.className = 'premium'; span.textContent = label; element.append(span); }
    } else {
      element.append(createTile({ letter: square.letter, points: 0, id: key }, false));
    }

    const pending = state.pending.get(key);
    if (pending) {
      element.replaceChildren();
      element.classList.add('selected');
      element.append(createTile(pending.tile, true, key));
    }

    element.addEventListener('click', () => {
      selectedSquare = { row: square.row, column: square.column }; placeSelected(element);
    });
    element.addEventListener('keydown', event => {
      if (event.key === 'Enter' || event.key === ' ') { event.preventDefault(); selectedSquare = { row: square.row, column: square.column }; placeSelected(element); }
    });
    element.tabIndex = 0;
    element.addEventListener('dragover', event => event.preventDefault());
    element.addEventListener('drop', event => {
      event.preventDefault();
      const key = `${square.row},${square.column}`;
      const tileId = event.dataTransfer.getData('text/tile');
      if (tileId) state.selectedRackId = tileId;
      const pendingKey = event.dataTransfer.getData('text/key');
      if (pendingKey) {
        const pending = state.pending.get(pendingKey);
        if (pending) { state.pending.delete(pendingKey); state.pending.set(key, pending); }
      }
      placeSelected(element);
      renderBoard(); renderRack(); updateControls(); updatePreview();
    });
    boardElement.append(element);
  }
}

function createTile(tile, pending = false, key = null) {
  const element = document.createElement('button');
  element.type = 'button';
  element.className = 'tile';
  element.dataset.tileId = tile.id;
  element.setAttribute('aria-label', `${tile.letter}, ${tile.points} points`);
  element.innerHTML = `<span>${tile.letter === '?' ? '_' : tile.letter}</span><span class="points">${tile.points || ''}</span>`;
  element.draggable = true;

  if (pending) {
    element.addEventListener('click', event => {
      event.stopPropagation();
      recallPending(key);
    });
  } else if (tile.id && state.game?.rack?.some(rackTile => rackTile.id === tile.id)) {
    element.addEventListener('click', () => selectRackTile(tile.id));
  }

  element.addEventListener('dragstart', event => {
    if (pending) event.dataTransfer.setData('text/key', key);
    else event.dataTransfer.setData('text/tile', tile.id);
  });
  return element;
}

function renderRack() {
  rackElement.replaceChildren();
  if (!state.game?.rack) return;
  for (const tile of state.game.rack) {
    if ([...state.pending.values()].some(item => item.tile.id === tile.id)) continue;
    const element = createTile(tile);
    if (state.selectedRackId === tile.id) element.classList.add('selected');
    if (state.exchangeIds.has(tile.id)) element.classList.add('selected');
    element.addEventListener('click', event => {
      if (event.shiftKey) toggleExchange(tile.id);
      else selectRackTile(tile.id);
    });
    rackElement.append(element);
  }
}

function renderPlayers() {
  playersElement.replaceChildren();
  if (!state.game) return;
  for (const player of state.game.players) {
    const card = document.createElement('div');
    card.className = `player-card${player.id === state.game.currentPlayerId ? ' current' : ''}`;
    card.innerHTML = `<span class="player-name">${escapeHtml(player.name)}</span><span class="player-score">${player.score}</span><span class="player-count">${player.rackCount} tiles</span>`;
    playersElement.append(card);
  }
}

function escapeHtml(value) {
  const element = document.createElement('span');
  element.textContent = value;
  return element.innerHTML;
}

function selectRackTile(tileId) {
  state.selectedRackId = state.selectedRackId === tileId ? null : tileId;
  renderRack();
}

function placeSelected(square) {
  if (square.querySelector('.tile') && !state.pending.has(`${square.dataset.row},${square.dataset.column}`)) return;
  if (!state.selectedRackId) return;
  const tile = state.game.rack.find(candidate => candidate.id === state.selectedRackId);
  if (!tile) return;
  const key = `${square.dataset.row},${square.dataset.column}`;

  if (tile.isBlank) {
    pendingBlank = { key, tile };
    openBlankDialog();
    return;
  }
  state.pending.set(key, { tile });
  state.selectedRackId = null;
  renderBoard(); renderRack(); updateControls(); updatePreview();
}

function openBlankDialog() {
  blankLetters.replaceChildren();
  for (const letter of 'ABCDEFGHIJKLMNOPQRSTUVWXYZ') {
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = letter;
    button.addEventListener('click', () => {
      const copy = { ...pendingBlank.tile, letter: '?', assignedLetter: letter };
      state.pending.set(pendingBlank.key, { tile: copy });
      state.selectedRackId = null;
      pendingBlank = null;
      blankDialog.close();
      renderBoard(); renderRack(); updateControls(); updatePreview();
    });
    blankLetters.append(button);
  }
  blankDialog.showModal();
}

function recallPending(key) {
  state.pending.delete(key);
  renderBoard(); renderRack(); updateControls(); updatePreview();
}

function toggleExchange(tileId) {
  if (state.exchangeIds.has(tileId)) state.exchangeIds.delete(tileId);
  else state.exchangeIds.add(tileId);
  renderRack(); updateControls();
}

function updateControls() {
  const canAct = Boolean(state.game && state.playerId === state.game.currentPlayerId && state.game.status === 'Playing');
  const currentPlayer = state.game?.players?.find(player => player.id === state.game.currentPlayerId);
  const computerTurn = Boolean(state.game && currentPlayer?.name === 'Computer' && state.game.status === 'Playing');
  const loader = document.querySelector('#computerLoader');
  if (loader) loader.hidden = !computerTurn;
  playButton.disabled = !canAct || state.pending.size === 0;
  recallButton.disabled = state.pending.size === 0;
  exchangeButton.disabled = !canAct || state.exchangeIds.size === 0;
  passButton.disabled = !canAct;
  document.querySelector('#turnStatus').textContent = state.game
    ? (state.playerId === state.game.currentPlayerId ? 'Your turn' : `${state.game.players.find(p => p.id === state.game.currentPlayerId)?.name || 'Opponent'}'s turn`)
    : 'Create or join a game';
}

async function refresh() {
  if (!state.gameId || !state.playerId || !state.sessionToken) return;
  try {
    state.game = await getGame(state.gameId, state.sessionToken);
  } catch (error) {
    if (/Request failed \(404\)/.test(error.message)) {
      clearSession();
      state.game = null;
      renderBoard(); renderRack(); renderPlayers(); renderHistory(); updateControls();
      showToast('Session expired; create or join a game', true);
      return;
    }
    throw error;
  }
  document.querySelector('#gameTitle').textContent = `Game ${state.game.joinCode}`;
  renderBoard(); renderRack(); renderPlayers(); renderHistory(); updateControls();
  await connectGameHub();

  const current = state.game.players.find(player => player.id === state.game.currentPlayerId);
  if (current?.name === 'Computer' && state.game.status === 'Playing' && !computerTurnPending) {
    computerTurnPending = true;
    updateControls();
    setTimeout(async () => {
      try {
        await playComputerTurn(state.gameId, state.sessionToken);
      } catch (error) {
        showToast(error.message, true);
      } finally {
        computerTurnPending = false;
        await refresh();
      }
    }, 500);
  }
}

async function connectGameHub() {
  if (!window.signalR || !state.gameId || !state.sessionToken) return;
  if (gameConnection?.state === 'Connected') return;
  gameConnection = new signalR.HubConnectionBuilder().withUrl('/hubs/game').withAutomaticReconnect().build();
  gameConnection.on('GameStateUpdated', async () => {
    await refresh().catch(error => showToast(error.message, true));
  });
  gameConnection.onreconnected(async () => {
    await gameConnection.invoke('JoinGame', state.gameId, state.sessionToken);
    await refresh().catch(error => showToast(error.message, true));
  });
  try {
    await gameConnection.start();
    await gameConnection.invoke('JoinGame', state.gameId, state.sessionToken);
  } catch (error) {
    showToast('Real-time updates unavailable', true);
  }
}

function renderHistory() {
  const history = document.querySelector('#history');
  history.replaceChildren();
  if (!state.game?.history) return;
  for (const entry of state.game.history) {
    const element = document.createElement('div');
    element.className = 'history-entry';
    element.textContent = `${entry.playerName}: +${entry.score}${entry.words.length ? ` (${entry.words.join(', ')})` : ''}`;
    history.append(element);
  }
}

function updatePreview() {
  clearTimeout(previewTimer);
  previewTimer = setTimeout(async () => {
    if (!state.game || state.pending.size === 0) {
      document.querySelector('#previewScore').textContent = 'No preview';
      return;
    }
    const placements = [...state.pending.entries()].map(([key, item]) => {
      const [row, column] = key.split(',').map(Number);
      return { row, column, letter: item.tile.letter, assignedLetter: item.tile.assignedLetter || null };
    });
    try {
      const preview = await previewGame(state.gameId, state.sessionToken, placements);
      document.querySelector('#previewScore').textContent = `${preview.score} pts`;
    } catch (error) {
      document.querySelector('#previewScore').textContent = 'Invalid placement';
    }
  }, 120);
}

function showMenu() {
  gameMenu.hidden = !gameMenu.hidden;
  menuButton.setAttribute('aria-expanded', String(!gameMenu.hidden));
}

async function createGameHandler() {
  try {
    const name = document.querySelector('#playerName').value.trim();
    const computerOpponent = document.querySelector('#computerOpponent').checked;
    const result = await createGame(name, computerOpponent);
    state.gameId = result.id; state.playerId = result.playerId; state.sessionToken = result.sessionToken;
    saveSession(); gameMenu.hidden = true; await refresh(); showToast('Game created');
  } catch (error) { showToast(error.message, true); }
}

async function joinGameHandler() {
  try {
    const name = document.querySelector('#playerName').value.trim();
    const code = document.querySelector('#joinCode').value.trim().toUpperCase();
    if (!name || !code) throw new Error('Name and join code are required');
    const result = await joinGameByCode(code, name);
    state.gameId = result.gameId;
    state.playerId = result.playerId;
    state.sessionToken = result.sessionToken;
    saveSession();
    await refresh();
    gameMenu.hidden = true;
    showToast('Joined game');
  } catch (error) { showToast(error.message, true); }
}

async function playHandler() {
  try {
    const placements = [...state.pending.entries()].map(([key, item]) => {
      const [row, column] = key.split(',').map(Number);
      return { row, column, letter: item.tile.letter, assignedLetter: item.tile.assignedLetter || null };
    });
    const result = await playGame(state.gameId, state.sessionToken, placements);
    state.pending.clear(); state.exchangeIds.clear(); await refresh(); showToast(result.message || 'Move played');
  } catch (error) { showToast(error.message, true); }
}

async function exchangeHandler() {
  try {
    await exchangeGame(state.gameId, state.sessionToken, [...state.exchangeIds]);
    state.exchangeIds.clear(); await refresh(); showToast('Tiles exchanged');
  } catch (error) { showToast(error.message, true); }
}

async function passHandler() {
  try { await passGame(state.gameId, state.sessionToken); await refresh(); showToast('Turn passed'); }
  catch (error) { showToast(error.message, true); }
}

recallButton.addEventListener('click', () => { state.pending.clear(); renderBoard(); renderRack(); updateControls(); updatePreview(); });
shuffleButton.addEventListener('click', () => {
  if (!state.game?.rack) return;
  for (let index = state.game.rack.length - 1; index > 0; index--) {
    const swapIndex = Math.floor(Math.random() * (index + 1));
    [state.game.rack[index], state.game.rack[swapIndex]] = [state.game.rack[swapIndex], state.game.rack[index]];
  }
  renderRack();
});
menuButton.addEventListener('click', showMenu);
document.querySelector('#createButton').addEventListener('click', createGameHandler);
document.querySelector('#joinButton').addEventListener('click', joinGameHandler);
playButton.addEventListener('click', playHandler);
exchangeButton.addEventListener('click', exchangeHandler);
passButton.addEventListener('click', passHandler);

document.addEventListener('keydown', event => {
  if (/^[a-z]$/i.test(event.key) && !event.ctrlKey && !event.metaKey && !event.altKey) {
    const tile = state.game?.rack?.find(candidate => candidate.letter.toUpperCase() === event.key.toUpperCase());
    if (tile) selectRackTile(tile.id);
  }
});

boardElement.addEventListener('keydown', event => {
  if (!['ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight'].includes(event.key)) return;
  event.preventDefault();
  if (event.key === 'ArrowUp') selectedSquare.row = Math.max(0, selectedSquare.row - 1);
  if (event.key === 'ArrowDown') selectedSquare.row = Math.min(14, selectedSquare.row + 1);
  if (event.key === 'ArrowLeft') selectedSquare.column = Math.max(0, selectedSquare.column - 1);
  if (event.key === 'ArrowRight') selectedSquare.column = Math.min(14, selectedSquare.column + 1);
  boardElement.querySelector(`[data-row="${selectedSquare.row}"][data-column="${selectedSquare.column}"]`)?.focus();
});

if (state.gameId && state.playerId && state.sessionToken) refresh().catch(error => showToast(error.message, true));
else {
  clearSession();
  renderBoard(); renderRack(); renderPlayers(); renderHistory(); updateControls();
}
