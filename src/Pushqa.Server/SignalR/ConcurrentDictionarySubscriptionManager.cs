using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;

namespace Pushqa.Server.SignalR
{
    /// <summary>
    /// An in-memory, non-webfarm friendly, Subscription Manager implementation
    /// </summary>
    public class ConcurrentDictionarySubscriptionManager : ISubscriptionManager
    {
        /// <summary>
        /// Subscriptions
        /// </summary>
        private static readonly ConcurrentDictionary<string, IDisposable> _Subscriptions = new ConcurrentDictionary<string, IDisposable>();

        private int _subscriptionCount;
        
        /// <summary>
        /// 
        /// </summary>
        public int SubscriptionCount { get { return _subscriptionCount; } }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        public bool TryRemoveSubscription(string connectionId, string resourceName)
        {
            IDisposable subscription;
            if (_Subscriptions.TryRemove(connectionId + ":" + resourceName, out subscription))
            {
                Interlocked.Decrement(ref _subscriptionCount);
                subscription.Dispose();
                return true;
            }
            return false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="resourceName"></param>
        /// <param name="subscription"></param>
        /// <returns></returns>
        public bool TryGetSubscription(string connectionId, string resourceName, out IDisposable subscription)
        {
            return _Subscriptions.TryGetValue(connectionId + ":" + resourceName, out subscription);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionId"></param>
        /// <param name="resourceName"></param>
        /// <param name="subscription"></param>
        public void AddOrUpdateSubscription(string connectionId, string resourceName, IDisposable subscription)
        {
            _Subscriptions.AddOrUpdate(connectionId + ":" + resourceName, subscription, (connId, origSubscription) =>
            {
                if (origSubscription != null)
                {
                    // Update
                    origSubscription.Dispose();
                }
                else
                {
                    // Insert
                    Interlocked.Increment(ref _subscriptionCount);
                }
                return subscription;
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionId"></param>
        /// <returns></returns>
        public bool TryRemoveAllSubscriptions(string connectionId)
        {
            var count = 0;
            var subscriptionKeys = _Subscriptions.ToArray().Select(x => x.Key).ToArray();
            foreach (var subscriptionKey in subscriptionKeys)
            {
                IDisposable subscription;
                if (_Subscriptions.TryRemove(subscriptionKey, out subscription))
                {
                    subscription.Dispose();
                    count++;
                }
            }

            if (count > 0)
            {
                Interlocked.Add(ref _subscriptionCount, -1 * count);
            }

            return count > 0;
        }
    }
}