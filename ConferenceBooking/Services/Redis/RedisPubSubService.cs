using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ConferenceBooking.Services.Redis
{
    /// <summary>
    /// Service for pub/sub messaging using Redis
    /// </summary>
    public class RedisPubSubService
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisPubSubService> _logger;
        private readonly ISubscriber _subscriber;

        public RedisPubSubService(IConnectionMultiplexer redis, ILogger<RedisPubSubService> logger)
        {
            _redis = redis;
            _logger = logger;
            _subscriber = redis.GetSubscriber();
        }

        /// <summary>
        /// Publish a message to a channel
        /// </summary>
        /// <typeparam name="T">The type of the message</typeparam>
        /// <param name="channel">The channel name</param>
        /// <param name="message">The message to publish</param>
        /// <returns>The number of subscribers that received the message</returns>
        public async Task<long> PublishAsync<T>(string channel, T message)
        {
            try
            {
                string json = JsonSerializer.Serialize(message);
                long subscriberCount = await _subscriber.PublishAsync(channel, json);
                
                _logger.LogDebug("Published message to channel {Channel}, received by {SubscriberCount} subscribers", 
                    channel, subscriberCount);
                    
                return subscriberCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish message to channel {Channel}", channel);
                return 0;
            }
        }

        /// <summary>
        /// Subscribe to a channel
        /// </summary>
        /// <typeparam name="T">The expected type of messages</typeparam>
        /// <param name="channel">The channel name</param>
        /// <param name="handler">The handler to invoke when messages are received</param>
        /// <returns>A ChannelMessageQueue that can be used to unsubscribe</returns>
        public Task SubscribeAsync<T>(string channel, Action<T> handler)
        {
            return _subscriber.SubscribeAsync(channel, (_, message) =>
            {
                try
                {
                    var typedMessage = JsonSerializer.Deserialize<T>(message);
                    handler(typedMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error handling message from channel {Channel}", channel);
                }
            });
        }

        /// <summary>
        /// Unsubscribe from a channel
        /// </summary>
        /// <param name="channel">The channel name</param>
        /// <returns>A task representing the asynchronous operation</returns>
        public Task UnsubscribeAsync(string channel)
        {
            return _subscriber.UnsubscribeAsync(channel);
        }

        /// <summary>
        /// Unsubscribe from all channels
        /// </summary>
        /// <returns>A task representing the asynchronous operation</returns>
        public Task UnsubscribeAllAsync()
        {
            return _subscriber.UnsubscribeAllAsync();
        }

        /// <summary>
        /// Get a channel name for a specific topic and ID
        /// </summary>
        /// <param name="topic">The topic</param>
        /// <param name="id">The ID</param>
        /// <returns>The channel name</returns>
        public static string GetChannelName(string topic, Guid id) => $"{topic}:{id}";
    }
}
