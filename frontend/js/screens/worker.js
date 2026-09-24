import { api } from "../api.js";
import { requireAuth } from "../auth.js";
import { toast, setLoading, fieldError } from "../ui.js";
import { avatarHtml, escapeHtml, starsHtml } from "../components.js";

const user = requireAuth();
if (!user) throw new Error("Sin sesión");

const root = document.getElementById("profileRoot");
const modalRoot = document.getElementById("modalRoot");
const headerName = document.getElementById("headerName");
const params = new URLSearchParams(window.location.search);
const workerId = Number(params.get("id"));

document.getElementById("backBtn")?.addEventListener("click", () => {
  history.length > 1 ? history.back() : (window.location.href = "explore.html");
});

let worker = null;
let activeTab = "about";
let selectedRating = 5;

function normalizeWorker(raw) {
  const toList = (v) => (Array.isArray(v) ? v : Array.isArray(v?.$values) ? v.$values : []);
  const reviews = toList(raw.reviews).map((r) => ({
    id: r.id ?? r.Id,
    rating: Number(r.rating ?? r.Rating) || 0,
    text: r.text ?? r.Text ?? "",
    photos: toList(r.photos ?? r.Photos),
    createdAt: r.createdAt ?? r.CreatedAt ?? null,
    author: r.author ?? r.Author ?? null,
  }));
  return {
    ...raw,
    id: raw.id ?? raw.Id,
    userId: raw.userId ?? raw.UserId,
    profession: raw.profession ?? raw.Profession ?? "",
    experienceYears: raw.experienceYears ?? raw.ExperienceYears ?? 0,
    distanceKm: raw.distanceKm ?? raw.DistanceKm ?? null,
    jobsCount: raw.jobsCount ?? raw.JobsCount ?? 0,
    about: raw.about ?? raw.About ?? "",
    certifications: toList(raw.certifications ?? raw.Certifications),
    gallery: toList(raw.gallery ?? raw.Gallery),
    reviews,
  };
}

async function resolveUserId() {
  if (worker.userId != null) return;
  try {
    const data = await api("/api/requests?pageSize=50");
    const hit = (data.items || []).find((r) => r.workerId === worker.id && r.worker?.userId != null);
    if (hit?.worker?.userId != null) worker.userId = hit.worker.userId;
  } catch {
  }
}

async function load() {
  if (!workerId) {
    root.innerHTML = `<div class="empty-state"><h3>ID inválido</h3><a href="explore.html">Volver a explorar</a></div>`;
    return;
  }
  try {
    worker = normalizeWorker(await api(`/api/workers/${workerId}`, { auth: false }));
    headerName.textContent = worker.name || "Perfil";
    await resolveUserId();
    render();
  } catch (err) {
    root.innerHTML = `<div class="empty-state"><h3>No encontramos al técnico</h3><p>${escapeHtml(err.message)}</p><p style="margin-top:12px"><a class="btn btn-outline" href="explore.html">Volver</a></p></div>`;
  }
}

function render() {
  const w = worker;
  const rating = Number(w.ratingAverage) || 0;
  const certs = w.certifications || [];
  root.innerHTML = `
    <section class="profile-hero">
      ${avatarHtml(w, "avatar-lg")}
      <h1>${escapeHtml(w.name)}</h1>
      <p class="profession">${escapeHtml(w.profession)}</p>
      <p class="stars">${starsHtml(rating, w.ratingCount ?? 0)}</p>
      <div class="profile-stats">
        <div class="stat"><strong>${w.experienceYears ?? 0}</strong><span>Años exp.</span></div>
        <div class="stat"><strong>${w.distanceKm ?? "—"} km</strong><span>Distancia</span></div>
        <div class="stat"><strong>${w.jobsCount ?? 0}+</strong><span>Trabajos</span></div>
      </div>
      <div class="badge-row">
        <span class="badge badge-success">✓ Verificado</span>
        <span class="badge badge-info">🎓 Certificado</span>
        <span class="badge badge-warning">⚡ Urgencias 24/7</span>
        ${certs.map((c) => `<span class="badge badge-neutral">${escapeHtml(c)}</span>`).join("")}
      </div>
      <div class="profile-actions">
        ${
          user.role === "Customer"
            ? `<button type="button" class="btn btn-primary btn-block" id="requestBtn">Solicitar Servicio</button>`
            : ""
        }
        ${
          worker.userId
            ? `<button type="button" class="btn btn-outline btn-block" id="messageBtn">Enviar Mensaje</button>`
            : ""
        }
      </div>
    </section>

    <div class="page">
      <div class="tabs" role="tablist">
        <button type="button" class="tab-btn ${activeTab === "about" ? "active" : ""}" data-tab="about" role="tab">Sobre mí</button>
        <button type="button" class="tab-btn ${activeTab === "gallery" ? "active" : ""}" data-tab="gallery" role="tab">Galería</button>
        <button type="button" class="tab-btn ${activeTab === "reviews" ? "active" : ""}" data-tab="reviews" role="tab">Reseñas</button>
      </div>
      <div id="tabPanel"></div>
    </div>
  `;

  root.querySelectorAll(".tab-btn").forEach((btn) => {
    btn.addEventListener("click", () => {
      activeTab = btn.dataset.tab;
      render();
    });
  });

  document.getElementById("requestBtn")?.addEventListener("click", openRequestModal);
  document.getElementById("messageBtn")?.addEventListener("click", () => {
    if (worker.userId) {
      window.location.href = `chat.html?with=${worker.userId}`;
    } else {
      toast("Este perfil no expone usuario de chat.", "warning");
    }
  });

  renderTab();
}

function renderTab() {
  const panel = document.getElementById("tabPanel");
  if (!panel) return;

  if (activeTab === "about") {
    panel.innerHTML = `
      <div class="card">
        <p style="font-size:14px;line-height:1.6">${escapeHtml(worker.about || "Sin descripción todavía.")}</p>
        <p class="text-muted" style="margin-top:12px;font-size:13px">
          ${worker.isOnline ? "● En línea ahora" : "○ Desconectado"} · ${escapeHtml(worker.profession)}
        </p>
      </div>`;
    return;
  }

  if (activeTab === "gallery") {
    const gallery = worker.gallery || [];
    if (!gallery.length) {
      panel.innerHTML = `<div class="empty-state"><h3>Sin galería</h3><p>Este técnico aún no subió fotos.</p></div>`;
      return;
    }
    panel.innerHTML = `<div class="gallery-grid">${gallery
      .map((src, i) => `<img src="${escapeHtml(src)}" alt="Trabajo ${i + 1}" loading="lazy" />`)
      .join("")}</div>`;
    return;
  }

  // Reviews tab
  const reviews = worker.reviews || [];
  panel.innerHTML = `
    ${
      user.role === "Customer"
        ? `<form class="card" id="reviewForm" style="margin-bottom:16px" novalidate>
        <h3 style="font-size:15px;margin-bottom:10px">¿Qué tal el servicio?</h3>
        <div class="star-rating" id="starRating" role="radiogroup" aria-label="Calificación">
          ${[1, 2, 3, 4, 5]
            .map(
              (n) =>
                `<span class="star ${n <= selectedRating ? "active" : ""}" data-value="${n}" role="radio" aria-checked="${n === selectedRating}" tabindex="0">★</span>`
            )
            .join("")}
        </div>
        <div class="field" style="margin-top:12px">
          <label for="reviewText">Describe tu experiencia</label>
          <textarea id="reviewText" rows="3" placeholder="¿El trabajo fue puntual? ¿Quedaste satisfecho?" required></textarea>
          <span class="field-error" data-for="reviewText"></span>
        </div>
        <div class="field" style="margin-top:8px">
          <label for="reviewPhoto">Fotos del trabajo (opcional, URL)</label>
          <input id="reviewPhoto" type="url" placeholder="https://..." />
          <div class="preview-row" id="reviewPreview"></div>
        </div>
        <button type="submit" class="btn btn-primary btn-block" id="reviewSubmit" style="margin-top:12px">Publicar Reseña</button>
      </form>`
        : `<div class="card card-soft" style="margin-bottom:16px"><p style="font-size:13px">Inicia sesión como Customer para publicar una reseña.</p></div>`
    }
    <div class="card">
      ${reviews.length ? reviews.map(reviewItem).join("") : `<div class="empty-state"><h3>Sin reseñas aún</h3><p>Sé el primero en opinar.</p></div>`}
    </div>
  `;

  panel.querySelectorAll(".star").forEach((star) => {
    const pick = () => {
      selectedRating = Number(star.dataset.value);
      panel.querySelectorAll(".star").forEach((s) => {
        const on = Number(s.dataset.value) <= selectedRating;
        s.classList.toggle("active", on);
        s.setAttribute("aria-checked", String(Number(s.dataset.value) === selectedRating));
      });
    };
    star.addEventListener("click", pick);
    star.addEventListener("keydown", (e) => {
      if (e.key === "Enter" || e.key === " ") {
        e.preventDefault();
        pick();
      }
    });
  });

  const photoInput = document.getElementById("reviewPhoto");
  photoInput?.addEventListener("input", () => {
    const preview = document.getElementById("reviewPreview");
    const url = photoInput.value.trim();
    preview.innerHTML = url ? `<img class="photo-thumb" src="${escapeHtml(url)}" alt="Preview" onerror="this.style.display='none'" />` : "";
  });

  document.getElementById("reviewForm")?.addEventListener("submit", submitReview);
}

function reviewItem(r) {
  const rating = Number(r.rating) || 0;
  return `
    <article class="review-item">
      <div class="review-head">
        <strong>${escapeHtml(r.author || "Cliente")}</strong>
        <span class="review-stars">${"★".repeat(rating)}${"☆".repeat(Math.max(0, 5 - rating))}</span>
      </div>
      <p>${escapeHtml(r.text)}</p>
      ${
        r.photos?.length
          ? `<div class="photo-list" style="margin-top:8px">${r.photos
              .map((p) => `<img class="photo-thumb" src="${escapeHtml(p)}" alt="" loading="lazy" />`)
              .join("")}</div>`
          : ""
      }
      <p class="text-muted" style="font-size:11px;margin-top:6px">${r.createdAt ? new Date(r.createdAt).toLocaleDateString() : ""}</p>
    </article>`;
}

async function submitReview(e) {
  e.preventDefault();
  const textEl = document.getElementById("reviewText");
  const photoEl = document.getElementById("reviewPhoto");
  const btn = document.getElementById("reviewSubmit");
  const text = textEl.value.trim();
  if (!text) {
    fieldError(textEl, "Escribe un comentario.");
    return;
  }
  fieldError(textEl, "");
  const photos = photoEl.value.trim() ? [photoEl.value.trim()] : [];
  setLoading(btn, true, "Publicando...");
  try {
    await api(`/api/workers/${worker.id}/reviews`, {
      method: "POST",
      body: { rating: selectedRating, text, photos },
    });
    toast("Reseña publicada", "success");
    worker = normalizeWorker(await api(`/api/workers/${worker.id}`, { auth: false }));
    render();
  } catch (err) {
    toast(err.message, "error");
  } finally {
    setLoading(btn, false);
  }
}

function openRequestModal() {
  const categories = ["Plomería", "Electricidad", "Carpintería", "Pintura"];
  const urgencies = ["Lo antes posible", "Flexible", "Programar"];
  const defaultCat = categories.includes(worker.profession) ? worker.profession : categories[0];

  modalRoot.innerHTML = `
    <div class="modal-backdrop" id="requestBackdrop">
      <div class="modal" role="dialog" aria-modal="true" aria-labelledby="reqTitle">
        <h2 class="modal-title" id="reqTitle">Solicitar Servicio</h2>
        <form id="requestForm" class="stack" novalidate>
          <div class="field">
            <label for="reqCategory">Categoría</label>
            <select id="reqCategory" required>
              ${categories.map((c) => `<option value="${c}" ${c === defaultCat ? "selected" : ""}>${c}</option>`).join("")}
            </select>
          </div>
          <div class="field">
            <label for="reqUrgency">Urgencia</label>
            <select id="reqUrgency" required>
              ${urgencies.map((u, i) => `<option value="${u}" ${i === 0 ? "selected" : ""}>${u}</option>`).join("")}
            </select>
          </div>
          <div class="field">
            <label for="reqDesc">Descripción</label>
            <textarea id="reqDesc" rows="3" placeholder="Cuéntanos qué necesitas..." required></textarea>
            <span class="field-error" data-for="reqDesc"></span>
          </div>
          <div class="field">
            <label for="reqPhoto">Fotos (URL, opcional)</label>
            <input id="reqPhoto" type="url" placeholder="https://..." />
            <div class="preview-row" id="reqPreview"></div>
            <span class="field-hint">Pega una URL o elige un archivo local (se envía como base64).</span>
            <input id="reqFile" type="file" accept="image/*" />
          </div>
          <div class="row" style="gap:10px;margin-top:4px">
            <button type="button" class="btn btn-ghost" id="reqCancel" style="flex:1">Cancelar</button>
            <button type="submit" class="btn btn-primary" id="reqSubmit" style="flex:2">Enviar solicitud</button>
          </div>
        </form>
      </div>
    </div>
  `;

  const close = () => {
    modalRoot.innerHTML = "";
  };
  document.getElementById("reqCancel")?.addEventListener("click", close);
  document.getElementById("requestBackdrop")?.addEventListener("click", (e) => {
    if (e.target.id === "requestBackdrop") close();
  });

  const photoUrl = document.getElementById("reqPhoto");
  const reqFile = document.getElementById("reqFile");
  const preview = document.getElementById("reqPreview");

  photoUrl?.addEventListener("input", () => {
    const url = photoUrl.value.trim();
    preview.innerHTML = url ? `<img class="photo-thumb" src="${escapeHtml(url)}" alt="" onerror="this.style.display='none'" />` : "";
  });

  reqFile?.addEventListener("change", () => {
    const file = reqFile.files?.[0];
    if (!file) return;
    if (file.size > 400 * 1024) {
      toast("Imagen muy grande (máx 400KB para base64).", "warning");
      reqFile.value = "";
      return;
    }
    const reader = new FileReader();
    reader.onload = () => {
      photoUrl.value = "";
      preview.innerHTML = `<img class="photo-thumb" src="${reader.result}" alt="" />`;
      preview.dataset.dataUrl = String(reader.result);
    };
    reader.readAsDataURL(file);
  });

  document.getElementById("requestForm")?.addEventListener("submit", async (e) => {
    e.preventDefault();
    const desc = document.getElementById("reqDesc");
    const text = desc.value.trim();
    if (!text) {
      fieldError(desc, "La descripción es obligatoria.");
      return;
    }
    fieldError(desc, "");
    const btn = document.getElementById("reqSubmit");
    const dataUrl = preview.dataset.dataUrl;
    const urlPhoto = photoUrl.value.trim();
    const photos = dataUrl ? [dataUrl] : urlPhoto ? [urlPhoto] : [];

    setLoading(btn, true, "Enviando...");
    try {
      await api("/api/requests", {
        method: "POST",
        body: {
          workerId: worker.id,
          category: document.getElementById("reqCategory").value,
          description: text,
          photos,
          urgency: document.getElementById("reqUrgency").value,
        },
      });
      toast("Solicitud enviada", "success");
      close();
      setTimeout(() => {
        window.location.href = "requests.html";
      }, 800);
    } catch (err) {
      toast(err.message, "error");
    } finally {
      setLoading(btn, false);
    }
  });
}

load();
