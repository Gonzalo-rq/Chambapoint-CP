import { redirectIfAuthed } from "../auth.js";

const REDIRECT_DELAY = 1600;

if (redirectIfAuthed()) {
  // ya redirigió
} else {
  setTimeout(() => {
    window.location.href = "create-profile.html";
  }, REDIRECT_DELAY);
}
