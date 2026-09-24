let container;

function ensureContainer() {
  if (container) return container;
  container = document.createElement("div");
  container.className = "toast-container";
  document.body.appendChild(container);
  return container;
}

export function toast(message, type = "info", duration = 3500) {
  const root = ensureContainer();
  const el = document.createElement("div");
  el.className = `toast toast-${type}`;
  el.setAttribute("role", "status");
  el.textContent = message;
  root.appendChild(el);
  requestAnimationFrame(() => el.classList.add("toast-show"));
  setTimeout(() => {
    el.classList.remove("toast-show");
    setTimeout(() => el.remove(), 300);
  }, duration);
}

export function setLoading(button, loading, label = "Cargando...") {
  if (!button) return;
  if (loading) {
    button.dataset.originalText = button.innerHTML;
    button.disabled = true;
    button.classList.add("is-loading");
    button.innerHTML = `<span class="spinner" aria-hidden="true"></span><span>${label}</span>`;
  } else {
    button.disabled = false;
    button.classList.remove("is-loading");
    if (button.dataset.originalText) button.innerHTML = button.dataset.originalText;
  }
}

export function showSkeleton(listEl, count = 3) {
  if (!listEl) return;
  listEl.innerHTML = Array.from({ length: count })
    .map(
      () => `
      <div class="skeleton-card" aria-hidden="true">
        <div class="skeleton-avatar skeleton"></div>
        <div class="skeleton-lines">
          <div class="skeleton skeleton-line"></div>
          <div class="skeleton skeleton-line short"></div>
        </div>
      </div>`
    )
    .join("");
}

export function fieldError(input, message) {
  if (!input) return;
  input.classList.toggle("is-invalid", Boolean(message));
  const err = input.parentElement?.querySelector(".field-error");
  if (err) err.textContent = message || "";
}
