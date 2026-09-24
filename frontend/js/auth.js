import { api, getUser, getToken, saveSession, clearSession } from "./api.js";

export function homeForRole(role) {
  return role === "Worker" ? "dashboard.html" : "explore.html";
}

export function redirectByRole(role) {
  window.location.href = homeForRole(role);
}

export async function login(email, password) {
  const data = await api("/api/auth/login", {
    method: "POST",
    body: { email, password },
    auth: false,
  });
  saveSession(data.token, data.user);
  return data.user;
}

export async function register(payload) {
  const data = await api("/api/auth/register", {
    method: "POST",
    body: payload,
    auth: false,
  });
  saveSession(data.token, data.user);
  return data.user;
}

export async function fetchMe() {
  const data = await api("/api/auth/me");
  return data.user;
}

export function logout() {
  clearSession();
  window.location.href = "index.html";
}

export function requireAuth(roles = null) {
  const token = getToken();
  const user = getUser();
  if (!token || !user) {
    window.location.href = "login.html";
    return null;
  }
  if (roles && !roles.includes(user.role)) {
    window.location.href = homeForRole(user.role);
    return null;
  }
  return user;
}

export function redirectIfAuthed() {
  const token = getToken();
  const user = getUser();
  if (token && user) {
    window.location.href = homeForRole(user.role);
    return true;
  }
  return false;
}
