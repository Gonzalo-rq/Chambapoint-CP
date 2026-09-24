import { register, redirectByRole, redirectIfAuthed } from "../auth.js";
import { toast, setLoading, fieldError } from "../ui.js";
import { ApiError } from "../api.js";

if (redirectIfAuthed()) {
  // ya redirigida
}

const params = new URLSearchParams(window.location.search);
const preRole = params.get("role") || sessionStorage.getItem("cp_pending_role");
const roleSelect = document.getElementById("role");
if (roleSelect && (preRole === "Customer" || preRole === "Worker")) {
  roleSelect.value = preRole;
}

const form = document.getElementById("registerForm");
const nameInput = document.getElementById("name");
const emailInput = document.getElementById("email");
const passwordInput = document.getElementById("password");
const confirmInput = document.getElementById("confirmPassword");
const submitBtn = document.getElementById("submitBtn");
const togglePw = document.getElementById("togglePw");

togglePw?.addEventListener("click", () => {
  const show = passwordInput.type === "password";
  passwordInput.type = show ? "text" : "password";
  togglePw.textContent = show ? "Ocultar" : "Ver";
});

function validate() {
  let ok = true;
  const name = nameInput.value.trim();
  const email = emailInput.value.trim();
  const password = passwordInput.value;
  const confirm = confirmInput.value;

  if (name.length < 2) {
    fieldError(nameInput, "Ingresa tu nombre (mínimo 2 caracteres).");
    ok = false;
  } else {
    fieldError(nameInput, "");
  }

  if (!email) {
    fieldError(emailInput, "El correo es obligatorio.");
    ok = false;
  } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
    fieldError(emailInput, "Ingresa un correo válido.");
    ok = false;
  } else {
    fieldError(emailInput, "");
  }

  if (password.length < 6) {
    fieldError(passwordInput, "La contraseña debe tener al menos 6 caracteres.");
    ok = false;
  } else {
    fieldError(passwordInput, "");
  }

  if (!confirm) {
    fieldError(confirmInput, "Confirma tu contraseña.");
    ok = false;
  } else if (confirm !== password) {
    fieldError(confirmInput, "Las contraseñas no coinciden.");
    ok = false;
  } else {
    fieldError(confirmInput, "");
  }

  return ok;
}

[nameInput, emailInput, passwordInput, confirmInput].forEach((el) => {
  el?.addEventListener("input", () => fieldError(el, ""));
});

form?.addEventListener("submit", async (e) => {
  e.preventDefault();
  if (!validate()) return;

  setLoading(submitBtn, true, "Creando cuenta...");
  try {
    const user = await register({
      name: nameInput.value.trim(),
      email: emailInput.value.trim(),
      password: passwordInput.value,
      role: roleSelect.value,
    });
    sessionStorage.removeItem("cp_pending_role");
    toast(`Cuenta creada. ¡Bienvenido/a, ${user.name}!`, "success");
    redirectByRole(user.role);
  } catch (err) {
    const msg = err instanceof ApiError ? err.message : "Error inesperado.";
    toast(msg, "error");
    if (err instanceof ApiError && err.status === 409) {
      fieldError(emailInput, "Ese correo ya está registrado.");
    }
  } finally {
    setLoading(submitBtn, false);
  }
});
