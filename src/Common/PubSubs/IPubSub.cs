using Common.Dependencies;

namespace Common;

public interface IPubSub 
{
    Task SubscribeAsync(string channel, ISubscriberHandler handler);
    Task PublishAsync(string channel, object payload);
}
public interface ISubscriberHandler
{
    Task HandleAsync(string? message);
}

public interface ISubscriberRegisterHandler : ISingletonDependency
{
    Task RegisterAsync();
}