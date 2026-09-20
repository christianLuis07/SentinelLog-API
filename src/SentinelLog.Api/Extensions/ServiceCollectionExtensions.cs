using System.Reflection;
using System.Text;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SentinelLog.Api.Services;
using SentinelLog.Application.Interfaces;
using SentinelLog.Application.Validators;
using SentinelLog.Domain.Constants;
using SentinelLog.Infrastructure.Authentication;
using SentinelLog.Infrastructure.Data;
using SentinelLog.Infrastructure.Services;

namespace SentinelLog.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? configuration["DATABASE_URL"];

        var useInMemory = string.Equals(configuration["USE_IN_MEMORY_DB"], "true", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(connectionString)
            || connectionString.Contains("InMemory", StringComparison.OrdinalIgnoreCase);

        services.AddDbContext<SentinelLogDbContext>(options =>
        {
            if (!useInMemory)
            {
                options.UseNpgsql(connectionString, npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsAssembly(typeof(SentinelLogDbContext).Assembly.FullName);
                    npgsqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null);
                });
            }
            else
            {
                var dbName = configuration["IN_MEMORY_DB_NAME"] ?? "SentinelLogTestDb";
                options.UseInMemoryDatabase(dbName);
            }
        });

        return services;
    }

    public static IServiceCollection AddSecurityAndAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var jwtSection = configuration.GetSection(JwtOptions.SectionName);
        services.Configure<JwtOptions>(jwtSection);
        var jwtOptions = jwtSection.Get<JwtOptions>() ?? new JwtOptions();

        if (string.IsNullOrWhiteSpace(jwtOptions.Secret))
        {
            // Default dev secret if not provided in configuration (overridden in production via env var)
            jwtOptions.Secret = "SentinelLogSecureDevSecretKeyForJwtSigningMustBeAtLeast32BytesLong!";
            services.PostConfigure<JwtOptions>(opt => opt.Secret = jwtOptions.Secret);
        }

        var key = Encoding.UTF8.GetBytes(jwtOptions.Secret);

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false; // Set to true in strict production HTTPS environments
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero
            };

            options.Events = new JwtBearerEvents
            {
                OnChallenge = async context =>
                {
                    context.HandleResponse();
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    context.Response.ContentType = "application/problem+json";

                    var problem = new
                    {
                        type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.2",
                        title = "Unauthorized",
                        status = StatusCodes.Status401Unauthorized,
                        detail = "Valid Bearer authentication token is missing or has expired.",
                        instance = context.Request.Path.Value,
                        correlationId = context.HttpContext.Items["CorrelationId"]?.ToString() ?? context.HttpContext.TraceIdentifier
                    };

                    await context.Response.WriteAsJsonAsync(problem);
                },
                OnForbidden = async context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    context.Response.ContentType = "application/problem+json";

                    var problem = new
                    {
                        type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.4",
                        title = "Forbidden",
                        status = StatusCodes.Status403Forbidden,
                        detail = "Your role lacks sufficient authorization for this resource.",
                        instance = context.Request.Path.Value,
                        correlationId = context.HttpContext.Items["CorrelationId"]?.ToString() ?? context.HttpContext.TraceIdentifier
                    };

                    await context.Response.WriteAsJsonAsync(problem);
                }
            };
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.RequireAdmin, policy =>
                policy.RequireRole(Roles.Admin));

            options.AddPolicy(Policies.RequireSecurityAnalystOrAdmin, policy =>
                policy.RequireRole(Roles.Admin, Roles.SecurityAnalyst));

            options.AddPolicy(Policies.RequireEventIngestion, policy =>
                policy.RequireRole(Roles.Admin, Roles.SecurityAnalyst, Roles.Service));

            options.AddPolicy(Policies.RequireEventRead, policy =>
                policy.RequireRole(Roles.Admin, Roles.SecurityAnalyst, Roles.Service, Roles.Viewer));

            options.AddPolicy(Policies.RequireEventStatistics, policy =>
                policy.RequireRole(Roles.Admin, Roles.SecurityAnalyst, Roles.Viewer));

            options.AddPolicy(Policies.RequireAuditLogAccess, policy =>
                policy.RequireRole(Roles.Admin, Roles.SecurityAnalyst));

            options.AddPolicy(Policies.RequireUserManagement, policy =>
                policy.RequireRole(Roles.Admin));
        });

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISecurityEventService, SecurityEventService>();
        services.AddScoped<IAuditLogService, AuditLogService>();
        services.AddScoped<IUserService, UserService>();

        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();

        return services;
    }

    public static IServiceCollection AddCustomRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var loginPermitLimit = configuration.GetValue("RateLimiting:LoginPermitLimit", 5);
        var registerPermitLimit = configuration.GetValue("RateLimiting:RegisterPermitLimit", 10);
        var eventsPermitLimit = configuration.GetValue("RateLimiting:EventsPermitLimit", 60);

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.ContentType = "application/problem+json";
                var problem = new
                {
                    type = "https://datatracker.ietf.org/doc/html/rfc6585#section-4",
                    title = "Too Many Requests",
                    status = StatusCodes.Status429TooManyRequests,
                    detail = "API rate limit exceeded. Please throttle your requests and try again later.",
                    instance = context.HttpContext.Request.Path.Value,
                    correlationId = context.HttpContext.Items["CorrelationId"]?.ToString() ?? context.HttpContext.TraceIdentifier
                };

                await context.HttpContext.Response.WriteAsJsonAsync(problem, cancellationToken: token);
            };

            // Login policy: Partitioned by Client IP
            options.AddPolicy("auth-login", httpContext =>
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = loginPermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
            });

            // Register policy: Partitioned by Client IP
            options.AddPolicy("auth-register", httpContext =>
            {
                var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = registerPermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
            });

            // Event ingestion policy: Partitioned by Authenticated User or IP
            options.AddPolicy("events-ingest", httpContext =>
            {
                var partitionKey = httpContext.User.Identity?.Name
                    ?? httpContext.Connection.RemoteIpAddress?.ToString()
                    ?? "anonymous";

                return RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = eventsPermitLimit,
                    Window = TimeSpan.FromMinutes(1),
                    SegmentsPerWindow = 6,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
            });
        });

        return services;
    }

    public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "SentinelLog API",
                Version = "v1",
                Description = "A production-oriented Security Event & Audit Log RESTful API built with ASP.NET Core, EF Core, and PostgreSQL.",
                Contact = new OpenApiContact
                {
                    Name = "SentinelLog Security Engineering",
                    Email = "security@sentinellog.local"
                }
            });

            // JWT Bearer documentation setup
            var securityScheme = new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Description = "Enter JWT Bearer token format: **Bearer {your_token}**",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Reference = new OpenApiReference
                {
                    Id = JwtBearerDefaults.AuthenticationScheme,
                    Type = ReferenceType.SecurityScheme
                }
            };

            options.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, securityScheme);
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                { securityScheme, Array.Empty<string>() }
            });

            // Include XML comments if generated
            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }
}
