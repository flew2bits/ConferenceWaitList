using System.Text.Json;
using StackExchange.Redis;

namespace ConferenceBooking.Services.Redis
{
    /// <summary>
    /// Service for caching session availability information in Redis
    /// </summary>
    public class RedisAvailabilityCache
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisAvailabilityCache> _logger;
        private readonly TimeSpan _defaultExpiry = TimeSpan.FromMinutes(10);

        public RedisAvailabilityCache(IConnectionMultiplexer redis, ILogger<RedisAvailabilityCache> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        /// <summary>
        /// Get the availability information for a session
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>The session availability or null if not found</returns>
        public async Task<SessionAvailability?> GetSessionAvailabilityAsync(Guid sessionId)
        {
            var db = _redis.GetDatabase();
            string key = GetAvailabilityKey(sessionId);
            
            var value = await db.StringGetAsync(key);
            
            if (value.IsNullOrEmpty)
            {
                return null;
            }
            
            try
            {
                return JsonSerializer.Deserialize<SessionAvailability>(value);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize session availability for {SessionId}", sessionId);
                return null;
            }
        }

        /// <summary>
        /// Update the availability information for a session
        /// </summary>
        /// <param name="availability">The updated availability information</param>
        /// <param name="expiry">Optional custom expiration time</param>
        /// <returns>True if successful</returns>
        public async Task<bool> UpdateSessionAvailabilityAsync(SessionAvailability availability, TimeSpan? expiry = null)
        {
            var db = _redis.GetDatabase();
            string key = GetAvailabilityKey(availability.SessionId);
            
            try
            {
                string json = JsonSerializer.Serialize(availability);
                return await db.StringSetAsync(key, json, expiry ?? _defaultExpiry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to update session availability for {SessionId}", availability.SessionId);
                return false;
            }
        }

        /// <summary>
        /// Remove a session's availability from the cache
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>True if successful</returns>
        public async Task<bool> RemoveSessionAvailabilityAsync(Guid sessionId)
        {
            var db = _redis.GetDatabase();
            string key = GetAvailabilityKey(sessionId);
            
            return await db.KeyDeleteAsync(key);
        }

        /// <summary>
        /// Generates the Redis key for a session's availability
        /// </summary>
        private string GetAvailabilityKey(Guid sessionId) => $"session:availability:{sessionId}";
    }

    /// <summary>
    /// Data transfer object for session availability
    /// </summary>
    public class SessionAvailability
    {
        public Guid SessionId { get; set; }
        public int TotalSeats { get; set; }
        public int ReservedSeats { get; set; }
        public int AvailableSeats => TotalSeats - ReservedSeats;
        public int WaitlistCount { get; set; }
        public DateTimeOffset LastUpdated { get; set; }
    }
}
