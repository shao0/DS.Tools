using DS.Tools.Core.Interfaces;

namespace DS.Tools.Core.Infrastructure;

/// <summary>
/// 事件聚合器实现 - 基于标准 .NET 委托的发布/订阅（无 ReactiveUI 依赖）
/// AOT 兼容，无运行时反射
/// </summary>
public sealed class EventAggregator : IEventAggregator, IDisposable
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = new();
    private readonly Lock _lock = new();
    private bool _isDisposed;

    /// <summary>
    /// 发布事件到所有订阅者
    /// </summary>
    public void Publish<TEvent>(TEvent @event) where TEvent : class
    {
        ObjectDisposedException.ThrowIf(_isDisposed, typeof(EventAggregator));

        ArgumentNullException.ThrowIfNull(@event);

        // 在锁内拍快照，避免回调中修改集合导致迭代异常
        List<Delegate> snapshot;
        lock (_lock)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list))
                return;

            snapshot = new List<Delegate>(list);
        }

        foreach (var handler in snapshot)
        {
            if (handler is Action<TEvent> typed)
            {
                typed(@event);
            }
        }
    }

    /// <summary>
    /// 订阅事件
    /// </summary>
    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class
        => SubscribeInternal(handler, errorHandler: null);

    /// <summary>
    /// 订阅事件（带异常处理）
    /// </summary>
    public IDisposable Subscribe<TEvent>(
        Action<TEvent> handler,
        Action<Exception>? errorHandler) where TEvent : class
        => SubscribeInternal(handler, errorHandler);

    /// <summary>
    /// 内部订阅实现
    /// </summary>
    private IDisposable SubscribeInternal<TEvent>(
        Action<TEvent> handler,
        Action<Exception>? errorHandler) where TEvent : class
    {
        ObjectDisposedException.ThrowIf(_isDisposed, typeof(EventAggregator));

        ArgumentNullException.ThrowIfNull(handler);

        // 若提供错误处理器，则包装回调以捕获异常
        Action<TEvent> wrapped = errorHandler is null
            ? handler
            : e =>
            {
                try
                {
                    handler(e);
                }
                catch (Exception ex)
                {
                    errorHandler(ex);
                }
            };

        var key = typeof(TEvent);
        lock (_lock)
        {
            if (!_handlers.TryGetValue(key, out var list))
            {
                list = new List<Delegate>();
                _handlers[key] = list;
            }

            list.Add(wrapped);
        }

        return new Unsubscriber<TEvent>(this, wrapped);
    }

    /// <summary>
    /// 取消订阅
    /// </summary>
    private void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : class
    {
        lock (_lock)
        {
            if (_handlers.TryGetValue(typeof(TEvent), out var list))
            {
                list.Remove(handler);
                if (list.Count == 0)
                {
                    _handlers.Remove(typeof(TEvent));
                }
            }
        }
    }

    /// <summary>
    /// 释放资源
    /// </summary>
    public void Dispose()
    {
        if (_isDisposed)
            return;

        lock (_lock)
        {
            _handlers.Clear();
        }

        _isDisposed = true;
    }

    /// <summary>
    /// 取消订阅令牌
    /// </summary>
    private sealed class Unsubscriber<TEvent>(EventAggregator owner, Action<TEvent> handler) : IDisposable
        where TEvent : class
    {
        public void Dispose() => owner.Unsubscribe(handler);
    }
}
