using System.Text.Json;
using Polly;
using Polly.Retry;
using StackExchange.Redis;

namespace ConferenceBooking.RedisLocking;

public sealed record LockToken(string ResourceKey, string LockValue);

public interface IRedisLockService
{
    Task<LockToken> AcquireLockAsync(string resourceKey, TimeSpan expiry, string? value = null,
        CancellationToken cancellationToken = default);

    Task<LockToken?> TryAcquireLockAsync(string resourceKey, TimeSpan expiry, string? value = null,
        CancellationToken cancellationToken = default);

    Task<bool> ReleaseLockAsync(LockToken lockToken, CancellationToken cancellationToken = default);
    Task<bool> ForceReleaseLockAsync(string resourceKey, CancellationToken cancellationToken = default);
    string GetSerializableToken(LockToken lockToken);
    LockToken DeserializeToken(string serializedToken);
}

public interface IRedisLockService<T> : IRedisLockService;

public class RedisLockService : IRedisLockService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger _logger;
    private readonly string _lockKeyPrefix;

    public RedisLockService(
        IConnectionMultiplexer redis,
        ILogger<RedisLockService> logger,
        string lockKeyPrefix = "lock:")
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _lockKeyPrefix = lockKeyPrefix;
    }

    public async Task<LockToken> AcquireLockAsync(
        string resourceKey,
        TimeSpan expiry,
        string? value = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(resourceKey))
            throw new ArgumentException("Resource key cannot be null or empty", nameof(resourceKey));

        if (expiry.TotalMilliseconds <= 0)
            throw new ArgumentException("Expiry must be greater than zero", nameof(expiry));

        var lockValue = value ?? Guid.NewGuid().ToString("N");
        var fullKey = GetFullKey(resourceKey);
        var db = _redis.GetDatabase();

        var acquired = await db.StringSetAsync(
            fullKey,
            lockValue,
            expiry,
            When.NotExists,
            CommandFlags.None
        );

        if (!acquired)
        {
            _logger.LogWarning("Failed to acquire lock for resource {ResourceKey}", resourceKey);
            throw new LockAcquisitionException($"Failed to acquire lock for resource {resourceKey}");
        }

        _logger.LogInformation("Lock acquired for resource {ResourceKey} with value {LockValue}", resourceKey,
            lockValue);
        return new LockToken(resourceKey, lockValue);
    }

    public async Task<LockToken?> TryAcquireLockAsync(
        string resourceKey,
        TimeSpan expiry,
        string? value = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(resourceKey))
            throw new ArgumentException("Resource key cannot be null or empty", nameof(resourceKey));

        if (expiry.TotalMilliseconds <= 0)
            throw new ArgumentException("Expiry must be greater than zero", nameof(expiry));

        var lockValue = value ?? Guid.NewGuid().ToString("N");
        var fullKey = GetFullKey(resourceKey);
        var db = _redis.GetDatabase();

        var acquired = await db.StringSetAsync(
            fullKey,
            lockValue,
            expiry,
            When.NotExists,
            CommandFlags.None
        );

        if (!acquired)
        {
            _logger.LogInformation("Resource {ResourceKey} is already locked", resourceKey);
            return null;
        }

        _logger.LogInformation("Lock acquired for resource {ResourceKey} with value {LockValue}", resourceKey,
            lockValue);
        return new LockToken(resourceKey, lockValue);
    }

    /// <inheritdoc />
    public async Task<bool> ReleaseLockAsync(
        LockToken lockToken,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(lockToken);

        var fullKey = GetFullKey(lockToken.ResourceKey);
        var db = _redis.GetDatabase();

        // Lua script to release the lock only if the value matches
        const string script = """
                              if redis.call('get', KEYS[1]) == ARGV[1] then
                                  return redis.call('del', KEYS[1])
                              else
                                  return 0
                              end
                              """;

        var result = await db.ScriptEvaluateAsync(
            script,
            [fullKey],
            [lockToken.LockValue]
        );

        var released = (long?)result > 0;

        if (released)
        {
            _logger.LogInformation("Lock released for resource {ResourceKey}", lockToken.ResourceKey);
        }
        else
        {
            _logger.LogWarning(
                "Failed to release lock for resource {ResourceKey} - value mismatch or lock doesn't exist",
                lockToken.ResourceKey);
        }

        return released;
    }

    public async Task<bool> ForceReleaseLockAsync(
        string resourceKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(resourceKey))
            throw new ArgumentException("Resource key cannot be null or empty", nameof(resourceKey));

        var fullKey = GetFullKey(resourceKey);
        var db = _redis.GetDatabase();

        var released = await db.KeyDeleteAsync(fullKey);

        if (released)
        {
            _logger.LogInformation("Lock forcibly released for resource {ResourceKey}", resourceKey);
        }
        else
        {
            _logger.LogWarning("Failed to forcibly release lock for resource {ResourceKey} - lock doesn't exist",
                resourceKey);
        }

        return released;
    }

    public string GetSerializableToken(LockToken lockToken)
    {
        ArgumentNullException.ThrowIfNull(lockToken);

        return JsonSerializer.Serialize(lockToken);
    }

    public LockToken DeserializeToken(string serializedToken)
    {
        if (string.IsNullOrEmpty(serializedToken))
            throw new ArgumentException("Serialized token cannot be null or empty", nameof(serializedToken));

        try
        {
            var lockToken = JsonSerializer.Deserialize<LockToken>(serializedToken);
            if (lockToken == null)
                throw new InvalidOperationException("Deserialized token is null");
            return lockToken;
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("Invalid serialized token format", nameof(serializedToken), ex);
        }
    }

    private string GetFullKey(string resourceKey) => $"{_lockKeyPrefix}{resourceKey}";
}

public class LockAcquisitionException : Exception
{
    public LockAcquisitionException(string message) : base(message)
    {
    }

    public LockAcquisitionException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public class RedisLockService<T> : IRedisLockService<T>
{
    private readonly IRedisLockService _innerService;
    private readonly string _contextPrefix;

    public RedisLockService(IRedisLockService innerService)
    {
        _innerService = innerService ?? throw new ArgumentNullException(nameof(innerService));
        _contextPrefix = typeof(T).Name + ":";
    }

    public Task<LockToken> AcquireLockAsync(string resourceKey, TimeSpan expiry, string? value = null,
        CancellationToken cancellationToken = default) =>
        _innerService.AcquireLockAsync(GetContextKey(resourceKey), expiry, value, cancellationToken);

    public Task<LockToken?> TryAcquireLockAsync(string resourceKey, TimeSpan expiry, string? value = null,
        CancellationToken cancellationToken = default) =>
        _innerService.TryAcquireLockAsync(GetContextKey(resourceKey), expiry, value, cancellationToken);

    public Task<bool> ReleaseLockAsync(LockToken lockToken, CancellationToken cancellationToken = default) =>
        _innerService.ReleaseLockAsync(lockToken, cancellationToken);

    public Task<bool> ForceReleaseLockAsync(string resourceKey, CancellationToken cancellationToken = default) =>
        _innerService.ForceReleaseLockAsync(GetContextKey(resourceKey), cancellationToken);

    public string GetSerializableToken(LockToken lockToken) => _innerService.GetSerializableToken(lockToken);

    public LockToken DeserializeToken(string serializedToken) => _innerService.DeserializeToken(serializedToken);

    private string GetContextKey(string resourceName) => $"{_contextPrefix}{resourceName}";
}

public static class RetryPolicies
{
    public static AsyncRetryPolicy<LockToken?> Default => Policy
        .Handle<LockAcquisitionException>()
        .OrResult<LockToken?>(result => result == null)
        .WaitAndRetryAsync(3, _ => TimeSpan.FromMilliseconds(200));

    public static AsyncRetryPolicy<LockToken?> Aggressive => Policy
        .Handle<LockAcquisitionException>()
        .OrResult<LockToken?>(result => result == null)
        .WaitAndRetryAsync(10, _ => TimeSpan.FromMilliseconds(100));

    public static AsyncRetryPolicy<LockToken?> Conservative => Policy
        .Handle<LockAcquisitionException>()
        .OrResult<LockToken?>(result => result == null)
        .WaitAndRetryAsync(
            5,
            retryAttempt => TimeSpan.FromMilliseconds(Math.Min(100 * Math.Pow(2, retryAttempt), 3000)));

    public static AsyncRetryPolicy<LockToken?> GetPolicy(string policyName) => policyName.ToLower() switch
    {
        "default" => Default,
        "aggressive" => Aggressive,
        "conservative" => Conservative,
        _ => throw new ArgumentException($"Unknown policy name: {policyName}", nameof(policyName))
    };
}

public static class RedisLockServiceExtensions
{
    public static Task<LockToken?> TryAcquireLockWithRetriesAsync(
        this IRedisLockService lockService,
        string resourceKey,
        TimeSpan expiry,
        string policyName,
        string? value = null,
        CancellationToken cancellationToken = default)
    {
        var policy = RetryPolicies.GetPolicy(policyName);
        return TryAcquireLockWithRetriesInternalAsync(
            lockService,
            resourceKey,
            expiry,
            policy,
            value,
            cancellationToken);
    }

    public static Task<LockToken?> TryAcquireLockWithRetriesAsync(
        this IRedisLockService lockService,
        string resourceKey,
        TimeSpan expiry,
        int retryCount,
        TimeSpan retryDelay,
        string? value = null,
        CancellationToken cancellationToken = default)
    {
        var policy = Policy
            .Handle<LockAcquisitionException>()
            .OrResult<LockToken?>(result => result == null)
            .WaitAndRetryAsync(
                retryCount,
                _ => retryDelay);

        return TryAcquireLockWithRetriesInternalAsync(
            lockService,
            resourceKey,
            expiry,
            policy,
            value,
            cancellationToken);
    }

    public static Task<LockToken?> TryAcquireLockWithExponentialBackoffAsync(
        this IRedisLockService lockService,
        string resourceKey,
        TimeSpan expiry,
        int retryCount,
        TimeSpan initialBackoff,
        TimeSpan maxBackoff,
        string? value = null,
        CancellationToken cancellationToken = default)
    {
        var policy = Policy
            .Handle<LockAcquisitionException>()
            .OrResult<LockToken?>(result => result == null)
            .WaitAndRetryAsync(
                retryCount,
                retryAttempt => TimeSpan.FromMilliseconds(
                    Math.Min(
                        initialBackoff.TotalMilliseconds * Math.Pow(2, retryAttempt),
                        maxBackoff.TotalMilliseconds)));

        return TryAcquireLockWithRetriesInternalAsync(
            lockService,
            resourceKey,
            expiry,
            policy,
            value,
            cancellationToken);
    }

    private static async Task<LockToken?> TryAcquireLockWithRetriesInternalAsync(
        IRedisLockService lockService,
        string resourceKey,
        TimeSpan expiry,
        AsyncRetryPolicy<LockToken?> policy,
        string? value,
        CancellationToken cancellationToken)
    {
        return await policy.ExecuteAsync(async () =>
        {
            try
            {
                return await lockService.TryAcquireLockAsync(
                    resourceKey,
                    expiry,
                    value,
                    cancellationToken);
            }
            catch (LockAcquisitionException)
            {
                return null;
            }
        });
    }
}