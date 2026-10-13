using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using WmsDistributionCenter.Api.Data;
using WmsDistributionCenter.Api.Infrastructure;
using WmsDistributionCenter.Api.Security;
using WmsDistributionCenter.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Add DbContext
builder.Services.AddDbContext<WmsDbContext>(options =>
    options.UseNpgsql(
        string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("DefaultConnection"))
            ? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured. Set ConnectionStrings__DefaultConnection or use appsettings.Development.json.")
            : builder.Configuration.GetConnectionString("DefaultConnection")));

// 2. JWT authentication (configuration is validated at startup, see JwtSettings)
using var bootstrapLoggerFactory = LoggerFactory.Create(l => l.AddConsole());
var jwt = JwtSettings.Load(builder.Configuration, builder.Environment, bootstrapLoggerFactory.CreateLogger("Startup"));
builder.Services.AddSingleton(jwt);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwt.Issuer,
            ValidAudience = jwt.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ClockSkew = TimeSpan.FromMinutes(1),
            RoleClaimType = ClaimTypes.Role,
            NameClaimType = ClaimTypes.Name
        };
        options.Events = new JwtBearerEvents
        {
            // Reject tokens of deactivated users and tokens whose role changed after they were issued.
            OnTokenValidated = async ctx =>
            {
                var db = ctx.HttpContext.RequestServices.GetRequiredService<WmsDbContext>();
                var idClaim = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var roleClaim = ctx.Principal?.FindFirstValue(ClaimTypes.Role);
                if (!Guid.TryParse(idClaim, out var userId))
                {
                    ctx.Fail("Invalid token subject");
                    return;
                }
                var current = await db.Users.AsNoTracking()
                    .Where(u => u.Id == userId)
                    .Select(u => new { u.IsActive, RoleName = u.Role.Name })
                    .FirstOrDefaultAsync(ctx.HttpContext.RequestAborted);
                if (current is null || !current.IsActive || current.RoleName != roleClaim)
                    ctx.Fail("User is inactive, removed or has a different role");
            }
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Read, p => p.RequireAuthenticatedUser())
    .AddPolicy(Policies.Operate, p => p.RequireRole(AppRoles.Admin, AppRoles.Supervisor, AppRoles.Operator))
    .AddPolicy(Policies.Manage, p => p.RequireRole(AppRoles.Admin, AppRoles.Supervisor))
    .AddPolicy(Policies.AdminOnly, p => p.RequireRole(AppRoles.Admin));

// Rate limiting for the unauthenticated auth endpoints (per client IP)
var authPermits = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 10);
var authWindow = TimeSpan.FromSeconds(builder.Configuration.GetValue("RateLimiting:AuthWindowSeconds", 60));
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = authPermits, Window = authWindow, QueueLimit = 0 }));
    o.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync("{\"message\":\"Too many attempts. Try again later.\"}", ct);
    };
});

// Behind a reverse proxy (nginx in docker compose) the client IP comes from X-Forwarded-For.
if (builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.KnownNetworks.Clear();
        o.KnownProxies.Clear();
    });
}

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

// 3. Add Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IInboundService, InboundService>();
builder.Services.AddScoped<IOutboundService, OutboundService>();
builder.Services.AddScoped<ITransferService, TransferService>();
builder.Services.AddScoped<IReturnService, ReturnService>();
builder.Services.AddScoped<IReportService, ReportService>();

// 4. Add Controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

// 5. Add Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "WMS Distribution Center API",
        Version = "v1",
        Description = "Warehouse Management System API for a distribution center (portfolio case study)"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter 'Bearer' [space] and then your token.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// 6. CORS: only the configured origins (AllowedOrigins). Empty means same-origin only.
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>() ?? [];
if (allowedOrigins.Any(o => o.Trim() == "*"))
    throw new InvalidOperationException("AllowedOrigins must list explicit origins; '*' is not allowed.");
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length > 0)
            policy.WithOrigins(allowedOrigins).WithMethods("GET", "POST", "PUT", "DELETE").WithHeaders("Authorization", "Content-Type");
    });
});

// Schema migration + seeding run when the host starts (see DatabaseInitializer)
builder.Services.AddHostedService<DatabaseInitializer>();

var app = builder.Build();

// 7. Configure pipeline
app.UseExceptionHandler();
if (builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled"))
    app.UseForwardedHeaders();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "WMS Distribution Center API v1");
    });
}

app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Exposed for integration tests
public partial class Program { }
