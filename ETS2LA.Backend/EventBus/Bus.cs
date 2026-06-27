using ETS2LA.Logging;
using ETS2LA.Shared;

namespace ETS2LA.Backend.Events
{
    // Named `Events` since it's nicer to write than `EventBus`. i.e.
    // Events.Current.Publish(...)
    // EventBus.Current.Publish(...)
    public class Events : IEventBus
    {
        private static readonly Lazy<Events> _instance = new(() => new Events());
        public static Events Current => _instance.Value;

        private static readonly Dictionary<string, List<Delegate>> _subscribers = new();
        private static readonly object _lock = new();

        public void Subscribe<T>(string topic, Action<T> handler)
        {
            lock (_lock)
            {
                if (!_subscribers.TryGetValue(topic, out var handlers))
                {
                    handlers = new List<Delegate>();
                    _subscribers[topic] = handlers;
                }
                handlers.Add(handler);
            }
        }

        public void Unsubscribe<T>(string topic, Action<T> handler)
        {
            lock (_lock)
            {
                if (_subscribers.TryGetValue(topic, out var handlers))
                {
                    handlers.Remove(handler);
                }
            }
        }

        public void Publish<T>(string topic, T data)
        {
            Delegate[] handlers;
            lock (_lock)
            {
                if (!_subscribers.TryGetValue(topic, out var list) || list.Count == 0)
                {
                    return;
                }
                handlers = list.ToArray();
            }

            foreach (var handler in handlers)
            {
                try
                {
                    if (handler is Action<T> action)
                    {
                        action(data);
                    }
                    else
                    {
                        handler.DynamicInvoke(data);
                    }
                } catch (Exception ex)
                {
                    Logger.Error($"Error while publishing event on topic '{topic}': {ex}");
                }
            }
        }
    }
}
