if (typeof API_BASE === 'undefined') {
  var API_BASE = 'http://localhost:5153';
}

async function loadDashboardMetrics() {
    try {
        const dateFilter = document.getElementById('dateFilter').value;
        const dateParam = dateFilter === 'all' ? '' : `?days=${dateFilter}`;
        
        // Load summary metrics
        const summaryResponse = await fetch(`${API_BASE}/api/metrics/summary${dateParam}`);
        const summary = await summaryResponse.json();
        
        // Update hero section
        document.getElementById('hero-total').textContent = `${summary.totalOpportunities} oportunidades`;
        document.getElementById('hero-priority').textContent = `${summary.priorityOpportunities} procesos con prioridad alta`;
        
        // Update stats cards
        document.getElementById('total-opportunities').textContent = summary.totalOpportunities;
        document.getElementById('total-categories').textContent = `${summary.categories} categorías`;
        document.getElementById('average-score').textContent = `${Math.round(summary.averageScore)}%`;
        document.getElementById('priority-opportunities').textContent = summary.priorityOpportunities;
        document.getElementById('total-amount').textContent = `S/ ${formatNumber(summary.totalAmount)}`;
        
        // Load opportunities to calculate closing within 48 hours
        const opportunitiesResponse = await fetch(`${API_BASE}/api/opportunities${dateParam}`);
        const opportunities = await opportunitiesResponse.json();
        
        const now = new Date();
        const closingIn48Hours = opportunities.filter(o => {
            if (!o.closingDate) return false;
            const closingDate = new Date(o.closingDate);
            const hoursUntilClosing = (closingDate - now) / (1000 * 60 * 60);
            return hoursUntilClosing > 0 && hoursUntilClosing <= 48;
        }).length;
        
        document.getElementById('hero-closing').textContent = `${closingIn48Hours} cierres dentro de 48 horas`;
        document.getElementById('hero-alerts').textContent = '0 alertas nuevas para revision';
        
        // Load category chart data
        const categoryResponse = await fetch(`${API_BASE}/api/metrics/by-category${dateParam}`);
        const categoryData = await categoryResponse.json();
        renderCategoryChart(categoryData);
        
        // Load entity chart data
        const entityResponse = await fetch(`${API_BASE}/api/metrics/by-entity${dateParam}`);
        const entityData = await entityResponse.json();
        renderEntityChart(entityData);
        
        // Load trends chart data
        const trendsResponse = await fetch(`${API_BASE}/api/metrics/trends${dateParam}`);
        const trendsData = await trendsResponse.json();
        renderTrendsChart(trendsData);
        
    } catch (error) {
        console.error('Error loading dashboard metrics:', error);
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

function renderCategoryChart(data) {
    const ctx = document.getElementById('categoryChart').getContext('2d');
    
    const labels = data.map(d => d.category);
    const counts = data.map(d => d.count);
    const amounts = data.map(d => d.totalAmount);
    
    // Create gradient
    const gradient = ctx.createLinearGradient(0, 0, 0, 400);
    gradient.addColorStop(0, 'rgba(59, 130, 246, 0.8)');
    gradient.addColorStop(1, 'rgba(124, 58, 237, 0.6)');
    
    new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: 'Oportunidades',
                data: counts,
                backgroundColor: gradient,
                borderColor: 'rgba(59, 130, 246, 1)',
                borderWidth: 2,
                borderRadius: 8,
                hoverBackgroundColor: 'rgba(124, 58, 237, 0.8)'
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            animation: {
                duration: 1000,
                easing: 'easeOutQuart'
            },
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    backgroundColor: 'rgba(10, 21, 38, 0.95)',
                    titleColor: '#e7f0ff',
                    bodyColor: '#92a4bf',
                    borderColor: 'rgba(154, 181, 219, 0.2)',
                    borderWidth: 1,
                    padding: 12,
                    displayColors: false,
                    callbacks: {
                        label: function(context) {
                            const amount = amounts[context.dataIndex];
                            return [
                                `Cantidad: ${context.raw}`,
                                `Monto: S/ ${formatNumber(amount)}`
                            ];
                        }
                    }
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        color: '#92a4bf',
                        font: { size: 11 }
                    },
                    grid: {
                        color: 'rgba(154, 181, 219, 0.12)'
                    }
                },
                x: {
                    ticks: {
                        color: '#92a4bf',
                        font: { size: 11 }
                    },
                    grid: {
                        display: false
                    }
                }
            }
        }
    });
}

function renderEntityChart(data) {
    const ctx = document.getElementById('entityChart').getContext('2d');
    
    const labels = data.map(d => d.entity);
    const counts = data.map(d => d.count);
    const amounts = data.map(d => d.totalAmount);
    
    // Create gradient
    const gradient = ctx.createLinearGradient(0, 0, 400, 0);
    gradient.addColorStop(0, 'rgba(28, 200, 183, 0.8)');
    gradient.addColorStop(1, 'rgba(59, 130, 246, 0.6)');
    
    new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: 'Oportunidades',
                data: counts,
                backgroundColor: gradient,
                borderColor: 'rgba(28, 200, 183, 1)',
                borderWidth: 2,
                borderRadius: 8,
                hoverBackgroundColor: 'rgba(59, 130, 246, 0.8)'
            }]
        },
        options: {
            indexAxis: 'y',
            responsive: true,
            maintainAspectRatio: false,
            animation: {
                duration: 1000,
                easing: 'easeOutQuart'
            },
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    backgroundColor: 'rgba(10, 21, 38, 0.95)',
                    titleColor: '#e7f0ff',
                    bodyColor: '#92a4bf',
                    borderColor: 'rgba(154, 181, 219, 0.2)',
                    borderWidth: 1,
                    padding: 12,
                    displayColors: false,
                    callbacks: {
                        label: function(context) {
                            const amount = amounts[context.dataIndex];
                            return [
                                `Cantidad: ${context.raw}`,
                                `Monto: S/ ${formatNumber(amount)}`
                            ];
                        }
                    }
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        color: '#92a4bf',
                        font: { size: 11 }
                    },
                    grid: {
                        display: false
                    }
                },
                x: {
                    beginAtZero: true,
                    ticks: {
                        color: '#92a4bf',
                        font: { size: 11 }
                    },
                    grid: {
                        color: 'rgba(154, 181, 219, 0.12)'
                    }
                }
            }
        }
    });
}

function renderTrendsChart(data) {
    const ctx = document.getElementById('trendsChart').getContext('2d');
    
    const monthNames = ['Ene', 'Feb', 'Mar', 'Abr', 'May', 'Jun', 'Jul', 'Ago', 'Sep', 'Oct', 'Nov', 'Dic'];
    
    const labels = data.map(d => `${monthNames[d.month - 1]} ${d.year}`);
    const counts = data.map(d => d.count);
    const amounts = data.map(d => d.totalAmount);
    
    // Create gradient
    const gradient = ctx.createLinearGradient(0, 0, 0, 400);
    gradient.addColorStop(0, 'rgba(59, 130, 246, 0.4)');
    gradient.addColorStop(1, 'rgba(124, 58, 237, 0.05)');
    
    new Chart(ctx, {
        type: 'line',
        data: {
            labels: labels,
            datasets: [{
                label: 'Oportunidades',
                data: counts,
                borderColor: 'rgba(59, 130, 246, 1)',
                backgroundColor: gradient,
                borderWidth: 3,
                fill: true,
                tension: 0.4,
                pointBackgroundColor: 'rgba(59, 130, 246, 1)',
                pointBorderColor: '#fff',
                pointBorderWidth: 2,
                pointRadius: 4,
                pointHoverRadius: 6
            }]
        },
        options: {
            responsive: true,
            maintainAspectRatio: false,
            animation: {
                duration: 1000,
                easing: 'easeOutQuart'
            },
            plugins: {
                legend: {
                    display: false
                },
                tooltip: {
                    backgroundColor: 'rgba(10, 21, 38, 0.95)',
                    titleColor: '#e7f0ff',
                    bodyColor: '#92a4bf',
                    borderColor: 'rgba(154, 181, 219, 0.2)',
                    borderWidth: 1,
                    padding: 12,
                    displayColors: false,
                    callbacks: {
                        label: function(context) {
                            const amount = amounts[context.dataIndex];
                            return [
                                `Cantidad: ${context.raw}`,
                                `Monto: S/ ${formatNumber(amount)}`
                            ];
                        }
                    }
                }
            },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        color: '#92a4bf',
                        font: { size: 11 }
                    },
                    grid: {
                        color: 'rgba(154, 181, 219, 0.12)'
                    }
                },
                x: {
                    ticks: {
                        color: '#92a4bf',
                        font: { size: 11 }
                    },
                    grid: {
                        display: false
                    }
                }
            }
        }
    });
}

function exportDashboard() {
    const message = document.getElementById('message');
    message.textContent = 'Generando exportación del dashboard...';
    
    // Simple CSV export of current metrics
    const dateFilter = document.getElementById('dateFilter').value;
    const dateParam = dateFilter === 'all' ? '' : `?days=${dateFilter}`;
    
    Promise.all([
        fetch(`${API_BASE}/api/metrics/summary${dateParam}`).then(r => r.json()),
        fetch(`${API_BASE}/api/metrics/by-category${dateParam}`).then(r => r.json()),
        fetch(`${API_BASE}/api/metrics/by-entity${dateParam}`).then(r => r.json()),
        fetch(`${API_BASE}/api/metrics/trends${dateParam}`).then(r => r.json())
    ])
    .then(([summary, categories, entities, trends]) => {
        let csv = 'LicitIA Dashboard Export\n\n';
        csv += 'RESUMEN\n';
        csv += `Total Oportunidades,${summary.totalOpportunities}\n`;
        csv += `Oportunidades Prioritarias,${summary.priorityOpportunities}\n`;
        csv += `Monto Total,${summary.totalAmount}\n`;
        csv += `Score Promedio,${summary.averageScore}\n`;
        csv += `Categorías,${summary.categories}\n`;
        csv += `Entidades,${summary.entities}\n\n`;
        
        csv += 'POR CATEGORÍA\n';
        csv += 'Categoría,Cantidad,Monto,Score Promedio\n';
        categories.forEach(c => {
            csv += `${c.category},${c.count},${c.totalAmount},${c.averageScore}\n`;
        });
        
        csv += '\nTOP ENTIDADES\n';
        csv += 'Entidad,Cantidad,Monto\n';
        entities.forEach(e => {
            csv += `${e.entity},${e.count},${e.totalAmount}\n`;
        });
        
        csv += '\nTENDENCIAS MENSUALES\n';
        csv += 'Año,Mes,Cantidad,Monto\n';
        trends.forEach(t => {
            csv += `${t.year},${t.month},${t.count},${t.totalAmount}\n`;
        });
        
        const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
        const link = document.createElement('a');
        const url = URL.createObjectURL(blob);
        link.setAttribute('href', url);
        link.setAttribute('download', `licitia-dashboard-${new Date().toISOString().split('T')[0]}.csv`);
        link.style.visibility = 'hidden';
        document.body.appendChild(link);
        link.click();
        document.body.removeChild(link);
        
        message.textContent = 'Dashboard exportado exitosamente';
        setTimeout(() => message.textContent = '', 3000);
    })
    .catch(error => {
        console.error('Error exporting dashboard:', error);
        message.textContent = 'Error al exportar dashboard';
    });
}

// Load metrics when page loads
document.addEventListener('DOMContentLoaded', loadDashboardMetrics);
