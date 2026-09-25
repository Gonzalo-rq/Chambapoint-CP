import { api } from "../api.js";
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
    jobsCount: raw.jobsCount ?? raw.JobsCount ?? 0,
    certifications: normalizeList(raw.certifications ?? raw.Certifications),
    gallery: normalizeList(raw.gallery ?? raw.Gallery),
  };
}

function fill(p) {
  document.getElementById("profession").value = p.profession || "Plomería";
  aboutEl.value = p.about || "";
  aboutCount.textContent = String(aboutEl.value.length);
  document.getElementById("experienceYears").value = p.experienceYears;
  document.getElementById("distanceKm").value = p.distanceKm;
  document.getElementById("jobsCount").value = p.jobsCount;
  document.getElementById("certifications").value = p.certifications.join("\n");
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
        jobsCount: 0,
        certifications: [],
        gallery: [],
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
  const jobs = document.getElementById("jobsCount");
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

  const jobsVal = Number(jobs.value);
  if (!Number.isFinite(jobsVal) || jobsVal < 0) {
    fieldError(jobs, "No puede ser negativo.");
    ok = false;
  } else {
    fieldError(jobs, "");
  }

  return ok;
}

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
    jobsCount: Number(document.getElementById("jobsCount").value),
    certifications: certs,
    gallery: base?.gallery || [],
  };

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

load();
