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

export function bottomNav(active = "explore") {
  const items = [
    { id: "explore", href: "explore.html", label: "Explorar", path: "M11 4a7 7 0 1 0 4.9 12l3.1 3.1 1.4-1.4-3.1-3.1A7 7 0 0 0 11 4zm0 2a5 5 0 1 1 0 10 5 5 0 0 1 0-10z" },
    { id: "requests", href: "requests.html", label: "Solicitudes", path: "M8 4h8a2 2 0 0 1 2 2v14l-6-3-6 3V6a2 2 0 0 1 2-2z" },
    { id: "messages", href: "conversations.html", label: "Mensajes", path: "M4 6a2 2 0 0 1 2-2h12a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H9l-5 4V6z" },
    { id: "profile", href: "dashboard.html", label: "Perfil", path: "M12 12a4 4 0 1 0 0-8 4 4 0 0 0 0 8zm-8 9a8 8 0 0 1 16 0H4z" },
  ];
  return `<nav class="bottom-nav" aria-label="Navegación principal">
    ${items
      .map(
        (it) => `<a href="${it.href}" class="${active === it.id ? "active" : ""}">
      <svg viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="${it.path}"/></svg>
      <span>${it.label}</span>
    </a>`
      )
      .join("")}
  </nav>`;
}
