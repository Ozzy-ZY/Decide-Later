using Application;
using Infrastructure;
using Infrastructure.Configurations;
using Microsoft.OpenApi.Models;
using Npgsql;
using Serilog;
using WebProj.Hubs;
using WebProj.Middleware;
using WebProj.RateLimiting;

namespace WebProj;

public class Program
{
    public static void Main(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(configuration)
            .Enrich.FromLogContext()
            .Enrich.WithEnvironmentName()
            .Enrich.WithThreadId()
            .CreateLogger();

        try
        {
            Log.Information("Starting Balzgram API");
            
            var builder = WebApplication.CreateBuilder(args);
            builder.Host.UseSerilog();
            builder.Services.AddApplication();
            builder.Services.AddInfrastructure(builder.Configuration);

            builder.Services.AddControllers();

            builder.Services.AddSignalR();

            builder.Services.AddAppRateLimiting(builder.Configuration);
            var rateLimitingSettings = builder.Configuration.GetSection(RateLimitingSettings.SectionName)
                .Get<RateLimitingSettings>()
                ?? new RateLimitingSettings();

            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "Balzgram API",
                    Version = "v1",
                    Description = "A simple chat application API with SignalR support"
                });

                // Add JWT Authentication to Swagger
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter your JWT token"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            }
                        },
                        Array.Empty<string>()
                    }
                });
            });

            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", policy =>
                {
                    policy
                        .SetIsOriginAllowed(_ => true)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();             
                });
            });

            var app = builder.Build();

            app.UseSerilogRequestLogging(options =>
            {
                options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
            });
            app.UseGlobalExceptionHandler();
            app.UseAppRateLimiting();
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Balzgram API v1");
            });


            app.UseHttpsRedirection();
            app.UseCors("AllowAll");
            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers()
                .RequireCors("AllowAll")
                .RequireAppRateLimiting(rateLimitingSettings.Http.PerUser.PolicyName);

            app.MapHub<ChatHub>("/hubs/chat")
                .RequireCors("AllowAll")
                .RequireAppRateLimiting(rateLimitingSettings.Http.PerUser.PolicyName);

            // Simple health endpoint
            app.MapGet("/health", async (IConfiguration config) =>
            {
                Log.Debug("Health check initiated");
                var connStr = config.GetConnectionString("DefaultConnection");
                if (string.IsNullOrWhiteSpace(connStr))
                {
                    Log.Error("No connection string configured");
                    return Results.Problem("No connection string configured", statusCode: 500);
                }

                var maxAttempts = 5;
                var delay = TimeSpan.FromSeconds(2);

                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        await using var conn = new NpgsqlConnection(connStr);
                        await conn.OpenAsync();
                        await conn.CloseAsync();
                        Log.Debug("Health check passed - database reachable");
                        return Results.Ok(new { status = "Healthy", db = "reachable" });
                    }
                    catch (Exception ex) when (attempt < maxAttempts)
                    {
                        Log.Warning(ex, "Health check attempt {Attempt} failed, retrying...", attempt);
                        await Task.Delay(delay);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Database unreachable after {MaxAttempts} attempts", maxAttempts);
                        return Results.Problem($"Database unreachable: {ex.Message}", statusCode: 500);
                    }
                }

                return Results.Problem("Database unreachable after retries", statusCode: 500);
            });

            Log.Information("Chat App API started successfully");
            app.Run();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Application terminated unexpectedly");
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}