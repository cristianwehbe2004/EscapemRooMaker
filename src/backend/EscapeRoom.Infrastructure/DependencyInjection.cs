using EscapeRoom.Application.Abstractions;
using EscapeRoom.Application.Triggering;
using EscapeRoom.Domain.Entities;
using EscapeRoom.Infrastructure.Data;
using EscapeRoom.Infrastructure.Realtime;
using EscapeRoom.Infrastructure.Seeding;
using EscapeRoom.Infrastructure.Security;
using EscapeRoom.Infrastructure.Triggering;
using EscapeRoom.Application.Realtime;
using EscapeRoom.Application.Rooms;
using EscapeRoom.Application.Sessions;
using EscapeRoom.TriggerEngine;
using EscapeRoom.TriggerEngine.Idempotency;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using EscapeRoom.Infrastructure.Rooms;
using EscapeRoom.Infrastructure.Sessions;

namespace EscapeRoom.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' was not found.");
        var redisConnection = configuration.GetConnectionString("Redis") ?? "localhost:6379";

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
        services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnection));
        services.AddTriggerEngineCore();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IGmPanelQueryService, GmPanelQueryService>();
        services.AddScoped<ICreatorRoomService, CreatorRoomService>();
        services.AddScoped<ILibraryService, LibraryService>();
        services.AddScoped<IPlayerSessionService, PlayerSessionService>();
        services.AddScoped<ISessionActionProcessor, SessionActionProcessor>();
        services.AddScoped<ISessionLockService, RedisSessionLockService>();
        services.AddScoped<ISessionStateStore, RedisSessionStateStore>();
        services.AddScoped<IIdempotencyStore, RedisIdempotencyStore>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
