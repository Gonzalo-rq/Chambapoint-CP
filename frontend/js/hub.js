import { HUB_URL, STORAGE_KEYS } from "./config.js";
import { getToken, getUser } from "./api.js";

let connection = null;
let handlers = {};

export async function connectHub() {
  if (connection) return connection;
  const user = getUser();
  const token = getToken();
  if (!user || !token) return null;

  if (!window.signalR) {
    await loadScript("https://cdn.jsdelivr.net/npm/@microsoft/signalr@8.0.7/dist/browser/signalr.min.js");
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
    connection.on(ev, (payload) => {
      (handlers[ev] || []).forEach((fn) => {
        try {
          fn(payload);
        } catch {
          /* ignore handler errors */
        }
      });
    });
  }

  connection.onreconnected(() => {
    joinUserGroup(user);
  });

  try {
    await connection.start();
    await joinUserGroup(user);
  } catch {
    connection = null;
    return null;
  }
  return connection;
}

async function joinUserGroup(user) {
  if (!connection) return;
  const userId = user.id ?? user.userId ?? user.Id;
  if (userId != null) {
    await connection.invoke("JoinUserGroup", Number(userId));
  }
}

export function onHub(event, fn) {
  if (!handlers[event]) handlers[event] = [];
  handlers[event].push(fn);
}

export async function disconnectHub() {
  if (!connection) return;
  try {
    await connection.stop();
  } catch {
    /* ignore */
  }
  connection = null;
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
