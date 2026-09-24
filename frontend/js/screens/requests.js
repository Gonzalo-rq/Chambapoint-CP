import { api } from "../api.js";
import { requireAuth } from "../auth.js";
import { toast, setLoading, showSkeleton, fieldError } from "../ui.js";
import { avatarHtml, escapeHtml, statusBadge, bottomNav } from "../components.js";

const user = requireAuth();
if (!user) throw new Error("Sin sesión");

const listEl = document.getElementById("requestsList");
const apptListEl = document.getElementById("appointmentsList");
const apptTitle = document.getElementById("apptTitle");
const modalRoot = document.getElementById("modalRoot");
const tabs = document.querySelectorAll(".tab-btn");

document.getElementById("userAvatar").outerHTML = avatarHtml(user);
document.body.insertAdjacentHTML("beforeend", bottomNav("requests"));

let statusFilter = "activas";
let requestsCache = [];

tabs.forEach((tab) => {
  tab.addEventListener("click", () => {
    tabs.forEach((t) => {
      t.classList.remove("active");
      t.setAttribute("aria-selected", "false");
    });
    tab.classList.add("active");
    tab.setAttribute("aria-selected", "true");
    statusFilter = tab.dataset.status;
    load();
  });
});

async function load() {
  showSkeleton(listEl, 3);
  try {
    const data = await api(`/api/requests?status=${statusFilter}&pageSize=20`);
    requestsCache = data.items || [];
    renderRequests(requestsCache);
    await loadAppointments();
  } catch (err) {
    listEl.innerHTML = `<div class="empty-state"><h3>No pudimos cargar</h3><p>${escapeHtml(err.message)}</p></div>`;
    toast(err.message, "error");
  }
}

function renderRequests(items) {
  if (!items.length) {
    listEl.innerHTML = `<div class="empty-state"><h3>Sin solicitudes</h3><p>${
      statusFilter === "activas" ? "Explorar y solicita un servicio." : "Aún no hay historial."
    }</p><p style="margin-top:12px"><a class="btn btn-primary" href="explore.html">Explorar</a></p></div>`;
    return;
  }

  listEl.innerHTML = items
    .map((r) => {
      const party =
        user.role === "Worker"
          ? r.customer?.name || "Cliente"
          : r.worker?.name || "Sin asignar";
      const partyUser = user.role === "Worker" ? r.customer : r.worker;
      const showWorkerActions = user.role === "Worker" && r.status === "Pendiente";
      const showComplete = user.role === "Worker" && r.status === "Aceptada";
      const canSchedule =
        (user.role === "Customer" || user.role === "Worker") &&
        (r.status === "Aceptada" || r.status === "Pendiente");

      return `
      <article class="request-card" data-id="${r.id}">
        <div class="req-head">
          <h3>${escapeHtml(r.description)}</h3>
          ${statusBadge(r.status)}
        </div>
        <div class="req-meta">
          <span>${escapeHtml(r.category)} · ${escapeHtml(r.urgency)}</span>
          <span>${r.createdAt ? new Date(r.createdAt).toLocaleString() : ""}</span>
        </div>
        <div class="req-party">
          ${avatarHtml(partyUser || { name: party })}
          <span>${user.role === "Worker" ? "Cliente: " : "Técnico: "}<strong>${escapeHtml(party)}</strong></span>
        </div>
        <div class="actions">
          ${
            showWorkerActions
              ? `<button type="button" class="btn btn-primary btn-sm" data-action="Aceptada" data-id="${r.id}">Aceptar</button>
                 <button type="button" class="btn btn-danger btn-sm" data-action="Rechazada" data-id="${r.id}">Rechazar</button>`
              : ""
          }
          ${
            showComplete
              ? `<button type="button" class="btn btn-primary btn-sm" data-action="Completada" data-id="${r.id}">Completar</button>`
              : ""
          }
          ${
            canSchedule && r.status !== "Rechazada" && r.status !== "Completada"
              ? `<button type="button" class="btn btn-outline btn-sm" data-schedule="${r.id}">Agendar cita</button>`
              : ""
          }
          ${
            user.role === "Customer" && r.worker?.userId
              ? `<button type="button" class="btn btn-ghost btn-sm" data-chat="${r.worker.userId}" data-worker="${r.worker.id}">Chat</button>`
              : ""
          }
        </div>
      </article>`;
    })
    .join("");

  listEl.querySelectorAll("[data-action]").forEach((btn) => {
    btn.addEventListener("click", () => updateRequestStatus(btn.dataset.id, btn.dataset.action, btn));
  });
  listEl.querySelectorAll("[data-schedule]").forEach((btn) => {
    btn.addEventListener("click", () => openScheduleModal(btn.dataset.schedule));
  });
  listEl.querySelectorAll("[data-chat]").forEach((btn) => {
    btn.addEventListener("click", () => {
      window.location.href = `chat.html?with=${btn.dataset.chat}&workerId=${btn.dataset.worker || ""}`;
    });
  });
}

async function updateRequestStatus(id, status, btn) {
  setLoading(btn, true, "...");
  try {
    await api(`/api/requests/${id}/status`, { method: "PATCH", body: { status } });
    toast(`Solicitud ${status.toLowerCase()}`, "success");
    await load();
  } catch (err) {
    toast(err.message, "error");
  } finally {
    setLoading(btn, false);
  }
}

async function loadAppointments() {
  try {
    const data = await api(`/api/appointments?pageSize=20`);
    const items = (data.items || []).filter((a) => {
      if (statusFilter === "activas") return a.status === "Nueva" || a.status === "Aceptada";
      return a.status === "Completada" || a.status === "Rechazada";
    });
    if (!items.length) {
      apptTitle.hidden = true;
      apptListEl.innerHTML = "";
      return;
    }
    apptTitle.hidden = false;
    apptListEl.innerHTML = items.map(appointmentCard).join("");
    apptListEl.querySelectorAll("[data-appt-action]").forEach((btn) => {
      btn.addEventListener("click", () => updateAppointment(btn.dataset.apptId, btn.dataset.apptAction, btn));
    });
  } catch {
    apptTitle.hidden = true;
    apptListEl.innerHTML = "";
  }
}

function appointmentCard(a) {
  const showActions = a.status === "Nueva";
  return `
    <article class="appointment-card">
      <div class="appt-label">📅 Cita agendada ${statusBadge(a.status)}</div>
      <h3>${escapeHtml(a.request?.description || a.description || "Cita")}</h3>
      <p class="when">${a.dateTime ? new Date(a.dateTime).toLocaleString() : ""} · ${escapeHtml(a.request?.category || "")}</p>
      <p class="desc">${escapeHtml(a.description || "")}</p>
      ${
        showActions
          ? `<div class="actions">
              <button type="button" class="btn btn-primary btn-sm" data-appt-action="Aceptada" data-appt-id="${a.id}">Aceptar</button>
              <button type="button" class="btn btn-danger btn-sm" data-appt-action="Rechazada" data-appt-id="${a.id}">Rechazar</button>
            </div>`
          : ""
      }
    </article>`;
}

async function updateAppointment(id, status, btn) {
  setLoading(btn, true, "...");
  try {
    await api(`/api/appointments/${id}/status`, { method: "PATCH", body: { status } });
    toast(`Cita ${status.toLowerCase()}`, "success");
    await loadAppointments();
  } catch (err) {
    toast(err.message, "error");
  } finally {
    setLoading(btn, false);
  }
}

function openScheduleModal(requestId) {
  const req = requestsCache.find((r) => String(r.id) === String(requestId));
  const defaultWorkerId = req?.workerId || null;
  const minDate = new Date(Date.now() + 60 * 60 * 1000);
  const minStr = minDate.toISOString().slice(0, 16);

  modalRoot.innerHTML = `
    <div class="modal-backdrop" id="apptBackdrop">
      <div class="modal" role="dialog" aria-modal="true" aria-labelledby="apptTitleH">
        <h2 class="modal-title" id="apptTitleH">Agendar cita</h2>
        <form id="apptForm" class="stack" novalidate>
          <div class="field">
            <label for="apptDate">Fecha y hora</label>
            <input id="apptDate" type="datetime-local" min="${minStr}" required />
            <span class="field-error" data-for="apptDate"></span>
          </div>
          <div class="field">
            <label for="apptDesc">Descripción</label>
            <textarea id="apptDesc" rows="3" placeholder="Detalles del encuentro..." required></textarea>
            <span class="field-error" data-for="apptDesc"></span>
          </div>
          ${
            !defaultWorkerId
              ? `<div class="field">
            <label for="apptWorker">workerId (si la solicitud está libre)</label>
            <input id="apptWorker" type="number" min="1" placeholder="Ej. 1" />
            <span class="field-error" data-for="apptWorker"></span>
          </div>`
              : ""
          }
          <div class="row" style="gap:10px">
            <button type="button" class="btn btn-ghost" id="apptCancel" style="flex:1">Cancelar</button>
            <button type="submit" class="btn btn-primary" id="apptSubmit" style="flex:2">Agendar</button>
          </div>
        </form>
      </div>
    </div>
  `;

  const close = () => {
    modalRoot.innerHTML = "";
  };
  document.getElementById("apptCancel").addEventListener("click", close);
  document.getElementById("apptBackdrop").addEventListener("click", (e) => {
    if (e.target.id === "apptBackdrop") close();
  });

  document.getElementById("apptForm").addEventListener("submit", async (e) => {
    e.preventDefault();
    const dateEl = document.getElementById("apptDate");
    const descEl = document.getElementById("apptDesc");
    let ok = true;
    if (!dateEl.value) {
      fieldError(dateEl, "Selecciona fecha y hora.");
      ok = false;
    } else {
      fieldError(dateEl, "");
    }
    if (!descEl.value.trim()) {
      fieldError(descEl, "Describe la cita.");
      ok = false;
    } else {
      fieldError(descEl, "");
    }
    const workerInput = document.getElementById("apptWorker");
    const workerId = workerInput ? Number(workerInput.value) || null : defaultWorkerId;
    if (!workerId && workerInput) {
      fieldError(workerInput, "Indica el trabajador.");
      ok = false;
    }
    if (!ok) return;

    const btn = document.getElementById("apptSubmit");
    setLoading(btn, true, "Agendando...");
    try {
      const body = {
        requestId: Number(requestId),
        dateTime: new Date(dateEl.value).toISOString(),
        description: descEl.value.trim(),
      };
      if (workerId) body.workerId = workerId;
      await api("/api/appointments", { method: "POST", body });
      toast("Cita agendada", "success");
      close();
      await load();
    } catch (err) {
      toast(err.message, "error");
    } finally {
      setLoading(btn, false);
    }
  });
}

load();
