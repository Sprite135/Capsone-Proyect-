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

let allOpportunities = [];

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

if (opportunitySearch) {
  opportunitySearch.addEventListener("input", () => {
    renderOpportunities();
  });
}

if (opportunitiesContainer) {
  loadOpportunities();
}

if (detailHeading) {
  loadOpportunityDetail();
}

async function loadOpportunities() {
  try {
    if (opportunityMeta) {
      opportunityMeta.textContent = "Consultando oportunidades en la API...";
    }

    const response = await fetch(`${API_BASE}/api/opportunities`);
    const data = await response.json().catch(() => []);

    if (!response.ok) {
      throw new Error("No se pudieron obtener las oportunidades.");
    }

    allOpportunities = Array.isArray(data) ? data : [];
    updateOpportunitySummary();
    renderOpportunities();
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
  const searchTerm = (opportunitySearch?.value || "").trim().toLowerCase();

  const filtered = allOpportunities.filter((item) => {
    const matchesCategory =
      categoryFilter === "Todos" || item.category === categoryFilter;
    const matchesLocation =
      locationFilter === "Todos" || item.location === locationFilter;
    const haystack = `${item.processCode} ${item.title} ${item.entityName} ${item.category} ${item.modality}`.toLowerCase();
    const matchesSearch = !searchTerm || haystack.includes(searchTerm);

    return matchesCategory && matchesLocation && matchesSearch;
  });

  if (opportunityMeta) {
    opportunityMeta.textContent = `${filtered.length} oportunidades mostradas de ${allOpportunities.length} cargadas.`;
  }

  if (filtered.length === 0) {
    opportunitiesContainer.innerHTML = `
      <div class="result-empty">
        No hay oportunidades que coincidan con los filtros actuales.
      </div>`;
    return;
  }

  opportunitiesContainer.innerHTML = filtered.map((item) => createOpportunityMarkup(item)).join("");
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

function createOpportunityMarkup(item) {
  const scoreClass = getScoreClass(item.matchScore);
  const statusChip = item.isPriority
    ? '<span class="status active">Prioridad alta</span>'
    : '<span class="status review">Revision pendiente</span>';

  return `
    <article class="result-card">
      <div class="result-top">
        <div>
          <h4>${escapeHtml(item.processCode)} | ${escapeHtml(item.title)}</h4>
          <p>${escapeHtml(item.entityName)}</p>
        </div>
        <span class="score ${scoreClass}">${item.matchScore}%</span>
      </div>
      <div class="result-meta">
        <div><span>Monto estimado</span><strong>${formatCurrency(item.estimatedAmount)}</strong></div>
        <div><span>Cierre</span><strong>${formatDate(item.closingDate)}</strong></div>
        <div><span>Modalidad</span><strong>${escapeHtml(item.modality)}</strong></div>
      </div>
      <div class="result-foot">
        <p class="help-copy">${escapeHtml(item.summary)}</p>
        <div class="inline-actions">
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

function formatDate(value) {
  const date = new Date(value);
  return new Intl.DateTimeFormat("es-PE", {
    day: "2-digit",
    month: "short"
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
