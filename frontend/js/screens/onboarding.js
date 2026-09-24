import { redirectIfAuthed } from "../auth.js";

if (redirectIfAuthed()) {
  // ya redirigida
}

const params = new URLSearchParams(window.location.search);
const preRole = params.get("role");
const signupLink = document.getElementById("signupLink");
const options = document.querySelectorAll(".role-option");

function selectRole(role, { navigate = false } = {}) {
  options.forEach((btn) => {
    const active = btn.dataset.role === role;
    btn.classList.toggle("selected", active);
    btn.setAttribute("aria-pressed", String(active));
  });
  sessionStorage.setItem("cp_pending_role", role);
  if (signupLink) signupLink.href = `register.html?role=${encodeURIComponent(role)}`;
  if (navigate) {
    window.location.href = `register.html?role=${encodeURIComponent(role)}`;
  }
}

if (preRole === "Customer" || preRole === "Worker") {
  selectRole(preRole);
}

options.forEach((btn) => {
  btn.addEventListener("click", () => {
    selectRole(btn.dataset.role, { navigate: true });
  });
});
