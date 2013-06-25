using System;

namespace Pushqa.Server.SignalR
{
    /// <summary>
    /// 
    /// </summary>
    public interface ISubscriptionManager
    {
        /// <summary>
        /// 
        /// </summary>
        int SubscriptionCount { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        bool TryRemoveSubscription(string connectionId, string resourceName);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="resourceName"></param>
        /// <param name="subscription"></param>
        /// <returns></returns>
        bool TryGetSubscription(string connectionId, string resourceName, out IDisposable subscription);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="resourceName"></param>
        /// <param name="subscription"></param>
        void AddOrUpdateSubscription(string connectionId, string resourceName, IDisposable subscription);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionId"></param>
        bool TryRemoveAllSubscriptions(string connectionId);
    }
}