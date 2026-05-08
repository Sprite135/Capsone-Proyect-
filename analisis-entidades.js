if (typeof API_BASE === 'undefined') {
  var API_BASE = 'http://localhost:5153';
}

async function loadEntityAnalysis() {
    try {
        const dateFilter = document.getElementById('dateFilter').value;
        const dateParam = dateFilter === 'all' ? '' : `?days=${dateFilter}`;
        
        const response = await fetch(`${API_BASE}/api/metrics/entity-analysis${dateParam}`);
        const data = await response.json();
        
        // Update stats
        document.getElementById('total-entities').textContent = data.length;
        document.getElementById('total-amount').textContent = `S/ ${formatNumber(data.reduce((sum, e) => sum + e.totalAmount, 0))}`;
        
        if (data.length > 0) {
            document.getElementById('top-entity').textContent = data[0].entity;
            const allCategories = data.flatMap(e => e.categories);
            const topCategory = allCategories.sort((a, b) => b.count - a.count)[0];
            document.getElementById('top-category').textContent = topCategory ? topCategory.category : '-';
        }
        
        // Render charts
        renderTopEntitiesChart(data);
        renderCategoryDistributionChart(data);
        
        // Render table
        renderEntityTable(data);
        
    } catch (error) {
        console.error('Error loading entity analysis:', error);
        document.getElementById('message').textContent = 'Error al cargar análisis de entidades';
    }
}

function formatNumber(num) {
    if (num >= 1000000) {
        return (num / 1000000).toFixed(1) + 'M';
    }
    if (num >= 1000) {
        return (num / 1000).toFixed(0) + 'K';
    }
    return num.toFixed(0);
}

function renderTopEntitiesChart(data) {
    const ctx = document.getElementById('topEntitiesChart').getContext('2d');
    
    const labels = data.slice(0, 10).map(d => d.entity);
    const counts = data.slice(0, 10).map(d => d.totalOpportunities);
    
    new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: 'Oportunidades',
                data: counts,
                backgroundColor: 'rgba(59, 130, 246, 0.6)',
                borderColor: 'rgba(59, 130, 246, 1)',
                borderWidth: 1
            }]
        },
        options: {
            indexAxis: 'y',
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    display: false
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        color: '#92a4bf'
                    },
                    grid: {
                        display: false
                    }
                },
                x: {
                    beginAtZero: true,
                    ticks: {
                        color: '#92a4bf'
                    },
                    grid: {
                        color: 'rgba(154, 181, 219, 0.12)'
                    }
                }
            }
        }
    });
}

function renderCategoryDistributionChart(data) {
    const ctx = document.getElementById('categoryDistributionChart').getContext('2d');
    
    // Aggregate categories across all entities
    const categoryMap = new Map();
    data.forEach(entity => {
        entity.categories.forEach(cat => {
            const existing = categoryMap.get(cat.category) || { count: 0, amount: 0 };
            categoryMap.set(cat.category, {
                count: existing.count + cat.count,
                amount: existing.amount + cat.amount
            });
        });
    });
    
    const sortedCategories = Array.from(categoryMap.entries())
        .sort((a, b) => b[1].count - a[1].count)
        .slice(0, 10);
    
    const labels = sortedCategories.map(c => c[0]);
    const counts = sortedCategories.map(c => c[1].count);
    
    new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: labels,
            datasets: [{
                data: counts,
                backgroundColor: [
                    'rgba(59, 130, 246, 0.8)',
                    'rgba(28, 200, 183, 0.8)',
                    'rgba(124, 58, 237, 0.8)',
                    'rgba(242, 193, 78, 0.8)',
                    'rgba(255, 107, 107, 0.8)',
                    'rgba(74, 222, 128, 0.8)',
                    'rgba(251, 146, 60, 0.8)',
                    'rgba(167, 139, 250, 0.8)',
                    'rgba(236, 72, 153, 0.8)',
                    'rgba(20, 184, 166, 0.8)'
                ]
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            plugins: {
                legend: {
                    position: 'right',
                    labels: {
                        color: '#92a4bf'
                    }
                }
            }
        }
    });
}

function renderEntityTable(data) {
    const tbody = document.getElementById('entityTableBody');
    tbody.innerHTML = '';
    
    data.forEach(entity => {
        const row = document.createElement('tr');
        
        const topCategories = entity.categories
            .slice(0, 3)
            .map(c => `${c.category} (${c.count})`)
            .join(', ');
        
        row.innerHTML = `
            <td><strong>${entity.entity}</strong></td>
            <td>${entity.totalOpportunities}</td>
            <td>S/ ${formatNumber(entity.totalAmount)}</td>
            <td>${topCategories}</td>
        `;
        
        tbody.appendChild(row);
    });
}

function exportAnalysis() {
    const message = document.getElementById('message');
    message.textContent = 'Generando exportación del análisis...';
    
    const dateFilter = document.getElementById('dateFilter').value;
    const dateParam = dateFilter === 'all' ? '' : `?days=${dateFilter}`;
    
    fetch(`${API_BASE}/api/metrics/entity-analysis${dateParam}`)
        .then(r => r.json())
        .then(data => {
            let csv = 'LicitIA Análisis de Entidades\n\n';
            csv += 'Entidad,Oportunidades,Monto Total,Categorías\n';
            
            data.forEach(entity => {
                const categories = entity.categories.map(c => `${c.category}:${c.count}`).join('; ');
                csv += `"${entity.entity}",${entity.totalOpportunities},${entity.totalAmount},"${categories}"\n`;
            });
            
            const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
            const link = document.createElement('a');
            const url = URL.createObjectURL(blob);
            link.setAttribute('href', url);
            link.setAttribute('download', `licitia-analisis-entidades-${new Date().toISOString().split('T')[0]}.csv`);
            link.style.visibility = 'hidden';
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
            
            message.textContent = 'Análisis exportado exitosamente';
            setTimeout(() => message.textContent = '', 3000);
        })
        .catch(error => {
            console.error('Error exporting analysis:', error);
            message.textContent = 'Error al exportar análisis';
        });
}

// Load analysis when page loads
document.addEventListener('DOMContentLoaded', loadEntityAnalysis);
