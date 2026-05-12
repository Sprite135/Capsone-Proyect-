if (typeof API_BASE === 'undefined') {
  var API_BASE = "http://localhost:5153";
}
const authForm = document.querySelector("[data-auth-form]");
const formStatus = document.getElementById("formStatus");
const googleButtons = document.querySelectorAll(".google-button");

window.addEventListener("message", (event) => {
  if (!event.data || event.data.type !== "licitia-google-auth") {
    return;
  }

  if (event.data.error) {
    setStatus(`Error en autenticacion de Google: ${event.data.error}`, "error");
    return;
  }

  if (event.data.token) {
    try {
      localStorage.setItem("authToken", event.data.token);
      window.AUTH_TOKEN = event.data.token;
      storeAuthUser(event.data.user || readUserFromToken(event.data.token));
    } catch (e) {
      // ignore storage errors
    }
  }

  window.location.href = event.data.redirectUrl || "index.html";
});

googleButtons.forEach((button) => {
  button.addEventListener("click", (event) => {
    event.preventDefault();
    openGoogleAuthWindow(button.getAttribute("href") || "/api/auth/google/login");
  });
});

function openGoogleAuthWindow(authUrl) {
  const url = authUrl.startsWith("http") ? authUrl : `${API_BASE}${authUrl}`;
  const width = 520;
  const height = 680;
  const left = Math.max(0, window.screenX + (window.outerWidth - width) / 2);
  const top = Math.max(0, window.screenY + (window.outerHeight - height) / 2);
  const features = [
    `width=${width}`,
    `height=${height}`,
    `left=${Math.round(left)}`,
    `top=${Math.round(top)}`,
    "popup=yes",
    "toolbar=no",
    "menubar=no",
    "location=no",
    "status=no",
    "resizable=yes",
    "scrollbars=yes"
  ].join(",");

  const popup = window.open(url, "licitia-google-auth", features);
  if (!popup) {
    setStatus("El navegador bloqueo la ventana de Google. Permite ventanas emergentes para iniciar sesion.", "error");
    return;
  }

  // Try to focus popup with error handling for Cross-Origin-Opener-Policy
  try {
    popup.focus();
  } catch (error) {
    console.log("[Google Auth] Could not focus popup due to Cross-Origin policy:", error.message);
    // Focus is optional, continue without it
  }
}

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
            password: String(formData.get("password") ?? ""),
            rememberMe: formData.get("rememberMe") === "on"
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
        
        // Handle account lockout
        if (data.isLocked) {
          setStatus(errorMessage, "error");
          // Disable submit button temporarily
          submitButton.disabled = true;
          setTimeout(() => {
            submitButton.disabled = false;
          }, 10 * 1000); // 10 seconds
          return;
        }
        
        // Show remaining attempts if available
        if (data.remainingAttempts !== undefined) {
          setStatus(`${errorMessage} (${data.remainingAttempts} intentos restantes)`, "error");
          return;
        }
        
        setStatus(errorMessage, "error");
        return;
      }

      setStatus(data.message || "Operacion completada correctamente.", "success");

      // Store JWT if provided
      if (data && data.token) {
        try {
          localStorage.setItem("authToken", data.token);
          window.AUTH_TOKEN = data.token;
          storeAuthUser(data.user || readUserFromToken(data.token));
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

function storeAuthUser(user) {
  if (!user) {
    return;
  }

  localStorage.setItem("authUser", JSON.stringify({
    fullName: user.fullName || user.name || "",
    email: user.email || "",
    role: user.role || ""
  }));
}

function readUserFromToken(token) {
  try {
    const [, payload] = token.split(".");
    if (!payload) {
      return null;
    }

    const base64 = payload.replace(/-/g, "+").replace(/_/g, "/");
    const padded = base64.padEnd(base64.length + (4 - base64.length % 4) % 4, "=");
    const json = JSON.parse(decodeURIComponent(escape(atob(padded))));

    return {
      fullName: json.unique_name || json.name || "",
      email: json.email || "",
      role: json.role || json["http://schemas.microsoft.com/ws/2008/06/identity/claims/role"] || ""
    };
  } catch (e) {
    return null;
  }
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

// Handle OAuth redirect from Google - token comes in URL params
(function handleGoogleOAuthRedirect() {
  const urlParams = new URLSearchParams(window.location.search);
  const token = urlParams.get('token');
  const name = urlParams.get('name');
  const error = urlParams.get('error');

  if (error) {
    setStatus('Error en autenticacion de Google: ' + error, 'error');
    return;
  }

  if (token) {
    try {
      localStorage.setItem('authToken', token);
      window.AUTH_TOKEN = token;
      storeAuthUser({
        fullName: name || '',
        email: urlParams.get('email') || '',
        role: ''
      });
    } catch (e) {
      // ignore storage errors
    }

    if (window.opener && !window.opener.closed) {
      window.opener.postMessage({
        type: 'licitia-google-auth',
        token,
        redirectUrl: 'index.html'
      }, '*');
      window.close();
      return;
    }

    // Clean URL and redirect to dashboard
    window.history.replaceState({}, document.title, window.location.pathname);
    window.location.href = 'index.html';
  }
})();
