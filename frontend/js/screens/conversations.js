import { api } from "../api.js";
import { requireAuth } from "../auth.js";
import { toast, showSkeleton } from "../ui.js";
import { avatarHtml, escapeHtml, bottomNav } from "../components.js";
import { connectHub, onHub, disconnectHub } from "../hub.js";

const user = requireAuth();
if (!user) throw new Error("Sin sesión");

document.getElementById("userAvatar").outerHTML = avatarHtml(user);
document.body.insertAdjacentHTML("beforeend", bottomNav("messages"));

const listEl = document.getElementById("convList");
const searchInput = document.getElementById("convSearch");
const emptyEl = document.getElementById("convEmpty");

let conversations = [];

async function load() {
  showSkeleton(listEl, 4);
  try {
    const data = await api("/api/messages");
    conversations = data.conversations || [];
    render(conversations);
  } catch (err) {
    listEl.innerHTML = `<div class="empty-state"><h3>No pudimos cargar</h3><p>${escapeHtml(err.message)}</p></div>`;
    toast(err.message, "error");
  }
}

function render(items) {
  const q = (searchInput.value || "").trim().toLowerCase();
  const filtered = q
    ? items.filter(
        (c) =>
          (c.partnerName || "").toLowerCase().includes(q) ||
          (c.lastMessage?.text || "").toLowerCase().includes(q)
      )
    : items;

  emptyEl.hidden = filtered.length > 0;
  if (!filtered.length) {
    listEl.innerHTML = "";
    return;
  }

  listEl.innerHTML = filtered
    .map((c) => {
      const last = c.lastMessage;
      const preview = last ? last.text : "Sin mensajes aún";
      const time = last?.sentAt ? formatTime(last.sentAt) : "";
      const unread = Number(c.unreadCount) || 0;
      return `
      <a class="conv-item ${unread ? "is-unread" : ""}" href="chat.html?with=${c.partnerId}">
        ${avatarHtml({ name: c.partnerName, avatarUrl: c.partnerAvatar })}
        <div class="conv-body">
          <div class="conv-top">
            <strong>${escapeHtml(c.partnerName)}</strong>
            <span class="conv-time">${escapeHtml(time)}</span>
          </div>
          <div class="conv-bottom">
            <span class="conv-preview">${last?.isFromMe ? "Tú: " : ""}${escapeHtml(preview)}</span>
            ${unread ? `<span class="conv-badge">${unread}</span>` : ""}
          </div>
          ${c.profession ? `<span class="conv-role">${escapeHtml(c.profession)}</span>` : ""}
        </div>
      </a>`;
    })
    .join("");
}

function formatTime(iso) {
  const d = new Date(iso);
  const now = new Date();
  const sameDay = d.toDateString() === now.toDateString();
  if (sameDay) return d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
  return d.toLocaleDateString([], { day: "2-digit", month: "2-digit" });
}

searchInput.addEventListener("input", () => render(conversations));

onHub("newMessage", () => {
  load();
});

connectHub().then(() => load());
window.addEventListener("beforeunload", () => disconnectHub());
