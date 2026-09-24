import { api } from "../api.js";
import { requireAuth, logout } from "../auth.js";
import { toast, setLoading, showSkeleton } from "../ui.js";
import { avatarHtml, escapeHtml, statusBadge, bottomNav } from "../components.js";
import { connectHub, onHub, disconnectHub } from "../hub.js";

const user = requireAuth(["Worker"]);
if (!user) throw new Error("Sin sesión");

document.getElementById("userAvatar").outerHTML = avatarHtml(user);
document.getElementById("heroAvatar").outerHTML = avatarHtml(user, "avatar-lg");
document.getElementById("logoutBtn").addEventListener("click", () => logout());
document.body.insertAdjacentHTML("beforeend", bottomNav("profile"));

const countersEl = document.getElementById("counters");
const chartEl = document.getElementById("weekChart");
const pendingEl = document.getElementById("pendingAppts");
const activityEl = document.getElementById("recentActivity");

showSkeleton(pendingEl, 2);
showSkeleton(activityEl, 3);

let loadSeq = 0;

async function load() {
  const seq = ++loadSeq;
  try {
    const d = await api("/api/dashboard/worker");
    if (seq !== loadSeq) return;
    renderHeader(d);
    renderEarnings(d.weeklyEarnings, d.weeklyChart || []);
    renderCounters(d.counters || {});
    renderPending(d.pendingAppointments || []);
    renderActivity(d.recentActivity || []);
  } catch (err) {
    if (seq !== loadSeq) return;
    pendingEl.innerHTML = `<div class="empty-state"><h3>No pudimos cargar</h3><p>${escapeHtml(err.message)}</p></div>`;
    activityEl.innerHTML = "";
    toast(err.message, "error");
  }
}

function renderHeader(d) {
  const name = d.worker?.name || user.name || "";
  document.getElementById("helloName").textContent = `Hola, ${name}`;
  document.getElementById("dashTitle").textContent = d.worker?.profession || "Resumen de chambas";
}

function renderEarnings(earn, chart) {
  document.getElementById("earnTotal").textContent = earn?.formatted || "S/ 0.00";
  document.getElementById("earnPeriod").textContent = earn?.period || "";
  const max = Math.max(...chart.map((c) => Number(c.amount) || 0), 1);
  chartEl.innerHTML = chart
    .map((c) => {
      const amount = Number(c.amount) || 0;
      const h = Math.max(8, Math.round((amount / max) * 72));
      const today = new Date().toISOString().slice(0, 10);
      const active = c.date === today ? "is-today" : "";
      return `
        <div class="bar-col ${active}" title="${escapeHtml(c.formatted || "")}">
          <div class="bar" style="height:${h}px"></div>
          <span>${escapeHtml(c.day || "")}</span>
        </div>`;
    })
    .join("");
}

function renderCounters(c) {
  const items = [
    { label: "Pendientes", value: c.pendingRequests, tone: "warning" },
    { label: "Confirmadas", value: c.confirmedRequests, tone: "success" },
    { label: "Completadas", value: c.completedRequests, tone: "info" },
    { label: "Citas nuevas", value: c.pendingAppointments, tone: "accent" },
    { label: "Citas ok", value: c.confirmedAppointments, tone: "success" },
  ];
  countersEl.innerHTML = items
    .map(
      (it) => `
    <article class="counter-card tone-${it.tone}">
      <span class="counter-value">${Number(it.value) || 0}</span>
      <span class="counter-label">${escapeHtml(it.label)}</span>
    </article>`
    )
    .join("");
}

function renderPending(items) {
  if (!items.length) {
    pendingEl.innerHTML = `<div class="empty-state compact"><h3>Sin citas por confirmar</h3><p>Aparecerán aquí cuando un cliente agende.</p></div>`;
    return;
  }
  pendingEl.innerHTML = items
    .map(
      (a) => `
    <article class="appt-card" data-id="${a.id}">
      <div class="appt-head">
        <div>
          <h3>${escapeHtml(a.title || "Cita")}</h3>
          <p class="text-muted">${escapeHtml(a.formattedDate || "")} · ${escapeHtml(a.category || "")}</p>
        </div>
        ${statusBadge(a.status)}
      </div>
      ${a.customerName ? `<p class="appt-customer">Cliente: <strong>${escapeHtml(a.customerName)}</strong></p>` : ""}
      ${a.description ? `<p class="appt-desc">${escapeHtml(a.description)}</p>` : ""}
      <div class="appt-actions">
        <button type="button" class="btn btn-primary btn-sm" data-appt-action="Aceptada">Aceptar</button>
        <button type="button" class="btn btn-danger btn-sm" data-appt-action="Rechazada">Rechazar</button>
      </div>
    </article>`
    )
    .join("");

  pendingEl.querySelectorAll("[data-appt-action]").forEach((btn) => {
    btn.addEventListener("click", async () => {
      const card = btn.closest("[data-id]");
      const id = card.dataset.id;
      const status = btn.dataset.apptAction;
      setLoading(btn, true, "...");
      try {
        await api(`/api/appointments/${id}/status`, { method: "PATCH", body: { status } });
        toast(`Cita ${status.toLowerCase()}`, "success");
        await load();
      } catch (err) {
        toast(err.message, "error");
      } finally {
        setLoading(btn, false);
      }
    });
  });
}

function renderActivity(items) {
  if (!items.length) {
    activityEl.innerHTML = `<div class="empty-state compact"><h3>Sin actividad</h3><p>Cuando aceptes chambas verás el historial.</p></div>`;
    return;
  }
  activityEl.innerHTML = items
    .map(
      (r) => `
    <article class="activity-card">
      <div class="activity-dot"></div>
      <div class="activity-body">
        <h3>${escapeHtml(r.title || "Solicitud")}</h3>
        <p class="text-muted">${escapeHtml(r.subtitle || "")}</p>
      </div>
      <div class="activity-side">
        <strong>${escapeHtml(r.amountFormatted || "")}</strong>
        ${statusBadge(r.status)}
      </div>
    </article>`
    )
    .join("");
}

onHub("newRequest", () => load());
onHub("appointmentStatusChanged", () => load());
onHub("newAppointment", () => load());

connectHub().catch(() => null).then(() => load());
window.addEventListener("beforeunload", () => disconnectHub());
