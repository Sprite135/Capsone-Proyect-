if (typeof API_BASE === 'undefined') {
  var API_BASE = "http://localhost:5153";
}

const message = document.getElementById("message");
const demoButtons = document.querySelectorAll("[data-demo-message]");

let alertSummary = { activeRules: 0, todayTriggered: 0, pending: 0 };
let alertRules = [];

function getAuthHeaders(includeJson = false) {
  const headers = includeJson ? { 'Content-Type': 'application/json' } : {};
  const token = localStorage.getItem('authToken');
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  return headers;
}

// Load alert summary on page load
document.addEventListener('DOMContentLoaded', async () => {
  await loadAlertSummary();
  await loadAlertRules();
  setupEventListeners();
});

async function loadAlertSummary() {
  try {
    const response = await fetch(`${API_BASE}/api/alerts/summary`, {
      headers: getAuthHeaders()
    });
    if (response.ok) {
      alertSummary = await response.json();
      updateSummaryUI();
    } else if (response.status === 401) {
      showMessage('Inicia sesión para ver las alertas.', 'error');
    } else {
      showMessage('Error al cargar resumen de alertas', 'error');
    }
  } catch (error) {
    console.error('Error loading alert summary:', error);
    showMessage('Error de conexión al cargar resumen de alertas', 'error');
  }
}

async function loadAlertRules() {
  try {
    const response = await fetch(`${API_BASE}/api/alerts/rules`, {
      headers: getAuthHeaders()
    });
    if (response.ok) {
      alertRules = await response.json();
      renderAlertRules();
    } else if (response.status === 401) {
      showMessage('Inicia sesión para ver las reglas de alerta.', 'error');
    } else {
      showMessage('Error al cargar reglas de alerta', 'error');
    }
  } catch (error) {
    console.error('Error loading alert rules:', error);
    showMessage('Error de conexión al cargar reglas de alerta', 'error');
  }
}

function updateSummaryUI() {
  const activeRulesElement = document.querySelector('.summary-strip article:nth-child(1) strong');
  const todayTriggeredElement = document.querySelector('.summary-strip article:nth-child(2) strong');
  const pendingElement = document.querySelector('.summary-strip article:nth-child(3) strong');

  if (activeRulesElement) activeRulesElement.textContent = alertSummary.activeRules;
  if (todayTriggeredElement) todayTriggeredElement.textContent = alertSummary.todayTriggered;
  if (pendingElement) pendingElement.textContent = alertSummary.pending;
}

function renderAlertRules() {
  // For now, just log the rules
  console.log('Alert rules loaded:', alertRules);
  // TODO: Render alert rules in the UI
}

function setupEventListeners() {
  // Demo buttons - show message instead of real functionality
  demoButtons.forEach(button => {
    button.addEventListener('click', (event) => {
      event.preventDefault();
      const message = button.getAttribute('data-demo-message');
      showMessage(message, 'success');
    });
  });

  // Save rule button
  const saveRuleButton = document.querySelector('button[type="button"]:has-text("Guardar regla")');
  if (saveRuleButton) {
    saveRuleButton.addEventListener('click', async (event) => {
      event.preventDefault();
      await saveAlertRule();
    });
  }

  // Send test button
  const sendTestButton = document.querySelector('button[type="button"]:has-text("Enviar prueba")');
  if (sendTestButton) {
    sendTestButton.addEventListener('click', async (event) => {
      event.preventDefault();
      await sendTestEmail();
    });
  }
}

async function saveAlertRule() {
  try {
    // This is a simplified version - in production you'd collect form data
    const newRule = {
      name: "Nueva regla de alerta",
      trigger: "alta_afinidad",
      conditionsJson: JSON.stringify({ affinityScore: 85, status: "recomendado" }),
      channelsJson: JSON.stringify(["email", "panel"]),
      recipientsJson: JSON.stringify(["comercial@emdersoft.com"]),
      messageTemplate: "Nueva licitación X con {score}% afinidad",
      isActive: true
    };

    const response = await fetch(`${API_BASE}/api/alerts/rules`, {
      method: 'POST',
      headers: getAuthHeaders(true),
      body: JSON.stringify(newRule)
    });

    if (response.ok) {
      showMessage('Regla de alerta creada exitosamente.', 'success');
      await loadAlertSummary();
      await loadAlertRules();
    } else if (response.status === 401) {
      showMessage('Inicia sesión para crear reglas de alerta.', 'error');
    } else {
      showMessage('Error al crear regla de alerta', 'error');
    }
  } catch (error) {
    console.error('Error saving alert rule:', error);
    showMessage('Error de conexión al guardar regla de alerta', 'error');
  }
}

async function sendTestEmail() {
  try {
    const response = await fetch(`${API_BASE}/api/alerts/send-test`, {
      method: 'POST',
      headers: getAuthHeaders()
    });

    if (response.ok) {
      const result = await response.json();
      showMessage(result.message || 'Correo de prueba enviado exitosamente.', 'success');
    } else if (response.status === 401) {
      showMessage('Inicia sesión para enviar correos de prueba.', 'error');
    } else {
      showMessage('Error al enviar correo de prueba', 'error');
    }
  } catch (error) {
    console.error('Error sending test email:', error);
    showMessage('Error de conexión al enviar correo de prueba', 'error');
  }
}

function showMessage(text, type = 'info') {
  if (message) {
    message.textContent = text;
    message.className = `message ${type}`;
    message.style.display = 'block';
    
    setTimeout(() => {
      message.style.display = 'none';
    }, 5000);
  }
}
