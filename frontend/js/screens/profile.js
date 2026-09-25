import { requireAuth, logout } from "../auth.js";
import { toast } from "../ui.js";
import { avatarHtml, escapeHtml, bottomNav } from "../components.js";

const user = requireAuth();
if (!user) throw new Error("Sin sesión");

document.getElementById("profileAvatar").outerHTML = avatarHtml(user, "avatar-lg");
document.getElementById("profileName").textContent = user.name || "Sin nombre";
document.getElementById("profileRole").textContent = user.role === "Worker" ? "Trabajador" : "Cliente";
document.getElementById("profileEmail").textContent = user.email || "";
document.body.insertAdjacentHTML("beforeend", bottomNav("profile", user.role));

document.getElementById("logoutBtn").addEventListener("click", () => {
  if (!window.confirm("¿Cerrar sesión?")) return;
  toast("Sesión cerrada", "success");
  setTimeout(() => logout(), 350);
});

document.title = `${escapeHtml(user.name || "Mi perfil")} — ChambaPoint`;
