using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Data.SqlClient;
using System.Text;
using System.Text.Json;
using LicitIA.Api.Configuration;
using LicitIA.Api.Contracts;
using LicitIA.Api.Data;
using LicitIA.Api.Models;
using LicitIA.Api.Security;
using LicitIA.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
});

builder.WebHost.UseUrls("http://localhost:5153");

builder.Services.AddProblemDetails();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Add JWT Authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>();
        if (jwtOptions != null)
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidAudience = jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key))
            };
        }
    });

builder.Services.AddAuthorization();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<GoogleAuthOptions>(builder.Configuration.GetSection("GoogleAuth"));
builder.Services.Configure<GeminiOptions>(builder.Configuration.GetSection("Gemini"));
builder.Services.AddHttpClient();
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<SqlConnectionFactory>();
builder.Services.AddSingleton<AuthRepository>();
builder.Services.AddSingleton<OpportunityRepository>();
builder.Services.AddSingleton<CompanyProfileRepository>();
builder.Services.AddSingleton<EmailService>();
builder.Services.AddSingleton<SeaceScraperService>();
builder.Services.AddSingleton<CsvImportService>();
builder.Services.AddSingleton<AffinityService>();
builder.Services.AddSingleton<AlertRepository>();
builder.Services.AddSingleton<AlertService>();
builder.Services.AddSingleton<AlertSchedulerService>();
builder.Services.AddSingleton<NotificationRepository>();
builder.Services.AddSingleton<OpportunityAiAnalysisRepository>();
builder.Services.AddHttpClient<OeceDataService>();
builder.Services.AddHttpClient<OeceApiService>();
builder.Services.AddHttpClient<GeminiAnalysisService>();

// Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "LicitIA API",
        Version = "v1",
        Description = "API para gestión de oportunidades de licitaciones públicas en Perú"
    });
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "LicitIA.Api.xml"));
    c.TagActionsBy(api =>
    {
        if (api.RelativePath.Contains("health"))
            return new[] { "Health" };
        if (api.RelativePath.Contains("opportunities"))
            return new[] { "Oportunidades" };
        if (api.RelativePath.Contains("auth"))
            return new[] { "Autenticación" };
        if (api.RelativePath.Contains("seace"))
            return new[] { "SEACE Scraping" };
        if (api.RelativePath.Contains("oece"))
            return new[] { "OECE Scraping" };
        if (api.RelativePath.Contains("profile"))
            return new[] { "Perfil de Usuario" };
        return new[] { "General" };
    });
});

var app = builder.Build();

app.UseExceptionHandler(exceptionApp =>
{
    exceptionApp.Run(async context =>
    {
        var feature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = feature?.Error;

        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await Results.Problem(
            title: "Error interno",
            detail: exception?.Message ?? "Ocurrio un error inesperado.")
            .ExecuteAsync(context);
    });
});

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Enable Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "LicitIA API v1");
    c.RoutePrefix = "swagger";
});

// Serve static frontend files - find the project root by looking for home.html
var contentRoot = builder.Environment.ContentRootPath;
var frontendPath = FindFrontendRoot(contentRoot);

if (!string.IsNullOrEmpty(frontendPath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(frontendPath),
        RequestPath = ""
    });

    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = new PhysicalFileProvider(frontendPath),
        DefaultFileNames = new List<string> { "home.html", "index.html" }
    });
}

app.MapGet("/", () => Results.Redirect("home.html"));

// Start Alert Scheduler in background
var alertScheduler = app.Services.GetRequiredService<AlertSchedulerService>();
_ = Task.Run(async () =>
{
    try
    {
        // Check for alerts every 1 hour
        await alertScheduler.StartAsync(TimeSpan.FromHours(1));
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[AlertScheduler] Fatal error: {ex.Message}");
    }
});

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "LicitIA API"
}));

app.MapGet("/api/opportunities", async (
    OpportunityRepository repository,
    AffinityService affinityService,
    HttpContext httpContext,
    IOptions<JwtOptions> jwtOptions,
    CancellationToken cancellationToken,
    [FromQuery] int? days = null) =>
{
    try
    {
        var opportunities = await repository.GetAllAsync(cancellationToken);
        
        // Filter by date if specified
        if (days.HasValue)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days.Value);
            opportunities = opportunities.Where(o => o.PublishedDate.HasValue && o.PublishedDate >= cutoffDate).ToList();
        }
        
        var userId = GetAuthenticatedUserId(httpContext, jwtOptions.Value);
        var response = new List<OpportunityResponse>();
        foreach (var opportunity in opportunities)
        {
            var analysis = await affinityService.AnalyzeOpportunityAsync(opportunity, userId, cancellationToken);
            response.Add(OpportunityResponse.FromModel(opportunity, analysis));
        }

        return Results.Ok(response.OrderByDescending(item => item.MatchScore));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "No se pudo consultar SQL Server.",
            detail: "Revisa la conexion y ejecuta el script de base de datos.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
    catch (Exception)
    {
        return Results.Problem(
            title: "No se pudo obtener las oportunidades.",
            detail: "Ocurrio un error inesperado al consultar la API.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/api/opportunities/{id:int}", async (
    int id,
    OpportunityRepository repository,
    AffinityService affinityService,
    HttpContext httpContext,
    IOptions<JwtOptions> jwtOptions,
    CancellationToken cancellationToken) =>
{
    try
    {
        var opportunity = await repository.GetByIdAsync(id, cancellationToken);

        if (opportunity is null)
        {
            return Results.NotFound(new { message = "No se encontro la oportunidad solicitada." });
        }

        var userId = GetAuthenticatedUserId(httpContext, jwtOptions.Value);
        var analysis = await affinityService.AnalyzeOpportunityAsync(opportunity, userId, cancellationToken);
        return Results.Ok(OpportunityResponse.FromModel(opportunity, analysis));
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "No se pudo consultar SQL Server.",
            detail: "Revisa la conexion y ejecuta el script de base de datos.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
    catch (Exception)
    {
        return Results.Problem(
            title: "No se pudo obtener el detalle.",
            detail: "Ocurrio un error inesperado al consultar la API.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/api/opportunities/{id:int}/ai-analysis", async (
    int id,
    OpportunityAiAnalysisRepository analysisRepository,
    GeminiAnalysisService geminiService,
    HttpContext httpContext,
    IOptions<JwtOptions> jwtOptions,
    CancellationToken cancellationToken) =>
{
    var userId = GetAuthenticatedUserId(httpContext, jwtOptions.Value);
    if (!userId.HasValue)
    {
        return Results.Unauthorized();
    }

    try
    {
        var cached = await analysisRepository.GetByUserAndOpportunityAsync(userId.Value, id, cancellationToken);
        var usedToday = await analysisRepository.CountCreatedTodayAsync(userId.Value, cancellationToken);
        var dailyLimit = geminiService.DailyLimitPerUser;

        return Results.Ok(new
        {
            configured = geminiService.IsConfigured,
            dailyLimit,
            usedToday,
            remainingToday = Math.Max(0, dailyLimit - usedToday),
            analysis = cached
        });
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "No se pudo consultar el analisis IA.",
            detail: "Ejecuta database/migration_add_opportunity_ai_analysis.sql para crear la tabla requerida.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/opportunities/{id:int}/ai-analysis", async (
    int id,
    OpportunityRepository opportunityRepository,
    OpportunityAiAnalysisRepository analysisRepository,
    AffinityService affinityService,
    GeminiAnalysisService geminiService,
    HttpContext httpContext,
    IOptions<JwtOptions> jwtOptions,
    CancellationToken cancellationToken,
    [FromQuery] bool force = false) =>
{
    var userId = GetAuthenticatedUserId(httpContext, jwtOptions.Value);
    if (!userId.HasValue)
    {
        return Results.Unauthorized();
    }

    if (!geminiService.IsConfigured)
    {
        return Results.BadRequest(new
        {
            message = "Gemini no esta configurado. Agrega Gemini:ApiKey en appsettings.json o como variable de entorno."
        });
    }

    try
    {
        var opportunity = await opportunityRepository.GetByIdAsync(id, cancellationToken);
        if (opportunity is null)
        {
            return Results.NotFound(new { message = "No se encontro la oportunidad solicitada." });
        }

        if (!force)
        {
            var cached = await analysisRepository.GetByUserAndOpportunityAsync(userId.Value, id, cancellationToken);
            if (cached is not null)
            {
                var cachedUsedToday = await analysisRepository.CountCreatedTodayAsync(userId.Value, cancellationToken);
                return Results.Ok(new
                {
                    fromCache = true,
                    dailyLimit = geminiService.DailyLimitPerUser,
                    usedToday = cachedUsedToday,
                    remainingToday = Math.Max(0, geminiService.DailyLimitPerUser - cachedUsedToday),
                    analysis = cached
                });
            }
        }

        var used = await analysisRepository.CountCreatedTodayAsync(userId.Value, cancellationToken);
        if (used >= geminiService.DailyLimitPerUser)
        {
            return Results.Json(new
            {
                message = "Limite diario de IA alcanzado. La recomendacion basica sigue disponible.",
                dailyLimit = geminiService.DailyLimitPerUser,
                usedToday = used,
                remainingToday = 0
            }, statusCode: StatusCodes.Status429TooManyRequests);
        }

        var recommendation = await affinityService.AnalyzeOpportunityAsync(opportunity, userId.Value, cancellationToken);
        var analysis = await geminiService.AnalyzeAsync(userId.Value, opportunity, recommendation, cancellationToken);
        await analysisRepository.UpsertAsync(analysis, cancellationToken);

        var updated = await analysisRepository.GetByUserAndOpportunityAsync(userId.Value, id, cancellationToken) ?? analysis;
        var usedToday = await analysisRepository.CountCreatedTodayAsync(userId.Value, cancellationToken);

        return Results.Ok(new
        {
            fromCache = false,
            dailyLimit = geminiService.DailyLimitPerUser,
            usedToday,
            remainingToday = Math.Max(0, geminiService.DailyLimitPerUser - usedToday),
            analysis = updated
        });
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "No se pudo guardar el analisis IA.",
            detail: "Ejecuta database/migration_add_opportunity_ai_analysis.sql para crear la tabla requerida.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Problem(
            title: "No se pudo completar el analisis con Gemini.",
            detail: ex.Message,
            statusCode: StatusCodes.Status502BadGateway);
    }
});

// Endpoint para obtener categorías únicas
app.MapGet("/api/opportunities/categories", async (OpportunityRepository repository, CancellationToken cancellationToken) =>
{
    try
    {
        var opportunities = await repository.GetAllAsync(cancellationToken);
        var categories = opportunities
            .Select(o => o.Category)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct()
            .OrderBy(c => c)
            .ToList();

        return Results.Ok(new { categories });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "No se pudieron obtener las categorías.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

// Endpoint para obtener modalidades únicas
app.MapGet("/api/opportunities/modalities", async (OpportunityRepository repository, CancellationToken cancellationToken) =>
{
    try
    {
        var opportunities = await repository.GetAllAsync(cancellationToken);
        var modalities = opportunities
            .Select(o => o.Modality)
            .Where(m => !string.IsNullOrWhiteSpace(m))
            .Distinct()
            .OrderBy(m => m)
            .ToList();

        return Results.Ok(new { modalities });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "No se pudieron obtener las modalidades.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

// Endpoint para obtener entidades únicas
app.MapGet("/api/opportunities/entities", async (OpportunityRepository repository, CancellationToken cancellationToken) =>
{
    try
    {
        var opportunities = await repository.GetAllAsync(cancellationToken);
        var entities = opportunities
            .Select(o => o.EntityName)
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Distinct()
            .OrderBy(e => e)
            .ToList();

        return Results.Ok(new { entities });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "No se pudieron obtener las entidades.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

// Metrics endpoints for dashboard
app.MapGet("/api/metrics/summary", async (
    OpportunityRepository repository,
    CancellationToken cancellationToken,
    [FromQuery] int? days = null) =>
{
    try
    {
        var opportunities = await repository.GetAllAsync(cancellationToken);
        
        // Filter by date if specified
        if (days.HasValue)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days.Value);
            opportunities = opportunities.Where(o => o.PublishedDate.HasValue && o.PublishedDate >= cutoffDate).ToList();
        }
        
        var summary = new
        {
            totalOpportunities = opportunities.Count,
            priorityOpportunities = opportunities.Count(o => o.IsPriority),
            totalAmount = opportunities.Sum(o => o.EstimatedAmount),
            averageScore = opportunities.Any() ? opportunities.Average(o => o.MatchScore) : 0,
            categories = opportunities.Select(o => o.Category).Distinct().Count(),
            entities = opportunities.Select(o => o.EntityName).Distinct().Count()
        };
        
        return Results.Ok(summary);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "No se pudieron obtener las métricas.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/api/metrics/by-category", async (
    OpportunityRepository repository,
    CancellationToken cancellationToken,
    [FromQuery] int? days = null) =>
{
    try
    {
        var opportunities = await repository.GetAllAsync(cancellationToken);
        
        // Filter by date if specified
        if (days.HasValue)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days.Value);
            opportunities = opportunities.Where(o => o.PublishedDate.HasValue && o.PublishedDate >= cutoffDate).ToList();
        }
        
        var byCategory = opportunities
            .Where(o => !string.IsNullOrWhiteSpace(o.Category))
            .GroupBy(o => o.Category)
            .Select(g => new
            {
                category = g.Key,
                count = g.Count(),
                totalAmount = g.Sum(o => o.EstimatedAmount),
                averageScore = g.Average(o => o.MatchScore)
            })
            .OrderByDescending(g => g.count)
            .ToList();
        
        return Results.Ok(byCategory);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "No se pudieron obtener las métricas por categoría.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/api/metrics/by-entity", async (
    OpportunityRepository repository,
    CancellationToken cancellationToken,
    [FromQuery] int? days = null) =>
{
    try
    {
        var opportunities = await repository.GetAllAsync(cancellationToken);
        
        // Filter by date if specified
        if (days.HasValue)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days.Value);
            opportunities = opportunities.Where(o => o.PublishedDate.HasValue && o.PublishedDate >= cutoffDate).ToList();
        }
        
        var byEntity = opportunities
            .Where(o => !string.IsNullOrWhiteSpace(o.EntityName))
            .GroupBy(o => o.EntityName)
            .Select(g => new
            {
                entity = g.Key,
                count = g.Count(),
                totalAmount = g.Sum(o => o.EstimatedAmount)
            })
            .OrderByDescending(g => g.count)
            .Take(10)
            .ToList();
        
        return Results.Ok(byEntity);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "No se pudieron obtener las métricas por entidad.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/api/metrics/trends", async (
    OpportunityRepository repository,
    CancellationToken cancellationToken,
    [FromQuery] int? days = null) =>
{
    try
    {
        var opportunities = await repository.GetAllAsync(cancellationToken);
        
        // Filter by date if specified
        if (days.HasValue)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days.Value);
            opportunities = opportunities.Where(o => o.PublishedDate.HasValue && o.PublishedDate >= cutoffDate).ToList();
        }
        
        var trends = opportunities
            .Where(o => o.PublishedDate.HasValue)
            .GroupBy(o => new { Year = o.PublishedDate.Value.Year, Month = o.PublishedDate.Value.Month })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month)
            .Select(g => new
            {
                year = g.Key.Year,
                month = g.Key.Month,
                count = g.Count(),
                totalAmount = g.Sum(o => o.EstimatedAmount)
            })
            .ToList();
        
        return Results.Ok(trends);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "No se pudieron obtener las tendencias.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapGet("/api/metrics/entity-analysis", async (
    OpportunityRepository repository,
    CancellationToken cancellationToken,
    [FromQuery] int? days = null) =>
{
    try
    {
        var opportunities = await repository.GetAllAsync(cancellationToken);
        
        // Filter by date if specified
        if (days.HasValue)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-days.Value);
            opportunities = opportunities.Where(o => o.PublishedDate.HasValue && o.PublishedDate >= cutoffDate).ToList();
        }
        
        var entityAnalysis = opportunities
            .Where(o => !string.IsNullOrWhiteSpace(o.EntityName) && !string.IsNullOrWhiteSpace(o.Category))
            .GroupBy(o => o.EntityName)
            .Select(g => new
            {
                entity = g.Key,
                totalOpportunities = g.Count(),
                totalAmount = g.Sum(o => o.EstimatedAmount),
                categories = g.GroupBy(o => o.Category)
                    .Select(cg => new
                    {
                        category = cg.Key,
                        count = cg.Count(),
                        amount = cg.Sum(o => o.EstimatedAmount)
                    })
                    .OrderByDescending(cg => cg.count)
                    .ToList()
            })
            .OrderByDescending(g => g.totalOpportunities)
            .Take(20)
            .ToList();
        
        return Results.Ok(entityAnalysis);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "No se pudo obtener el análisis de entidades.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/auth/register", async (
    RegisterRequest request,
    AuthRepository repository,
    IOptions<JwtOptions> jwtOptions,
    EmailService emailService,
    CancellationToken cancellationToken) =>
{
    var validationErrors = ValidateRegisterRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    try
    {
        var existingUser = await repository.GetByEmailAsync(request.Email, cancellationToken);
        if (existingUser is not null)
        {
            return Results.BadRequest(new { message = "Ya existe un usuario registrado con ese correo." });
        }

        var createdUser = await repository.CreateUserAsync(request, cancellationToken);
        var token = GenerateJwtToken(createdUser, jwtOptions.Value);

        // Send welcome email
        try
        {
            await emailService.SendWelcomeEmailAsync(createdUser.Email, createdUser.FullName, cancellationToken);
            Console.WriteLine($"[Register] Welcome email sent to: {createdUser.Email}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Register] Email failed for {createdUser.Email}: {ex.Message}");
        }

        return Results.Ok(new
        {
            message = "Registro completado correctamente. Revisa tu correo para el mensaje de bienvenida.",
            redirectUrl = "index.html",
            token,
            user = new
            {
                fullName = createdUser.FullName,
                email = createdUser.Email,
                role = createdUser.RoleName
            }
        });
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "No se pudo registrar el usuario.",
            detail: "Revisa la conexion a SQL Server y confirma que la base existe.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/auth/login", async (
    LoginRequest request,
    AuthRepository repository,
    PasswordService passwordService,
    IOptions<JwtOptions> jwtOptions,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var validationErrors = ValidateLoginRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    try
    {
        // Check for failed login attempts (3 attempts in 10 seconds = block)
        const int maxAttempts = 3;
        var lockoutWindow = TimeSpan.FromSeconds(10);
        var failedAttempts = await repository.GetFailedLoginAttemptsAsync(request.Email, lockoutWindow, cancellationToken);

        if (failedAttempts >= maxAttempts)
        {
            return Results.BadRequest(new { 
                message = "Cuenta bloqueada temporalmente. Demasiados intentos fallidos. Intenta nuevamente en 10 segundos.",
                isLocked = true
            });
        }

        var user = await repository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !passwordService.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            // Record failed login attempt
            var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString();
            await repository.RecordLoginAttemptAsync(request.Email, false, ipAddress, cancellationToken);

            var remainingAttempts = maxAttempts - failedAttempts - 1;
            return Results.BadRequest(new { 
                message = $"Correo o contrasena incorrectos. Intentos restantes: {remainingAttempts}",
                remainingAttempts
            });
        }

        // Record successful login attempt
        var successIpAddress = httpContext.Connection.RemoteIpAddress?.ToString();
        await repository.RecordLoginAttemptAsync(request.Email, true, successIpAddress, cancellationToken);

        var token = GenerateJwtToken(user, jwtOptions.Value, request.RememberMe);

        return Results.Ok(new
        {
            message = $"Bienvenido, {user.FullName}.",
            redirectUrl = "index.html",
            token,
            user = new
            {
                fullName = user.FullName,
                email = user.Email,
                role = user.RoleName
            }
        });
    }
    catch (SqlException)
    {
        return Results.Problem(
            title: "No se pudo procesar el login.",
            detail: "Revisa la conexion a SQL Server y confirma que la base existe.",
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

// OECE data download endpoint
app.MapGet("/api/oece/download", async (
    OeceApiService oeceApiService,
    OpportunityRepository repository,
    [FromQuery] DateTime? fromDate = null,
    CancellationToken cancellationToken = default) =>
{
    try
    {
        Console.WriteLine($"[OeceApi] Iniciando descarga de datos de OECE API REST...");
        if (fromDate.HasValue)
        {
            Console.WriteLine($"[OeceApi] Filtrando por fecha de publicación desde: {fromDate:yyyy-MM-dd}");
        }

        var oeceOpportunities = await oeceApiService.DownloadOeceDataAsync(maxPages: 50, fromDate, cancellationToken, null);

        if (oeceOpportunities.Count == 0)
        {
            return Results.Ok(new { message = "No se encontraron oportunidades en OECE.", count = 0 });
        }

        // Convertir oece opportunities a Opportunities y guardar en BD
        int savedCount = 0;
        int skippedCount = 0;
        foreach (var oece in oeceOpportunities)
        {
            // Verificar si ya existe una oportunidad con el mismo ProcessCode
            var existing = await repository.GetByProcessCodeAsync(oece.ProcessCode, cancellationToken);
            if (existing != null)
            {
                skippedCount++;
                continue;
            }

            var opportunity = new Opportunity
            {
                ProcessCode = oece.ProcessCode,
                Title = oece.Title,
                EntityName = oece.EntityName,
                EstimatedAmount = oece.EstimatedAmount,
                ClosingDate = oece.ClosingDate ?? DateTime.Now.AddDays(30),
                Category = oece.Category,
                Modality = oece.Modality,
                MatchScore = 50, // Valor por defecto hasta que se calcule con IA
                Summary = oece.Description,
                Location = "Lima", // Valor por defecto
                IsPriority = false,
                PublishedDate = oece.PublishedDate
            };

            await repository.InsertOpportunityAsync(opportunity, cancellationToken);
            savedCount++;
        }

        Console.WriteLine($"[OeceApi] Se guardaron {savedCount} oportunidades. Se omitieron {skippedCount} duplicados.");

        return Results.Ok(new { message = $"Datos de OECE descargados. Se guardaron {savedCount} oportunidades. Se omitieron {skippedCount} duplicados.", count = savedCount, skipped = skippedCount });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[OeceApi] Error: {ex.Message}");
        return Results.Problem(
            title: "No se pudo descargar datos de OECE.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

// OECE refresh endpoint - clear DB and download only the most recent opportunities
app.MapPost("/api/oece/refresh", async (
    OeceApiService oeceApiService,
    OpportunityRepository repository,
    CancellationToken cancellationToken,
    [FromQuery] int maxResults = 100) =>
{
    try
    {
        Console.WriteLine($"[OeceApi] Iniciando refresh de OECE - limpiando BD y descargando {maxResults} oportunidades más recientes...");

        // Limpiar todas las oportunidades existentes
        await repository.ClearAllOpportunitiesAsync(cancellationToken);
        Console.WriteLine($"[OeceApi] Base de datos limpiada.");

        // Descargar solo las N oportunidades más recientes
        var oeceOpportunities = await oeceApiService.DownloadOeceDataAsync(maxPages: 10, null, cancellationToken, maxResults);

        if (oeceOpportunities.Count == 0)
        {
            return Results.Ok(new { message = "No se encontraron oportunidades en OECE.", count = 0 });
        }

        // Guardar las oportunidades
        int savedCount = 0;
        for (int i = 0; i < oeceOpportunities.Count; i++)
        {
            var oece = oeceOpportunities[i];
            var opportunity = new Opportunity
            {
                ProcessCode = oece.ProcessCode,
                Title = oece.Title,
                EntityName = oece.EntityName,
                EstimatedAmount = oece.EstimatedAmount,
                ClosingDate = oece.ClosingDate ?? DateTime.Now.AddDays(30),
                Category = oece.Category,
                Modality = oece.Modality,
                MatchScore = 50,
                Summary = oece.Description,
                Location = "Lima",
                IsPriority = false,
                PublishedDate = oece.PublishedDate,
                SeaceIndex = i + 1 // Índice original de SEACE/OECE
            };

            await repository.InsertOpportunityAsync(opportunity, cancellationToken);
            savedCount++;
        }

        Console.WriteLine($"[OeceApi] Refresh completado. Se guardaron {savedCount} oportunidades más recientes.");

        return Results.Ok(new { message = $"Refresh completado. Se guardaron {savedCount} oportunidades más recientes.", count = savedCount });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[OeceApi] Error en refresh: {ex.Message}");
        return Results.Problem(
            title: "No se pudo completar el refresh.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

// SEACE refresh endpoint - clear DB and download from SEACE
app.MapPost("/api/seace/refresh", async (
    SeaceScraperService seaceScraperService,
    AffinityService affinityService,
    OpportunityRepository repository,
    CompanyProfileRepository profileRepository,
    IOptions<JwtOptions> jwtOptions,
    HttpContext httpContext,
    CancellationToken cancellationToken,
    [FromQuery] int maxResults = 30) =>
{
    try
    {
        var userId = GetAuthenticatedUserId(httpContext, jwtOptions.Value);
        if (userId == null)
        {
            return Results.Unauthorized();
        }

        Console.WriteLine($"[SeaceScraper] Iniciando refresh de SEACE para usuario {userId} - limpiando BD y descargando {maxResults} oportunidades...");

        var profile = await profileRepository.GetByUserIdAsync(userId.Value, cancellationToken);
        var objectDescription = profile?.SeaceObjectDescription;
        var callYear = profile?.SeaceCallYear ?? DateTime.UtcNow.Year;
        var contractObject = profile?.SeaceContractObject;
        var entityAcronym = profile?.SeaceEntityAcronym;
        var department = profile?.SeaceDepartment;
        if (!string.IsNullOrWhiteSpace(objectDescription))
        {
            Console.WriteLine($"[SeaceScraper] Usando filtro Descripcion del Objeto: {objectDescription}");
        }
        if (!string.IsNullOrWhiteSpace(contractObject))
        {
            Console.WriteLine($"[SeaceScraper] Usando filtro Objeto de Contratacion: {contractObject}");
        }
        if (!string.IsNullOrWhiteSpace(entityAcronym))
        {
            Console.WriteLine($"[SeaceScraper] Usando filtro avanzado Sigla/Nomenclatura: {entityAcronym}");
        }
        if (!string.IsNullOrWhiteSpace(department))
        {
            Console.WriteLine($"[SeaceScraper] Usando filtro avanzado Departamento: {department}");
        }
        Console.WriteLine($"[SeaceScraper] Usando filtro Año de Convocatoria: {callYear}");

        // Limpiar todas las oportunidades existentes
        await repository.ClearAllOpportunitiesAsync(cancellationToken);
        Console.WriteLine($"[SeaceScraper] Base de datos limpiada.");

        // Descargar datos de SEACE
        var seaceOpportunities = await seaceScraperService.ScrapeOpportunitiesAsync(maxResults, cancellationToken, objectDescription, callYear, contractObject, entityAcronym, department);

        if (seaceOpportunities.Count == 0)
        {
            return Results.Ok(new { message = "No se encontraron oportunidades en SEACE.", count = 0 });
        }

        // Calcular afinidad antes de guardar usando perfil del usuario
        var rankedOpportunities = await affinityService.RankOpportunitiesAsync(seaceOpportunities, userId.Value, cancellationToken);

        // Guardar las oportunidades con scores de afinidad
        int savedCount = 0;
        int skippedCount = 0;
        for (int i = 0; i < rankedOpportunities.Count; i++)
        {
            var scraped = rankedOpportunities[i];
            try
            {
                var opportunity = new Opportunity
                {
                    ProcessCode = scraped.ProcessCode,
                    Title = scraped.Title,
                    EntityName = scraped.EntityName,
                    EstimatedAmount = scraped.EstimatedAmount,
                    ClosingDate = scraped.ClosingDate,
                    Category = scraped.Category,
                    Modality = scraped.Modality,
                    MatchScore = scraped.MatchScore,
                    MatchedKeywordsCount = scraped.MatchedKeywordsCount,
                    Summary = scraped.Description,
                    Location = scraped.Location,
                    IsPriority = scraped.MatchScore >= 85,
                    PublishedDate = scraped.PublishedDate,
                    SeaceIndex = i + 1,
                    SelectionType = scraped.SelectionType,
                    ConvocationNumber = scraped.ConvocationNumber,
                    ApplicableRegulation = scraped.ApplicableRegulation,
                    SeaceVersion = scraped.SeaceVersion,
                    EntityLegalAddress = scraped.EntityLegalAddress,
                    EntityWebsite = scraped.EntityWebsite,
                    EntityPhone = scraped.EntityPhone,
                    ContractObject = scraped.ContractObject,
                    ParticipationCost = scraped.ParticipationCost,
                    BasesReproductionCost = scraped.BasesReproductionCost,
                    SeaceDetailJson = scraped.SeaceDetailJson,
                    SeaceScheduleJson = scraped.SeaceScheduleJson
                };
                await repository.InsertOpportunityAsync(opportunity, cancellationToken);
                savedCount++;
                Console.WriteLine($"[SeaceScraper] Guardada: {opportunity.ProcessCode} (Score: {opportunity.MatchScore}%)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SeaceScraper] Error guardando oportunidad {scraped.ProcessCode}: {ex.Message}");
                skippedCount++;
            }
        }

        Console.WriteLine($"[SeaceScraper] Refresh completado. Se guardaron {savedCount} oportunidades. Duplicados omitidos: {skippedCount}.");

        return Results.Ok(new { message = $"Refresh completado. Se guardaron {savedCount} oportunidades de SEACE con analisis de afinidad.", count = savedCount, skipped = skippedCount });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[SeaceScraper] Error en refresh: {ex.Message}");
        return Results.Problem(
            title: "No se pudo completar el refresh.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

// Company Profile endpoints
app.MapGet("/api/profile", async (
    CompanyProfileRepository repository,
    IOptions<JwtOptions> jwtOptions,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var userId = GetAuthenticatedUserId(httpContext, jwtOptions.Value);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        Console.WriteLine($"[Profile] Loading profile for user {userId}...");
        var profile = await repository.GetByUserIdAsync(userId.Value, cancellationToken);
        if (profile == null)
        {
            Console.WriteLine($"[Profile] No profile found for user {userId}");
            return Results.NotFound(new { message = "No se encontró perfil de empresa." });
        }
        Console.WriteLine($"[Profile] Profile loaded: {profile.CompanyName}, Keywords: {profile.PreferredKeywords.Count}");
        return Results.Ok(profile);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Profile] Error: {ex.Message}\n{ex.StackTrace}");
        return Results.Problem(
            title: "Error al obtener perfil",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}).RequireAuthorization();

app.MapPost("/api/profile", async (
    CompanyProfileRepository repository,
    IOptions<JwtOptions> jwtOptions,
    HttpContext httpContext,
    LicitIA.Api.Models.CompanyProfile requestProfile,
    CancellationToken cancellationToken) =>
{
    try
    {
        var userId = GetAuthenticatedUserId(httpContext, jwtOptions.Value);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var existingProfile = await repository.GetByUserIdAsync(userId.Value, cancellationToken);
        if (existingProfile is not null)
        {
            var updatedProfile = requestProfile with
            {
                ProfileId = existingProfile.ProfileId,
                UserId = userId.Value
            };

            await repository.UpdateProfileAsync(updatedProfile, userId.Value, cancellationToken);
            return Results.Ok(updatedProfile);
        }

        var newProfile = requestProfile with { UserId = userId.Value };
        var profileId = await repository.InsertProfileAsync(newProfile, cancellationToken);
        var createdProfile = newProfile with { ProfileId = profileId };
        return Results.Created($"/api/profile/{profileId}", createdProfile);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Error al crear perfil",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}).RequireAuthorization();

app.MapPut("/api/profile/{id}", async (
    int id,
    CompanyProfileRepository repository,
    IOptions<JwtOptions> jwtOptions,
    HttpContext httpContext,
    LicitIA.Api.Models.CompanyProfile requestProfile,
    CancellationToken cancellationToken) =>
{
    try
    {
        var userId = GetAuthenticatedUserId(httpContext, jwtOptions.Value);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var updatedProfile = requestProfile with
        {
            ProfileId = id,
            UserId = userId.Value
        };

        var affectedRows = await repository.UpdateProfileAsync(updatedProfile, userId.Value, cancellationToken);
        if (affectedRows == 0)
        {
            return Results.NotFound(new { message = "No se encontro perfil de empresa para este usuario." });
        }

        return Results.Ok(updatedProfile);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Error al actualizar perfil",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}).RequireAuthorization();

// Alert endpoints
app.MapGet("/api/alerts/summary", async (
    AlertService alertService,
    IOptions<JwtOptions> jwtOptions,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var userId = GetAuthenticatedUserId(httpContext, jwtOptions.Value);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var summary = await alertService.GetAlertSummaryAsync(userId.Value, cancellationToken);
        return Results.Ok(summary);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Error al obtener resumen de alertas",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}).RequireAuthorization();

app.MapGet("/api/alerts/rules", async (
    AlertService alertService,
    IOptions<JwtOptions> jwtOptions,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var userId = GetAuthenticatedUserId(httpContext, jwtOptions.Value);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var rules = await alertService.GetAlertRulesByUserIdAsync(userId.Value, cancellationToken);
        return Results.Ok(rules);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Error al obtener reglas de alerta",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}).RequireAuthorization();

app.MapPost("/api/alerts/rules", async (
    AlertService alertService,
    IOptions<JwtOptions> jwtOptions,
    HttpContext httpContext,
    LicitIA.Api.Models.AlertRule requestRule,
    CancellationToken cancellationToken) =>
{
    try
    {
        var userId = GetAuthenticatedUserId(httpContext, jwtOptions.Value);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var newRule = requestRule with
        {
            UserId = userId.Value,
            CreatedAtUtc = DateTime.UtcNow,
            TriggerCount = 0,
            LastTriggeredAtUtc = null
        };

        var ruleId = await alertService.CreateAlertRuleAsync(newRule, cancellationToken);
        var createdRule = newRule with { RuleId = ruleId };
        return Results.Created($"/api/alerts/rules/{ruleId}", createdRule);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Error al crear regla de alerta",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}).RequireAuthorization();

app.MapPut("/api/alerts/rules/{id}", async (
    int id,
    AlertService alertService,
    IOptions<JwtOptions> jwtOptions,
    HttpContext httpContext,
    LicitIA.Api.Models.AlertRule requestRule,
    CancellationToken cancellationToken) =>
{
    try
    {
        var userId = GetAuthenticatedUserId(httpContext, jwtOptions.Value);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var existingRule = await alertService.GetAlertRuleByIdAsync(id, cancellationToken);
        if (existingRule == null)
        {
            return Results.NotFound(new { message = "No se encontró la regla de alerta." });
        }

        if (existingRule.UserId != userId.Value)
        {
            return Results.Forbid();
        }

        var updatedRule = requestRule with
        {
            RuleId = id,
            UserId = userId.Value
        };

        await alertService.UpdateAlertRuleAsync(updatedRule, cancellationToken);
        return Results.Ok(updatedRule);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Error al actualizar regla de alerta",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}).RequireAuthorization();

app.MapDelete("/api/alerts/rules/{id}", async (
    int id,
    AlertService alertService,
    IOptions<JwtOptions> jwtOptions,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var userId = GetAuthenticatedUserId(httpContext, jwtOptions.Value);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var existingRule = await alertService.GetAlertRuleByIdAsync(id, cancellationToken);
        if (existingRule == null)
        {
            return Results.NotFound(new { message = "No se encontró la regla de alerta." });
        }

        if (existingRule.UserId != userId.Value)
        {
            return Results.Forbid();
        }

        await alertService.DeleteAlertRuleAsync(id, cancellationToken);
        return Results.Ok(new { message = "Regla de alerta eliminada exitosamente." });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Error al eliminar regla de alerta",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}).RequireAuthorization();

app.MapPost("/api/alerts/check-now", async (
    bool? force,
    AlertSchedulerService alertScheduler,
    IOptions<JwtOptions> jwtOptions,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var userId = GetAuthenticatedUserId(httpContext, jwtOptions.Value);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var forceMode = force ?? false;
        var result = await alertScheduler.RunOnceForUserAsync(userId.Value, forceMode, cancellationToken);
        return Results.Ok(new
        {
            message = forceMode
                ? "Revision forzada ejecutada."
                : "Revision de alertas ejecutada.",
            result.RulesProcessed,
            result.OpportunitiesMatched,
            result.SummariesCreated,
            force = forceMode
        });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Error al revisar alertas",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}).RequireAuthorization();

app.MapPost("/api/alerts/send-test", async (
    AlertService alertService,
    AuthRepository authRepository,
    IOptions<JwtOptions> jwtOptions,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var userId = GetAuthenticatedUserId(httpContext, jwtOptions.Value);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var user = await authRepository.GetByIdAsync(userId.Value, cancellationToken);
        if (user is null)
        {
            return Results.NotFound(new { message = "No se encontro el usuario autenticado." });
        }

        await alertService.SendTestEmailAsync(user.Email, "Prueba de Alerta", cancellationToken);
        return Results.Ok(new { message = $"Correo de prueba enviado a {user.Email}." });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Error al enviar correo de prueba",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}).RequireAuthorization();

// Panel Notifications endpoints
app.MapGet("/api/notifications", async (
    NotificationRepository repository,
    IOptions<JwtOptions> jwtOptions,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var userId = GetAuthenticatedUserId(httpContext, jwtOptions.Value);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var notifications = await repository.GetByUserIdAsync(userId.Value, cancellationToken);
        return Results.Ok(notifications);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Error al obtener notificaciones",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}).RequireAuthorization();

app.MapGet("/api/notifications/unread", async (
    NotificationRepository repository,
    IOptions<JwtOptions> jwtOptions,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var userId = GetAuthenticatedUserId(httpContext, jwtOptions.Value);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var notifications = await repository.GetUnreadByUserIdAsync(userId.Value, cancellationToken);
        return Results.Ok(notifications);
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Error al obtener notificaciones no leídas",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}).RequireAuthorization();

app.MapGet("/api/notifications/unread-count", async (
    NotificationRepository repository,
    IOptions<JwtOptions> jwtOptions,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var userId = GetAuthenticatedUserId(httpContext, jwtOptions.Value);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var count = await repository.GetUnreadCountByUserIdAsync(userId.Value, cancellationToken);
        return Results.Ok(new { count });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Error al obtener contador de notificaciones",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}).RequireAuthorization();

app.MapPost("/api/notifications/{id}/read", async (
    int id,
    NotificationRepository repository,
    IOptions<JwtOptions> jwtOptions,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var userId = GetAuthenticatedUserId(httpContext, jwtOptions.Value);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        await repository.MarkAsReadAsync(id, cancellationToken);
        return Results.Ok(new { message = "Notificación marcada como leída." });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Error al marcar notificación como leída",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}).RequireAuthorization();

app.MapPost("/api/notifications/mark-all-read", async (
    NotificationRepository repository,
    IOptions<JwtOptions> jwtOptions,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    try
    {
        var userId = GetAuthenticatedUserId(httpContext, jwtOptions.Value);
        if (userId is null)
        {
            return Results.Unauthorized();
        }

        var count = await repository.MarkAllAsReadByUserIdAsync(userId.Value, cancellationToken);
        return Results.Ok(new { message = $"{count} notificaciones marcadas como leídas." });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "Error al marcar todas las notificaciones como leídas",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
}).RequireAuthorization();

// CSV Import endpoint - import data from manually downloaded SEACE CSV
app.MapPost("/api/opportunities/import-csv", async (
    CsvImportService csvImportService,
    HttpRequest request,
    CancellationToken cancellationToken) =>
{
    try
    {
        using var reader = new StreamReader(request.Body);
        var csvContent = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(csvContent))
        {
            return Results.BadRequest(new { message = "No se proporcionó contenido CSV" });
        }

        Console.WriteLine($"[CsvImport] Importando CSV de SEACE ({csvContent.Length} caracteres)...");

        var (imported, skipped, errors) = await csvImportService.ImportFromCsvAsync(csvContent, cancellationToken);

        Console.WriteLine($"[CsvImport] Importación completada. Importados: {imported}, Omitidos: {skipped}, Errores: {errors.Count}");

        return Results.Ok(new
        {
            message = $"Importación completada. {imported} registros importados, {skipped} omitidos.",
            imported,
            skipped,
            errors = errors.Count > 0 ? errors : null
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[CsvImport] Error: {ex.Message}");
        return Results.Problem(
            title: "Error al importar CSV",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

// OECE incremental update endpoint - download only new data since last update
app.MapGet("/api/oece/update-incremental", async (
    OeceApiService oeceApiService,
    OpportunityRepository repository,
    CancellationToken cancellationToken) =>
{
    try
    {
        Console.WriteLine($"[OeceApi] Iniciando actualización incremental de OECE...");

        // Obtener la fecha de publicación más reciente en la base de datos
        var latestDate = await repository.GetLatestPublishedDateAsync(cancellationToken);

        if (latestDate == null)
        {
            Console.WriteLine($"[OeceApi] No hay datos en la base de datos. Descargando todos los datos...");
            // Si no hay datos, descargar todo (sin filtro de fecha)
            var oeceOpportunities = await oeceApiService.DownloadOeceDataAsync(maxPages: 50, null, cancellationToken, null);

            if (oeceOpportunities.Count == 0)
            {
                return Results.Ok(new { message = "No se encontraron oportunidades en OECE.", count = 0 });
            }

            int savedCount = 0;
            foreach (var oece in oeceOpportunities)
            {
                var opportunity = new Opportunity
                {
                    ProcessCode = oece.ProcessCode,
                    Title = oece.Title,
                    EntityName = oece.EntityName,
                    EstimatedAmount = oece.EstimatedAmount,
                    ClosingDate = oece.ClosingDate ?? DateTime.Now.AddDays(30),
                    Category = oece.Category,
                    Modality = oece.Modality,
                    MatchScore = 50,
                    Summary = oece.Description,
                    Location = "Lima",
                    IsPriority = false,
                    PublishedDate = oece.PublishedDate
                };

                await repository.InsertOpportunityAsync(opportunity, cancellationToken);
                savedCount++;
            }

            Console.WriteLine($"[OeceApi] Se guardaron {savedCount} oportunidades (descarga inicial).");
            return Results.Ok(new { message = $"Descarga inicial completada. Se guardaron {savedCount} oportunidades.", count = savedCount, isInitial = true });
        }

        Console.WriteLine($"[OeceApi] Fecha más reciente en BD: {latestDate:yyyy-MM-dd}");
        Console.WriteLine($"[OeceApi] Descargando solo datos posteriores a esta fecha...");

        // Descargar solo datos posteriores a la fecha más reciente
        var fromDate = latestDate.Value.AddDays(-1); // Incluir un día antes para no perder datos del mismo día
        var newOpportunities = await oeceApiService.DownloadOeceDataAsync(maxPages: 50, fromDate, cancellationToken, null);

        if (newOpportunities.Count == 0)
        {
            Console.WriteLine($"[OeceApi] No hay datos nuevos desde la última actualización.");
            return Results.Ok(new { message = "No hay datos nuevos desde la última actualización.", count = 0, isInitial = false });
        }

        // Guardar solo las oportunidades nuevas
        int newSavedCount = 0;
        int skippedCount = 0;
        foreach (var oece in newOpportunities)
        {
            var existing = await repository.GetByProcessCodeAsync(oece.ProcessCode, cancellationToken);
            if (existing != null)
            {
                skippedCount++;
                continue;
            }

            var opportunity = new Opportunity
            {
                ProcessCode = oece.ProcessCode,
                Title = oece.Title,
                EntityName = oece.EntityName,
                EstimatedAmount = oece.EstimatedAmount,
                ClosingDate = oece.ClosingDate ?? DateTime.Now.AddDays(30),
                Category = oece.Category,
                Modality = oece.Modality,
                MatchScore = 50,
                Summary = oece.Description,
                Location = "Lima",
                IsPriority = false,
                PublishedDate = oece.PublishedDate
            };

            await repository.InsertOpportunityAsync(opportunity, cancellationToken);
            newSavedCount++;
        }

        Console.WriteLine($"[OeceApi] Actualización incremental completada. Nuevas oportunidades: {newSavedCount}. Duplicados omitidos: {skippedCount}.");

        return Results.Ok(new { message = $"Actualización incremental completada. Se guardaron {newSavedCount} nuevas oportunidades. Se omitieron {skippedCount} duplicados.", count = newSavedCount, skipped = skippedCount, isInitial = false });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[OeceApi] Error en actualización incremental: {ex.Message}");
        return Results.Problem(
            title: "No se pudo completar la actualización incremental.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

// Forgot password endpoint
app.MapPost("/api/auth/forgot-password", async (
    ForgotPasswordRequest request,
    AuthRepository repository,
    EmailService emailService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Email))
    {
        return Results.BadRequest(new { message = "El correo es requerido." });
    }

    try
    {
        var user = await repository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null)
        {
            // Don't reveal if email exists, but return success message
            return Results.Ok(new { message = "Si el correo existe en nuestro sistema, recibirás un enlace de recuperación." });
        }

        // Generate reset token (6-digit code)
        var resetToken = new Random().Next(100000, 999999).ToString();

        // Save token to database
        await repository.SavePasswordResetTokenAsync(user.UserId, resetToken, cancellationToken);

        // Send email with reset token
        try
        {
            await emailService.SendPasswordResetEmailAsync(user.Email, resetToken, cancellationToken);
            Console.WriteLine($"[ForgotPassword] Reset email sent to: {user.Email}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ForgotPassword] Email failed for {user.Email}: {ex.Message}");
        }

        return Results.Ok(new { message = "Si el correo existe en nuestro sistema, recibirás un enlace de recuperación." });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ForgotPassword] Error: {ex.Message}");
        return Results.Problem(
            title: "No se pudo procesar la solicitud.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

// Reset password endpoint
app.MapPost("/api/auth/reset-password", async (
    ResetPasswordRequest request,
    AuthRepository repository,
    PasswordService passwordService,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
    {
        return Results.BadRequest(new { message = "El token y la nueva contraseña son requeridos." });
    }

    if (request.NewPassword.Length < 8)
    {
        return Results.BadRequest(new { message = "La contraseña debe tener al menos 8 caracteres." });
    }

    try
    {
        // Validate token and get user
        var user = await repository.GetUserByResetTokenAsync(request.Token, cancellationToken);
        if (user is null)
        {
            return Results.BadRequest(new { message = "Token inválido o expirado." });
        }

        // Hash new password
        var (passwordHash, passwordSalt) = passwordService.HashPassword(request.NewPassword);

        // Update password
        await repository.UpdatePasswordAsync(user.UserId, passwordHash, passwordSalt, cancellationToken);

        // Clear reset token
        await repository.ClearPasswordResetTokenAsync(user.UserId, cancellationToken);

        return Results.Ok(new { message = "Contraseña restablecida correctamente." });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ResetPassword] Error: {ex.Message}");
        return Results.Problem(
            title: "No se pudo restablecer la contraseña.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

// Complete profile endpoint for new Google users
app.MapPost("/api/auth/complete-profile", async (
    CompleteProfileRequest request,
    IOptions<JwtOptions> jwtOptions,
    AuthRepository repository,
    CancellationToken cancellationToken) =>
{
    try
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(request.CompanyName))
        {
            return Results.BadRequest(new { message = "El nombre de empresa es requerido." });
        }

        if (string.IsNullOrWhiteSpace(request.Role))
        {
            return Results.BadRequest(new { message = "El rol es requerido." });
        }

        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.BadRequest(new { message = "Token y email son requeridos." });
        }

        // Validate JWT token manually
        var isValidToken = ValidateJwtToken(request.Token, request.Email, jwtOptions.Value);
        if (!isValidToken)
        {
            return Results.Unauthorized();
        }

        // Update user profile
        await repository.UpdateUserProfileAsync(request.Email, request.CompanyName, request.Role, request.Phone, cancellationToken);

        return Results.Ok(new { message = "Perfil actualizado correctamente." });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[CompleteProfile] Error: {ex.Message}");
        return Results.Problem(
            title: "No se pudo actualizar el perfil.",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

// Google OAuth redirect flow - Step 1: Redirect to Google
app.MapGet("/api/auth/google/login", (IOptions<GoogleAuthOptions> options) =>
{
    var clientId = options.Value.ClientId;
    var redirectUri = options.Value.RedirectUri;

    if (string.IsNullOrWhiteSpace(clientId) || clientId.Contains("YOUR_GOOGLE"))
    {
        return Results.BadRequest(new { message = "Google Client ID no configurado." });
    }

    var state = Convert.ToBase64String(Guid.NewGuid().ToByteArray())[..16];

    var authUrl = $"https://accounts.google.com/o/oauth2/v2/auth" +
        $"?client_id={Uri.EscapeDataString(clientId)}" +
        $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
        $"&response_type=code" +
        $"&scope={Uri.EscapeDataString("openid email profile")}" +
        $"&state={Uri.EscapeDataString(state)}" +
        $"&access_type=offline" +
        $"&prompt=consent";

    return Results.Redirect(authUrl);
});

// Google OAuth redirect flow - Step 2: Handle callback from Google
app.MapGet("/api/auth/google/callback", async (
    [Microsoft.AspNetCore.Mvc.FromQuery] string? code,
    [Microsoft.AspNetCore.Mvc.FromQuery] string? state,
    [Microsoft.AspNetCore.Mvc.FromQuery] string? error,
    IOptions<GoogleAuthOptions> authOptions,
    IOptions<JwtOptions> jwtOptions,
    IHttpClientFactory httpClientFactory,
    AuthRepository repository,
    EmailService emailService,
    CancellationToken cancellationToken) =>
{
    if (!string.IsNullOrWhiteSpace(error))
    {
        return GoogleAuthPopupResult(error: error);
    }

    if (string.IsNullOrWhiteSpace(code))
    {
        return GoogleAuthPopupResult(error: "no_code");
    }

    try
    {
        var httpClient = httpClientFactory.CreateClient();

        // Exchange code for access token
        var tokenResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = authOptions.Value.ClientId,
                ["client_secret"] = authOptions.Value.ClientSecret,
                ["redirect_uri"] = authOptions.Value.RedirectUri,
                ["grant_type"] = "authorization_code"
            }), cancellationToken);

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var errorBody = await tokenResponse.Content.ReadAsStringAsync(cancellationToken);
            Console.WriteLine($"[GoogleAuth] Token exchange failed: {errorBody}");
            Console.WriteLine($"[GoogleAuth] ClientId: {authOptions.Value.ClientId?.Substring(0, 20)}...");
            Console.WriteLine($"[GoogleAuth] RedirectUri: {authOptions.Value.RedirectUri}");
            var errorSummary = Uri.EscapeDataString(errorBody.Length > 100 ? errorBody[..100] : errorBody);
            return GoogleAuthPopupResult(error: $"token_exchange&details={errorSummary}");
        }

        var tokenData = await tokenResponse.Content.ReadFromJsonAsync<GoogleTokenResponse>(cancellationToken);
        if (tokenData?.AccessToken is null)
        {
            return GoogleAuthPopupResult(error: "no_access_token");
        }

        // Get user info from Google
        httpClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenData.AccessToken);
        var userInfoResponse = await httpClient.GetAsync("https://www.googleapis.com/oauth2/v2/userinfo", cancellationToken);

        if (!userInfoResponse.IsSuccessStatusCode)
        {
            return GoogleAuthPopupResult(error: "user_info");
        }

        var googleUser = await userInfoResponse.Content.ReadFromJsonAsync<GoogleUserInfo>(cancellationToken);
        if (googleUser is null || string.IsNullOrWhiteSpace(googleUser.Email))
        {
            return GoogleAuthPopupResult(error: "no_email");
        }

        // Check if user exists
        var existingUser = await repository.GetByEmailAsync(googleUser.Email, cancellationToken);
        AppUser user;
        bool isNewUser = false;

        if (existingUser is not null)
        {
            user = existingUser;
        }
        else
        {
            var registerRequest = new RegisterRequest
            {
                FullName = googleUser.Name ?? $"{googleUser.GivenName} {googleUser.FamilyName}".Trim(),
                Email = googleUser.Email,
                CompanyName = "No especificada",
                Role = "Analista",
                Password = Guid.NewGuid().ToString("N")[..16]
            };

            user = await repository.CreateUserAsync(registerRequest, cancellationToken);
            isNewUser = true;

            try
            {
                Console.WriteLine($"[GoogleAuth] Sending welcome email to: {user.Email}");
                await emailService.SendWelcomeEmailAsync(user.Email, user.FullName, cancellationToken);
                Console.WriteLine($"[GoogleAuth] Welcome email sent successfully to: {user.Email}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GoogleAuth] Email failed for {user.Email}: {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"[GoogleAuth] Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        var jwtToken = GenerateJwtToken(user, jwtOptions.Value, false);

        // Redirect to frontend with token
        // New users go to complete profile page, existing users go to dashboard
        var redirectPage = isNewUser ? "completar-perfil.html" : "index.html";
        var fallbackUrl = $"/{redirectPage}?token={Uri.EscapeDataString(jwtToken)}&name={Uri.EscapeDataString(user.FullName)}&email={Uri.EscapeDataString(user.Email)}&new={isNewUser}";
        return GoogleAuthPopupResult(jwtToken, redirectPage, fallbackUrl);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[GoogleAuth] Exception: {ex.Message}");
        return GoogleAuthPopupResult(error: "exception");
    }
});

// Endpoint para iniciar actualización automática de OECE
app.MapPost("/api/oece/start-auto-update", async (OeceDataService oeceService, OpportunityRepository repository) =>
{
    try
    {
        Console.WriteLine("[AutoUpdate] Iniciando actualización automática de OECE en background...");
        
        // Iniciar tarea en background
        _ = Task.Run(async () => await AutoUpdateOeceData(oeceService, repository));
        
        return Results.Ok(new { message = "Actualización automática iniciada. Se actualizará cada 7 días." });
    }
    catch (Exception ex)
    {
        return Results.Problem(
            title: "No se pudo iniciar la actualización automática",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.Run();

static Dictionary<string, string[]> ValidateRegisterRequest(RegisterRequest request)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.FullName))
    {
        errors["fullName"] = ["Ingresa el nombre completo."];
    }

    if (string.IsNullOrWhiteSpace(request.CompanyName))
    {
        errors["companyName"] = ["Ingresa la empresa."];
    }

    if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
    {
        errors["email"] = ["Ingresa un correo valido."];
    }

    if (string.IsNullOrWhiteSpace(request.Role))
    {
        errors["role"] = ["Selecciona un rol."];
    }

    if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
    {
        errors["password"] = ["La contrasena debe tener al menos 6 caracteres."];
    }

    return errors;
}

static Dictionary<string, string[]> ValidateLoginRequest(LoginRequest request)
{
    var errors = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(request.Email))
    {
        errors["email"] = ["Ingresa tu correo."];
    }

    if (string.IsNullOrWhiteSpace(request.Password))
    {
        errors["password"] = ["Ingresa tu contrasena."];
    }

    return errors;
}

static string GenerateJwtToken(AppUser user, JwtOptions options, bool rememberMe = false)
{
    var secret = string.IsNullOrWhiteSpace(options.Key)
        ? "CHANGE_ME_REPLACE_WITH_STRONG_SECRET_1234567890"
        : options.Key;

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    // Use extended expiration if rememberMe is true
    var expires = rememberMe
        ? DateTime.UtcNow.AddDays(options.RememberMeExpirationDays > 0 ? options.RememberMeExpirationDays : 30)
        : DateTime.UtcNow.AddMinutes(options.ExpirationMinutes <= 0 ? 1440 : options.ExpirationMinutes);

    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
        new(ClaimTypes.NameIdentifier, user.UserId.ToString()),
        new(JwtRegisteredClaimNames.Email, user.Email),
        new(JwtRegisteredClaimNames.UniqueName, user.FullName),
        new(ClaimTypes.Role, user.RoleName)
    };

    var token = new JwtSecurityToken(
        issuer: string.IsNullOrWhiteSpace(options.Issuer) ? "LicitIA" : options.Issuer,
        audience: string.IsNullOrWhiteSpace(options.Audience) ? "LicitIAUsers" : options.Audience,
        claims: claims,
        expires: expires,
        signingCredentials: credentials);

    return new JwtSecurityTokenHandler().WriteToken(token);
}

static bool ValidateJwtToken(string token, string expectedEmail, JwtOptions options)
{
    try
    {
        var secret = string.IsNullOrWhiteSpace(options.Key)
            ? "CHANGE_ME_REPLACE_WITH_STRONG_SECRET_1234567890"
            : options.Key;

        Console.WriteLine($"[ValidateJwtToken] Validating token for email: {expectedEmail}");
        Console.WriteLine($"[ValidateJwtToken] Using secret (first 20 chars): {secret[..Math.Min(20, secret.Length)]}...");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = string.IsNullOrWhiteSpace(options.Issuer) ? "LicitIA" : options.Issuer,
            ValidAudience = string.IsNullOrWhiteSpace(options.Audience) ? "LicitIAUsers" : options.Audience,
            IssuerSigningKey = key
        };

        var principal = tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
        
        // Log all claims for debugging
        Console.WriteLine($"[ValidateJwtToken] Token received (first 50 chars): {token[..Math.Min(50, token.Length)]}...");
        Console.WriteLine($"[ValidateJwtToken] All claims:");
        foreach (var claim in principal.Claims)
        {
            Console.WriteLine($"  - {claim.Type}: {claim.Value}");
        }
        
        var emailClaim = principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
        var emailClaim2 = principal.FindFirst(ClaimTypes.Email)?.Value;

        Console.WriteLine($"[ValidateJwtToken] Email from JwtRegisteredClaimNames.Email: {emailClaim}");
        Console.WriteLine($"[ValidateJwtToken] Email from ClaimTypes.Email: {emailClaim2}");
        Console.WriteLine($"[ValidateJwtToken] Expected email: {expectedEmail}");
        
        var finalEmail = emailClaim ?? emailClaim2;
        Console.WriteLine($"[ValidateJwtToken] Match: {finalEmail == expectedEmail}");

        return finalEmail == expectedEmail;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ValidateJwtToken] Exception: {ex.GetType().Name}: {ex.Message}");
        return false;
    }
}

static Guid? GetAuthenticatedUserId(HttpContext httpContext, JwtOptions options)
{
    var authorization = httpContext.Request.Headers.Authorization.ToString();
    if (string.IsNullOrWhiteSpace(authorization) ||
        !authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        return null;
    }

    var token = authorization["Bearer ".Length..].Trim();
    if (string.IsNullOrWhiteSpace(token))
    {
        return null;
    }

    try
    {
        var secret = string.IsNullOrWhiteSpace(options.Key)
            ? "CHANGE_ME_REPLACE_WITH_STRONG_SECRET_1234567890"
            : options.Key;

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = string.IsNullOrWhiteSpace(options.Issuer) ? "LicitIA" : options.Issuer,
            ValidAudience = string.IsNullOrWhiteSpace(options.Audience) ? "LicitIAUsers" : options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
        }, out _);

        var userIdClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[Auth] Invalid bearer token: {ex.GetType().Name}: {ex.Message}");
        return null;
    }
}

static string? FindFrontendRoot(string startPath)
{
    var current = startPath;
    // Walk up directory tree looking for home.html
    for (int i = 0; i < 6 && !string.IsNullOrEmpty(current); i++)
    {
        if (File.Exists(Path.Combine(current, "home.html")))
        {
            Console.WriteLine($"[StaticFiles] Serving frontend from: {current}");
            return current;
        }
        current = Path.GetDirectoryName(current);
    }

    // Fallback: try project directory (one level up from LicitIA.Api folder)
    var apiProjectDir = Path.GetDirectoryName(startPath);
    if (!string.IsNullOrEmpty(apiProjectDir))
    {
        var projectRoot = Path.GetDirectoryName(apiProjectDir);
        if (!string.IsNullOrEmpty(projectRoot) && File.Exists(Path.Combine(projectRoot, "home.html")))
        {
            Console.WriteLine($"[StaticFiles] Serving frontend from (fallback): {projectRoot}");
            return projectRoot;
        }
    }

    Console.WriteLine($"[StaticFiles] WARNING: Could not find home.html. Searched from: {startPath}");
    return null;
}

static IResult GoogleAuthPopupResult(string? token = null, string? redirectUrl = null, string? fallbackUrl = null, string? error = null)
{
    var payload = JsonSerializer.Serialize(new
    {
        type = "licitia-google-auth",
        token,
        redirectUrl,
        error
    });

    var fallback = error is null
        ? fallbackUrl ?? $"/{redirectUrl ?? "index.html"}"
        : $"/registro.html?error={Uri.EscapeDataString(error)}";

    var fallbackJson = JsonSerializer.Serialize(fallback);
    var html = $$"""
        <!DOCTYPE html>
        <html lang="es">
        <head>
          <meta charset="UTF-8">
          <meta name="viewport" content="width=device-width, initial-scale=1.0">
          <title>LicitIA | Google</title>
        </head>
        <body>
          <script>
            (function () {
              var payload = {{payload}};
              if (window.opener && !window.opener.closed) {
                window.opener.postMessage(payload, "*");
                window.close();
                return;
              }

              window.location.replace({{fallbackJson}});
            })();
          </script>
        </body>
        </html>
        """;

    return Results.Content(html, "text/html; charset=utf-8");
}

static async Task AutoUpdateOeceData(OeceDataService oeceService, OpportunityRepository repository)
{
    while (true)
    {
        try
        {
            Console.WriteLine("[AutoUpdate] Descargando datos de OECE...");
            
            int currentYear = DateTime.Now.Year;
            var oeceOpportunities = await oeceService.DownloadOeceDataAsync(currentYear);

            if (oeceOpportunities.Count > 0)
            {
                int savedCount = 0;
                foreach (var oece in oeceOpportunities)
                {
                    var opportunity = new Opportunity
                    {
                        ProcessCode = oece.ProcessCode,
                        Title = oece.Title,
                        EntityName = oece.EntityName,
                        EstimatedAmount = oece.EstimatedAmount,
                        ClosingDate = oece.ClosingDate,
                        Category = oece.Category,
                        Modality = oece.Modality,
                        MatchScore = 50,
                        Summary = oece.Description,
                        Location = "Lima",
                        IsPriority = false,
                        PublishedDate = oece.PublishedDate
                    };

                    await repository.InsertOpportunityAsync(opportunity, CancellationToken.None);
                    savedCount++;
                }

                Console.WriteLine($"[AutoUpdate] Se guardaron {savedCount} oportunidades automáticamente.");
            }
            else
            {
                Console.WriteLine("[AutoUpdate] No se encontraron oportunidades en OECE.");
            }

            // Esperar 7 días
            Console.WriteLine("[AutoUpdate] Próxima actualización en 7 días...");
            await Task.Delay(TimeSpan.FromDays(7));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AutoUpdate] Error: {ex.Message}");
            Console.WriteLine("[AutoUpdate] Reintentando en 1 hora...");
            await Task.Delay(TimeSpan.FromHours(1));
        }
    }
}


