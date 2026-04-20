const API_BASE = '';   // same origin — backend serves this file

// Conversation history sent with every request so Claude has context
let history = [];

// ── DOM refs ──────────────────────────────────────────────────────
const chatMessages = document.getElementById('chatMessages');
const messageInput  = document.getElementById('messageInput');
const sendBtn       = document.getElementById('sendBtn');
const favoritesList = document.getElementById('favoritesList');

// ── Send message ──────────────────────────────────────────────────
async function sendMessage() {
  const text = messageInput.value.trim();
  if (!text) return;

  appendMessage('user', text);
  messageInput.value = '';
  setLoading(true);

  const typingEl = appendTyping();

  try {
    const res = await fetch(`${API_BASE}/api/chat`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ message: text, history })
    });

    if (!res.ok) {
      const err = await res.text();
      throw new Error(err);
    }

    const data = await res.json();
    const reply = data.reply;

    // Update conversation history for next turn
    history.push({ role: 'user',      content: text  });
    history.push({ role: 'assistant', content: reply });

    typingEl.remove();
    appendMessage('assistant', reply);

    // Refresh favorites — a tool call may have just added/removed one
    await loadFavorites();

  } catch (err) {
    typingEl.remove();
    appendMessage('assistant', `⚠️ Error: ${err.message}`);
  } finally {
    setLoading(false);
  }
}

// ── Append a chat message bubble ──────────────────────────────────
function appendMessage(role, text) {
  const div = document.createElement('div');
  div.className = `message ${role}`;
  div.innerHTML = `<div class="bubble">${escapeHtml(text)}</div>`;
  chatMessages.appendChild(div);
  chatMessages.scrollTop = chatMessages.scrollHeight;
  return div;
}

// ── Typing indicator ──────────────────────────────────────────────
function appendTyping() {
  const div = document.createElement('div');
  div.className = 'message assistant typing';
  div.innerHTML = '<div class="bubble"><span class="dot"></span><span class="dot"></span><span class="dot"></span></div>';
  chatMessages.appendChild(div);
  chatMessages.scrollTop = chatMessages.scrollHeight;
  return div;
}

// ── Load favorites from API ───────────────────────────────────────
async function loadFavorites() {
  try {
    const res = await fetch(`${API_BASE}/api/favorites`);
    const data = await res.json();

    if (!data.length) {
      favoritesList.innerHTML = '<p class="empty-msg">No favorites yet.<br>Ask me to save a movie!</p>';
      return;
    }

    favoritesList.innerHTML = data.map(f => `
      <div class="favorite-card">
        <div class="fav-title">${escapeHtml(f.title)}</div>
        <div class="fav-meta">${escapeHtml(f.genre)} &bull; ${f.year}</div>
      </div>
    `).join('');

  } catch {
    // Silently ignore — favorites are a bonus feature
  }
}

// ── Helpers ───────────────────────────────────────────────────────
function setLoading(on) {
  sendBtn.disabled      = on;
  messageInput.disabled = on;
}

function escapeHtml(str) {
  return str
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/\n/g, '<br>');
}

// ── Genre chip ────────────────────────────────────────────────────
function sendGenre(genre) {
  messageInput.value = `Recommend a ${genre} movie`;
  sendMessage();
}

// ── Surprise me ───────────────────────────────────────────────────
function surpriseMe() {
  messageInput.value = 'Surprise me with a random movie recommendation';
  sendMessage();
}

// ── Clear chat ────────────────────────────────────────────────────
function clearChat() {
  history = [];
  chatMessages.innerHTML = `
    <div class="message assistant">
      <div class="bubble">
        Hey there! 🍿 I'm your Movie Night assistant. Ask me for recommendations,
        info about any film, or tell me to save a movie to your favorites!
        <br><br>
        <em>Try: "Recommend a sci-fi movie" or "Tell me about Inception"</em>
      </div>
    </div>`;
}

// ── Enter key support ─────────────────────────────────────────────
messageInput.addEventListener('keydown', e => {
  if (e.key === 'Enter' && !e.shiftKey) {
    e.preventDefault();
    sendMessage();
  }
});

// ── Init ──────────────────────────────────────────────────────────
loadFavorites();
