namespace DS.Tools.Core.Interfaces;

/// <summary>
/// 事件聚合器接口 - 模块间松耦合通信机制
/// </summary>
public interface IEventAggregator
{
    /// <summary>
    /// 发布事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型（必须是 class）</typeparam>
    /// <param name="event">事件实例</param>
    void Publish<TEvent>(TEvent @event) where TEvent : class;

    /// <summary>
    /// 订阅事件
    /// </summary>
    /// <typeparam name="TEvent">事件类型（必须是 class）</typeparam>
    /// <param name="handler">事件处理器</param>
    /// <returns>取消订阅的令牌</returns>
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class;

    /// <summary>
    /// 订阅事件（带异常处理）
    /// </summary>
    /// <typeparam name="TEvent">事件类型（必须是 class）</typeparam>
    /// <param name="handler">事件处理器</param>
    /// <param name="errorHandler">异常处理器</param>
    /// <returns>取消订阅的令牌</returns>
    IDisposable Subscribe<TEvent>(
        Action<TEvent> handler,
        Action<Exception>? errorHandler) where TEvent : class;
}