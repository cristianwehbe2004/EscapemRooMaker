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
        services.AddTriggerEngineCore();

        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IGmPanelQueryService, GmPanelQueryService>();
        services.AddScoped<ISessionSnapshotHydrator, PersistentSessionSnapshotHydrator>();
        services.AddScoped<ICreatorRoomService, CreatorRoomService>();
        services.AddScoped<ILibraryService, LibraryService>();
        services.AddScoped<IPlayerSessionService, PlayerSessionService>();
        services.AddScoped<ISessionActionProcessor, SessionActionProcessor>();
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<DatabaseSeeder>();

        if (TryConnectRedis(redisConnection, out var multiplexer))
        {
            services.AddSingleton<IConnectionMultiplexer>(_ => multiplexer);
            services.AddScoped<ISessionLockService, RedisSessionLockService>();
            services.AddScoped<ISessionStateStore, RedisSessionStateStore>();
            services.AddScoped<IIdempotencyStore, RedisIdempotencyStore>();
        }
        else
        {
            services.AddSingleton<InMemorySessionStateStore>();
            services.AddSingleton<InMemorySessionLockService>();
            services.AddSingleton<InMemoryIdempotencyStore>();
            services.AddScoped<ISessionLockService>(sp => sp.GetRequiredService<InMemorySessionLockService>());
            services.AddScoped<ISessionStateStore>(sp => sp.GetRequiredService<InMemorySessionStateStore>());
            services.AddScoped<IIdempotencyStore>(sp => sp.GetRequiredService<InMemoryIdempotencyStore>());
        }

        return services;
    }

    private static bool TryConnectRedis(string redisConnection, out IConnectionMultiplexer multiplexer)
    {
        try
        {
            var options = ConfigurationOptions.Parse(redisConnection, true);
            options.AbortOnConnectFail = true;
            options.ConnectRetry = 1;
            options.ConnectTimeout = Math.Min(options.ConnectTimeout <= 0 ? 1000 : options.ConnectTimeout, 1000);
            options.SyncTimeout = Math.Min(options.SyncTimeout <= 0 ? 3000 : options.SyncTimeout, 3000);

            var connectedMultiplexer = ConnectionMultiplexer.Connect(options);
            var hasConnectedServer = connectedMultiplexer.GetEndPoints()
                .Select(endpoint => connectedMultiplexer.GetServer(endpoint))
                .Any(server => server.IsConnected);
            if (!hasConnectedServer)
            {
                connectedMultiplexer.Dispose();
                multiplexer = null!;
                return false;
            }

            multiplexer = connectedMultiplexer;
            return true;
        }
        catch
        {
            multiplexer = null!;
            return false;
        }
    }
}
