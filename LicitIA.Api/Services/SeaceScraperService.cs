using Microsoft.Playwright;
using HtmlAgilityPack;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("LicitIA.Tests")]

namespace LicitIA.Api.Services;

public class SeaceScraperService
{
    private readonly HttpClient _httpClient;

    public SeaceScraperService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<List<ScrapedOpportunity>> ScrapeOpportunitiesAsync(int maxResults = 30, CancellationToken cancellationToken = default)
    {
        try
        {
            Console.WriteLine("[SeaceScraper] Iniciando scraping con Playwright...");

            // Usar Playwright para scraping de SEACE
            var opportunities = await ScrapeWithPlaywright(maxResults, cancellationToken);

            if (opportunities.Count > 0)
            {
                Console.WriteLine($"[SeaceScraper] Se obtuvieron {opportunities.Count} oportunidades de SEACE");
                return opportunities;
            }

            // Si falla, usar datos de ejemplo
            Console.WriteLine("[SeaceScraper] Scraping falló, usando datos de ejemplo");
            return GetExampleOpportunities();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SeaceScraper] Error: {ex.Message}");
            Console.WriteLine("[SeaceScraper] Usando datos de ejemplo como fallback");
            return GetExampleOpportunities();
        }
    }

    private async Task<List<ScrapedOpportunity>> ScrapeWithPlaywright(int maxResults, CancellationToken cancellationToken)
    {
        var opportunities = new List<ScrapedOpportunity>();

        try
        {
            using var playwright = await Playwright.CreateAsync();

            // Usar Chromium (headless=true para más velocidad)
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = false,
                SlowMo = 100 // Hacer más lento para que sea visible
            });

            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();

            Console.WriteLine("[SeaceScraper] Navegando a SEACE - página principal...");
            await page.GotoAsync("https://prod2.seace.gob.pe/seacebus-uiwd-pub/buscadorPublico/buscadorPublico.xhtml#", new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
                Timeout = 60000
            });

            Console.WriteLine("[SeaceScraper] Esperando botón 'Buscador de Procedimientos de Selección'...");
            try
            {
                await page.WaitForSelectorAsync("button:has-text('Buscador de Procedimientos de Selección'), a:has-text('Buscador de Procedimientos de Selección')", new PageWaitForSelectorOptions
                {
                    Timeout = 10000
                });
                
                var button = await page.QuerySelectorAsync("button:has-text('Buscador de Procedimientos de Selección'), a:has-text('Buscador de Procedimientos de Selección')");
                if (button != null)
                {
                    Console.WriteLine("[SeaceScraper] Botón encontrado, haciendo clic...");
                    await button.ClickAsync();
                }
            }
            catch
            {
                Console.WriteLine("[SeaceScraper] Botón no encontrado con selector, intentando por JavaScript...");
                await page.EvaluateAsync(@"() => {
                    const elements = Array.from(document.querySelectorAll('button, a, div, span'));
                    const target = elements.find(el => el.textContent.includes('Buscador de Procedimientos de Selección'));
                    if (target) target.click();
                }");
            }

            Console.WriteLine("[SeaceScraper] Esperando botón 'Buscar'...");
            try
            {
                await page.WaitForSelectorAsync("#tbBuscador\\:idFormBuscarProceso\\:btnBuscarSelToken", new PageWaitForSelectorOptions
                {
                    Timeout = 10000
                });
                
                var searchButton = await page.QuerySelectorAsync("#tbBuscador\\:idFormBuscarProceso\\:btnBuscarSelToken");
                if (searchButton != null)
                {
                    Console.WriteLine("[SeaceScraper] Botón Buscar encontrado, haciendo clic...");
                    await searchButton.ClickAsync();
                }
            }
            catch
            {
                Console.WriteLine("[SeaceScraper] Botón Buscar no encontrado, intentando por JavaScript...");
                await page.EvaluateAsync(@"() => {
                    const buttons = Array.from(document.querySelectorAll('button, input[type=""submit""]'));
                    const searchBtn = buttons.find(btn => btn.textContent.includes('Buscar') || btn.value.includes('Buscar'));
                    if (searchBtn) searchBtn.click();
                }");
            }

            Console.WriteLine("[SeaceScraper] Esperando tabla de resultados...");
            try
            {
                await page.WaitForSelectorAsync("table tbody tr", new PageWaitForSelectorOptions
                {
                    Timeout = 15000
                });
                Console.WriteLine("[SeaceScraper] Tabla de resultados encontrada");
            }
            catch
            {
                Console.WriteLine("[SeaceScraper] Timeout esperando tabla, procediendo con lo disponible");
            }

            // Buscar tablas de resultados con paginación
            int currentPage = 1;
            int maxPages = 10; // Extraer de las primeras 10 páginas
            var pageProcessCodes = new HashSet<string>(); // Para detectar duplicados entre páginas
            
            while (opportunities.Count < maxResults && currentPage <= maxPages)
            {
                Console.WriteLine($"[SeaceScraper] Procesando página {currentPage}...");
                var pageStartCount = opportunities.Count;
                
                // Log del HTML de la página para depuración
                var pageContent = await page.ContentAsync();
                Console.WriteLine($"[SeaceScraper] Longitud del HTML de la página: {pageContent.Length}");
                
                // Captura de pantalla para depuración (solo página 2)
                if (currentPage == 2)
                {
                    await page.ScreenshotAsync(new PageScreenshotOptions
                    {
                        Path = "d:\\Proyecto_Respalto_caps\\pagina2_seace.png"
                    });
                    Console.WriteLine("[SeaceScraper] Captura de pantalla guardada en pagina2_seace.png");
                }
                
                // Buscar tablas de resultados
                var tables = await page.QuerySelectorAllAsync("table");
                Console.WriteLine($"[SeaceScraper] Encontradas {tables.Count} tablas en página {currentPage}");

                foreach (var table in tables)
                {
                    try
                    {
                        var rows = await table.QuerySelectorAllAsync("tbody tr");
                        Console.WriteLine($"[SeaceScraper] Tabla con {rows.Count} filas");
                        
                        // Log para depuración: mostrar contenido de primera fila
                        if (rows.Count > 0 && currentPage == 2)
                        {
                            var firstRowCells = await rows[0].QuerySelectorAllAsync("td");
                            Console.WriteLine($"[SeaceScraper] Primera fila de página 2 tiene {firstRowCells.Count} celdas");
                            for (int i = 0; i < firstRowCells.Count; i++)
                            {
                                var cellText = await firstRowCells[i].TextContentAsync();
                                Console.WriteLine($"[SeaceScraper] Celda {i}: {cellText}");
                            }
                        }

                        if (rows.Count > 0)
                        {
                            int count = 0;
                            // Saltar la primera fila (encabezado)
                            foreach (var row in rows.Skip(1))
                            {
                                if (opportunities.Count >= maxResults) break;

                                try
                                {
                                    var cells = await row.QuerySelectorAllAsync("td");
                                    if (cells.Count >= 6)
                                    {
                                        var cellData = new string[Math.Min(cells.Count, 12)];
                                        for (int j = 0; j < cellData.Length; j++)
                                        {
                                            cellData[j] = (await cells[j].TextContentAsync())?.Trim() ?? "";
                                        }

                                        var publishedDate = ParseSeaceDate(cellData[2]);
                                        
                                        // Validar fecha antes de crear la oportunidad
                                        if (publishedDate == null || publishedDate < new DateTime(1753, 1, 1) || publishedDate > new DateTime(9999, 12, 31))
                                        {
                                            Console.WriteLine($"[SeaceScraper] Fila ignorada por fecha inválida: {cellData[2]}");
                                            continue;
                                        }

                                        var opportunity = new ScrapedOpportunity
                                        {
                                            EntityName = cellData[1],
                                            PublishedDate = publishedDate,
                                            Title = cellData[3],
                                            Category = cellData[5],
                                            Description = cellData[6],
                                            EstimatedAmount = ParseSeaceAmount(cellData[9]),
                                            ClosingDate = publishedDate?.AddDays(30) ?? DateTime.Now.AddDays(30)
                                        };

                                        // Usar nomenclatura como process code
                                        opportunity.ProcessCode = cellData[3];

                                        // Verificar si ya existe en esta página
                                        if (pageProcessCodes.Contains(cellData[3]))
                                        {
                                            Console.WriteLine($"[SeaceScraper] Duplicado en misma página: {cellData[3]}");
                                            continue;
                                        }
                                        
                                        pageProcessCodes.Add(cellData[3]);
                                        opportunities.Add(opportunity);
                                        count++;

                                        Console.WriteLine($"[SeaceScraper] {cellData[1]} - {cellData[2]} - {cellData[3]}");
                                    }
                                }
                                catch
                                {
                                    // Error al procesar fila, continuar
                                }
                            }

                            Console.WriteLine($"[SeaceScraper] Se extrajeron {count} oportunidades de esta tabla");
                        }
                    }
                    catch
                    {
                        // Error al procesar tabla, continuar
                    }
                }

                // Si ya tenemos suficientes, salir
                var pageEndCount = opportunities.Count;
                Console.WriteLine($"[SeaceScraper] Página {currentPage}: Extraídas {pageEndCount - pageStartCount} oportunidades. Total: {opportunities.Count}");
                
                if (opportunities.Count >= maxResults)
                {
                    Console.WriteLine("[SeaceScraper] Se alcanzó maxResults, saliendo del loop de paginación");
                    break;
                }
                
                // Si estamos en la última página, salir
                if (currentPage >= maxPages)
                {
                    Console.WriteLine("[SeaceScraper] Se alcanzó maxPages, saliendo del loop de paginación");
                    break;
                }
                
                Console.WriteLine("[SeaceScraper] Procediendo a navegar a la siguiente página");

                // Hacer scroll al final de la página para encontrar el paginador bottom
                Console.WriteLine("[SeaceScraper] Haciendo scroll al final de la página...");
                await page.EvaluateAsync(@"() => {
                    window.scrollTo(0, document.body.scrollHeight);
                }");
                await Task.Delay(1000);

                // Buscar y hacer clic en botón de siguiente página del paginador bottom
                Console.WriteLine($"[SeaceScraper] Buscando botón de siguiente página en paginador bottom...");
                try
                {
                    // Verificar si el paginador bottom existe
                    var paginatorInfo = await page.EvaluateAsync<string>(@"() => {
                        const paginator = document.getElementById('tbBuscador:idFormBuscarProceso:dtProcesos_paginator_bottom');
                        if (paginator) {
                            const nextButton = paginator.querySelector('.ui-paginator-next');
                            const hasDisabledClass = nextButton ? nextButton.classList.contains('ui-state-disabled') : false;
                            const paginatorText = paginator.querySelector('.ui-paginator-current')?.textContent || '';
                            return JSON.stringify({
                                found: true,
                                nextButtonFound: nextButton !== null,
                                hasDisabledClass: hasDisabledClass,
                                paginatorText: paginatorText
                            });
                        }
                        return JSON.stringify({ found: false });
                    }");
                    
                    Console.WriteLine($"[SeaceScraper] Info del paginador: {paginatorInfo}");
                    
                    // Verificar número de página actual antes del clic
                    var currentPageNumBefore = await page.EvaluateAsync<int>(@"() => {
                        const paginator = document.getElementById('tbBuscador:idFormBuscarProceso:dtProcesos_paginator_bottom');
                        if (paginator) {
                            const activePage = paginator.querySelector('.ui-paginator-page.ui-state-active');
                            if (activePage) {
                                return parseInt(activePage.textContent);
                            }
                        }
                        return 1;
                    }");
                    Console.WriteLine($"[SeaceScraper] Página actual ANTES del clic: {currentPageNumBefore}");
                    
                    // Intentar hacer clic en el botón siguiente del paginador bottom
                    var nextClicked = await page.EvaluateAsync<bool>(@"() => {
                        const paginator = document.getElementById('tbBuscador:idFormBuscarProceso:dtProcesos_paginator_bottom');
                        if (paginator) {
                            const nextButton = paginator.querySelector('.ui-paginator-next');
                            if (nextButton && !nextButton.classList.contains('ui-state-disabled')) {
                                console.log('Haciendo click en botón siguiente del paginador bottom');
                                nextButton.click();
                                console.log('Click ejecutado');
                                return true;
                            }
                        }
                        console.log('Botón siguiente no encontrado o deshabilitado');
                        return false;
                    }");
                    
                    Console.WriteLine($"[SeaceScraper] JavaScript click resultado: {nextClicked}");
                    
                    if (nextClicked)
                    {
                        Console.WriteLine("[SeaceScraper] Botón siguiente clickeado por JavaScript");
                        Console.WriteLine("[SeaceScraper] Esperando carga de siguiente página (AJAX)...");
                        
                        // Esperar a que la red esté inactiva (para llamadas AJAX)
                        try
                        {
                            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
                            {
                                Timeout = 15000
                            });
                            Console.WriteLine("[SeaceScraper] Red inactiva, tabla actualizada");
                        }
                        catch
                        {
                            Console.WriteLine("[SeaceScraper] Timeout esperando red inactiva");
                        }
                        
                        // Esperar adicional para asegurar que el DOM se actualizó
                        await Task.Delay(3000);
                        
                        // Hacer scroll al inicio de la página para procesar la nueva tabla
                        await page.EvaluateAsync(@"() => {
                            window.scrollTo(0, 0);
                        }");
                        await Task.Delay(500);
                        
                        // Verificar número de página actual después del clic
                        var currentPageNumAfter = await page.EvaluateAsync<int>(@"() => {
                            const paginator = document.getElementById('tbBuscador:idFormBuscarProceso:dtProcesos_paginator_bottom');
                            if (paginator) {
                                const activePage = paginator.querySelector('.ui-paginator-page.ui-state-active');
                                if (activePage) {
                                    return parseInt(activePage.textContent);
                                }
                            }
                            return 1;
                        }");
                        Console.WriteLine($"[SeaceScraper] Página actual DESPUÉS del clic: {currentPageNumAfter}");
                        
                        if (currentPageNumAfter == currentPageNumBefore)
                        {
                            Console.WriteLine("[SeaceScraper] WARNING: La página NO cambió después del clic");
                        }
                        else
                        {
                            Console.WriteLine($"[SeaceScraper] Página cambió de {currentPageNumBefore} a {currentPageNumAfter}");
                        }
                        
                        currentPage++;
                    }
                    else
                    {
                        Console.WriteLine("[SeaceScraper] No se encontró botón de siguiente o está deshabilitado, terminando paginación");
                        break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SeaceScraper] Error al navegar a siguiente página: {ex.Message}");
                    break;
                }
            }

            Console.WriteLine("[SeaceScraper] Cerrando navegador...");
            await browser.CloseAsync();

            return opportunities;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SeaceScraper] Error en Playwright: {ex.Message}");
            return new List<ScrapedOpportunity>();
        }
    }

    private DateTime? ParseSeaceDate(string dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return null;

        // Formato esperado: "23/04/2026 11:04"
        if (DateTime.TryParseExact(dateStr, "dd/MM/yyyy HH:mm", null, System.Globalization.DateTimeStyles.None, out var date))
        {
            return date;
        }

        return null;
    }

    // Test helper for unit tests
    internal DateTime? TestParseSeaceDate(string dateStr) => ParseSeaceDate(dateStr);

    private decimal ParseSeaceAmount(string amountStr)
    {
        if (string.IsNullOrWhiteSpace(amountStr))
            return 0;

        // Remover espacios y formatear
        amountStr = amountStr.Replace(" ", "").Replace(",", ".");

        if (decimal.TryParse(amountStr, out var amount))
        {
            return amount;
        }

        return 0;
    }

    // Test helper for unit tests
    internal decimal TestParseSeaceAmount(string amountStr) => ParseSeaceAmount(amountStr);

    private string ExtractProcessCode(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return "";

        // Buscar patrón de código de proceso (ej: LP-ABR-4-2026-MPC/CS-1)
        var match = System.Text.RegularExpressions.Regex.Match(title, @"[A-Z]{2,}-[A-Z]{3,}-\d+-\d{4}-[A-Z/]+-\d+");
        if (match.Success)
        {
            return match.Value;
        }

        return "";
    }

    private List<ScrapedOpportunity> GetExampleOpportunities()
    {
        // Datos de ejemplo eliminados - ahora usamos datos reales de OECE
        // Usar el endpoint /api/oece/download para descargar datos reales
        Console.WriteLine("[SeaceScraper] Datos de ejemplo desactivados. Usa /api/oece/download para datos reales.");
        return new List<ScrapedOpportunity>();
    }
}
