import { getToken, getUser, api } from "./api.js";
import { connectHub, onHub } from "./hub.js";

export function initials(name = "") {
  return name
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0].toUpperCase())
    .join("") || "?";
}

export function avatarHtml(user, sizeClass = "") {
  const url = user?.avatarUrl;
  if (url) {
    return `<img class="avatar ${sizeClass}" src="${escapeAttr(url)}" alt="" />`;
  }
  return `<span class="avatar ${sizeClass}" aria-hidden="true">${escapeHtml(initials(user?.name))}</span>`;
}

export function escapeHtml(str = "") {
  return String(str)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}

export function escapeAttr(str = "") {
  return escapeHtml(str);
}

export function starsHtml(rating = 0, count = null) {
  const full = Math.round(Number(rating) || 0);
  const stars = "★".repeat(Math.min(5, full)) + "☆".repeat(Math.max(0, 5 - full));
  const suffix = count != null ? ` <span class="text-muted">(${count})</span>` : "";
  return `<span class="review-stars" aria-label="${full} de 5">${stars}</span>${suffix}`;
}

export function statusBadge(status) {
  const map = {
    Pendiente: "badge-warning",
    Aceptada: "badge-success",
    Completada: "badge-info",
    Rechazada: "badge-danger",
    Nueva: "badge-warning",
  };
  return `<span class="badge ${map[status] || "badge-neutral"}">${escapeHtml(status)}</span>`;
}

export function updateUnreadBadge(count) {
  const num = Number(count) || 0;
  const badges = document.querySelectorAll(".messages-badge");
  badges.forEach((el) => {
    if (num > 0) {
      el.textContent = num > 99 ? "99+" : String(num);
      el.style.display = "inline-flex";
    } else {
      el.textContent = "0";
      el.style.display = "none";
    }
  });
}

export async function refreshUnreadCount() {
  const token = getToken();
  if (!token) return 0;
  try {
    const data = await api("/api/messages");
    const convs = data?.conversations || [];
    const total = convs.reduce((acc, c) => acc + (Number(c.unreadCount) || 0), 0);
    updateUnreadBadge(total);
    return total;
  } catch {
    return 0;
  }
}

export function showMessagePopup(payload = {}) {
  const senderId = payload.senderId;
  const senderName = payload.senderName || "Nuevo mensaje";
  const text = payload.text || "";
  const avatarUrl = payload.avatarUrl;

  let container = document.getElementById("messagePopupContainer");
  if (!container) {
    container = document.createElement("div");
    container.id = "messagePopupContainer";
    container.className = "message-popup-container";
    document.body.appendChild(container);
  }

  const popup = document.createElement("div");
  popup.className = "message-popup";
  popup.setAttribute("role", "alert");
  popup.setAttribute("aria-live", "assertive");

  const avatarContent = avatarUrl
    ? `<img src="${escapeAttr(avatarUrl)}" alt="" />`
    : `<span>${escapeHtml(initials(senderName))}</span>`;

  popup.innerHTML = `
    <div class="message-popup-inner">
      <div class="message-popup-avatar">${avatarContent}</div>
      <div class="message-popup-body">
        <div class="message-popup-header">
          <span class="message-popup-title">${escapeHtml(senderName)}</span>
          <span class="message-popup-tag">Mensaje</span>
        </div>
        <div class="message-popup-text">${escapeHtml(text)}</div>
      </div>
      <button type="button" class="message-popup-close" aria-label="Cerrar">&times;</button>
    </div>
  `;

  const closeBtn = popup.querySelector(".message-popup-close");
  const dismiss = () => {
    popup.classList.remove("message-popup-show");
    setTimeout(() => popup.remove(), 200);
  };

  closeBtn?.addEventListener("click", (e) => {
    e.stopPropagation();
    dismiss();
  });

  popup.addEventListener("click", () => {
    dismiss();
    if (senderId) {
      window.location.href = `chat.html?with=${encodeURIComponent(senderId)}`;
    } else {
      window.location.href = "conversations.html";
    }
  });

  container.appendChild(popup);
  requestAnimationFrame(() => popup.classList.add("message-popup-show"));
  setTimeout(dismiss, 5000);
}

let navNotificationsInitialized = false;

export function initNavNotifications() {
  const token = getToken();
  const user = getUser();
  if (!token || !user) return;

  refreshUnreadCount();

  if (navNotificationsInitialized) return;
  navNotificationsInitialized = true;

  connectHub().catch(() => null);

  onHub("newMessage", (payload) => {
    showMessagePopup(payload);
    refreshUnreadCount();
  });

  onHub("messagesRead", () => {
    refreshUnreadCount();
  });

  document.addEventListener("visibilitychange", () => {
    if (document.visibilityState === "visible") {
      refreshUnreadCount();
    }
  });
}

export function bottomNav(active = "explore", role = null) {
  const profileHref = role === "Customer" ? "profile.html" : "dashboard.html";
  const items = [
    {
      id: "explore",
      href: "explore.html",
      label: "Explorar",
      path: "M11 4a7 7 0 1 0 4.9 12l3.1 3.1 1.4-1.4-3.1-3.1A7 7 0 0 0 11 4zm0 2a5 5 0 1 1 0 10 5 5 0 0 1 0-10z",
    },
    {
      id: "requests",
      href: "requests.html",
      label: "Solicitudes",
      path: "M8 4h8a2 2 0 0 1 2 2v14l-6-3-6 3V6a2 2 0 0 1 2-2z",
    },
    {
      id: "messages",
      href: "conversations.html",
      label: "Mensajes",
      path: "M4 6a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H9l-5 4V6z",
    },
    {
      id: "profile",
      href: profileHref,
      label: "Perfil",
      path: "M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8zm-8 9a8 8 0 0 1 16 0H4z",
    },
  ];

  setTimeout(() => initNavNotifications(), 0);

  const badgeIcon = '<span class="nav-messages-badge messages-badge" id="messagesNavBadge" ' +
    'style="display:none;" aria-label="Mensajes no leídos">0</span>';
  const badgeLabel = '<span class="nav-label-badge messages-badge" id="messagesBadge" ' +
    'style="display:none;">0</span>';

  return `<nav class="bottom-nav" aria-label="Navegación principal">
    ${items
      .map(
        (it) => `<a href="${it.href}" class="${active === it.id ? "active" : ""}" data-nav="${it.id}">
      <span class="nav-icon">
        <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="${it.path}"/></svg>
        ${it.id === "messages" ? badgeIcon : ""}
      </span>
      <span>${it.label}${it.id === "messages" ? badgeLabel : ""}</span>
    </a>`
      )
      .join("")}
  </nav>`;
}
