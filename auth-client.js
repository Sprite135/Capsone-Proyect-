const API_BASE = "http://localhost:5153";
const authForm = document.querySelector("[data-auth-form]");
const formStatus = document.getElementById("formStatus");

if (authForm) {
  authForm.addEventListener("submit", async (event) => {
    event.preventDefault();

    const submitButton = authForm.querySelector("button[type='submit']");
    const formData = new FormData(authForm);
    const mode = authForm.dataset.authForm;

    const payload =
      mode === "login"
        ? {
            email: String(formData.get("email") ?? "").trim(),
            password: String(formData.get("password") ?? "")
          }
        : {
            fullName: String(formData.get("fullName") ?? "").trim(),
            companyName: String(formData.get("companyName") ?? "").trim(),
            email: String(formData.get("email") ?? "").trim(),
            role: String(formData.get("role") ?? "").trim(),
            password: String(formData.get("password") ?? "")
          };

    const endpoint = mode === "login" ? "/api/auth/login" : "/api/auth/register";

    try {
      setStatus("Conectando con el backend...", "success");
      submitButton.disabled = true;

      const response = await fetch(`${API_BASE}${endpoint}`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify(payload)
      });

      const data = await response.json().catch(() => ({}));

      if (!response.ok) {
        const validationMessage = readValidationMessage(data);
        const errorMessage = validationMessage || data.message || "No se pudo completar la solicitud.";
        setStatus(errorMessage, "error");
        return;
      }

      setStatus(data.message || "Operacion completada correctamente.", "success");

      // Store JWT if provided
      if (data && data.token) {
        try {
          localStorage.setItem("authToken", data.token);
          window.AUTH_TOKEN = data.token;
        } catch (e) {
          // ignore storage errors
        }
      }

      const redirectUrl = data.redirectUrl || "index.html";
      window.setTimeout(() => {
        window.location.href = redirectUrl;
      }, 700);
    } catch (error) {
      setStatus(
        "No fue posible conectar con la API. Verifica que LicitIA.Api este ejecutandose en http://localhost:5153.",
        "error");
    } finally {
      submitButton.disabled = false;
    }
  });
}

function setStatus(message, type) {
  if (!formStatus) {
    return;
  }

  formStatus.textContent = message;
  formStatus.classList.remove("success", "error");
  formStatus.classList.add(type);
}

function readValidationMessage(data) {
  if (!data || !data.errors) {
    return "";
  }

  const firstKey = Object.keys(data.errors)[0];
  if (!firstKey) {
    return "";
  }

  const messages = data.errors[firstKey];
  return Array.isArray(messages) && messages.length > 0 ? messages[0] : "";
}

// Password visibility toggle for any .password-toggle buttons
document.addEventListener("click", (e) => {
  const btn = e.target.closest && e.target.closest('.password-toggle');
  if (!btn) return;
  const wrap = btn.closest('.password-wrap');
  if (!wrap) return;
  const input = wrap.querySelector('input[type="password"], input[type="text"]');
  if (!input) return;
  const wasPassword = input.type === 'password';
  input.type = wasPassword ? 'text' : 'password';
  // aria-pressed = true when the password is visible (input type=text)
  const visible = input.type === 'text';
  btn.setAttribute('aria-pressed', String(visible));
});
