using System.Text.Json;
using ConferenceBooking.Entities.Sessions;
using StackExchange.Redis;

namespace ConferenceBooking.Services.Redis
{
    /// <summary>
    /// Service for managing waitlists using Redis sorted sets
    /// </summary>
    public class RedisWaitlistService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisWaitlistService> _logger;
        
        public RedisWaitlistService(IConnectionMultiplexer redis, ILogger<RedisWaitlistService> logger)
        {
            _redis = redis;
            _logger = logger;
        }

        /// <summary>
        /// Add a user to the waitlist for a session
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <param name="waitlistEntry">The waitlist entry to add</param>
        /// <returns>The position in the waitlist (1-based)</returns>
        public async Task<int> AddToWaitlistAsync(Guid sessionId, WaitlistEntry waitlistEntry)
        {
            var db = _redis.GetDatabase();
            string key = GetWaitlistKey(sessionId);
            
            // Use the join timestamp as the score for sorting
            double score = waitlistEntry.JoinedAt.ToUnixTimeMilliseconds();
            string value = JsonSerializer.Serialize(waitlistEntry);
            
            // Add to sorted set
            await db.SortedSetAddAsync(key, value, score);
            
            // Get position (Redis is 0-based, we want 1-based)
            long? rank = await db.SortedSetRankAsync(key, value);
            int position = rank.HasValue ? (int)rank.Value + 1 : -1;
            
            _logger.LogInformation("Added user {UserId} to waitlist for session {SessionId} at position {Position}", 
                waitlistEntry.UserId, sessionId, position);
                
            return position;
        }

        /// <summary>
        /// Get the next user from the waitlist
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>The next waitlist entry or null if the waitlist is empty</returns>
        public async Task<WaitlistEntry> GetNextWaitlistedUserAsync(Guid sessionId)
        {
            var db = _redis.GetDatabase();
            string key = GetWaitlistKey(sessionId);
            
            // Get the earliest entry (lowest score)
            var entry = await db.SortedSetRangeByRankWithScoresAsync(key, 0, 0);
            
            if (entry.Length == 0)
            {
                return null;
            }
            
            try
            {
                return JsonSerializer.Deserialize<WaitlistEntry>(entry[0].Element);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to deserialize waitlist entry for session {SessionId}", sessionId);
                return null;
            }
        }

        /// <summary>
        /// Remove a user from the waitlist
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <param name="userId">The user ID</param>
        /// <returns>True if successful</returns>
        public async Task<bool> RemoveFromWaitlistAsync(Guid sessionId, Guid userId)
        {
            var db = _redis.GetDatabase();
            string key = GetWaitlistKey(sessionId);
            
            // Get all entries
            var entries = await db.SortedSetRangeByRankAsync(key);
            
            // Find the entry with the matching user ID
            foreach (var entry in entries)
            {
                try
                {
                    var waitlistEntry = JsonSerializer.Deserialize<WaitlistEntry>(entry);
                    if (waitlistEntry.UserId == userId)
                    {
                        // Remove the entry
                        bool removed = await db.SortedSetRemoveAsync(key, entry);
                        _logger.LogInformation("Removed user {UserId} from waitlist for session {SessionId}: {Result}", 
                            userId, sessionId, removed);
                        return removed;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize waitlist entry while removing user {UserId} from session {SessionId}", 
                        userId, sessionId);
                }
            }
            
            _logger.LogWarning("User {UserId} not found in waitlist for session {SessionId}", userId, sessionId);
            return false;
        }

        /// <summary>
        /// Get all waitlisted users for a session
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>A list of waitlist entries in order</returns>
        public async Task<List<WaitlistEntry>> GetWaitlistAsync(Guid sessionId)
        {
            var db = _redis.GetDatabase();
            string key = GetWaitlistKey(sessionId);
            
            var entries = await db.SortedSetRangeByRankAsync(key);
            var result = new List<WaitlistEntry>();
            
            foreach (var entry in entries)
            {
                try
                {
                    var waitlistEntry = JsonSerializer.Deserialize<WaitlistEntry>(entry);
                    result.Add(waitlistEntry);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to deserialize waitlist entry for session {SessionId}", sessionId);
                }
            }
            
            return result;
        }

        /// <summary>
        /// Get the waitlist count for a session
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>The number of users on the waitlist</returns>
        public async Task<int> GetWaitlistCountAsync(Guid sessionId)
        {
            var db = _redis.GetDatabase();
            string key = GetWaitlistKey(sessionId);
            
            long count = await db.SortedSetLengthAsync(key);
            return (int)count;
        }

        /// <summary>
        /// Generates the Redis key for a session's waitlist
        /// </summary>
        private string GetWaitlistKey(Guid sessionId) => $"session:waitlist:{sessionId}";
    }
}
