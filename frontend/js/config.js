const isLocal = ["localhost", "127.0.0.1"].includes(location.hostname);

export const API_BASE = isLocal ? "http://localhost:5020" : "";
export const HUB_URL = API_BASE
  ? `${API_BASE}/hubs/notifications`
  : `${location.origin}/hubs/notifications`;
export const STORAGE_KEYS = {
  token: "cp_token",
  user: "cp_user",
};
