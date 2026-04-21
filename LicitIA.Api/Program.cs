using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Data.SqlClient;
using System.Text;
using LicitIA.Api.Configuration;
using LicitIA.Api.Contracts;
using LicitIA.Api.Data;
using LicitIA.Api.Models;
using LicitIA.Api.Security;
using Microsoft.AspNetCore.Diagnostics;
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
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton<PasswordService>();
builder.Services.AddSingleton<SqlConnectionFactory>();
builder.Services.AddSingleton<AuthRepository>();
builder.Services.AddSingleton<OpportunityRepository>();

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

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    service = "LicitIA API"
}));

app.MapGet("/api/opportunities", async (OpportunityRepository repository, CancellationToken cancellationToken) =>
{
    try
    {
        var opportunities = await repository.GetAllAsync(cancellationToken);
        return Results.Ok(opportunities.Select(OpportunityResponse.FromModel));
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

app.MapGet("/api/opportunities/{id:int}", async (int id, OpportunityRepository repository, CancellationToken cancellationToken) =>
{
    try
    {
        var opportunity = await repository.GetByIdAsync(id, cancellationToken);

        return opportunity is null
            ? Results.NotFound(new { message = "No se encontro la oportunidad solicitada." })
            : Results.Ok(OpportunityResponse.FromModel(opportunity));
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

app.MapPost("/api/auth/register", async (
    RegisterRequest request,
    AuthRepository repository,
    IOptions<JwtOptions> jwtOptions,
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

        return Results.Ok(new
        {
            message = "Registro completado correctamente.",
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
    CancellationToken cancellationToken) =>
{
    var validationErrors = ValidateLoginRequest(request);
    if (validationErrors.Count > 0)
    {
        return Results.ValidationProblem(validationErrors);
    }

    try
    {
        var user = await repository.GetByEmailAsync(request.Email, cancellationToken);
        if (user is null || !passwordService.VerifyPassword(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            return Results.BadRequest(new { message = "Correo o contrasena incorrectos." });
        }

        var token = GenerateJwtToken(user, jwtOptions.Value);

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
            title: "No se pudo iniciar sesion.",
            detail: "Revisa la conexion a SQL Server y confirma que la base existe.",
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

static string GenerateJwtToken(AppUser user, JwtOptions options)
{
    var secret = string.IsNullOrWhiteSpace(options.Key)
        ? "CHANGE_ME_REPLACE_WITH_STRONG_SECRET_1234567890"
        : options.Key;

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var expires = DateTime.UtcNow.AddMinutes(options.ExpirationMinutes <= 0 ? 1440 : options.ExpirationMinutes);

    var claims = new List<Claim>
    {
        new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
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
