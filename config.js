const CONFIG_API_BASE = 'http://localhost:5153/api';

let currentProfile = null;
let preferredKeywords = [];
let excludedKeywords = [];
let isConfigInitialized = false;
let isLoadingProfile = false;
let areEventListenersReady = false;
let profileLoadPromise = null;

function getAuthHeaders(includeJson = false) {
  const headers = includeJson ? { 'Content-Type': 'application/json' } : {};
  const token = localStorage.getItem('authToken');
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  return headers;
}

document.addEventListener('DOMContentLoaded', async () => {
  if (isConfigInitialized) {
    return;
  }

  isConfigInitialized = true;
  populateSeaceYearOptions();
  hydrateAccountPanel();
  await loadProfile();
  setupEventListeners();
});

async function loadProfile() {
  if (isLoadingProfile && profileLoadPromise) {
    return profileLoadPromise;
  }

  isLoadingProfile = true;
  profileLoadPromise = doLoadProfile();

  try {
    return await profileLoadPromise;
  } finally {
    isLoadingProfile = false;
    profileLoadPromise = null;
  }
}

async function doLoadProfile() {
  try {
    const response = await fetch(`${CONFIG_API_BASE}/profile`, {
      headers: getAuthHeaders()
    });
    if (response.ok) {
      currentProfile = await response.json();
      populateForm(currentProfile);
    } else if (response.status === 401) {
      showMessage('Inicia sesion para cargar tu configuracion.', 'error');
    } else if (response.status === 404) {
      showMessage('No se encontro perfil. Crea uno nuevo.', 'info');
    } else {
      showMessage('Error al cargar perfil', 'error');
    }
  } catch (error) {
    console.error('Error loading profile:', error);
    showMessage('Error de conexion al cargar perfil', 'error');
  }
}

function populateForm(profile) {
  const companyNameInput = document.getElementById('companyName');
  if (companyNameInput) {
    companyNameInput.value = profile.companyName || '';
  }

  const seaceObjectDescriptionInput = document.getElementById('seaceObjectDescription');
  if (seaceObjectDescriptionInput) {
    seaceObjectDescriptionInput.value = profile.seaceObjectDescription || '';
  }

  const seaceCallYearInput = document.getElementById('seaceCallYear');
  if (seaceCallYearInput) {
    seaceCallYearInput.value = String(normalizeSeaceYear(profile.seaceCallYear));
  }

  preferredKeywords = profile.preferredKeywords || [];
  excludedKeywords = profile.excludedKeywords || [];
  renderKeywords();
}

function populateSeaceYearOptions() {
  const select = document.getElementById('seaceCallYear');
  if (!select) {
    return;
  }

  const currentYear = new Date().getFullYear();
  const years = [];
  for (let year = currentYear; year >= 2004; year -= 1) {
    years.push(`<option value="${year}">${year}</option>`);
  }

  select.innerHTML = years.join('');
  select.value = String(currentYear);
}

function setupEventListeners() {
  if (areEventListenersReady) {
    return;
  }

  areEventListenersReady = true;

  const safeAddListener = (id, event, handler) => {
    const el = document.getElementById(id);
    if (el) el.addEventListener(event, handler);
  };

  safeAddListener('saveProfile', 'click', saveProfile);
  safeAddListener('resetProfile', 'click', resetForm);
  safeAddListener('recalculateAffinity', 'click', recalculateAffinity);

  const logoutButton = document.querySelector('.logout-button');
  if (logoutButton) {
    logoutButton.addEventListener('click', () => {
      localStorage.removeItem('authToken');
      localStorage.removeItem('authUser');
      window.AUTH_TOKEN = null;
    });
  }

  safeAddListener('addPreferredKeyword', 'click', () => addKeyword('preferred'));
  safeAddListener('addExcludedKeyword', 'click', () => addKeyword('excluded'));

  safeAddListener('preferredKeywordInput', 'keypress', (e) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      addKeyword('preferred');
    }
  });
  safeAddListener('excludedKeywordInput', 'keypress', (e) => {
    if (e.key === 'Enter') {
      e.preventDefault();
      addKeyword('excluded');
    }
  });
}

function hydrateAccountPanel() {
  const user = getStoredUser() || getUserFromToken();
  if (!user) {
    return;
  }

  const displayName = user.fullName || user.name || user.email || 'Usuario';
  const email = user.email || 'Correo no disponible';
  const role = user.role || 'Cuenta Google';

  setText('accountName', displayName);
  setText('accountEmail', email);
  setText('accountRole', role);
  setText('userChip', getInitials(displayName, email));
}

function getStoredUser() {
  try {
    const stored = localStorage.getItem('authUser');
    return stored ? JSON.parse(stored) : null;
  } catch (error) {
    return null;
  }
}

function getUserFromToken() {
  const token = localStorage.getItem('authToken');
  if (!token) {
    return null;
  }

  try {
    const [, payload] = token.split('.');
    const json = JSON.parse(decodeBase64Url(payload));
    const user = {
      fullName: json.unique_name || json.name || '',
      email: json.email || '',
      role: json.role || json['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || ''
    };

    localStorage.setItem('authUser', JSON.stringify(user));
    return user;
  } catch (error) {
    console.warn('No se pudo leer la cuenta desde el token.', error);
    return null;
  }
}

function decodeBase64Url(value) {
  const base64 = value.replace(/-/g, '+').replace(/_/g, '/');
  const padded = base64.padEnd(base64.length + (4 - base64.length % 4) % 4, '=');
  const binary = atob(padded);
  const bytes = Uint8Array.from(binary, (char) => char.charCodeAt(0));
  return new TextDecoder().decode(bytes);
}

function getInitials(name, email) {
  const source = name && name !== 'Usuario' ? name : email;
  const parts = String(source || 'US')
    .replace(/@.*/, '')
    .split(/\s+/)
    .filter(Boolean);

  if (parts.length === 0) {
    return 'US';
  }

  return parts
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase();
}

function setText(id, value) {
  const el = document.getElementById(id);
  if (el) {
    el.textContent = value;
  }
}

function addKeyword(type) {
  const inputId = type === 'preferred' ? 'preferredKeywordInput' : 'excludedKeywordInput';
  const input = document.getElementById(inputId);
  if (!input) return;

  const keyword = input.value.trim();
  if (!keyword) return;

  if (type === 'preferred') {
    if (!preferredKeywords.includes(keyword)) {
      preferredKeywords.push(keyword);
    }
  } else if (!excludedKeywords.includes(keyword)) {
    excludedKeywords.push(keyword);
  }

  input.value = '';
  renderKeywords();
}

function removeKeyword(type, keyword) {
  if (type === 'preferred') {
    preferredKeywords = preferredKeywords.filter(k => k !== keyword);
  } else {
    excludedKeywords = excludedKeywords.filter(k => k !== keyword);
  }
  renderKeywords();
}

function renderKeywords() {
  const preferredList = document.getElementById('preferredKeywordsList');
  if (preferredList) {
    preferredList.innerHTML = preferredKeywords.map(k => `
      <span class="keyword-tag">
        ${escapeHtml(k)}<button type="button" onclick="removeKeyword('preferred', '${escapeAttribute(k)}')">&times;</button>
      </span>
    `).join('');
  }

  const excludedList = document.getElementById('excludedKeywordsList');
  if (excludedList) {
    excludedList.innerHTML = excludedKeywords.map(k => `
      <span class="keyword-tag" style="background: rgba(255,107,107,0.2); color: var(--red);">
        ${escapeHtml(k)}<button type="button" style="color: var(--red);" onclick="removeKeyword('excluded', '${escapeAttribute(k)}')">&times;</button>
      </span>
    `).join('');
  }
}

async function saveProfile() {
  try {
    const profileData = getFormData();
    console.log('Saving profile:', profileData);

    let response;
    if (currentProfile && currentProfile.profileId) {
      response = await fetch(`${CONFIG_API_BASE}/profile/${currentProfile.profileId}`, {
        method: 'PUT',
        headers: getAuthHeaders(true),
        body: JSON.stringify(profileData)
      });
    } else {
      response = await fetch(`${CONFIG_API_BASE}/profile`, {
        method: 'POST',
        headers: getAuthHeaders(true),
        body: JSON.stringify(profileData)
      });
    }

    if (response.ok) {
      currentProfile = await response.json();
      preferredKeywords = currentProfile.preferredKeywords || [];
      excludedKeywords = currentProfile.excludedKeywords || [];
      renderKeywords();

      showMessage('Perfil guardado exitosamente', 'success');
    } else if (response.status === 401) {
      showMessage('Inicia sesion para guardar tu configuracion.', 'error');
    } else {
      const errorText = await response.text();
      console.error('Error response:', errorText);
      showMessage(`Error al guardar perfil: ${response.status}`, 'error');
    }
  } catch (error) {
    console.error('Error saving profile:', error);
    showMessage(`Error de conexion: ${error.message}`, 'error');
  }
}

function getFormData() {
  const companyNameInput = document.getElementById('companyName');
  const companyName = companyNameInput ? companyNameInput.value.trim() : 'Default';
  const seaceObjectDescriptionInput = document.getElementById('seaceObjectDescription');
  const seaceObjectDescription = seaceObjectDescriptionInput ? seaceObjectDescriptionInput.value.trim() : '';
  const seaceCallYearInput = document.getElementById('seaceCallYear');
  const seaceCallYear = normalizeSeaceYear(seaceCallYearInput ? Number(seaceCallYearInput.value) : null);

  return {
    companyName: companyName || 'Default',
    preferredCategories: currentProfile?.preferredCategories || [],
    preferredLocations: currentProfile?.preferredLocations || [],
    preferredModalities: currentProfile?.preferredModalities || [],
    minAmount: currentProfile?.minAmount || 10000,
    maxAmount: currentProfile?.maxAmount || 500000,
    idealAmount: currentProfile?.idealAmount || 250000,
    favoriteEntities: currentProfile?.favoriteEntities || [],
    excludedEntities: currentProfile?.excludedEntities || [],
    preferredKeywords: preferredKeywords || [],
    excludedKeywords: excludedKeywords || [],
    seaceObjectDescription,
    seaceCallYear,
    minDaysToClose: currentProfile?.minDaysToClose || 3,
    maxDaysToClose: currentProfile?.maxDaysToClose || 30,
    idealDaysToClose: currentProfile?.idealDaysToClose || 15
  };
}

function normalizeSeaceYear(value) {
  const currentYear = new Date().getFullYear();
  const year = Number(value);
  return Number.isInteger(year) && year >= 2004 && year <= currentYear ? year : currentYear;
}

function resetForm() {
  preferredKeywords = [];
  excludedKeywords = [];
  renderKeywords();

  if (currentProfile) {
    populateForm(currentProfile);
  }

  showMessage('Formulario restaurado', 'info');
}

async function recalculateAffinity() {
  try {
    showMessage('Recalculando afinidad...', 'info');

    const response = await fetch(`${CONFIG_API_BASE}/opportunities/analyze`, {
      method: 'POST',
      headers: getAuthHeaders()
    });

    if (response.ok) {
      const result = await response.json();
      showMessage(`Afinidad recalculada: ${result.updatedCount} oportunidades actualizadas`, 'success');
    } else {
      showMessage('Error al recalcular afinidad', 'error');
    }
  } catch (error) {
    console.error('Error recalculating affinity:', error);
    showMessage('Error de conexion al recalcular afinidad', 'error');
  }
}

function showMessage(text, type = 'info') {
  const messageEl = document.getElementById('message');
  if (!messageEl) return;

  messageEl.textContent = text;
  messageEl.className = `message ${type}`;

  setTimeout(() => {
    messageEl.textContent = '';
    messageEl.className = 'message';
  }, 5000);
}

function escapeHtml(value) {
  return String(value)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#039;');
}

function escapeAttribute(value) {
  return String(value).replace(/\\/g, '\\\\').replace(/'/g, "\\'");
}

window.removeKeyword = removeKeyword;
