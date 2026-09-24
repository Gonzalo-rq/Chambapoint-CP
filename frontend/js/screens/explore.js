import { api } from "../api.js";
import { requireAuth } from "../auth.js";
import { showSkeleton, toast } from "../ui.js";
import { avatarHtml, escapeHtml, bottomNav } from "../components.js";

const user = requireAuth();
if (!user) throw new Error("Sin sesión");

const listEl = document.getElementById("workersList");
const searchInput = document.getElementById("searchInput");
const searchBtn = document.getElementById("searchBtn");
const chips = document.querySelectorAll(".chip");
const pins = document.querySelectorAll(".pin");
const avatar = document.getElementById("userAvatar");

avatar.outerHTML = avatarHtml(user);

let profession = "";
let query = "";
let searchTimer = null;

document.body.insertAdjacentHTML("beforeend", bottomNav("explore"));

async function loadWorkers() {
  showSkeleton(listEl, 3);
  const params = new URLSearchParams();
  if (query) params.set("q", query);
  if (profession) params.set("profession", profession);
  const qs = params.toString();
  try {
    const workers = await api(`/api/workers${qs ? `?${qs}` : ""}`, { auth: false });
    renderWorkers(workers || []);
  } catch (err) {
    listEl.innerHTML = `<div class="empty-state"><h3>No pudimos cargar</h3><p>${escapeHtml(err.message)}</p></div>`;
    toast(err.message, "error");
  }
}

function renderWorkers(workers) {
  if (!workers.length) {
    listEl.innerHTML = `
      <div class="empty-state">
        <h3>Sin resultados</h3>
        <p>Prueba otra categoría o limpia la búsqueda.</p>
      </div>`;
    return;
  }

  listEl.innerHTML = workers
    .map((w) => {
      const rating = Number(w.ratingAverage) || 0;
      const tags = [];
      if (w.isOnline) tags.push("En línea");
      if (w.experienceYears) tags.push(`${w.experienceYears} años`);
      if (w.distanceKm != null) tags.push(`${w.distanceKm} km`);
      if (w.jobsCount) tags.push(`${w.jobsCount} trabajos`);
      return `
      <article class="worker-card" data-id="${w.id}" tabindex="0" role="link" aria-label="Ver perfil de ${escapeHtml(w.name)}">
        ${avatarHtml(w, "")}
        <div class="meta">
          <div class="name-row">
            <span class="name">${escapeHtml(w.name)}</span>
            <span class="rating">★ ${rating ? rating.toFixed(1) : " Nuevo"}</span>
          </div>
          <div class="profession">${escapeHtml(w.profession)} • ${w.distanceKm ?? "—"} km (dist.)</div>
          <div class="tags">${tags.map((t) => `<span class="tag">${escapeHtml(t)}</span>`).join("")}</div>
        </div>
      </article>`;
    })
    .join("");

  listEl.querySelectorAll(".worker-card").forEach((card) => {
    const open = () => {
      window.location.href = `worker.html?id=${card.dataset.id}`;
    };
    card.addEventListener("click", open);
    card.addEventListener("keydown", (e) => {
      if (e.key === "Enter" || e.key === " ") {
        e.preventDefault();
        open();
      }
    });
  });
}

chips.forEach((chip) => {
  chip.addEventListener("click", () => {
    chips.forEach((c) => {
      c.classList.remove("active");
      c.setAttribute("aria-selected", "false");
    });
    chip.classList.add("active");
    chip.setAttribute("aria-selected", "true");
    profession = chip.dataset.profession || "";
    loadWorkers();
  });
});

function scheduleSearch() {
  clearTimeout(searchTimer);
  searchTimer = setTimeout(() => {
    query = searchInput.value.trim();
    loadWorkers();
  }, 350);
}

searchInput.addEventListener("input", scheduleSearch);
searchBtn.addEventListener("click", () => {
  query = searchInput.value.trim();
  loadWorkers();
});

pins.forEach((pin) => {
  pin.addEventListener("click", () => {
    pins.forEach((p) => p.classList.remove("active"));
    pin.classList.add("active");
    toast(`Zona: ${pin.dataset.zone}`, "info", 2000);
  });
});

document.getElementById("verMapa")?.addEventListener("click", (e) => {
  e.preventDefault();
  document.getElementById("mapPanel")?.scrollIntoView({ behavior: "smooth", block: "center" });
});

loadWorkers();
