using StackExchange.Redis;
using System.Text.Json;

namespace Common;

public class RedisPubSub : IPubSub
{
    private readonly ISubscriber? _subscriber;
    private readonly string _prefix;

    public RedisPubSub(string? connectionString, string? prefix)
    {
        if (string.IsNullOrEmpty(connectionString)) return;
        var redis = ConnectionMultiplexer.Connect(connectionString);
        _subscriber = redis.GetSubscriber();
        _prefix = prefix!;
    }

    private string GetChannelName(string channel)
    {
        return $"{_prefix}_{channel}";
    }

    public Task SubscribeAsync(string channel, ISubscriberHandler handler)
    {
        if (_subscriber is null) throw new Exception("subscriber is null");
        var channelName = GetChannelName(channel);
        var redisChannel = new RedisChannel(channelName, RedisChannel.PatternMode.Auto);

        _subscriber.SubscribeAsync(redisChannel, (_, message) =>
            handler.HandleAsync(message)
        );
        Console.WriteLine("SubscribeAsync > " + channelName);
        return Task.CompletedTask;
    }

    public Task PublishAsync(string channel, object payload)
    {
        if (_subscriber is null) throw new Exception("subscriber is null");
        var channelName = GetChannelName(channel);
        JsonSerializerOptions jsonSetting = new()
        {
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            MaxDepth = 1024,
            WriteIndented = false
        };

        var serializedPayload = JsonSerializer.Serialize(payload, jsonSetting);

        var redisValue = new RedisValue(serializedPayload);

        _subscriber.PublishAsync(channelName, redisValue, CommandFlags.FireAndForget);

        Console.WriteLine("PublishAsync > " + channelName);
        return Task.CompletedTask;
    }
}
