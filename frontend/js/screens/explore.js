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
const avatar = document.getElementById("userAvatar");

avatar.outerHTML = avatarHtml(user);

const LIMA = [-12.0464, -77.0428];

let profession = "";
let query = "";
let searchTimer = null;
let loadSeq = 0;
let map = null;
let markersLayer = null;
let meMarker = null;
let currentWorkers = [];

document.body.insertAdjacentHTML("beforeend", bottomNav("explore", user.role));

function workerIcon(name) {
  const initials = (name || "?")
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((p) => p[0])
    .join("")
    .toUpperCase();
  return L.divIcon({
    className: "cp-marker",
    html: `<span class="cp-marker-pin"><span>${escapeHtml(initials)}</span></span>`,
    iconSize: [34, 42],
    iconAnchor: [17, 40],
    popupAnchor: [0, -36],
  });
}

function initMap() {
  if (map) return;
  if (typeof L === "undefined") {
    toast("No pudimos cargar el mapa. Revisa tu conexión.", "warning");
    return;
  }
  map = L.map("map", {
    zoomControl: true,
    attributionControl: true,
    scrollWheelZoom: false,
  }).setView(LIMA, 12);

  L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
    maxZoom: 19,
    attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
  }).addTo(map);

  markersLayer = L.layerGroup().addTo(map);
  setTimeout(() => map.invalidateSize(), 200);
}

function placeWorkerMarkers(workers) {
  if (!map || !markersLayer) return;
  markersLayer.clearLayers();
  currentWorkers = workers || [];
  const bounds = [];
  currentWorkers.forEach((w, i) => {
    const id = Number(w.id) || i;
    const lat = LIMA[0] + (((id * 37) % 7) - 3) * 0.01;
    const lng = LIMA[1] + (((id * 53) % 7) - 3) * 0.01;
    const rating = Number(w.ratingAverage) || 0;
    const marker = L.marker([lat, lng], { icon: workerIcon(w.name) }).addTo(markersLayer);
    bounds.push([lat, lng]);
    marker.bindPopup(`
      <div class="cp-popup">
        <strong>${escapeHtml(w.name)}</strong>
        <span>${escapeHtml(w.profession)} · ${rating ? "★ " + rating.toFixed(1) : "Nuevo"}</span>
        <button type="button" class="cp-popup-btn" data-worker-id="${w.id}">Ver perfil</button>
      </div>
    `);
    marker.on("popupopen", (e) => {
      const btn = e.popup.getElement()?.querySelector(".cp-popup-btn");
      btn?.addEventListener("click", () => {
        window.location.href = `worker.html?id=${w.id}`;
      });
    });
  });
  if (bounds.length) {
    map.setView(LIMA, 13);
  }
}

function locateMe() {
  if (!map || typeof L === "undefined") {
    toast("El mapa no está disponible.", "warning");
    return;
  }
  if (!navigator.geolocation) {
    toast("Tu navegador no soporta geolocalización.", "warning");
    return;
  }
  toast("Ubicando...", "info", 1500);
  navigator.geolocation.getCurrentPosition(
    (pos) => {
      const { latitude, longitude } = pos.coords;
      if (meMarker) meMarker.remove();
      meMarker = L.circleMarker([latitude, longitude], {
        radius: 9,
        color: "#fff",
        weight: 3,
        fillColor: "#2b6cb0",
        fillOpacity: 1,
      })
        .bindPopup("<strong>Estás aquí</strong>")
        .addTo(map);
      map.setView([latitude, longitude], 14);
      meMarker.openPopup();
    },
    () => {
      toast("No pudimos obtener tu ubicación.", "error");
      map.setView(LIMA, 12);
    },
    { enableHighAccuracy: true, timeout: 8000 }
  );
}

async function loadWorkers() {
  const seq = ++loadSeq;
  showSkeleton(listEl, 3);
  const params = new URLSearchParams();
  if (query) params.set("q", query);
  if (profession) params.set("profession", profession);
  const qs = params.toString();
  try {
    const workers = await api(`/api/workers${qs ? `?${qs}` : ""}`, { auth: false });
    if (seq !== loadSeq) return;
    const list = workers || [];
    renderWorkers(list);
    placeWorkerMarkers(list);
  } catch (err) {
    if (seq !== loadSeq) return;
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

document.getElementById("locateBtn")?.addEventListener("click", locateMe);

document.getElementById("verMapa")?.addEventListener("click", (e) => {
  e.preventDefault();
  document.getElementById("map")?.scrollIntoView({ behavior: "smooth", block: "center" });
  setTimeout(() => map?.invalidateSize(), 300);
});

initMap();
loadWorkers();
