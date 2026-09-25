import { redirectIfAuthed } from "../auth.js";

const REDIRECT_DELAY = 1600;
let timerId = null;
let left = false;

function goToCreateProfile() {
  if (left) return;
  if (!window.location.pathname.endsWith("index.html") && window.location.pathname !== "/") return;
  window.location.href = "create-profile.html";
}

if (redirectIfAuthed()) {
  left = true;
} else {
  timerId = window.setTimeout(goToCreateProfile, REDIRECT_DELAY);
}

window.addEventListener("pagehide", () => {
  left = true;
  if (timerId) window.clearTimeout(timerId);
});
window.addEventListener("beforeunload", () => {
  left = true;
  if (timerId) window.clearTimeout(timerId);
});
document.addEventListener(
  "click",
  (e) => {
    const link = e.target?.closest?.("a[href]");
    if (!link) return;
    left = true;
    if (timerId) window.clearTimeout(timerId);
  },
  true
);
