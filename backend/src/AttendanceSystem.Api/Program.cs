using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using AttendanceSystem.Api.Background;
using AttendanceSystem.Api.Data;
using AttendanceSystem.Api.Hubs;
using AttendanceSystem.Api.Infrastructure;
using AttendanceSystem.Api.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Render / Railway style hosting passes the port through PORT.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(port)) builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration).Enrich.FromLogContext().WriteTo.Console());

// ---- Options & infrastructure (configuration is read lazily so tests can override it) ----
builder.Services.AddOptions<JwtOptions>().Bind(builder.Configuration.GetSection("Jwt"));
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddDbContext<AppDbContext>((sp, o) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    if (cfg["Database:Provider"] == "InMemory")
        o.UseInMemoryDatabase(cfg["Database:InMemoryName"] ?? "attendance");
    else
        o.UseNpgsql(PostgresConnection.Resolve(cfg)).UseSnakeCaseNamingConvention();
});

builder.Services.AddSingleton<IQrTokenStore>(sp =>
{
    var cs = sp.GetRequiredService<IConfiguration>()["Redis:ConnectionString"];
    if (string.IsNullOrWhiteSpace(cs)) return new InMemoryQrTokenStore(sp.GetRequiredService<TimeProvider>());
    return new RedisQrTokenStore(ConnectionMultiplexer.Connect(RedisQrTokenStore.ParseOptions(cs)));
});

// ---- Auth ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((o, jwt) =>
    {
        var j = jwt.Value;
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true, ValidIssuer = j.Issuer,
            ValidateAudience = true, ValidAudience = j.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(j.Key)),
            ValidateLifetime = true, ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name", RoleClaimType = "role"
        };
        // SignalR sends the JWT in the query string for WebSocket connections.
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization();

// ---- Web ----
builder.Services.AddCors(o => o.AddPolicy("web", p =>
{
    var origins = (builder.Configuration["AllowedOrigins"] ?? "http://localhost:5173")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials().WithExposedHeaders("Content-Disposition");
}));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("checkin", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.User.FindFirst("sub")?.Value ?? ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddSignalR();
builder.Services.AddValidatorsFromAssemblyContaining<Program>();
builder.Services.AddHealthChecks().AddCheck<DependenciesHealthCheck>("dependencies");
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Student Attendance API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT", In = ParameterLocation.Header
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } }] = Array.Empty<string>()
    });
});

// ---- Application services ----
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<AttendanceService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddScoped<StudentService>();
builder.Services.AddScoped<AnalyticsService>();
builder.Services.AddScoped<AbsenceAlertEngine>();
builder.Services.AddSingleton<ILiveNotifier, SignalRLiveNotifier>();
builder.Services.AddHttpClient<IPushSender, PushSender>();
builder.Services.AddHostedService<QrRotationService>();
builder.Services.AddHostedService<AbsenceAlertService>();

var app = builder.Build();

if (app.Services.GetRequiredService<IOptions<JwtOptions>>().Value.Key.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be configured and at least 32 characters long.");

// Create / migrate the schema, then optionally seed demo data.
using (var scope = app.Services.CreateScope())
{
    var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (cfg.GetValue("Database:AutoMigrate", true))
    {
        if (db.Database.IsRelational() && db.Database.GetMigrations().Any()) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();
    }
    if (cfg.GetValue("Seed:Enabled", false)) await DbSeeder.SeedAsync(db);
}

// ---- Pipeline ----
var fwd = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
fwd.KnownNetworks.Clear();
fwd.KnownProxies.Clear();
app.UseForwardedHeaders(fwd);
app.UseMiddleware<ExceptionMiddleware>();
app.Use(async (ctx, next) =>
{
    ctx.Response.Headers["X-Content-Type-Options"] = "nosniff";
    ctx.Response.Headers["X-Frame-Options"] = "DENY";
    ctx.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});
if (app.Configuration.GetValue("Security:HttpsRedirection", false))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
if (app.Environment.IsDevelopment() || app.Configuration.GetValue("Swagger:Enabled", false))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseCors("web");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapControllers();
app.MapHub<AttendanceHub>("/hubs/attendance");
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Ok(new { name = "Student Attendance API", status = "running" }));

app.Run();

public partial class Program { }
