if (typeof API_BASE === 'undefined') {
  var API_BASE = "http://localhost:5153";
}

const alertMessage = document.getElementById("message");
const alertForm = document.getElementById("alertRuleForm");
const rulesList = document.getElementById("rulesList");
const notificationList = document.getElementById("notificationList");
const sidebarRuleCount = document.getElementById("sidebarRuleCount");
const ruleFormTitle = document.getElementById("ruleFormTitle");
const ruleFormBadge = document.getElementById("ruleFormBadge");
const checkAlertsButton = document.getElementById("checkAlertsButton");
const forceCheck = document.getElementById("forceCheck");
const sendTestButton = document.getElementById("sendTestButton");
const resetRuleButton = document.getElementById("resetRuleButton");
const markAllReadButton = document.getElementById("markAllReadButton");
const lastCheckTime = document.getElementById("lastCheckTime");
const lastCheckResult = document.getElementById("lastCheckResult");

let alertSummary = { activeRules: 0, todayTriggered: 0, pending: 0 };
let alertRules = [];
let notifications = [];
let pendingDeleteRuleId = null;
let pendingDeleteTimer = null;

document.addEventListener('DOMContentLoaded', async () => {
  setupEventListeners();
  resetForm();
  await Promise.all([
    loadAlertSummary(),
    loadAlertRules(),
    loadNotifications()
  ]);
});

function getAuthHeaders(includeJson = false) {
  const headers = includeJson ? { 'Content-Type': 'application/json' } : {};
  const token = localStorage.getItem('authToken');
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  return headers;
}

async function loadAlertSummary() {
  try {
    const response = await fetch(`${API_BASE}/api/alerts/summary`, {
      headers: getAuthHeaders()
    });

    if (response.ok) {
      alertSummary = await response.json();
      updateSummaryUI();
      return;
    }

    handleAuthOrError(response, 'Error al cargar resumen de alertas');
  } catch (error) {
    console.error('Error loading alert summary:', error);
    showMessage('Error de conexion al cargar resumen de alertas', 'error');
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
      updateSummaryUI();
      return;
    }

    handleAuthOrError(response, 'Error al cargar reglas de alerta');
  } catch (error) {
    console.error('Error loading alert rules:', error);
    showMessage('Error de conexion al cargar reglas de alerta', 'error');
  }
}

async function loadNotifications() {
  try {
    const response = await fetch(`${API_BASE}/api/notifications`, {
      headers: getAuthHeaders()
    });

    if (response.ok) {
      notifications = await response.json();
      renderNotifications();
      updateSummaryUI();
      return;
    }

    handleAuthOrError(response, 'Error al cargar historial de alertas');
  } catch (error) {
    console.error('Error loading notifications:', error);
    showMessage('Error de conexion al cargar historial de alertas', 'error');
  }
}

function updateSummaryUI() {
  const activeRulesElement = document.getElementById('activeRulesCount');
  const todayTriggeredElement = document.getElementById('todayTriggeredCount');
  const pendingElement = document.getElementById('pendingCount');
  const pendingNotifications = notifications.filter(notification => !notification.isRead).length;
  const activeRules = alertSummary.activeRules ?? alertRules.filter(rule => rule.isActive).length;
  const todayTriggered = alertSummary.todayTriggered ?? 0;

  if (activeRulesElement) activeRulesElement.textContent = activeRules;
  if (todayTriggeredElement) todayTriggeredElement.textContent = todayTriggered;
  if (pendingElement) pendingElement.textContent = pendingNotifications;
  if (sidebarRuleCount) sidebarRuleCount.textContent = `${activeRules} ${activeRules === 1 ? 'regla' : 'reglas'}`;
}

function renderAlertRules() {
  if (!rulesList) return;

  if (alertRules.length === 0) {
    rulesList.innerHTML = '<p class="empty-state">No tienes reglas configuradas. Crea tu primera regla.</p>';
    return;
  }

  rulesList.innerHTML = alertRules.map(rule => {
    const conditions = parseJson(rule.conditionsJson, {});
    const channels = parseJson(rule.channelsJson, []);
    const recipients = parseJson(rule.recipientsJson, []);
    const statusText = rule.isActive ? 'Activa' : 'Pausada';

    return `
      <article class="rule-item">
        <div>
          <strong>${escapeHtml(rule.name)}</strong>
          <p>${formatTrigger(rule.triggerType)} | Afinidad minima: ${conditions.affinityScore ?? 0}% | ${statusText}</p>
          <p>${conditions.syncBeforeCheck ? 'Sincroniza SEACE antes de revisar' : 'Revisa oportunidades guardadas'}</p>
          <p>Canales: ${channels.filter(channel => channel !== 'slack').map(formatChannel).join(', ') || 'Sin canales'}</p>
          <p>Destinatarios: ${recipients.join(', ') || 'Sin destinatarios'}</p>
        </div>
        <div class="rule-actions">
          <button class="edit-btn" type="button" data-edit-rule="${rule.ruleId}">Editar</button>
          <button class="delete-btn" type="button" data-delete-rule="${rule.ruleId}">Eliminar</button>
        </div>
      </article>
    `;
  }).join('');
}

function renderNotifications() {
  if (!notificationList) return;

  if (notifications.length === 0) {
    notificationList.innerHTML = `
      <li>
        <strong>No hay alertas generadas</strong>
        <span>Cuando una regla encuentre una oportunidad compatible, aparecera aqui.</span>
      </li>
    `;
    return;
  }

  notificationList.innerHTML = notifications.slice(0, 10).map(notification => `
    <li class="${notification.isRead ? 'is-read' : 'is-unread'}">
      <strong>${escapeHtml(notification.title)}</strong>
      <span>${escapeHtml(notification.message)}</span>
      <span>${formatNotificationMeta(notification)}</span>
      ${notification.isRead ? '' : `<button class="secondary-button small-button" type="button" data-read-notification="${notification.notificationId}">Marcar leida</button>`}
    </li>
  `).join('');
}

function setupEventListeners() {
  alertForm?.addEventListener('submit', async (event) => {
    event.preventDefault();
    await saveAlertRule();
  });

  alertForm?.addEventListener('input', updatePreview);
  alertForm?.addEventListener('change', updatePreview);

  resetRuleButton?.addEventListener('click', resetForm);
  checkAlertsButton?.addEventListener('click', checkAlertsNow);
  sendTestButton?.addEventListener('click', sendTestEmail);
  markAllReadButton?.addEventListener('click', markAllNotificationsAsRead);

  rulesList?.addEventListener('click', async (event) => {
    const editButton = event.target.closest('[data-edit-rule]');
    const deleteButton = event.target.closest('[data-delete-rule]');

    if (editButton) {
      editRule(Number(editButton.dataset.editRule));
    }

    if (deleteButton) {
      await deleteRule(Number(deleteButton.dataset.deleteRule));
    }
  });

  notificationList?.addEventListener('click', async (event) => {
    const readButton = event.target.closest('[data-read-notification]');
    if (readButton) {
      await markNotificationAsRead(Number(readButton.dataset.readNotification));
    }
  });
}

function resetForm() {
  if (!alertForm) return;

  alertForm.reset();
  alertForm.ruleId.value = '';
  alertForm.ruleName.value = '';
  alertForm.triggerType.value = 'alta_afinidad';
  alertForm.affinityScore.value = '85';
  alertForm.status.value = 'recomendado';
  alertForm.syncBeforeCheck.checked = false;
  alertForm.messageTemplate.value = '';
  alertForm.isActive.checked = true;
  alertForm.querySelector('input[name="channels"][value="panel"]').checked = true;
  alertForm.querySelector('input[name="channels"][value="email"]').checked = true;
  alertForm.querySelector('input[name="channels"][value="slack"]').checked = false;

  if (ruleFormTitle) ruleFormTitle.textContent = 'Configura tu regla de alerta';
  if (ruleFormBadge) ruleFormBadge.textContent = 'Borrador';
  updatePreview();
}

function editRule(ruleId) {
  const rule = alertRules.find(item => item.ruleId === ruleId);
  if (!rule || !alertForm) return;

  const conditions = parseJson(rule.conditionsJson, {});
  const channels = parseJson(rule.channelsJson, []);
  const recipients = parseJson(rule.recipientsJson, []);

  alertForm.ruleId.value = rule.ruleId;
  alertForm.ruleName.value = rule.name;
  alertForm.triggerType.value = rule.triggerType;
  alertForm.affinityScore.value = conditions.affinityScore ?? 85;
  alertForm.status.value = conditions.status ?? 'recomendado';
  alertForm.syncBeforeCheck.checked = Boolean(conditions.syncBeforeCheck);
  alertForm.recipients.value = recipients.join(', ');
  alertForm.messageTemplate.value = rule.messageTemplate || alertForm.messageTemplate.value;
  alertForm.isActive.checked = Boolean(rule.isActive);

  alertForm.querySelectorAll('input[name="channels"]').forEach(checkbox => {
    checkbox.checked = channels.includes(checkbox.value);
  });

  if (ruleFormTitle) ruleFormTitle.textContent = 'Editando regla';
  if (ruleFormBadge) ruleFormBadge.textContent = 'Edicion';
  updatePreview();
  showMessage('Regla cargada para edicion.', 'info');
}

async function deleteRule(ruleId) {
  if (pendingDeleteRuleId !== ruleId) {
    armDeleteConfirmation(ruleId);
    return;
  }

  try {
    const deleteButton = rulesList?.querySelector(`[data-delete-rule="${ruleId}"]`);
    setButtonLoading(deleteButton, true, 'Eliminando...');

    const response = await fetch(`${API_BASE}/api/alerts/rules/${ruleId}`, {
      method: 'DELETE',
      headers: getAuthHeaders()
    });

    if (response.ok) {
      clearDeleteConfirmation();
      showMessage('Regla eliminada.', 'success');
      await Promise.all([loadAlertSummary(), loadAlertRules()]);
      return;
    }

    handleAuthOrError(response, 'Error al eliminar regla de alerta');
  } catch (error) {
    console.error('Error deleting alert rule:', error);
    showMessage('Error de conexion al eliminar regla de alerta', 'error');
  } finally {
    const deleteButton = rulesList?.querySelector(`[data-delete-rule="${ruleId}"]`);
    setButtonLoading(deleteButton, false, 'Eliminar');
  }
}

function armDeleteConfirmation(ruleId) {
  clearDeleteConfirmation();
  pendingDeleteRuleId = ruleId;

  const button = rulesList?.querySelector(`[data-delete-rule="${ruleId}"]`);
  if (button) {
    button.textContent = 'Confirmar';
    button.classList.add('confirm-delete');
  }

  showMessage('Presiona Confirmar para eliminar la regla.', 'info');
  pendingDeleteTimer = setTimeout(clearDeleteConfirmation, 6000);
}

function clearDeleteConfirmation() {
  if (pendingDeleteTimer) {
    clearTimeout(pendingDeleteTimer);
    pendingDeleteTimer = null;
  }

  if (pendingDeleteRuleId !== null) {
    const button = rulesList?.querySelector(`[data-delete-rule="${pendingDeleteRuleId}"]`);
    if (button) {
      button.textContent = 'Eliminar';
      button.classList.remove('confirm-delete');
    }
  }

  pendingDeleteRuleId = null;
}

async function saveAlertRule() {
  if (!alertForm) return;

  const formData = new FormData(alertForm);
  const channels = Array.from(alertForm.querySelectorAll('input[name="channels"]:checked:not(:disabled)')).map(input => input.value);
  const recipients = String(formData.get('recipients') || '')
    .split(',')
    .map(email => email.trim())
    .filter(Boolean);

  if (channels.length === 0) {
    showMessage('Selecciona al menos un canal para la alerta.', 'error');
    return;
  }

  if (channels.includes('email') && recipients.length === 0) {
    showMessage('Agrega al menos un destinatario para enviar correos.', 'error');
    return;
  }

  const rulePayload = {
    name: String(formData.get('ruleName') || '').trim(),
    triggerType: formData.get('triggerType'),
    conditionsJson: JSON.stringify({
      affinityScore: Number.parseInt(formData.get('affinityScore'), 10) || 0,
      status: formData.get('status'),
      syncBeforeCheck: formData.get('syncBeforeCheck') === 'on'
    }),
    channelsJson: JSON.stringify(channels),
    recipientsJson: JSON.stringify(recipients),
    messageTemplate: String(formData.get('messageTemplate') || '').trim() || 'Resumen automatico de oportunidades compatibles.',
    isActive: formData.get('isActive') === 'on'
  };

  const ruleId = String(formData.get('ruleId') || '').trim();
  const url = ruleId ? `${API_BASE}/api/alerts/rules/${ruleId}` : `${API_BASE}/api/alerts/rules`;
  const method = ruleId ? 'PUT' : 'POST';

  try {
    const saveButtons = [
      document.getElementById('saveRuleButton'),
      alertForm.querySelector('button[type="submit"]')
    ].filter(Boolean);
    saveButtons.forEach(button => setButtonLoading(button, true, ruleId ? 'Actualizando...' : 'Guardando...'));

    const response = await fetch(url, {
      method,
      headers: getAuthHeaders(true),
      body: JSON.stringify(rulePayload)
    });

    if (response.ok) {
      showMessage(ruleId ? 'Regla actualizada exitosamente.' : 'Regla creada exitosamente.', 'success');
      resetForm();
      await Promise.all([loadAlertSummary(), loadAlertRules()]);
      return;
    }

    handleAuthOrError(response, 'Error al guardar regla de alerta');
  } catch (error) {
    console.error('Error saving alert rule:', error);
    showMessage('Error de conexion al guardar regla de alerta', 'error');
  } finally {
    const saveButtons = [
      document.getElementById('saveRuleButton'),
      alertForm.querySelector('button[type="submit"]')
    ].filter(Boolean);
    saveButtons.forEach(button => setButtonLoading(button, false, button.id === 'saveRuleButton' ? 'Guardar regla' : 'Guardar regla'));
  }
}

async function sendTestEmail() {
  try {
    setButtonLoading(sendTestButton, true, 'Enviando...');
    const response = await fetch(`${API_BASE}/api/alerts/send-test`, {
      method: 'POST',
      headers: getAuthHeaders()
    });

    if (response.ok) {
      const result = await response.json();
      showMessage(result.message || 'Correo de prueba enviado exitosamente.', 'success');
      return;
    }

    handleAuthOrError(response, 'Error al enviar correo de prueba');
  } catch (error) {
    console.error('Error sending test email:', error);
    showMessage('Error de conexion al enviar correo de prueba', 'error');
  } finally {
    setButtonLoading(sendTestButton, false, 'Enviar prueba');
  }
}

async function checkAlertsNow() {
  try {
    setButtonLoading(checkAlertsButton, true, 'Revisando...');
    const force = Boolean(forceCheck?.checked);
    showMessage(force ? 'Forzando revision para pruebas...' : 'Revisando oportunidades contra tus reglas activas...', 'info');

    const response = await fetch(`${API_BASE}/api/alerts/check-now?force=${force}`, {
      method: 'POST',
      headers: getAuthHeaders()
    });

    if (response.ok) {
      const result = await response.json();
      await Promise.all([
        loadAlertSummary(),
        loadAlertRules(),
        loadNotifications()
      ]);
      updateLastCheck(result);
      showMessage(formatCheckResult(result), 'success');
      return;
    }

    handleAuthOrError(response, 'Error al ejecutar la revision de alertas');
  } catch (error) {
    console.error('Error checking alerts now:', error);
    showMessage('Error de conexion al revisar alertas', 'error');
  } finally {
    setButtonLoading(checkAlertsButton, false, 'Revisar ahora');
  }
}

async function markNotificationAsRead(notificationId) {
  try {
    const readButton = notificationList?.querySelector(`[data-read-notification="${notificationId}"]`);
    setButtonLoading(readButton, true, 'Marcando...');

    const response = await fetch(`${API_BASE}/api/notifications/${notificationId}/read`, {
      method: 'POST',
      headers: getAuthHeaders()
    });

    if (response.ok) {
      await Promise.all([loadNotifications(), loadAlertSummary()]);
      return;
    }

    handleAuthOrError(response, 'Error al marcar la alerta como leida');
  } catch (error) {
    console.error('Error marking notification as read:', error);
    showMessage('Error de conexion al marcar la alerta', 'error');
  } finally {
    const readButton = notificationList?.querySelector(`[data-read-notification="${notificationId}"]`);
    setButtonLoading(readButton, false, 'Marcar leida');
  }
}

async function markAllNotificationsAsRead() {
  try {
    setButtonLoading(markAllReadButton, true, 'Marcando...');
    const response = await fetch(`${API_BASE}/api/notifications/mark-all-read`, {
      method: 'POST',
      headers: getAuthHeaders()
    });

    if (response.ok) {
      await Promise.all([loadNotifications(), loadAlertSummary()]);
      showMessage('Alertas marcadas como leidas.', 'success');
      return;
    }

    handleAuthOrError(response, 'Error al marcar las alertas como leidas');
  } catch (error) {
    console.error('Error marking all notifications as read:', error);
    showMessage('Error de conexion al marcar alertas', 'error');
  } finally {
    setButtonLoading(markAllReadButton, false, 'Marcar leidas');
  }
}

function updatePreview() {
  if (!alertForm) return;

  const previewSubject = document.getElementById('previewSubject');
  const previewRecipients = document.getElementById('previewRecipients');
  const previewMessage = document.getElementById('previewMessage');
  const ruleName = alertForm.ruleName.value.trim() || 'regla configurada';
  const recipients = alertForm.recipients.value.trim() || 'Sin destinatarios';
  const affinity = alertForm.affinityScore.value || '85';
  const syncBeforeCheck = alertForm.syncBeforeCheck?.checked;

  if (previewSubject) previewSubject.textContent = `Alerta: ${ruleName}`;
  if (previewRecipients) previewRecipients.textContent = recipients;
  if (previewMessage) {
    previewMessage.innerHTML = `
      <p>Se enviara un solo resumen con todas las oportunidades que superen ${escapeHtml(affinity)}% de afinidad.</p>
      <p>Incluira codigo, entidad, monto, cierre, modalidad, ubicacion y afinidad, ordenado de mayor a menor.</p>
      <p>${syncBeforeCheck ? 'Antes de revisar, sincronizara SEACE usando tus filtros guardados.' : 'Revisara las oportunidades que ya estan guardadas.'}</p>
    `;
  }
}

function formatNotificationMeta(notification) {
  const parts = [];
  if (notification.opportunityProcessCode) parts.push(notification.opportunityProcessCode);
  if (notification.affinityScore !== null && notification.affinityScore !== undefined) parts.push(`${notification.affinityScore}% afinidad`);
  if (notification.createdAtUtc) parts.push(new Date(notification.createdAtUtc).toLocaleString('es-PE'));
  return parts.join(' | ');
}

function formatTrigger(triggerType) {
  const labels = {
    alta_afinidad: 'Alta afinidad',
    nueva_oportunidad: 'Nueva oportunidad',
    cierre_proximo: 'Cierre proximo'
  };
  return labels[triggerType] || triggerType;
}

function formatChannel(channel) {
  const labels = {
    panel: 'Panel',
    email: 'Correo',
    slack: 'Slack'
  };
  return labels[channel] || channel;
}

function parseJson(value, fallback) {
  try {
    return value ? JSON.parse(value) : fallback;
  } catch {
    return fallback;
  }
}

function handleAuthOrError(response, fallbackMessage) {
  if (response.status === 401) {
    showMessage('Inicia sesion para usar las alertas.', 'error');
    return;
  }

  showMessage(fallbackMessage, 'error');
}

function updateLastCheck(result) {
  if (lastCheckTime) {
    lastCheckTime.textContent = new Date().toLocaleTimeString('es-PE', {
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  if (lastCheckResult) {
    lastCheckResult.textContent = formatCheckResult(result);
  }
}

function formatCheckResult(result) {
  const matches = Number(result?.opportunitiesMatched ?? 0);
  const summaries = Number(result?.summariesCreated ?? 0);
  const forced = Boolean(result?.force);

  if (matches === 0) {
    return forced
      ? 'Revision forzada: no se encontraron nuevas coincidencias.'
      : 'Sin nuevas coincidencias por ahora.';
  }

  return `${forced ? 'Revision forzada' : 'Revision'}: ${matches} oportunidad(es), ${summaries} resumen(es) generado(s).`;
}

function showMessage(text, type = 'info') {
  if (!alertMessage) return;

  alertMessage.textContent = text;
  alertMessage.className = `message ${type}`;
  alertMessage.style.display = 'block';

  setTimeout(() => {
    alertMessage.style.display = 'none';
  }, 5000);
}

function setButtonLoading(button, isLoading, loadingText) {
  if (!button) return;

  if (isLoading) {
    button.dataset.previousText = button.textContent;
    button.textContent = loadingText;
    button.disabled = true;
    return;
  }

  button.textContent = button.dataset.previousText || loadingText;
  button.disabled = false;
}

function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}
