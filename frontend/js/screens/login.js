import { login, redirectByRole, redirectIfAuthed } from "../auth.js";
import { toast, setLoading, fieldError } from "../ui.js";
import { ApiError } from "../api.js";

if (redirectIfAuthed()) {
  // ya redirigida
}

const form = document.getElementById("loginForm");
const emailInput = document.getElementById("email");
const passwordInput = document.getElementById("password");
const submitBtn = document.getElementById("submitBtn");
const togglePw = document.getElementById("togglePw");

togglePw?.addEventListener("click", () => {
  const show = passwordInput.type === "password";
  passwordInput.type = show ? "text" : "password";
  togglePw.textContent = show ? "Ocultar" : "Ver";
  togglePw.setAttribute("aria-label", show ? "Ocultar contraseña" : "Mostrar contraseña");
});

function validate() {
  let ok = true;
  const email = emailInput.value.trim();
  const password = passwordInput.value;

  if (!email) {
    fieldError(emailInput, "El correo es obligatorio.");
    ok = false;
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
    fieldError(emailInput, "Ingresa un correo válido.");
    ok = false;
  } else {
    fieldError(emailInput, "");
  }

  if (!password) {
    fieldError(passwordInput, "La contraseña es obligatoria.");
    ok = false;
  } else if (password.length < 6) {
    fieldError(passwordInput, "Mínimo 6 caracteres.");
    ok = false;
  } else {
    fieldError(passwordInput, "");
  }

  return ok;
}

[emailInput, passwordInput].forEach((el) => {
  el?.addEventListener("input", () => fieldError(el, ""));
});

form?.addEventListener("submit", async (e) => {
  e.preventDefault();
  if (!validate()) return;

  setLoading(submitBtn, true, "Ingresando...");
  try {
    const user = await login(emailInput.value.trim(), passwordInput.value);
    toast(`Hola, ${user.name}`, "success");
    redirectByRole(user.role);
  } catch (err) {
    const msg = err instanceof ApiError ? err.message : "Error inesperado.";
    toast(msg, "error");
    if (err instanceof ApiError && err.status === 401) {
      fieldError(passwordInput, "Credenciales inválidas.");
    }
  } finally {
    setLoading(submitBtn, false);
  }
});
