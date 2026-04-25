const message = document.getElementById("message");
const chips = document.querySelectorAll(".chip");
const demoButtons = document.querySelectorAll("[data-demo-message]");
const API_BASE = "http://localhost:5153";
const opportunitiesContainer = document.getElementById("opportunityResults");
const opportunitySearch = document.getElementById("opportunitySearch");
const opportunityMeta = document.getElementById("opportunityMeta");
const summaryHighMatch = document.getElementById("summaryHighMatch");
const summaryPriority = document.getElementById("summaryPriority");
const summaryTotal = document.getElementById("summaryTotal");
const detailBreadcrumbCode = document.getElementById("detailBreadcrumbCode");
const detailHeading = document.getElementById("detailHeading");
const detailInsightTitle = document.getElementById("detailInsightTitle");
const detailScore = document.getElementById("detailScore");
const detailSummary = document.getElementById("detailSummary");
const detailEntity = document.getElementById("detailEntity");
const detailAmount = document.getElementById("detailAmount");
const detailClosingDate = document.getElementById("detailClosingDate");
const detailModality = document.getElementById("detailModality");
const detailStatus = document.getElementById("detailStatus");
const detailLocation = document.getElementById("detailLocation");
const detailUrgency = document.getElementById("detailUrgency");
const syncSeaceButton = Array.from(document.querySelectorAll('button')).find(btn => btn.textContent.includes('Sincronizar SEACE'));

let allOpportunities = [];
let currentPage = 1;
const itemsPerPage = 15;
let lastFilteredCount = 0;
let profileKeywords = { preferred: [], excluded: [] };

const setMessage = (text) => {
  if (message) {
    message.textContent = text;
  }
};

demoButtons.forEach((button) => {
  button.addEventListener("click", () => {
    setMessage(button.dataset.demoMessage);
  });
});

// Conectar botón Sincronizar SEACE al endpoint de refresh
if (syncSeaceButton) {
  syncSeaceButton.addEventListener("click", async () => {
    try {
      syncSeaceButton.disabled = true;
      syncSeaceButton.textContent = "Sincronizando...";
      setMessage("Iniciando sincronización con SEACE...");

      const response = await fetch(`${API_BASE}/api/seace/refresh`, {
        method: 'POST'
      });

      if (!response.ok) {
        throw new Error("Error al sincronizar con SEACE");
      }

      const data = await response.json();
      setMessage(data.message || "Sincronización completada exitosamente");
      
      // Recargar oportunidades
      await loadOpportunities();
    } catch (error) {
      setMessage("Error al sincronizar con SEACE: " + error.message);
    } finally {
      syncSeaceButton.disabled = false;
      syncSeaceButton.textContent = "Sincronizar SEACE";
    }
  });
}

chips.forEach((chip) => {
  chip.addEventListener("click", () => {
    const group = chip.closest(".chip-row");

    if (group) {
      group.querySelectorAll(".chip").forEach((item) => item.classList.remove("active"));
    }

    chip.classList.add("active");
    setMessage(`Filtro aplicado: ${chip.textContent}.`);
    renderOpportunities();
  });
});

// Event listener for custom amount range filter
const applyAmountFilterBtn = document.getElementById('applyAmountFilter');
if (applyAmountFilterBtn) {
  applyAmountFilterBtn.addEventListener('click', () => {
    renderOpportunities();
    setMessage('Filtro de monto aplicado.');
  });
}

// Event listener for clear amount filter button
const clearAmountFilterBtn = document.getElementById('clearAmountFilter');
if (clearAmountFilterBtn) {
  clearAmountFilterBtn.addEventListener('click', () => {
    const minAmountInput = document.getElementById('minAmount');
    const maxAmountInput = document.getElementById('maxAmount');
    if (minAmountInput) minAmountInput.value = '';
    if (maxAmountInput) maxAmountInput.value = '';
    renderOpportunities();
    setMessage('Filtro de monto borrado.');
  });
}

if (opportunitySearch) {
  opportunitySearch.addEventListener("input", () => {
    renderOpportunities();
  });
}

// Favorite functionality
function isOpportunityFavorite(opportunityId) {
  const favorites = JSON.parse(localStorage.getItem('favoriteOpportunities') || '[]');
  return favorites.includes(opportunityId);
}

function isOpportunityUrgent(closingDate) {
  const now = new Date();
  const closing = new Date(closingDate);
  const diffTime = closing - now;
  const diffDays = diffTime / (1000 * 60 * 60 * 24);
  return diffDays <= 7 && diffDays > 0;
}

function getOpportunityTrackingStatus(opportunityId) {
  const tracking = JSON.parse(localStorage.getItem('opportunityTracking') || '{}');
  return tracking[opportunityId] || '';
}

function getTrackingStatusClass(status) {
  const classes = {
    '': '',
    'review': 'review',
    'preparing': 'preparing',
    'submitted': 'submitted',
    'won': 'won',
    'lost': 'lost'
  };
  return classes[status] || '';
}

function toggleFavorite(opportunityId) {
  const favorites = JSON.parse(localStorage.getItem('favoriteOpportunities') || '[]');
  const index = favorites.indexOf(opportunityId);
  
  if (index > -1) {
    favorites.splice(index, 1);
    setMessage("Oportunidad eliminada de favoritos.");
  } else {
    favorites.push(opportunityId);
    setMessage("Oportunidad agregada a favoritos.");
  }
  
  localStorage.setItem('favoriteOpportunities', JSON.stringify(favorites));
  renderOpportunities();
}

// Event delegation for favorite buttons
document.addEventListener('click', (e) => {
  const favoriteBtn = e.target.closest('.favorite-btn');
  if (favoriteBtn) {
    const opportunityId = parseInt(favoriteBtn.dataset.opportunityId);
    if (opportunityId) {
      toggleFavorite(opportunityId);
    }
  }
});

// Event delegation for tracking status dropdown
document.addEventListener('change', (e) => {
  const trackingSelect = e.target.closest('.tracking-status-select');
  if (trackingSelect) {
    const opportunityId = parseInt(trackingSelect.dataset.opportunityId);
    const status = trackingSelect.value;
    if (opportunityId) {
      setOpportunityTrackingStatus(opportunityId, status);
      setMessage(`Estado actualizado: ${trackingSelect.options[trackingSelect.selectedIndex].text}`);
      renderOpportunities();
    }
  }
});

// Tab functionality for page-tabs
const pageTabs = document.querySelectorAll('.page-tabs a');

pageTabs.forEach(tab => {
  tab.addEventListener('click', (e) => {
    e.preventDefault();
    
    // Update active state
    pageTabs.forEach(t => t.classList.remove('active'));
    tab.classList.add('active');
    
    // Update current tab
    currentTab = tab.dataset.tab;
    
    // Update favorite filter chip
    const favoriteFilterChips = document.querySelectorAll('.chip[data-filter-group="favorite"]');
    favoriteFilterChips.forEach(chip => {
      chip.classList.remove('active');
      if (currentTab === 'favorites' && chip.dataset.filterValue === 'only') {
        chip.classList.add('active');
      } else if (currentTab !== 'favorites' && chip.dataset.filterValue === 'all') {
        chip.classList.add('active');
      }
    });
    
    renderOpportunities();
  });
});

if (opportunitiesContainer) {
  loadOpportunities();
}

if (detailHeading) {
  loadOpportunityDetail();
}

// Load tracking page
if (document.getElementById('trackingBoard')) {
  loadTrackingBoard();
}

async function loadTrackingBoard() {
  try {
    // Cargar todas las oportunidades
    const response = await fetch(`${API_BASE}/api/opportunities`);
    const data = await response.json().catch(() => []);
    const allOpportunities = Array.isArray(data) ? data : [];

    // Obtener oportunidades en favoritos (seguimiento)
    const favoriteIds = JSON.parse(localStorage.getItem('favoriteOpportunities') || '[]');
    const favoriteOpportunities = allOpportunities.filter(op => favoriteIds.includes(op.opportunityId));

    // Guardar para filtros
    trackingAllOpportunities = favoriteOpportunities;

    // Obtener estados de tracking
    const trackingStatus = JSON.parse(localStorage.getItem('opportunityTracking') || '{}');

    // Agrupar por estado
    const review = favoriteOpportunities.filter(op => (trackingStatus[op.opportunityId] || '') === 'review');
    const preparing = favoriteOpportunities.filter(op => (trackingStatus[op.opportunityId] || '') === 'preparing');
    const submitted = favoriteOpportunities.filter(op => (trackingStatus[op.opportunityId] || '') === 'submitted');

    // Renderizar columnas
    renderTrackingColumn('tracking-review', review);
    renderTrackingColumn('tracking-preparing', preparing);
    renderTrackingColumn('tracking-submitted', submitted);

    // Actualizar contadores
    document.querySelector('article.panel-card:nth-child(1) strong').textContent = review.length;
    document.querySelector('article.panel-card:nth-child(2) strong').textContent = preparing.length;
    document.querySelector('article.panel-card:nth-child(3) strong').textContent = submitted.length;

    // Renderizar tabla de seguimiento
    renderTrackingTable(favoriteOpportunities, trackingStatus);
  } catch (error) {
    console.error('Error loading tracking board:', error);
  }
}

function renderTrackingTable(opportunities, trackingStatus) {
  const tbody = document.getElementById('trackingTableBody');
  if (!tbody) return;

  if (opportunities.length === 0) {
    tbody.innerHTML = '<tr><td colspan="9" class="empty-state">No hay procesos en seguimiento</td></tr>';
    updateTrackingPagination(0);
    return;
  }

  // Paginación
  const totalPages = Math.ceil(opportunities.length / trackingItemsPerPage);
  const startIndex = (trackingCurrentPage - 1) * trackingItemsPerPage;
  const endIndex = startIndex + trackingItemsPerPage;
  const pageData = opportunities.slice(startIndex, endIndex);

  tbody.innerHTML = pageData.map((op, index) => {
    const status = trackingStatus[op.opportunityId] || '';
    const statusText = {
      '': 'Pendiente',
      'review': 'En revisión',
      'preparing': 'Preparando propuesta',
      'submitted': 'Postulado',
      'won': 'Ganado',
      'lost': 'Perdido'
    }[status] || 'Pendiente';

    // Número correlativo (1-7 en página 1, 8-14 en página 2, etc.)
    const lineNumber = startIndex + index + 1;

    return `
      <tr>
        <td>${lineNumber}</td>
        <td>${escapeHtml(op.processCode)}</td>
        <td>${escapeHtml(op.summary || op.title)}</td>
        <td>${escapeHtml(op.entityName)}</td>
        <td>${escapeHtml(op.category || 'N/A')}</td>
        <td>${formatCurrency(op.estimatedAmount)}</td>
        <td>$ 0</td>
        <td>${op.closingDate ? formatDate(op.closingDate) : 'N/A'}</td>
        <td><span class="status review">${statusText}</span></td>
      </tr>
    `;
  }).join('');

  updateTrackingPagination(totalPages);
}

function updateTrackingPagination(totalPages) {
  const pageInfo = document.getElementById('trackingPageInfo');
  const prevBtn = document.getElementById('prevTrackingPage');
  const nextBtn = document.getElementById('nextTrackingPage');

  if (!pageInfo) return;

  if (totalPages === 0) {
    pageInfo.textContent = 'Página 1 de 1';
    prevBtn.disabled = true;
    nextBtn.disabled = true;
    return;
  }

  pageInfo.textContent = `Página ${trackingCurrentPage} de ${totalPages}`;
  prevBtn.disabled = trackingCurrentPage === 1;
  nextBtn.disabled = trackingCurrentPage === totalPages;
}

function previousTrackingPage() {
  if (trackingCurrentPage > 1) {
    trackingCurrentPage--;
    const trackingStatus = JSON.parse(localStorage.getItem('opportunityTracking') || '{}');
    renderTrackingTable(trackingFilteredOpportunities.length > 0 ? trackingFilteredOpportunities : trackingAllOpportunities, trackingStatus);
  }
}

function nextTrackingPage() {
  const opportunities = trackingFilteredOpportunities.length > 0 ? trackingFilteredOpportunities : trackingAllOpportunities;
  const totalPages = Math.ceil(opportunities.length / trackingItemsPerPage);
  
  if (trackingCurrentPage < totalPages) {
    trackingCurrentPage++;
    const trackingStatus = JSON.parse(localStorage.getItem('opportunityTracking') || '{}');
    renderTrackingTable(opportunities, trackingStatus);
  }
}

// Tracking filters functions
let trackingAllOpportunities = [];
let trackingFilteredOpportunities = [];
let trackingCurrentPage = 1;
const trackingItemsPerPage = 7;

function toggleFilterDropdown() {
  const dropdown = document.getElementById('filterDropdown');
  if (dropdown) {
    dropdown.style.display = dropdown.style.display === 'none' ? 'block' : 'none';
  }
}

function applyTrackingFilters() {
  const type = document.getElementById('typeFilter')?.value || 'all';
  const status = document.getElementById('statusFilter')?.value || 'all';
  const minorContract = document.getElementById('minorContractFilter')?.value || 'all';
  const object = document.getElementById('objectFilter')?.value?.toLowerCase() || '';
  const view = document.getElementById('viewFilter')?.value || 'all';
  const line = document.getElementById('lineFilter')?.value || 'all';
  const tag = document.getElementById('tagFilter')?.value?.toLowerCase() || '';
  const search = document.getElementById('searchFilter')?.value?.toLowerCase() || '';

  const trackingStatus = JSON.parse(localStorage.getItem('opportunityTracking') || '{}');

  let filtered = trackingAllOpportunities.filter(op => {
    // Type filter
    if (type !== 'all' && op.category !== type) return false;

    // Status filter (simplified - using tracking status)
    const opTrackingStatus = trackingStatus[op.opportunityId] || '';
    if (view !== 'all' && opTrackingStatus !== view) return false;

    // Object filter
    if (object && !op.summary?.toLowerCase().includes(object) && !op.title?.toLowerCase().includes(object)) return false;

    // Search filter (ID or keyword)
    if (search && !op.processCode?.toLowerCase().includes(search) && 
        !op.title?.toLowerCase().includes(search) && 
        !op.summary?.toLowerCase().includes(search)) return false;

    return true;
  });

  trackingFilteredOpportunities = filtered;
  renderTrackingTable(filtered, trackingStatus);
  
  // Ocultar dropdown después de aplicar
  document.getElementById('filterDropdown').style.display = 'none';
}

function clearTrackingFilters() {
  document.getElementById('typeFilter').value = 'all';
  document.getElementById('statusFilter').value = 'all';
  document.getElementById('minorContractFilter').value = 'all';
  document.getElementById('objectFilter').value = '';
  document.getElementById('viewFilter').value = 'all';
  document.getElementById('lineFilter').value = 'all';
  document.getElementById('tagFilter').value = '';
  document.getElementById('searchFilter').value = '';
  
  const trackingStatus = JSON.parse(localStorage.getItem('opportunityTracking') || '{}');
  renderTrackingTable(trackingAllOpportunities, trackingStatus);
  
  // Ocultar dropdown después de limpiar
  document.getElementById('filterDropdown').style.display = 'none';
}

function importOpportunities() {
  setMessage('Importando oportunidades desde SEACE...');
  fetch(`${API_BASE}/api/seace/refresh`, { method: 'POST' })
    .then(response => response.json())
    .then(data => {
      setMessage(`Importación completada: ${data.count} oportunidades`);
      loadTrackingBoard();
    })
    .catch(error => {
      setMessage('Error al importar oportunidades');
      console.error(error);
    });
}

// Expose functions globally for onclick handlers
window.toggleFilterDropdown = toggleFilterDropdown;
window.applyTrackingFilters = applyTrackingFilters;
window.clearTrackingFilters = clearTrackingFilters;
window.importOpportunities = importOpportunities;
window.previousTrackingPage = previousTrackingPage;
window.nextTrackingPage = nextTrackingPage;

function renderTrackingColumn(containerId, opportunities) {
  const container = document.getElementById(containerId);
  if (!container) return;

  if (opportunities.length === 0) {
    container.innerHTML = '<p class="empty-state">No hay procesos en esta categoría</p>';
    return;
  }

  container.innerHTML = opportunities.map(op => `
    <div class="board-card">
      <div class="board-card-head">
        <div>
          <h4>${escapeHtml(op.processCode)}</h4>
          <p>${escapeHtml(op.entityName)}</p>
        </div>
        <span class="status active">${formatCurrency(op.estimatedAmount)}</span>
      </div>
      <p>${escapeHtml(op.summary || op.title)}</p>
      <div class="board-card-actions">
        <a class="text-link" href="detalle-licitacion.html?id=${op.opportunityId}">Ver detalle</a>
      </div>
    </div>
  `).join('');
}

async function loadProfile() {
  try {
    const response = await fetch(`${API_BASE}/api/profile`);
    if (response.ok) {
      const profile = await response.json();
      profileKeywords.preferred = profile.preferredKeywords || [];
      profileKeywords.excluded = profile.excludedKeywords || [];
      console.log('Profile loaded:', profileKeywords);
    }
  } catch (error) {
    console.error('Error loading profile:', error);
  }
}

function highlightKeywords(text, keywords) {
  if (!text || !keywords || keywords.length === 0) return escapeHtml(text);
  
  let result = escapeHtml(text);
  keywords.forEach(keyword => {
    if (!keyword) return;
    const regex = new RegExp(`(${escapeRegex(keyword)})`, 'gi');
    result = result.replace(regex, '<span class="keyword-highlight">$1</span>');
  });
  return result;
}

function escapeRegex(string) {
  return string.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

async function loadOpportunities() {
  try {
    if (opportunityMeta) {
      opportunityMeta.textContent = "Consultando oportunidades en la API...";
    }

    // Cargar perfil con keywords
    await loadProfile();

    const response = await fetch(`${API_BASE}/api/opportunities`);
    const data = await response.json().catch(() => []);

    if (!response.ok) {
      throw new Error("No se pudieron obtener las oportunidades.");
    }

    allOpportunities = Array.isArray(data) ? data : [];
    updateOpportunitySummary();
    renderOpportunities();
    
    // Cargar filtros dinámicos
    loadDynamicFilters();
  } catch (error) {
    if (opportunitiesContainer) {
      opportunitiesContainer.innerHTML = `
        <div class="result-empty">
          No fue posible cargar oportunidades. Verifica que la API este corriendo y que la base de datos exista en SQL Server.
        </div>`;
    }

    if (opportunityMeta) {
      opportunityMeta.textContent = "La API no respondio o la base aun no esta configurada.";
    }

    setMessage("No fue posible cargar las oportunidades desde la API.");
  }
}

async function loadDynamicFilters() {
  try {
    // Cargar categorías
    const categoriesResponse = await fetch('http://localhost:5153/api/opportunities/categories');
    if (categoriesResponse.ok) {
      const categoriesData = await categoriesResponse.json();
      
      if (categoriesData.categories && Array.isArray(categoriesData.categories)) {
        renderCategoryFilters(categoriesData.categories);
      }
    }
    
    // Cargar modalidades
    const modalitiesResponse = await fetch('http://localhost:5153/api/opportunities/modalities');
    if (modalitiesResponse.ok) {
      const modalitiesData = await modalitiesResponse.json();
      
      if (modalitiesData.modalities && Array.isArray(modalitiesData.modalities)) {
        renderModalityFilters(modalitiesData.modalities);
      }
    }
  } catch (error) {
    console.error("Error cargando filtros dinámicos:", error);
  }
}

function renderCategoryFilters(categories) {
  // Buscar específicamente el filtro de "Rubros"
  const rubrosFilterGroup = Array.from(document.querySelectorAll('.filter-group h4'))
    .find(h4 => h4.textContent === 'Rubros')?.parentElement;
  
  if (!rubrosFilterGroup) return;
  const chipRow = rubrosFilterGroup.querySelector('.chip-row');
  if (!chipRow) return;
  
  // Limpiar completamente todos los filtros existentes
  chipRow.innerHTML = '';
  
  // Agregar "Todos" primero
  const todosChip = document.createElement('button');
  todosChip.className = 'chip active';
  todosChip.type = 'button';
  todosChip.dataset.filterGroup = 'category';
  todosChip.dataset.filterValue = 'Todos';
  todosChip.innerHTML = `Todos <span class="chip-count" id="count-Todos">${allOpportunities.length}</span>`;
  todosChip.addEventListener('click', () => {
    const group = todosChip.closest('.chip-row');
    if (group) {
      group.querySelectorAll('.chip').forEach((item) => item.classList.remove('active'));
    }
    todosChip.classList.add('active');
    setMessage(`Filtro aplicado: Todos.`);
    renderOpportunities();
  });
  chipRow.appendChild(todosChip);
  
  // Agregar categorías dinámicas
  categories.forEach(category => {
    const chip = document.createElement('button');
    chip.className = 'chip';
    chip.type = 'button';
    chip.dataset.filterGroup = 'category';
    chip.dataset.filterValue = category;
    
    // Calcular count
    const count = allOpportunities.filter(o => o.category === category).length;
    
    chip.innerHTML = `
      ${category}
      <span class="chip-count">${count}</span>
    `;
    
    // Agregar event listener
    chip.addEventListener('click', () => {
      const group = chip.closest('.chip-row');
      if (group) {
        group.querySelectorAll('.chip').forEach((item) => item.classList.remove('active'));
      }
      chip.classList.add('active');
      setMessage(`Filtro aplicado: ${category}.`);
      renderOpportunities();
    });
    
    chipRow.appendChild(chip);
  });
}

function renderModalityFilters(modalities) {
  const modalityFilterContainer = document.querySelector('[data-filter-group="modality"]')?.parentElement;
  if (!modalityFilterContainer) return;
  
  const chipRow = modalityFilterContainer.querySelector('.chip-row');
  if (!chipRow) return;
  
  // Si no existe el contenedor de modalidades, crearlo
  if (!modalityFilterContainer) {
    const filterGroup = document.createElement('div');
    filterGroup.className = 'filter-group';
    filterGroup.innerHTML = `
      <h4>Modalidad</h4>
      <div class="chip-row" id="modality-filters"></div>
    `;
    
    const filtersContainer = document.querySelector('.filter-card');
    if (filtersContainer) {
      filtersContainer.appendChild(filterGroup);
    }
  }
  
  // Limpiar filtros existentes excepto "Todos"
  const existingChips = chipRow.querySelectorAll('.chip');
  existingChips.forEach(chip => {
    if (chip.dataset.filterValue !== 'Todos') {
      chip.remove();
    }
  });
  
  // Agregar modalidades dinámicas
  modalities.forEach(modality => {
    const chip = document.createElement('button');
    chip.className = 'chip';
    chip.type = 'button';
    chip.dataset.filterGroup = 'modality';
    chip.dataset.filterValue = modality;
    
    // Calcular count
    const count = allOpportunities.filter(o => o.modality === modality).length;
    
    chip.innerHTML = `
      ${modality}
      <span class="chip-count" id="count-modality-${modality.replace(/\s+/g, '-')}">${count}</span>
    `;
    
    // Agregar event listener
    chip.addEventListener('click', () => {
      const group = chip.closest('.chip-row');
      if (group) {
        group.querySelectorAll('.chip').forEach((item) => item.classList.remove('active'));
      }
      chip.classList.add('active');
      setMessage(`Filtro aplicado: ${modality}.`);
      renderOpportunities();
    });
    
    chipRow.appendChild(chip);
  });
}

async function loadOpportunityDetail() {
  const opportunityId = new URLSearchParams(window.location.search).get("id");

  if (!opportunityId) {
    return;
  }

  try {
    setMessage("Consultando detalle de la oportunidad...");

    const response = await fetch(`${API_BASE}/api/opportunities/${encodeURIComponent(opportunityId)}`);
    const data = await response.json().catch(() => null);

    if (!response.ok || !data) {
      throw new Error("No se pudo obtener el detalle.");
    }

    renderOpportunityDetail(data);
    setMessage(`Detalle cargado: ${data.processCode}.`);
  } catch (error) {
    setMessage("No fue posible cargar el detalle desde la API.");
  }
}

function updateOpportunitySummary() {
  if (summaryHighMatch) {
    summaryHighMatch.textContent = String(
      allOpportunities.filter((item) => item.matchScore >= 85).length);
  }

  if (summaryPriority) {
    summaryPriority.textContent = String(
      allOpportunities.filter((item) => item.isPriority).length);
  }

  if (summaryTotal) {
    summaryTotal.textContent = String(allOpportunities.length);
  }
}

function renderOpportunities() {
  if (!opportunitiesContainer) {
    return;
  }

  const categoryFilter = getActiveFilterValue("category");
  const locationFilter = getActiveFilterValue("location");
  const favoriteFilter = getActiveFilterValue("favorite");
  const searchTerm = (opportunitySearch?.value || "").trim().toLowerCase();
  
  // Get custom amount range values
  const minAmountInput = document.getElementById('minAmount');
  const maxAmountInput = document.getElementById('maxAmount');
  const minAmount = minAmountInput ? parseCurrencyInput(minAmountInput.value) : 0;
  const maxAmount = maxAmountInput ? parseCurrencyInput(maxAmountInput.value) || Infinity : Infinity;

  const filtered = allOpportunities.filter((item) => {
    const matchesCategory =
      categoryFilter === "Todos" || item.category === categoryFilter;
    const matchesLocation =
      locationFilter === "Todos" || item.location === locationFilter;
    const matchesFavorite =
      favoriteFilter === "all" || (favoriteFilter === "only" && isOpportunityFavorite(item.opportunityId));
    const matchesAmount = item.estimatedAmount >= minAmount && item.estimatedAmount <= maxAmount;
    const haystack = `${item.processCode} ${item.title} ${item.entityName} ${item.category} ${item.modality} ${item.summary || ''}`.toLowerCase();
    const matchesSearch = !searchTerm || haystack.includes(searchTerm);

    // Filtrar por keywords del perfil
    const matchesPreferredKeywords = profileKeywords.preferred.length === 0 || 
      profileKeywords.preferred.some(keyword => haystack.includes(keyword.toLowerCase()));
    const matchesExcludedKeywords = profileKeywords.excluded.length === 0 || 
      !profileKeywords.excluded.some(keyword => haystack.includes(keyword.toLowerCase()));

    // Debug: log primer item que no pasa el filtro de keywords
    if (!matchesPreferredKeywords && profileKeywords.preferred.length > 0) {
      console.log('Filtered out (no preferred match):', item.title, 'Keywords:', profileKeywords.preferred, 'Haystack:', haystack);
    }

    return matchesCategory && matchesLocation && matchesFavorite && matchesAmount && matchesSearch && matchesPreferredKeywords && matchesExcludedKeywords;
  });

  // Update category counters
  updateCategoryCounters();

  // Reset to page 1 when filters change
  if (filtered.length !== lastFilteredCount) {
    currentPage = 1;
    lastFilteredCount = filtered.length;
  }

  // Calculate pagination
  const totalPages = Math.ceil(filtered.length / itemsPerPage);
  const startIndex = (currentPage - 1) * itemsPerPage;
  const endIndex = startIndex + itemsPerPage;
  const paginatedItems = filtered.slice(startIndex, endIndex);

  if (opportunityMeta) {
    opportunityMeta.textContent = `${filtered.length} oportunidades mostradas de ${allOpportunities.length} cargadas. Página ${currentPage} de ${totalPages || 1}.`;
  }

  if (filtered.length === 0) {
    opportunitiesContainer.innerHTML = `
      <div class="result-empty">
        No hay oportunidades que coincidan con los filtros actuales.
      </div>`;
    return;
  }

  opportunitiesContainer.innerHTML = paginatedItems.map((item, index) => createOpportunityMarkup(item, index)).join("");

  // Add pagination controls
  renderPaginationControls(totalPages);
}

function renderPaginationControls(totalPages) {
  if (totalPages <= 1) return;

  const paginationContainer = document.createElement('div');
  paginationContainer.className = 'pagination-controls';
  
  // Previous button
  const prevButton = document.createElement('button');
  prevButton.className = 'pagination-button';
  prevButton.textContent = '← Anterior';
  prevButton.disabled = currentPage === 1;
  prevButton.onclick = () => {
    if (currentPage > 1) {
      currentPage--;
      renderOpportunities();
    }
  };
  
  // Page numbers
  const pageNumbers = document.createElement('div');
  pageNumbers.className = 'pagination-numbers';
  
  for (let i = 1; i <= totalPages; i++) {
    const pageButton = document.createElement('button');
    pageButton.className = `pagination-number ${i === currentPage ? 'active' : ''}`;
    pageButton.textContent = i;
    pageButton.onclick = () => {
      currentPage = i;
      renderOpportunities();
    };
    pageNumbers.appendChild(pageButton);
  }
  
  // Next button
  const nextButton = document.createElement('button');
  nextButton.className = 'pagination-button';
  nextButton.textContent = 'Siguiente →';
  nextButton.disabled = currentPage === totalPages;
  nextButton.onclick = () => {
    if (currentPage < totalPages) {
      currentPage++;
      renderOpportunities();
    }
  };
  
  paginationContainer.appendChild(prevButton);
  paginationContainer.appendChild(pageNumbers);
  paginationContainer.appendChild(nextButton);
  
  opportunitiesContainer.appendChild(paginationContainer);
}

function matchesAmountRange(amount, range) {
  switch (range) {
    case 'low':
      return amount <= 200000;
    case 'medium':
      return amount > 200000 && amount <= 500000;
    case 'high':
      return amount > 500000;
    default:
      return true;
  }
}

function updateAmountCounters() {
  const lowCount = allOpportunities.filter(o => o.estimatedAmount <= 200000).length;
  const mediumCount = allOpportunities.filter(o => o.estimatedAmount > 200000 && o.estimatedAmount <= 500000).length;
  const highCount = allOpportunities.filter(o => o.estimatedAmount > 500000).length;

  const lowElement = document.getElementById('count-amount-low');
  const mediumElement = document.getElementById('count-amount-medium');
  const highElement = document.getElementById('count-amount-high');

  if (lowElement) lowElement.textContent = lowCount;
  if (mediumElement) mediumElement.textContent = mediumCount;
  if (highElement) highElement.textContent = highCount;
}

function updateCategoryCounters() {
  const categories = ['Todos', 'Software', 'Transformacion digital', 'Mesa de ayuda'];
  
  categories.forEach(category => {
    const countElement = document.getElementById(`count-${category}`);
    if (countElement) {
      let count;
      if (category === 'Todos') {
        count = allOpportunities.length;
      } else {
        count = allOpportunities.filter(item => item.category === category).length;
      }
      countElement.textContent = count;
    }
  });
}

function renderOpportunityDetail(item) {
  if (detailBreadcrumbCode) {
    detailBreadcrumbCode.textContent = item.processCode;
  }

  if (detailHeading) {
    detailHeading.textContent = `${item.processCode} | ${item.title}`;
  }

  if (detailInsightTitle) {
    detailInsightTitle.textContent = getInsightTitle(item);
  }

  if (detailScore) {
    detailScore.textContent = `${item.matchScore}%`;
    detailScore.classList.remove("high", "medium", "low");
    detailScore.classList.add(getScoreClass(item.matchScore));
  }

  if (detailSummary) {
    detailSummary.textContent = item.summary;
  }

  if (detailEntity) {
    detailEntity.textContent = item.entityName;
  }

  if (detailAmount) {
    detailAmount.textContent = formatCurrency(item.estimatedAmount);
  }

  if (detailClosingDate) {
    detailClosingDate.textContent = formatDate(item.closingDate);
  }

  if (detailModality) {
    detailModality.textContent = item.modality;
  }

  if (detailStatus) {
    detailStatus.textContent = item.isPriority ? "Prioridad alta" : "En revision";
  }

  if (detailLocation) {
    detailLocation.textContent = item.location;
  }

  if (detailUrgency) {
    detailUrgency.textContent = getUrgencyLabel(item.matchScore);
  }

  document.title = `LicitIA | ${item.processCode}`;
}

function getActiveFilterValue(groupName) {
  const activeChip = document.querySelector(`.chip[data-filter-group="${groupName}"].active`);
  return activeChip?.dataset.filterValue || "Todos";
}

function getInsightTitle(item) {
  if (item.matchScore >= 90) {
    return "Encaje fuerte con la experiencia registrada";
  }

  if (item.matchScore >= 80) {
    return "Oportunidad con buen nivel de afinidad";
  }

  return "Oportunidad con afinidad moderada";
}

function getScoreClass(score) {
  if (score >= 90) {
    return "high";
  }

  if (score >= 80) {
    return "medium";
  }

  return "low";
}

function getUrgencyLabel(score) {
  if (score >= 90) {
    return "Alto";
  }

  if (score >= 80) {
    return "Medio";
  }

  return "Bajo";
}

function createOpportunityMarkup(item, index) {
  const scoreClass = getScoreClass(item.matchScore);
  const statusChip = item.isPriority
    ? '<span class="status active">Prioridad alta</span>'
    : '<span class="status review">Revision pendiente</span>';
  const isFavorite = isOpportunityFavorite(item.opportunityId);
  const favoriteClass = isFavorite ? 'active' : '';
  const isUrgent = isOpportunityUrgent(item.closingDate);
  const urgencyChip = isUrgent
    ? '<span class="status critical">¡Vence pronto!</span>'
    : '';
  const trackingStatus = getOpportunityTrackingStatus(item.opportunityId);
  const trackingStatusClass = getTrackingStatusClass(trackingStatus);

  // Calcular número correlativo
  const startIndex = (currentPage - 1) * itemsPerPage;
  const lineNumber = startIndex + index + 1;

  return `
    <article class="result-card">
      <div class="result-top">
        <div>
          <h4>
            <span class="card-indicator">${lineNumber}</span>
            ${escapeHtml(item.processCode)} | ${escapeHtml(item.title)}
            ${urgencyChip}
          </h4>
          <p>${escapeHtml(item.entityName)}</p>
        </div>
        <div class="result-header-actions">
          <button class="favorite-btn ${favoriteClass}" data-opportunity-id="${item.opportunityId}" type="button" aria-label="Marcar como favorito">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M12 2l3.09 6.26L22 9.27l-5 4.87 1.18 6.88L12 17.77l-6.18 3.25L7 14.14 2 9.27l6.91-1.01L12 2z"/>
            </svg>
          </button>
          <span class="score ${scoreClass}">${item.matchScore}%</span>
        </div>
      </div>
      <div class="result-meta">
        <div><span>Categoría</span><strong>${escapeHtml(item.category || 'N/A')}</strong></div>
        <div><span>Monto estimado</span><strong>${formatCurrency(item.estimatedAmount)}</strong></div>
        <div><span>Cierre</span><strong>${formatDate(item.closingDate)}</strong></div>
        <div><span>Publicación</span><strong>${item.publishedDate ? formatDate(item.publishedDate) : 'N/A'}</strong></div>
        <div><span>Modalidad</span><strong>${escapeHtml(item.modality)}</strong></div>
      </div>
      <div class="result-foot">
        <p class="help-copy">${highlightKeywords(item.summary, profileKeywords.preferred)}</p>
        <div class="inline-actions">
          ${urgencyChip}
          <select class="tracking-status-select ${trackingStatusClass}" data-opportunity-id="${item.opportunityId}" aria-label="Estado de seguimiento">
            <option value="">Sin estado</option>
            <option value="review" ${trackingStatus === 'review' ? 'selected' : ''}>En revisión</option>
            <option value="preparing" ${trackingStatus === 'preparing' ? 'selected' : ''}>Preparando propuesta</option>
            <option value="submitted" ${trackingStatus === 'submitted' ? 'selected' : ''}>Postulado</option>
            <option value="won" ${trackingStatus === 'won' ? 'selected' : ''}>Ganado</option>
            <option value="lost" ${trackingStatus === 'lost' ? 'selected' : ''}>Perdido</option>
          </select>
          ${statusChip}
          <a class="text-link" href="detalle-licitacion.html?id=${item.opportunityId}">Ver detalle</a>
        </div>
      </div>
    </article>`;
}

function formatCurrency(value) {
  return new Intl.NumberFormat("es-PE", {
    style: "currency",
    currency: "PEN",
    maximumFractionDigits: 0
  }).format(value);
}

function formatCurrencyInput(input) {
  // Remove non-digit characters except commas
  let value = input.value.replace(/[^\d,]/g, '');
  
  // Remove existing commas
  let numericValue = value.replace(/,/g, '');
  
  // Add commas as thousands separator
  if (numericValue) {
    value = parseInt(numericValue).toLocaleString('es-PE');
  }
  
  input.value = value;
}

function parseCurrencyInput(value) {
  // Remove commas and convert to number
  if (!value) return 0;
  return parseFloat(value.replace(/,/g, '')) || 0;
}

function formatDate(value) {
  const date = new Date(value);
  return new Intl.DateTimeFormat("es-PE", {
    day: "2-digit",
    month: "short",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  }).format(date);
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}
