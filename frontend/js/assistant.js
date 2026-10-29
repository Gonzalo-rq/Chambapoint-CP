import { api, getUser } from "./api.js";
import { escapeHtml } from "./components.js";

const QUICK_REPLIES = [
  "¿Cómo publico un trabajo?",
  "Busco un electricista cerca",
  "¿Cómo funciona el pago?",
  "Ver mis solicitudes",
];

let assistantInitialized = false;
let exchangeInitialized = false;

function formatReply(text = "") {
  return escapeHtml(String(text))
    .replace(/\*\*(.+?)\*\*/g, "<strong>$1</strong>")
    .replace(/\n/g, "<br>");
}

export function mountAssistant() {
  if (assistantInitialized || !document.body) return;
  if (location.pathname.endsWith("chat.html")) return;
  assistantInitialized = true;

  const user = getUser();

  document.body.insertAdjacentHTML(
    "beforeend",
    `<button type="button" class="fab" id="fabAssistant" aria-label="Abrir asistente ChambaBot" aria-expanded="false" aria-controls="assistantPanel">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">
        <rect x="3" y="6" width="18" height="13" rx="3" />
        <path d="M12 3v3" />
        <circle cx="9" cy="12.5" r="1.2" fill="currentColor" stroke="none" />
        <circle cx="15" cy="12.5" r="1.2" fill="currentColor" stroke="none" />
      </svg>
    </button>
    <aside class="assistant-panel" id="assistantPanel" role="dialog" aria-modal="false" aria-label="Asistente ChambaBot" hidden>
      <header class="assistant-header">
        <div class="assistant-title">
          <span class="assistant-avatar" aria-hidden="true">🤖</span>
          <div>
            <strong>ChambaBot</strong>
            <small>Asistente ChambaPoint</small>
          </div>
        </div>
        <button type="button" class="assistant-close" id="assistantClose" aria-label="Cerrar asistente">×</button>
      </header>
      <div class="assistant-messages" id="assistantMessages" aria-live="polite">
        <div class="assistant-msg bot">
          ${user ? `¡Hola ${escapeHtml(user.name || "de nuevo")}! 👋` : "¡Hola! 👋"}
          Soy <strong>ChambaBot</strong>. ¿En qué puedo ayudarte hoy?
        </div>
        <div class="quick-replies" id="quickReplies">
          ${QUICK_REPLIES.map((q) => `<button type="button" class="quick-reply" data-q="${escapeHtml(q)}">${escapeHtml(q)}</button>`).join("")}
        </div>
      </div>
      <form class="assistant-input" id="assistantForm" autocomplete="off">
        <input type="text" id="assistantInput" placeholder="Escribe tu pregunta..." aria-label="Mensaje para ChambaBot" />
        <button type="submit" class="assistant-send" id="assistantSend" aria-label="Enviar">➤</button>
      </form>
    </aside>`
  );

  const fab = document.getElementById("fabAssistant");
  const panel = document.getElementById("assistantPanel");
  const closeBtn = document.getElementById("assistantClose");
  const form = document.getElementById("assistantForm");
  const input = document.getElementById("assistantInput");
  const send = document.getElementById("assistantSend");
  const messages = document.getElementById("assistantMessages");
  const quick = document.getElementById("quickReplies");

  const setOpen = (open) => {
    panel.hidden = !open;
    fab.classList.toggle("active", open);
    fab.setAttribute("aria-expanded", String(open));
    if (open) input.focus();
  };

  fab.addEventListener("click", () => setOpen(panel.hidden));
  closeBtn.addEventListener("click", () => setOpen(false));

  quick.addEventListener("click", (e) => {
    const btn = e.target.closest(".quick-reply");
    if (btn) void ask(btn.dataset.q);
  });

  form.addEventListener("submit", (e) => {
    e.preventDefault();
    void ask(input.value);
  });

  async function ask(text) {
    const message = (text || "").trim();
    if (!message) return;

    input.value = "";
    quick.remove();
    messages.insertAdjacentHTML("beforeend", `<div class="assistant-msg user">${escapeHtml(message)}</div>`);
    const loadingId = `loading-${Date.now()}`;
    messages.insertAdjacentHTML(
      "beforeend",
      `<div class="assistant-msg bot" id="${loadingId}"><span class="dots"><i></i><i></i><i></i></span></div>`
    );
    messages.scrollTop = messages.scrollHeight;

    try {
      const data = await api("/api/assistant/chat", {
        method: "POST",
        body: { message },
        auth: false,
      });
      const el = document.getElementById(loadingId);
      if (el) el.innerHTML = formatReply(data.reply || "No tengo una respuesta para eso aún.");
    } catch {
      const el = document.getElementById(loadingId);
      if (el) {
        el.classList.add("error");
        el.textContent = "No pude responder ahora. Intenta de nuevo.";
      }
    }

    messages.scrollTop = messages.scrollHeight;
  }
}

export function mountExchangeRate() {
  if (exchangeInitialized) return;
  const header = document.querySelector(".app-header");
  if (!header) return;
  exchangeInitialized = true;

  const el = document.createElement("span");
  el.className = "exchange-badge";
  el.id = "exchangeBadge";
  el.hidden = true;
  el.title = "Tipo de cambio Soles (PEN) / Dólar (USD)";
  header.insertBefore(el, document.getElementById("userAvatar"));

  api("/api/utility/exchange-rate", { auth: false })
    .then((data) => {
      const rate = Number(data?.rate);
      if (!Number.isFinite(rate) || rate <= 0) {
        el.remove();
        return;
      }
      el.hidden = false;
      el.textContent = `1 USD = ${rate.toFixed(2)} PEN`;
    })
    .catch(() => el.remove());
}

export function mountSharedWidgets() {
  mountAssistant();
  mountExchangeRate();
}
