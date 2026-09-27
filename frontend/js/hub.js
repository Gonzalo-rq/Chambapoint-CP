import { HUB_URL, STORAGE_KEYS } from "./config.js";
import { getToken, getUser } from "./api.js";

let connection = null;
let pendingConnection = null;
let handlers = {};
let reconnectTimer = null;
let closedByApp = false;

const RECONNECT_DELAY_MS = 15000;

export async function connectHub() {
  if (connection) return connection;
  if (pendingConnection) return pendingConnection;

  pendingConnection = openConnection();
  try {
    return await pendingConnection;
  } finally {
    pendingConnection = null;
  }
}

async function openConnection() {
  const user = getUser();
  const token = getToken();
  if (!user || !token) return null;

  if (!window.signalR) {
    try {
      await loadScript("https://cdn.jsdelivr.net/npm/@microsoft/signalr@8.0.7/dist/browser/signalr.min.js");
    } catch {
      return null;
    }
  }

  connection = new window.signalR.HubConnectionBuilder()
    .withUrl(HUB_URL, {
      accessTokenFactory: () => token,
      withCredentials: false,
    })
    .withAutomaticReconnect()
    .build();

  const events = ["newMessage", "messagesRead", "newRequest", "requestStatusChanged", "newAppointment", "appointmentStatusChanged", "newReview"];
  for (const ev of events) {
    connection.on(ev, (payload) => dispatch(ev, payload));
  }

  connection.onreconnected(() => {
    joinUserGroup(user).catch(() => null);
  });

  connection.onclose(() => {
    connection = null;
    if (closedByApp) {
      closedByApp = false;
      return;
    }
    scheduleReconnect();
  });

  try {
    await connection.start();
    await joinUserGroup(user);
  } catch {
    try {
      await connection.stop();
    } catch {
      /* ignore */
    }
    connection = null;
    return null;
  }
  return connection;
}

function dispatch(event, payload) {
  (handlers[event] || []).forEach((fn) => {
    try {
      fn(payload);
    } catch {
      /* ignore handler errors */
    }
  });
}

async function joinUserGroup(user) {
  if (!connection) return;
  const userId = user.id ?? user.userId ?? user.Id;
  if (userId != null) {
    await connection.invoke("JoinUserGroup", Number(userId));
    dispatch("resync");
  }
}

function scheduleReconnect() {
  if (reconnectTimer) return;
  reconnectTimer = setTimeout(() => {
    reconnectTimer = null;
    if (connection) return;
    if (!getUser() || !getToken()) return;
    connectHub().then(
      (conn) => {
        if (!conn) scheduleReconnect();
      },
      () => scheduleReconnect()
    );
  }, RECONNECT_DELAY_MS);
}

export function onHub(event, fn) {
  if (!handlers[event]) handlers[event] = [];
  handlers[event].push(fn);
}

export async function disconnectHub() {
  if (!connection) return;
  closedByApp = true;
  if (reconnectTimer) {
    clearTimeout(reconnectTimer);
    reconnectTimer = null;
  }
  try {
    await connection.stop();
  } catch {
    /* ignore */
  }
  connection = null;
  closedByApp = false;
  handlers = {};
}

function loadScript(src) {
  return new Promise((resolve, reject) => {
    const s = document.createElement("script");
    s.src = src;
    s.onload = resolve;
    s.onerror = () => reject(new Error("No se pudo cargar SignalR"));
    document.head.appendChild(s);
  });
}

export { STORAGE_KEYS };
