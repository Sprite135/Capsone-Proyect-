const CONFIG_API_BASE = 'http://localhost:5153/api';

let currentProfile = null;
let preferredKeywords = [];
let excludedKeywords = [];

function getAuthHeaders(includeJson = false) {
  const headers = includeJson ? { 'Content-Type': 'application/json' } : {};
  const token = localStorage.getItem('authToken');
  if (token) {
    headers.Authorization = `Bearer ${token}`;
  }
  return headers;
}

// Load profile on page load
document.addEventListener('DOMContentLoaded', async () => {
  await loadProfile();
  setupEventListeners();
});

async function loadProfile() {
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
      showMessage('No se encontró perfil. Crea uno nuevo.', 'info');
    } else {
      showMessage('Error al cargar perfil', 'error');
    }
  } catch (error) {
    console.error('Error loading profile:', error);
    showMessage('Error de conexión al cargar perfil', 'error');
  }
}

function populateForm(profile) {
  preferredKeywords = profile.preferredKeywords || [];
  excludedKeywords = profile.excludedKeywords || [];
  renderKeywords();
}

function setupEventListeners() {
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
      window.AUTH_TOKEN = null;
    });
  }
  
  // Keyword management
  safeAddListener('addPreferredKeyword', 'click', () => addKeyword('preferred'));
  safeAddListener('addExcludedKeyword', 'click', () => addKeyword('excluded'));
  
  safeAddListener('preferredKeywordInput', 'keypress', (e) => {
    if (e.key === 'Enter') { e.preventDefault(); addKeyword('preferred'); }
  });
  safeAddListener('excludedKeywordInput', 'keypress', (e) => {
    if (e.key === 'Enter') { e.preventDefault(); addKeyword('excluded'); }
  });
}

function addKeyword(type) {
  const inputId = type === 'preferred' ? 'preferredKeywordInput' : 'excludedKeywordInput';
  const input = document.getElementById(inputId);
  
  const keyword = input.value.trim();
  if (!keyword) return;
  
  if (type === 'preferred') {
    if (!preferredKeywords.includes(keyword)) {
      preferredKeywords.push(keyword);
    }
  } else {
    if (!excludedKeywords.includes(keyword)) {
      excludedKeywords.push(keyword);
    }
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
  // Render preferred keywords
  const preferredList = document.getElementById('preferredKeywordsList');
  if (preferredList) {
    preferredList.innerHTML = preferredKeywords.map(k => `
      <span class="keyword-tag">
        ${k}<button type="button" onclick="removeKeyword('preferred', '${k.replace(/'/g, "\\'")}')">×</button>
      </span>
    `).join('');
  }
  
  // Render excluded keywords
  const excludedList = document.getElementById('excludedKeywordsList');
  if (excludedList) {
    excludedList.innerHTML = excludedKeywords.map(k => `
      <span class="keyword-tag" style="background: rgba(255,107,107,0.2); color: var(--red);">
        ${k}<button type="button" style="color: var(--red);" onclick="removeKeyword('excluded', '${k.replace(/'/g, "\\'")}')">×</button>
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
      // Update existing profile
      response = await fetch(`${CONFIG_API_BASE}/profile/${currentProfile.profileId}`, {
        method: 'PUT',
        headers: getAuthHeaders(true),
        body: JSON.stringify(profileData)
      });
    } else {
      // Create new profile
      response = await fetch(`${CONFIG_API_BASE}/profile`, {
        method: 'POST',
        headers: getAuthHeaders(true),
        body: JSON.stringify(profileData)
      });
    }

    if (response.ok) {
      currentProfile = await response.json();
      // Sync local arrays with saved profile
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
    showMessage(`Error de conexión: ${error.message}`, 'error');
  }
}

function getFormData() {
  return {
    companyName: (currentProfile && currentProfile.companyName) || 'Default',
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
    minDaysToClose: currentProfile?.minDaysToClose || 3,
    maxDaysToClose: currentProfile?.maxDaysToClose || 30,
    idealDaysToClose: currentProfile?.idealDaysToClose || 15
  };
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
    showMessage('Error de conexión al recalcular afinidad', 'error');
  }
}

function showMessage(text, type = 'info') {
  const messageEl = document.getElementById('message');
  messageEl.textContent = text;
  messageEl.className = `message ${type}`;
  
  setTimeout(() => {
    messageEl.textContent = '';
    messageEl.className = 'message';
  }, 5000);
}

// Expose functions globally for onclick handlers
window.removeKeyword = removeKeyword;
