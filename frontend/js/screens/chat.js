import { api } from "../api.js";
import { requireAuth } from "../auth.js";
import { toast, setLoading, showSkeleton } from "../ui.js";
import { avatarHtml, escapeHtml } from "../components.js";
import { connectHub, onHub, disconnectHub } from "../hub.js";

const user = requireAuth();
if (!user) throw new Error("Sin sesión");

const params = new URLSearchParams(window.location.search);
const withUserId = Number(params.get("with"));
if (!withUserId) {
  window.location.href = "conversations.html";
}

const headerName = document.getElementById("peerName");
const headerSub = document.getElementById("peerSub");
const headerAvatar = document.getElementById("peerAvatar");
const messagesEl = document.getElementById("messages");
const form = document.getElementById("chatForm");
const input = document.getElementById("chatInput");
const sendBtn = document.getElementById("sendBtn");

let peer = null;
let requestId = Number(params.get("requestId")) || null;
let page = 1;
const pageSize = 50;
let loadingMore = false;

async function loadPeerAndHistory() {
  showSkeleton(messagesEl, 4);
  try {
    const data = await api(`/api/messages?withUserId=${withUserId}&page=1&pageSize=${pageSize}`);
    const first = (data.items || [])[0];
    peer = first?.senderId === withUserId ? first.sender : first?.receiver;
    if (peer?.id === user.id || peer?.Id === user.id) {
      peer = first?.senderId === withUserId ? first.receiver : first.sender;
    }
    if (!peer) {
      peer = { id: withUserId, name: `Usuario #${withUserId}` };
    }
    renderHeader(peer);
    renderMessages(data.items || []);
    if (data.markedAsRead) {
      /* already handled by API */
    }
  } catch (err) {
    messagesEl.innerHTML = `<div class="empty-state"><h3>No pudimos cargar</h3><p>${escapeHtml(err.message)}</p></div>`;
    toast(err.message, "error");
  }
}

function renderHeader(p) {
  headerName.textContent = p.name || `Usuario #${withUserId}`;
  headerSub.textContent = p.avatarUrl || "Chat en vivo";
  headerAvatar.outerHTML = avatarHtml(p, "avatar-sm");
}

function renderMessages(items) {
  if (!items.length) {
    messagesEl.innerHTML = `<div class="empty-state"><h3>Sin mensajes</h3><p>Escribe el primero para empezar.</p></div>`;
    return;
  }
  messagesEl.innerHTML = items
    .map((m) => {
      const mine = m.senderId === user.id;
      const time = new Date(m.sentAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
      return `
      <div class="bubble-row ${mine ? "mine" : ""}">
        <div class="bubble">
          <p>${escapeHtml(m.text)}</p>
          <span class="bubble-time">${escapeHtml(time)}${mine && m.isRead ? " · ✓✓" : ""}</span>
        </div>
      </div>`;
    })
    .join("");
  messagesEl.scrollTop = messagesEl.scrollHeight;
}

async function appendMessage(m) {
  const mine = m.senderId === user.id;
  const time = new Date(m.sentAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
  const empty = messagesEl.querySelector(".empty-state");
  if (empty) empty.remove();
  messagesEl.insertAdjacentHTML(
    "beforeend",
    `<div class="bubble-row ${mine ? "mine" : ""}">
      <div class="bubble">
        <p>${escapeHtml(m.text)}</p>
        <span class="bubble-time">${escapeHtml(time)}</span>
      </div>
    </div>`
  );
  messagesEl.scrollTop = messagesEl.scrollHeight;
}

async function loadOlder() {
  if (loadingMore) return;
  loadingMore = true;
  page += 1;
  try {
    const data = await api(`/api/messages?withUserId=${withUserId}&page=${page}&pageSize=${pageSize}`);
    const items = data.items || [];
    if (items.length) {
      const html = items
        .map((m) => {
          const mine = m.senderId === user.id;
          const time = new Date(m.sentAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
          return `<div class="bubble-row ${mine ? "mine" : ""}">
            <div class="bubble">
              <p>${escapeHtml(m.text)}</p>
              <span class="bubble-time">${escapeHtml(time)}</span>
            </div>
          </div>`;
        })
        .join("");
      messagesEl.insertAdjacentHTML("afterbegin", html);
    }
  } catch {
    /* ignore older page errors */
  } finally {
    loadingMore = false;
  }
}

form.addEventListener("submit", async (e) => {
  e.preventDefault();
  const text = input.value.trim();
  if (!text) return;
  setLoading(sendBtn, true, "Enviando...");
  try {
    const body = { receiverId: withUserId, text };
    if (requestId) body.requestId = requestId;
    const created = await api("/api/messages", { method: "POST", body });
    input.value = "";
    await appendMessage(created);
  } catch (err) {
    toast(err.message, "error");
  } finally {
    setLoading(sendBtn, false);
  }
});

messagesEl.addEventListener("scroll", () => {
  if (messagesEl.scrollTop <= 40) loadOlder();
});

onHub("newMessage", (payload) => {
  if (payload.senderId === withUserId) {
    appendMessage({
      senderId: payload.senderId,
      text: payload.text,
      sentAt: payload.sentAt,
      isRead: false,
    });
    api("/api/messages/read", { method: "POST", body: { withUserId } }).catch(() => {});
  }
});

onHub("messagesRead", () => {
  /* visual cue only */
});

loadPeerAndHistory().then(() => connectHub());
window.addEventListener("beforeunload", () => disconnectHub());
