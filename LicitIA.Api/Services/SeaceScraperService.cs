using Microsoft.Playwright;
using HtmlAgilityPack;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;

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

    public async Task<List<ScrapedOpportunity>> ScrapeOpportunitiesAsync(
        int maxResults = 30,
        CancellationToken cancellationToken = default,
        string? objectDescription = null,
        int? callYear = null)
    {
        try
        {
            Console.WriteLine("[SeaceScraper] Iniciando scraping con Playwright...");

            // Usar Playwright para scraping de SEACE
            var opportunities = await ScrapeWithPlaywright(maxResults, cancellationToken, objectDescription, callYear);

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

    private async Task<List<ScrapedOpportunity>> ScrapeWithPlaywright(int maxResults, CancellationToken cancellationToken, string? objectDescription, int? callYear)
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

            await ApplyCallYearFilterAsync(page, callYear);
            await ApplyObjectDescriptionFilterAsync(page, objectDescription);

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
                            var dataRows = rows.Skip(1).ToList();
                            var firstRowIndexOnPage = await GetFirstVisibleResultRowIndexAsync(page);
                            for (int rowIndex = 0; rowIndex < dataRows.Count; rowIndex++)
                            {
                                if (opportunities.Count >= maxResults) break;

                                try
                                {
                                    var row = dataRows[rowIndex];
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
                                            ClosingDate = publishedDate?.AddDays(30) ?? DateTime.Now.AddDays(30),
                                            SeaceDetailButtonId = await GetDetailButtonIdAsync(row),
                                            SeaceRowIndex = firstRowIndexOnPage + rowIndex
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
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[SeaceScraper] Error procesando fila {rowIndex}: {ex.Message}");
                                }
                            }

                            var pageOpportunities = opportunities.Skip(opportunities.Count - count).ToList();
                            for (int detailIndex = 0; detailIndex < pageOpportunities.Count; detailIndex++)
                            {
                                try
                                {
                                    await OpenDetailAndReturnAsync(page, detailIndex, pageOpportunities[detailIndex]);
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"[SeaceScraper] Error abriendo detalle de fila {detailIndex}: {ex.Message}");
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

    private async Task OpenDetailAndReturnAsync(IPage page, int rowIndex, ScrapedOpportunity opportunity)
    {
        Console.WriteLine($"[SeaceScraper] Abriendo detalle de fila {rowIndex}: {opportunity.ProcessCode}...");

        var effectiveRowIndex = opportunity.SeaceRowIndex > 0 ? opportunity.SeaceRowIndex : rowIndex;
        var detailSelector = $"#tbBuscador\\:idFormBuscarProceso\\:dtProcesos\\:{effectiveRowIndex}\\:j_idt377";
        try
        {
            if (!string.IsNullOrWhiteSpace(opportunity.SeaceDetailButtonId))
            {
                var clickedStoredButton = await page.EvaluateAsync<bool>(
                    @"(buttonId) => {
                        const button = document.getElementById(buttonId);
                        if (!button) return false;
                        button.click();
                        return true;
                    }",
                    opportunity.SeaceDetailButtonId);

                if (clickedStoredButton)
                {
                    Console.WriteLine($"[SeaceScraper] Detalle abierto con boton capturado: {opportunity.SeaceDetailButtonId}");
                    await WaitForDetailAndExtractAsync(page, rowIndex, opportunity);
                    return;
                }
            }

            var clicked = await page.EvaluateAsync<bool>(
                @"(args) => {
                    const normalize = value => (value || '').replace(/\s+/g, ' ').trim();
                    const processCode = normalize(args.processCode);
                    const title = normalize(args.title);
                    const rowsRoot = document.getElementById('tbBuscador:idFormBuscarProceso:dtProcesos_data');
                    const rows = rowsRoot ? Array.from(rowsRoot.querySelectorAll('tr')) : [];
                    const row = rows.find(current => {
                        const text = normalize(current.textContent);
                        return (processCode && text.includes(processCode)) ||
                            (title && text.includes(title));
                    });

                    const findButton = root => {
                        if (!root) return null;
                        return root.querySelector('[id$="":j_idt377""]') ||
                            Array.from(root.querySelectorAll('button, a, span.ui-button, input[type=""submit""]'))
                                .find(el => normalize(el.textContent || el.value || el.title || el.getAttribute('aria-label')).length <= 40);
                    };

                    const button = findButton(row) ||
                        document.getElementById(`tbBuscador:idFormBuscarProceso:dtProcesos:${args.effectiveRowIndex}:j_idt377`) ||
                        document.getElementById(`tbBuscador:idFormBuscarProceso:dtProcesos:${args.rowIndex}:j_idt377`);

                    if (!button) return false;
                    button.click();
                    return true;
                }",
                new { rowIndex, effectiveRowIndex, opportunity.ProcessCode, opportunity.Title });

            if (!clicked)
            {
                await page.WaitForSelectorAsync(detailSelector, new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = 10000
                });
                await page.ClickAsync(detailSelector);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SeaceScraper] No se pudo abrir detalle para {opportunity.ProcessCode}: {ex.Message}");
            return;
        }

        await WaitForDetailAndExtractAsync(page, rowIndex, opportunity);
    }

    private async Task WaitForDetailAndExtractAsync(IPage page, int rowIndex, ScrapedOpportunity opportunity)
    {
        await page.WaitForSelectorAsync("#tbFicha\\:pnlContenedorGral", new PageWaitForSelectorOptions
        {
            State = WaitForSelectorState.Visible,
            Timeout = 15000
        });

        try
        {
            await page.WaitForFunctionAsync(
                @"() => {
                    const ficha = document.getElementById('tbFicha:pnlContenedorFicha1');
                    return Boolean(ficha && ficha.textContent && ficha.textContent.includes('Nomenclatura'));
                }",
                null,
                new PageWaitForFunctionOptions { Timeout = 8000 });
        }
        catch (TimeoutException)
        {
            Console.WriteLine($"[SeaceScraper] Ficha1 no mostro Nomenclatura a tiempo en fila {rowIndex}; se intentara extraer igual.");
        }

        Console.WriteLine($"[SeaceScraper] Detalle de fila {rowIndex} abierto.");

        await ExtractDetailFromPanelAsync(page, opportunity, rowIndex);

        var returned = await page.EvaluateAsync<bool>(
            @"() => {
                const normalize = value => (value || '').replace(/\s+/g, ' ').trim().toLowerCase();
                const textNode = Array.from(document.querySelectorAll('.ui-button-text.ui-c, button, a, input[type=""submit""]'))
                    .find(el => {
                        const text = normalize(el.textContent || el.value || el.title || el.getAttribute('aria-label'));
                        return text === 'regresar' || text.includes('regresar');
                    });
                if (!textNode) return false;

                const clickable = textNode.closest('button, a, .ui-button') || textNode;
                clickable.dispatchEvent(new MouseEvent('mouseover', { bubbles: true }));
                clickable.dispatchEvent(new MouseEvent('mousedown', { bubbles: true }));
                clickable.dispatchEvent(new MouseEvent('mouseup', { bubbles: true }));
                clickable.click();
                return true;
            }");

        if (!returned)
        {
            Console.WriteLine($"[SeaceScraper] No se encontro boton Regresar en detalle de fila {rowIndex}.");
            return;
        }

        try
        {
            await page.WaitForFunctionAsync(
                @"() => {
                    const results = document.getElementById('tbBuscador:idFormBuscarProceso:dtProcesos_data');
                    const detail = document.getElementById('tbFicha:pnlContenedorGral');
                    return Boolean(results && results.querySelector('tr')) && (!detail || detail.offsetParent === null);
                }",
                null,
                new PageWaitForFunctionOptions { Timeout = 15000 });
        }
        catch (TimeoutException)
        {
            Console.WriteLine($"[SeaceScraper] No se confirmo visualmente el regreso desde fila {rowIndex}; se continuara con la siguiente fila.");
        }

        await Task.Delay(800);
        Console.WriteLine($"[SeaceScraper] Regreso a resultados desde fila {rowIndex}.");
    }

    private static async Task<string> GetDetailButtonIdAsync(IElementHandle row)
    {
        return await row.EvaluateAsync<string>(
            @"row => {
                const candidates = Array.from(row.querySelectorAll('[id], a, button, input[type=""submit""], span.ui-button'));
                const withId = candidates.filter(el => el.id && el.id.includes('tbBuscador:idFormBuscarProceso:dtProcesos'));
                const direct = withId.find(el => el.id.endsWith(':j_idt377'));
                const action = withId.find(el => {
                    const text = (el.textContent || el.value || el.title || el.getAttribute('aria-label') || '').toLowerCase();
                    return text.includes('ver') || text.includes('detalle') || el.classList.contains('ui-button');
                });
                return (direct || action || withId[withId.length - 1] || {}).id || '';
            }");
    }

    private static async Task<int> GetFirstVisibleResultRowIndexAsync(IPage page)
    {
        return await page.EvaluateAsync<int>(
            @"() => {
                const paginator = document.getElementById('tbBuscador:idFormBuscarProceso:dtProcesos_paginator_bottom') ||
                    document.getElementById('tbBuscador:idFormBuscarProceso:dtProcesos_paginator_top');
                const activePage = paginator?.querySelector('.ui-paginator-page.ui-state-active');
                const rowsSelector = paginator?.querySelector('select.ui-paginator-rpp-options');
                const pageNumber = parseInt(activePage?.textContent || '1', 10);
                const rowsPerPage = parseInt(rowsSelector?.value || '15', 10);
                return Math.max(0, (pageNumber - 1) * rowsPerPage);
            }");
    }

    private async Task ExtractDetailFromPanelAsync(IPage page, ScrapedOpportunity opportunity, int rowIndex)
    {
        var fichaText = await page.EvaluateAsync<string>(
            @"() => {
                const root = document.getElementById('tbFicha:pnlContenedorFicha1');
                return root ? (root.innerText || root.textContent || '') : '';
            }");

        var details = await page.EvaluateAsync<Dictionary<string, string>>(
            @"() => {
                const normalize = value => (value || '').replace(/\s+/g, ' ').replace(/:$/, '').trim();
                const cleanCellText = cell => {
                    const clone = cell.cloneNode(true);
                    clone.querySelectorAll('script, style, table').forEach(node => node.remove());
                    return normalize(clone.textContent || cell.textContent);
                };
                const normalizeKey = value => normalize(value)
                    .toLowerCase()
                    .normalize('NFD')
                    .replace(/[\u0300-\u036f]/g, '')
                    .replace(/[°º]/g, '')
                    .replace(/[^a-z0-9/ ]/g, '')
                    .replace(/\s+/g, ' ');
                const result = {};
                const addPair = (key, value) => {
                    key = normalize(key);
                    value = normalize(value);
                    if (!key || !value || key.length > 140) return;
                    if (!result[key]) {
                        result[key] = value;
                        return;
                    }

                    let index = 2;
                    while (result[`${key} ${index}`]) index++;
                    result[`${key} ${index}`] = value;
                };
                const extractKnownLabelsFromText = root => {
                    if (!root) return;
                    const labels = [
                        'Nomenclatura',
                        'N° Convocatoria',
                        'N Convocatoria',
                        'Tipo Compra o Selección',
                        'Tipo Compra o Seleccion',
                        'Normativa Aplicable',
                        'Versión SEACE',
                        'Version SEACE',
                        'Entidad Convocante',
                        'Dirección Legal',
                        'Direccion Legal',
                        'Pagina Web',
                        'Página Web',
                        'Teléfono de la Entidad',
                        'Telefono de la Entidad',
                        'Objeto de Contratación',
                        'Objeto de Contratacion',
                        'Descripción del Objeto',
                        'Descripcion del Objeto',
                        'VR / VE / Cuantía de la contratación',
                        'VR / VE / Cuantia de la contratacion',
                        'Monto del Derecho de Participación',
                        'Monto del Derecho de Participacion',
                        'Monto del costo de Reproducción de las Bases',
                        'Monto del costo de Reproduccion de las Bases',
                        'Fecha y Hora Publicación',
                        'Fecha y Hora Publicacion'
                    ];
                    const lines = normalize(root.innerText || root.textContent)
                        .split(/(?=(?:Nomenclatura|N° Convocatoria|N Convocatoria|Tipo Compra|Normativa Aplicable|Versión SEACE|Version SEACE|Entidad Convocante|Dirección Legal|Direccion Legal|Página Web|Pagina Web|Teléfono de la Entidad|Telefono de la Entidad|Objeto de Contratación|Objeto de Contratacion|Descripción del Objeto|Descripcion del Objeto|VR \/ VE|Monto del Derecho|Monto del costo|Fecha y Hora))/)
                        .map(line => normalize(line))
                        .filter(Boolean);

                    for (const line of lines) {
                        const match = labels.find(label => normalizeKey(line).startsWith(normalizeKey(label)));
                        if (!match) continue;
                        const value = normalize(line.substring(match.length).replace(/^:/, ''));
                        addPair(match, value);
                    }
                };
                const extractRoot = root => {
                    if (!root) return;

                    root.querySelectorAll('tr').forEach(row => {
                        const cells = Array.from(row.children).filter(cell =>
                            ['TD', 'TH'].includes(cell.tagName));
                        if (cells.length < 2) return;

                        if (cells.length >= 4) {
                            for (let index = 0; index < cells.length - 1; index += 2) {
                                addPair(cleanCellText(cells[index]), cleanCellText(cells[index + 1]));
                            }
                        } else {
                            addPair(cleanCellText(cells[0]), cells.slice(1).map(cleanCellText).join(' '));
                        }
                    });

                    root.querySelectorAll('td, th, label, span').forEach(node => {
                        const key = normalize(node.textContent);
                        if (!key || !key.endsWith(':')) return;
                        const next = node.nextElementSibling;
                        if (next) addPair(key, next.textContent);
                    });

                    extractKnownLabelsFromText(root);
                };

                extractRoot(document.getElementById('tbFicha:pnlContenedorFicha1'));
                if (Object.keys(result).length === 0) {
                    const candidates = Array.from(document.querySelectorAll('div, fieldset, table'))
                        .filter(node => {
                            const text = normalize(node.textContent);
                            return text.includes('Nomenclatura') &&
                                text.includes('Entidad Convocante') &&
                                text.includes('Descripcion del Objeto');
                        });
                    extractRoot(candidates[0]);
                }
                if (Object.keys(result).length === 0) {
                    extractRoot(document.getElementById('tbFicha:pnlContenedorGral'));
                }

                const knownLabels = [
                    'Nomenclatura',
                    'N° Convocatoria',
                    'N Convocatoria',
                    'Tipo Compra o Selección',
                    'Tipo Compra o Seleccion',
                    'Normativa Aplicable',
                    'Versión SEACE',
                    'Version SEACE',
                    'Entidad Convocante',
                    'Dirección Legal',
                    'Direccion Legal',
                    'Pagina Web',
                    'Página Web',
                    'Teléfono de la Entidad',
                    'Telefono de la Entidad',
                    'Objeto de Contratación',
                    'Objeto de Contratacion',
                    'Descripción del Objeto',
                    'Descripcion del Objeto',
                    'VR / VE / Cuantía de la contratación',
                    'VR / VE / Cuantia de la contratacion',
                    'Monto del Derecho de Participación',
                    'Monto del Derecho de Participacion',
                    'Monto del costo de Reproducción de las Bases',
                    'Monto del costo de Reproduccion de las Bases',
                    'Fecha y Hora Publicación',
                    'Fecha y Hora Publicacion'
                ];
                const wanted = new Map(knownLabels.map(label => [normalizeKey(label), label]));
                document.querySelectorAll('td, th').forEach(cell => {
                    const key = normalizeKey(cell.textContent);
                    if (!wanted.has(key)) return;
                    const row = cell.closest('tr');
                    const cells = row ? Array.from(row.children).filter(item => ['TD', 'TH'].includes(item.tagName)) : [];
                    const index = cells.indexOf(cell);
                    const nextCell = index >= 0 ? cells[index + 1] : null;
                    if (nextCell) {
                        addPair(wanted.get(key), cleanCellText(nextCell));
                    }
                });

                return result;
            }");

        if (details.Count == 0 && !string.IsNullOrWhiteSpace(fichaText))
        {
            details = ExtractDetailsFromFichaText(fichaText);
        }

        var scheduleJson = await page.EvaluateAsync<string>(
            @"() => {
                const root = document.getElementById('tbFicha:pnlContenedorFicha2') ||
                    document.getElementById('tbFicha:pnlContenedorGral');
                if (!root) return '';
                const normalize = value => (value || '').replace(/\s+/g, ' ').trim();
                const tables = Array.from(root.querySelectorAll('table'));
                const table = tables.find(current => {
                    const text = normalize(current.textContent);
                    return text.includes('Etapa') && text.includes('Fecha Inicio') && text.includes('Fecha Fin');
                });
                if (!table) return '';
                const rows = Array.from(table.querySelectorAll('tr')).slice(1).map(row => {
                    const cells = Array.from(row.querySelectorAll('td')).map(cell => normalize(cell.textContent));
                    return cells.length >= 3 ? { etapa: cells[0], fechaInicio: cells[1], fechaFin: cells[2] } : null;
                }).filter(Boolean);
                return JSON.stringify(rows);
            }");

        ApplyDetailFields(opportunity, details, scheduleJson);
        Console.WriteLine($"[SeaceScraper] Detalle extraido de fila {rowIndex}: {details.Count} campos. Texto ficha: {fichaText.Length} caracteres.");
    }

    private static Dictionary<string, string> ExtractDetailsFromFichaText(string fichaText)
    {
        var labels = new[]
        {
            "Nomenclatura",
            "N° Convocatoria",
            "N Convocatoria",
            "Tipo Compra o Selección",
            "Tipo Compra o Seleccion",
            "Normativa Aplicable",
            "Versión SEACE",
            "Version SEACE",
            "Entidad Convocante",
            "Dirección Legal",
            "Direccion Legal",
            "Pagina Web",
            "Página Web",
            "Teléfono de la Entidad",
            "Telefono de la Entidad",
            "Objeto de Contratación",
            "Objeto de Contratacion",
            "Descripción del Objeto",
            "Descripcion del Objeto",
            "VR / VE / Cuantía de la contratación",
            "VR / VE / Cuantia de la contratacion",
            "Monto del Derecho de Participación",
            "Monto del Derecho de Participacion",
            "Monto del costo de Reproducción de las Bases",
            "Monto del costo de Reproduccion de las Bases",
            "Fecha y Hora Publicación",
            "Fecha y Hora Publicacion"
        };

        var normalizedText = fichaText.Replace("\r", "\n");
        foreach (var label in labels)
        {
            normalizedText = normalizedText.Replace(label + ":", "\n" + label + ":\n", StringComparison.OrdinalIgnoreCase);
            normalizedText = normalizedText.Replace(label, "\n" + label + "\n", StringComparison.OrdinalIgnoreCase);
        }

        var lines = normalizedText
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        var details = new Dictionary<string, string>();
        for (var index = 0; index < lines.Count; index++)
        {
            var rawCurrent = lines[index].Trim();
            var current = rawCurrent.TrimEnd(':');
            var label = labels
                .OrderByDescending(item => item.Length)
                .FirstOrDefault(item =>
                    string.Equals(NormalizeLabel(item), NormalizeLabel(current), StringComparison.OrdinalIgnoreCase) ||
                    NormalizeLabel(rawCurrent).StartsWith(NormalizeLabel(item), StringComparison.OrdinalIgnoreCase));

            if (label is null)
            {
                continue;
            }

            var value = rawCurrent.Length > label.Length
                ? rawCurrent[label.Length..].Trim().TrimStart(':').Trim()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(value) && index + 1 < lines.Count)
            {
                var valueIndex = index + 1;
                while (valueIndex < lines.Count)
                {
                    var candidate = lines[valueIndex].Trim();
                    var candidateIsLabel = labels.Any(item =>
                        string.Equals(NormalizeLabel(item), NormalizeLabel(candidate.TrimEnd(':')), StringComparison.OrdinalIgnoreCase) ||
                        NormalizeLabel(candidate).StartsWith(NormalizeLabel(item), StringComparison.OrdinalIgnoreCase));
                    var candidateIsSectionHeader = NormalizeLabel(candidate).StartsWith("Informacion general", StringComparison.OrdinalIgnoreCase) ||
                        NormalizeLabel(candidate).StartsWith("Informacion General", StringComparison.OrdinalIgnoreCase);

                    if (!string.IsNullOrWhiteSpace(candidate) && candidate != ":" && !candidateIsLabel && !candidateIsSectionHeader)
                    {
                        value = candidate.Trim().TrimStart(':').Trim();
                        break;
                    }

                    valueIndex++;
                }
            }

            if (!string.IsNullOrWhiteSpace(value) && !details.ContainsKey(label))
            {
                details[label] = value;
            }
        }

        return details;
    }

    private void ApplyDetailFields(ScrapedOpportunity opportunity, Dictionary<string, string> details, string scheduleJson)
    {
        opportunity.ProcessCode = ReadDetail(details, "Nomenclatura", opportunity.ProcessCode);
        opportunity.ConvocationNumber = ReadDetail(details, "N Convocatoria", opportunity.ConvocationNumber);
        opportunity.SelectionType = ReadDetail(details, "Tipo Compra", opportunity.SelectionType);
        opportunity.ApplicableRegulation = ReadDetail(details, "Normativa Aplicable", opportunity.ApplicableRegulation);
        opportunity.SeaceVersion = ReadDetail(details, "Version SEACE", opportunity.SeaceVersion);
        opportunity.EntityLegalAddress = ReadDetail(details, "Direccion Legal", opportunity.EntityLegalAddress);
        opportunity.EntityWebsite = ReadDetail(details, "Pagina Web", opportunity.EntityWebsite);
        opportunity.EntityPhone = ReadDetail(details, "Telefono de la Entidad", opportunity.EntityPhone);
        opportunity.ContractObject = ReadDetail(details, "Objeto de Contratacion", opportunity.ContractObject);
        opportunity.ParticipationCost = ReadDetail(details, "Monto del Derecho de Participacion", opportunity.ParticipationCost);
        opportunity.BasesReproductionCost = ReadDetail(details, "Monto del costo de Reproduccion de las Bases", opportunity.BasesReproductionCost);

        var description = ReadDetail(details, "Descripcion del Objeto", string.Empty);
        if (!string.IsNullOrWhiteSpace(description))
        {
            opportunity.Description = description;
        }

        var amountText = ReadDetail(details, "VR / VE / Cuantia", string.Empty);
        var parsedAmount = ParseSeaceAmount(amountText.Replace("Soles", string.Empty, StringComparison.OrdinalIgnoreCase));
        if (parsedAmount > 0)
        {
            opportunity.EstimatedAmount = parsedAmount;
        }

        opportunity.SeaceDetailJson = JsonSerializer.Serialize(details);
        opportunity.SeaceScheduleJson = scheduleJson ?? string.Empty;
    }

    private static string ReadDetail(Dictionary<string, string> details, string label, string fallback)
    {
        var normalizedLabel = NormalizeLabel(label);
        var match = details.FirstOrDefault(item =>
            NormalizeLabel(item.Key).Contains(normalizedLabel, StringComparison.OrdinalIgnoreCase) ||
            normalizedLabel.Contains(NormalizeLabel(item.Key), StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrWhiteSpace(match.Value) ? fallback : match.Value.Trim();
    }

    private static string NormalizeLabel(string value)
    {
        return value
            .Replace("\u00f3", "o")
            .Replace("\u00e9", "e")
            .Replace("\u00e1", "a")
            .Replace("\u00ed", "i")
            .Replace("\u00fa", "u")
            .Replace("\u00f1", "n")
            .Replace("\u00d3", "O")
            .Replace("\u00c9", "E")
            .Replace("\u00c1", "A")
            .Replace("\u00cd", "I")
            .Replace("\u00da", "U")
            .Replace("\u00d1", "N")
            .Replace("ó", "o")
            .Replace("é", "e")
            .Replace("á", "a")
            .Replace("í", "i")
            .Replace("ú", "u")
            .Replace("ñ", "n")
            .Replace("Ó", "O")
            .Replace("É", "E")
            .Replace("Á", "A")
            .Replace("Í", "I")
            .Replace("Ú", "U")
            .Replace("Ñ", "N")
            .Replace("Ã³", "o")
            .Replace("Ã©", "e")
            .Replace("Ã¡", "a")
            .Replace("Ã­", "i")
            .Replace("Ãº", "u")
            .Replace("°", "")
            .Replace("Nro.", "N")
            .Trim();
    }

    private async Task ApplyCallYearFilterAsync(IPage page, int? callYear)
    {
        var currentYear = DateTime.UtcNow.Year;
        var year = callYear.HasValue && callYear.Value >= 2004 && callYear.Value <= currentYear
            ? callYear.Value
            : currentYear;
        var yearText = year.ToString();

        Console.WriteLine($"[SeaceScraper] Aplicando filtro Anio de Convocatoria: {year}");

        try
        {
            const string rootSelector = "#tbBuscador\\:idFormBuscarProceso\\:anioConvocatoria";
            const string panelSelector = "#tbBuscador\\:idFormBuscarProceso\\:anioConvocatoria_panel";

            await page.WaitForSelectorAsync(rootSelector, new PageWaitForSelectorOptions
            {
                Timeout = 10000
            });

            var opened = await page.EvaluateAsync<bool>(
                @"() => {
                    const root = document.getElementById('tbBuscador:idFormBuscarProceso:anioConvocatoria');
                    if (!root) return false;

                    const trigger = root.querySelector('.ui-selectonemenu-trigger');
                    const label = document.getElementById('tbBuscador:idFormBuscarProceso:anioConvocatoria_label');
                    const clickable = trigger || label || root;
                    clickable.click();
                    return true;
                }");

            if (!opened)
            {
                Console.WriteLine("[SeaceScraper] No se pudo abrir el combo anioConvocatoria.");
            }
            else
            {
                Console.WriteLine("[SeaceScraper] Combo Anio de Convocatoria abierto.");
            }

            await page.WaitForSelectorAsync($"{panelSelector} li.ui-selectonemenu-item", new PageWaitForSelectorOptions
            {
                State = WaitForSelectorState.Visible,
                Timeout = 10000
            });

            var selected = await page.EvaluateAsync<bool>(
                @"(year) => {
                    const panel = document.getElementById('tbBuscador:idFormBuscarProceso:anioConvocatoria_panel');
                    if (!panel) return false;

                    const items = Array.from(panel.querySelectorAll('li.ui-selectonemenu-item'));
                    const option = items.find(item => {
                        const value = (item.getAttribute('data-label') || item.textContent || '').trim();
                        return value === String(year);
                    });

                    if (!option) return false;

                    option.click();
                    return true;
                }",
                yearText);

            if (selected)
            {
                await Task.Delay(500);
                var selectedLabel = await page.TextContentAsync("#tbBuscador\\:idFormBuscarProceso\\:anioConvocatoria_label");
                Console.WriteLine($"[SeaceScraper] Anio de Convocatoria seleccionado en UI: {selectedLabel}");
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SeaceScraper] No se pudo seleccionar el anio desde el combo visible: {ex.Message}");
        }

        try
        {
            var applied = await page.EvaluateAsync<bool>(
                @"(year) => {
                    const rootId = 'tbBuscador:idFormBuscarProceso:anioConvocatoria';
                    const root = document.getElementById(rootId);
                    const label = document.getElementById(rootId + '_label');
                    const hidden = document.getElementById(rootId + '_input')
                        || document.querySelector(`input[name='${rootId}_input']`)
                        || document.querySelector(`select[name='${rootId}_input']`);

                    if (label) label.textContent = String(year);

                    if (hidden) {
                        hidden.value = String(year);
                        hidden.dispatchEvent(new Event('input', { bubbles: true }));
                        hidden.dispatchEvent(new Event('change', { bubbles: true }));
                    }

                    return Boolean(root || label || hidden);
                }",
                yearText);

            Console.WriteLine(applied
                ? "[SeaceScraper] Filtro Anio de Convocatoria aplicado por JavaScript como fallback."
                : "[SeaceScraper] No se encontro el combo anioConvocatoria.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SeaceScraper] Error aplicando filtro Anio de Convocatoria: {ex.Message}");
        }
    }

    private async Task ApplyObjectDescriptionFilterAsync(IPage page, string? objectDescription)
    {
        if (string.IsNullOrWhiteSpace(objectDescription))
        {
            Console.WriteLine("[SeaceScraper] Sin filtro de Descripcion del Objeto configurado.");
            return;
        }

        var description = objectDescription.Trim();
        Console.WriteLine($"[SeaceScraper] Aplicando filtro Descripcion del Objeto: {description}");

        try
        {
            const string selector = "#tbBuscador\\:idFormBuscarProceso\\:descripcionObjeto";
            await page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
            {
                Timeout = 10000
            });

            var input = await page.QuerySelectorAsync(selector);
            if (input != null)
            {
                await input.FillAsync(description);
                Console.WriteLine("[SeaceScraper] Filtro Descripcion del Objeto escrito en el formulario.");
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SeaceScraper] No se pudo llenar descripcionObjeto con selector directo: {ex.Message}");
        }

        try
        {
            var applied = await page.EvaluateAsync<bool>(
                @"(description) => {
                    const input = document.getElementById('tbBuscador:idFormBuscarProceso:descripcionObjeto');
                    if (!input) return false;

                    input.value = description;
                    input.dispatchEvent(new Event('input', { bubbles: true }));
                    input.dispatchEvent(new Event('change', { bubbles: true }));
                    return true;
                }",
                description);

            Console.WriteLine(applied
                ? "[SeaceScraper] Filtro Descripcion del Objeto escrito por JavaScript."
                : "[SeaceScraper] No se encontro el input descripcionObjeto.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SeaceScraper] Error aplicando filtro Descripcion del Objeto: {ex.Message}");
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
