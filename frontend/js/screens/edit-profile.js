import { api, apiFetch } from "../api.js";
import { requireAuth } from "../auth.js";
import { toast, setLoading, fieldError } from "../ui.js";
import { bottomNav } from "../components.js";

const user = requireAuth(["Worker"]);
if (!user) throw new Error("Sin sesión");

document.body.insertAdjacentHTML("beforeend", bottomNav("profile", user.role));

const form = document.getElementById("editForm");
const skeleton = document.getElementById("formSkeleton");
const aboutEl = document.getElementById("about");
const aboutCount = document.getElementById("aboutCount");

let workerId = null;
let base = null;
// TAREA 5: coordenadas GPS capturadas manualmente
let capturedLat = null;
let capturedLon = null;

aboutEl.addEventListener("input", () => {
  aboutCount.textContent = String(aboutEl.value.length);
});

function normalizeList(value) {
  if (Array.isArray(value)) return value;
  if (typeof value === "string") return value.split("\n").map((s) => s.trim()).filter(Boolean);
  return [];
}

function pickProfile(raw) {
  return {
    profession: raw.profession ?? raw.Profession ?? "",
    about: raw.about ?? raw.About ?? "",
    experienceYears: raw.experienceYears ?? raw.ExperienceYears ?? 0,
    distanceKm: raw.distanceKm ?? raw.DistanceKm ?? 0,
    certifications: normalizeList(raw.certifications ?? raw.Certifications),
    gallery: normalizeList(raw.gallery ?? raw.Gallery),
    isOnline: raw.isOnline ?? raw.IsOnline ?? true,
    latitude: raw.latitude ?? raw.Latitude ?? null,
    longitude: raw.longitude ?? raw.Longitude ?? null,
  };
}

function fill(p) {
  document.getElementById("profession").value = p.profession || "Plomería";
  aboutEl.value = p.about || "";
  aboutCount.textContent = String(aboutEl.value.length);
  document.getElementById("experienceYears").value = p.experienceYears;
  document.getElementById("distanceKm").value = p.distanceKm;
  document.getElementById("certifications").value = p.certifications.join("\n");
  // TAREA 5: poblar toggle de disponibilidad
  const chk = document.getElementById("chkIsOnline");
  if (chk) chk.checked = p.isOnline !== false;
  // Guardar coords existentes
  capturedLat = p.latitude;
  capturedLon = p.longitude;
}

async function load() {
  try {
    const dash = await api("/api/dashboard/worker");
    workerId = dash.worker?.id ?? dash.worker?.Id ?? null;
    if (workerId) {
      const raw = await api(`/api/workers/${workerId}`);
      base = pickProfile(raw);
      fill(base);
    }
  } catch (err) {
    if (err.status === 403 || err.status === 404) {
      base = {
        profession: "Plomería",
        about: "",
        experienceYears: 0,
        distanceKm: 0,
        certifications: [],
        gallery: [],
        isOnline: true,
        latitude: null,
        longitude: null,
      };
      fill(base);
      toast("Configura tu perfil de trabajador.", "info");
    } else {
      toast(err.message, "error");
      if (base) fill(base);
    }
  } finally {
    skeleton.hidden = true;
    form.hidden = false;
  }
}

function validate() {
  const exp = document.getElementById("experienceYears");
  const dist = document.getElementById("distanceKm");
  let ok = true;

  if (!aboutEl.value.trim()) {
    fieldError(aboutEl, "Cuéntanos sobre ti.");
    ok = false;
  } else {
    fieldError(aboutEl, "");
  }

  const expVal = Number(exp.value);
  if (!Number.isFinite(expVal) || expVal < 0 || expVal > 60) {
    fieldError(exp, "Entre 0 y 60 años.");
    ok = false;
  } else {
    fieldError(exp, "");
  }

  const distVal = Number(dist.value);
  if (!Number.isFinite(distVal) || distVal < 0) {
    fieldError(dist, "No puede ser negativo.");
    ok = false;
  } else {
    fieldError(dist, "");
  }

  return ok;
}

// TAREA 5: Capturar GPS al pulsar el botón
document.getElementById("btnCaptureGps")?.addEventListener("click", () => {
  if (!navigator.geolocation) {
    toast("Tu navegador no soporta geolocalización.", "warning");
    return;
  }
  const btn = document.getElementById("btnCaptureGps");
  btn.disabled = true;
  btn.textContent = "📍 Obteniendo ubicación...";
  navigator.geolocation.getCurrentPosition(
    (pos) => {
      capturedLat = pos.coords.latitude;
      capturedLon = pos.coords.longitude;
      btn.disabled = false;
      btn.textContent = `✅ GPS: ${capturedLat.toFixed(5)}, ${capturedLon.toFixed(5)}`;
      toast("Ubicación GPS capturada correctamente.", "success");
    },
    () => {
      btn.disabled = false;
      btn.textContent = "📍 Usar mi ubicación GPS actual";
      toast("No pudimos obtener tu ubicación. Revisa los permisos.", "error");
    },
    { enableHighAccuracy: true, timeout: 10000 }
  );
});

form.addEventListener("submit", async (e) => {
  e.preventDefault();
  if (!validate()) return;

  const btn = document.getElementById("saveBtn");
  setLoading(btn, true, "Guardando...");

  const certs = document
    .getElementById("certifications")
    .value.split("\n")
    .map((s) => s.trim())
    .filter(Boolean);

  const body = {
    profession: document.getElementById("profession").value,
    about: aboutEl.value.trim(),
    experienceYears: Number(document.getElementById("experienceYears").value),
    distanceKm: Number(document.getElementById("distanceKm").value),
    certifications: certs,
    gallery: base?.gallery || [],
    // TAREA 5: incluir disponibilidad y coordenadas GPS en el payload
    isOnline: document.getElementById("chkIsOnline")?.checked ?? true,
  };

  if (capturedLat !== null && capturedLon !== null) {
    body.latitude = capturedLat;
    body.longitude = capturedLon;
  }

  try {
    let saved;
    try {
      saved = await api("/api/workers", { method: "PUT", body });
    } catch (err) {
      if (err.status !== 404) throw err;
      saved = await api("/api/workers", { method: "POST", body });
    }
    workerId = saved?.id ?? saved?.Id ?? workerId;
    toast("Perfil actualizado", "success");
    setTimeout(() => {
      window.location.href = workerId ? `worker.html?id=${workerId}` : "dashboard.html";
    }, 500);
  } catch (err) {
    toast(err.message, "error");
    setLoading(btn, false);
  }
});

// TAREA 4: Eliminar cuenta
document.getElementById("btnDeleteAccount")?.addEventListener("click", async () => {
  const confirmacion = confirm("⚠️ ¿Estás completamente seguro de que deseas eliminar tu cuenta de ChambaPoint? Esta acción es permanente.");
  if (!confirmacion) return;

  try {
    await api("/api/auth/me", { method: "DELETE" });
    alert("Tu cuenta ha sido eliminada correctamente.");
    localStorage.clear();
    window.location.href = "login.html";
  } catch (err) {
    alert("Error al eliminar la cuenta: " + (err.message || "Intenta más tarde"));
  }
});

load();
