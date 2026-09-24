import { API_BASE, STORAGE_KEYS } from "./config.js";

export class ApiError extends Error {
  constructor(status, message) {
    super(message);
    this.status = status;
  }
}

export function getToken() {
  return localStorage.getItem(STORAGE_KEYS.token);
}

export function getUser() {
  const raw = localStorage.getItem(STORAGE_KEYS.user);
  if (!raw) return null;
  try {
    return JSON.parse(raw);
  } catch {
    return null;
  }
}

export function saveSession(token, user) {
  localStorage.setItem(STORAGE_KEYS.token, token);
  localStorage.setItem(STORAGE_KEYS.user, JSON.stringify(user));
}

export function clearSession() {
  localStorage.removeItem(STORAGE_KEYS.token);
  localStorage.removeItem(STORAGE_KEYS.user);
}

export async function api(path, { method = "GET", body, auth = true, signal } = {}) {
  const headers = { "Content-Type": "application/json" };
  const token = getToken();
  if (auth && token) headers.Authorization = `Bearer ${token}`;

  let res;
  try {
    res = await fetch(`${API_BASE}${path}`, {
      method,
      headers,
      body: body !== undefined ? JSON.stringify(body) : undefined,
      signal,
    });
  } catch {
    throw new ApiError(0, "No se pudo conectar con el servidor. Verifica que el backend esté corriendo.");
  }

  let data = null;
  const text = await res.text();
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = { message: text };
    }
  }

  if (!res.ok) {
    const message = data?.message || defaultErrorMessage(res.status);
    if (res.status === 401 && auth) {
      clearSession();
    }
    throw new ApiError(res.status, message);
  }

  return data;
}

function defaultErrorMessage(status) {
  const map = {
    400: "Datos inválidos. Revisa el formulario.",
    401: "Sesión expirada o no autorizado. Inicia sesión de nuevo.",
    403: "No tienes permisos para esta acción.",
    404: "Recurso no encontrado.",
    500: "Error interno del servidor. Intenta más tarde.",
  };
  return map[status] || `Error HTTP ${status}`;
}
