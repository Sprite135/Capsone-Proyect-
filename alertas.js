if (typeof API_BASE === 'undefined') {
  var API_BASE = "http://localhost:5153";
}

const alertMessage = document.getElementById("message");
const alertDemoButtons = document.querySelectorAll("[data-demo-message]");

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
  const rulesList = document.getElementById('rulesList');
  if (!rulesList) return;

  if (alertRules.length === 0) {
    rulesList.innerHTML = '<p class="empty-state">No tienes reglas configuradas. Crea tu primera regla arriba.</p>';
    return;
  }

  const rulesHTML = alertRules.map(rule => `
    <div class="rule-item">
      <div>
        <h4>${rule.name}</h4>
        <p>
          Disparador: ${rule.triggerType} | 
          Afinidad: ${JSON.parse(rule.conditionsJson).affinityScore}% | 
          Canales: ${JSON.parse(rule.channelsJson).join(', ')}
        </p>
        <p>Destinatarios: ${JSON.parse(rule.recipientsJson).join(', ')}</p>
      </div>
      <div class="rule-actions">
        <button class="edit-btn" onclick="editRule(${rule.ruleId})">Editar</button>
        <button class="delete-btn" onclick="deleteRule(${rule.ruleId})">Eliminar</button>
      </div>
    </div>
  `).join('');

  rulesList.innerHTML = rulesHTML;
}

function resetForm() {
  const form = document.getElementById('alertRuleForm');
  if (form) {
    form.reset();
    // Reset checkboxes to default state
    document.querySelector('input[name="channels"][value="email"]').checked = true;
    document.querySelector('input[name="channels"][value="panel"]').checked = true;
    document.querySelector('input[name="channels"][value="slack"]').checked = false;
  }
}

async function editRule(ruleId) {
  try {
    const rule = alertRules.find(r => r.ruleId === ruleId);
    if (!rule) return;

    // Populate form with rule data
    const form = document.getElementById('alertRuleForm');
    if (form) {
      form.ruleName.value = rule.name;
      form.triggerType.value = rule.triggerType;
      
      const conditions = JSON.parse(rule.conditionsJson);
      form.affinityScore.value = conditions.affinityScore;
      form.status.value = conditions.status;
      
      const channels = JSON.parse(rule.channelsJson);
      document.querySelectorAll('input[name="channels"]').forEach(checkbox => {
        checkbox.checked = channels.includes(checkbox.value);
      });
      
      const recipients = JSON.parse(rule.recipientsJson);
      form.recipients.value = recipients.join(', ');
      
      form.messageTemplate.value = rule.messageTemplate;
      form.isActive.checked = rule.isActive;
    }

    showMessage('Regla cargada para edición. Modifica y guarda los cambios.', 'info');
  } catch (error) {
    console.error('Error loading rule for edit:', error);
    showMessage('Error al cargar regla para edición', 'error');
  }
}

async function deleteRule(ruleId) {
  if (!confirm('¿Estás seguro de que quieres eliminar esta regla?')) return;

  try {
    const response = await fetch(`${API_BASE}/api/alerts/rules/${ruleId}`, {
      method: 'DELETE',
      headers: getAuthHeaders()
    });

    if (response.ok) {
      showMessage('Regla eliminada exitosamente.', 'success');
      await loadAlertSummary();
      await loadAlertRules();
    } else if (response.status === 401) {
      showMessage('Inicia sesión para eliminar reglas de alerta.', 'error');
    } else {
      showMessage('Error al eliminar regla de alerta', 'error');
    }
  } catch (error) {
    console.error('Error deleting alert rule:', error);
    showMessage('Error de conexión al eliminar regla de alerta', 'error');
  }
}

function setupEventListeners() {
  // Demo buttons - show message instead of real functionality
  alertDemoButtons.forEach(button => {
    button.addEventListener('click', (event) => {
      event.preventDefault();
      const message = button.getAttribute('data-demo-message');
      showMessage(message, 'success');
    });
  });

  // Send test button
  const sendTestButton = document.querySelector('.secondary-button');
  if (sendTestButton && sendTestButton.textContent.includes('Enviar prueba')) {
    sendTestButton.addEventListener('click', async (event) => {
      event.preventDefault();
      await sendTestEmail();
    });
  }

  // Alert form submission
  const alertForm = document.getElementById('alertRuleForm');
  if (alertForm) {
    alertForm.addEventListener('submit', async (event) => {
      event.preventDefault();
      await saveAlertRule();
    });
  }
}

async function saveAlertRule() {
  try {
    // Collect form data
    const form = document.getElementById('alertRuleForm');
    const formData = new FormData(form);
    
    // Get selected channels
    const channels = [];
    document.querySelectorAll('input[name="channels"]:checked').forEach(checkbox => {
      channels.push(checkbox.value);
    });
    
    // Get recipients as array
    const recipientsText = formData.get('recipients');
    const recipients = recipientsText.split(',').map(email => email.trim()).filter(email => email);
    
    const newRule = {
      name: formData.get('ruleName'),
      triggerType: formData.get('triggerType'),
      conditionsJson: JSON.stringify({ 
        affinityScore: parseInt(formData.get('affinityScore')), 
        status: formData.get('status') 
      }),
      channelsJson: JSON.stringify(channels),
      recipientsJson: JSON.stringify(recipients),
      messageTemplate: formData.get('messageTemplate'),
      isActive: formData.get('isActive') === 'on'
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
  if (alertMessage) {
    alertMessage.textContent = text;
    alertMessage.className = `message ${type}`;
    alertMessage.style.display = 'block';
    
    setTimeout(() => {
      alertMessage.style.display = 'none';
    }, 5000);
  }
}
