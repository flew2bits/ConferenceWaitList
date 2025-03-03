using ConferenceBooking.Entities.Sessions;
using ConferenceBooking.Services.Redis;
using Marten;

namespace ConferenceBooking.Services
{
    /// <summary>
    /// Service for retrieving and updating session availability
    /// </summary>
    public class SessionAvailabilityService
    {
        private readonly IDocumentStore _documentStore;
        private readonly RedisAvailabilityCache _availabilityCache;
        private readonly RedisWaitlistService _waitlistService;
        private readonly ILogger<SessionAvailabilityService> _logger;

        public SessionAvailabilityService(
            IDocumentStore documentStore,
            RedisAvailabilityCache availabilityCache,
            RedisWaitlistService waitlistService,
            ILogger<SessionAvailabilityService> logger)
        {
            _documentStore = documentStore;
            _availabilityCache = availabilityCache;
            _waitlistService = waitlistService;
            _logger = logger;
        }

        /// <summary>
        /// Get the current availability for a session
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>The session availability information</returns>
        public async Task<SessionAvailability?> GetSessionAvailabilityAsync(Guid sessionId)
        {
            // Try to get from cache first
            var availability = await _availabilityCache.GetSessionAvailabilityAsync(sessionId);
            
            if (availability != null)
            {
                return availability;
            }
            
            // Cache miss, need to calculate from database
            availability = await CalculateSessionAvailabilityAsync(sessionId);
            
            if (availability != null)
            {
                // Cache the calculated availability
                await _availabilityCache.UpdateSessionAvailabilityAsync(availability);
            }
            
            return availability;
        }

        /// <summary>
        /// Update the availability for a session after a reservation or cancellation
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <param name="reservedChange">Change to reservation count (positive for new reservations, negative for cancellations)</param>
        /// <returns>The updated availability</returns>
        public async Task<SessionAvailability> UpdateAvailabilityAsync(Guid sessionId, int reservedChange)
        {
            // Get current availability
            var availability = await GetSessionAvailabilityAsync(sessionId);
            
            if (availability == null)
            {
                return null;
            }
            
            // Update reserved count
            availability.ReservedSeats += reservedChange;
            availability.LastUpdated = DateTimeOffset.UtcNow;
            
            // Make sure we don't have negative reservations
            if (availability.ReservedSeats < 0)
            {
                availability.ReservedSeats = 0;
            }
            
            // Update the cache
            await _availabilityCache.UpdateSessionAvailabilityAsync(availability);
            
            return availability;
        }

        /// <summary>
        /// Refresh all session availability from the database
        /// </summary>
        /// <returns>The number of sessions processed</returns>
        public async Task<int> RefreshAllSessionAvailabilityAsync()
        {
            int count = 0;
            
            using (var session = _documentStore.OpenSession())
            {
                var sessions = await session.Query<Session>().ToListAsync();
                
                foreach (var sessionInfo in sessions)
                {
                    var availability = await CalculateSessionAvailabilityAsync(sessionInfo.Id);
                    
                    if (availability != null)
                    {
                        await _availabilityCache.UpdateSessionAvailabilityAsync(availability);
                        count++;
                    }
                }
            }
            
            _logger.LogInformation("Refreshed availability for {Count} sessions", count);
            return count;
        }

        /// <summary>
        /// Calculate session availability from the database
        /// </summary>
        /// <param name="sessionId">The session ID</param>
        /// <returns>The calculated availability</returns>
        private async Task<SessionAvailability?> CalculateSessionAvailabilityAsync(Guid sessionId)
        {
            await using var session = _documentStore.LightweightSession();
            // Get the session
            var sessionInfo = await session.Events.FetchForWriting<Session>(sessionId);
                
            if (sessionInfo == null)
            {
                _logger.LogWarning("Session {SessionId} not found", sessionId);
                return null;
            }
                
            // Count reservations
            var reservationCount = sessionInfo.Aggregate.Reservations.Count(r => r.Status != ReservationStatus.Cancelled);
                
            // Get waitlist count
            int waitlistCount = await _waitlistService.GetWaitlistCountAsync(sessionId);
                
            // Create availability object
            var availability = new SessionAvailability
            {
                SessionId = sessionId,
                TotalSeats = sessionInfo.Aggregate.TotalSeats,
                ReservedSeats = reservationCount,
                WaitlistCount = waitlistCount,
                LastUpdated = DateTimeOffset.UtcNow
            };
                
            _logger.LogDebug("Calculated availability for session {SessionId}: {AvailableSeats} of {TotalSeats} available, {WaitlistCount} on waitlist",
                sessionId, availability.AvailableSeats, availability.TotalSeats, waitlistCount);
                
            return availability;
        }
    }
}
