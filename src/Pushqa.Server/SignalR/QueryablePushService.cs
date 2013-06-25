using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Reactive.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.AspNet.SignalR;
using Pushqa.Communication;
using Pushqa.Infrastructure;


namespace Pushqa.Server.SignalR
{
    /// <summary>
    /// A SignalR connection that implements query semantics for oData
    /// </summary>
    public class QueryablePushService<T> : PersistentConnection where T : new()
    {
        private readonly IMessageSerializer messageSerializer;
        private readonly ISubscriptionManager subscriptionManager;

        private static readonly Logger logger = new Logger();
        private readonly T context;
        private readonly UriQueryDeserializer queryDeserializer = new UriQueryDeserializer();

        /// <summary>
        /// Initializes a new instance of the <see cref="QueryablePushService&lt;T&gt;"/> class.
        /// </summary>
        public QueryablePushService() : this(new JsonMessageSerializer(), new ConcurrentDictionarySubscriptionManager()) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="QueryablePushService&lt;T&gt;"/> class.
        /// </summary>
        /// <param name="messageSerializer">The message serializer to use. The default is the <see cref="JsonMessageSerializer"/></param>
        /// <param name="subscriptionManager">The subscription manager to use. The default is the <see cref="ConcurrentDictionarySubscriptionManager" /></param>
        public QueryablePushService(IMessageSerializer messageSerializer, ISubscriptionManager subscriptionManager)
        {
            VerifyArgument.IsNotNull("messageSerializer", messageSerializer);
            VerifyArgument.IsNotNull("subscriptionManager", subscriptionManager);

            this.messageSerializer = messageSerializer;
            this.subscriptionManager = subscriptionManager;

            context = new T();
        }

        /// <summary>
        /// Creates the subscription.
        /// </summary>
        /// <typeparam name="TItemType">The type of the item type.</typeparam>
        /// <param name="qbservable">The qbservable.</param>
        /// <param name="clientId">The client id.</param>
        /// <param name="resourceName"></param>
        /// <returns></returns>
        protected virtual IDisposable CreateSubscription<TItemType>(IQbservable<TItemType> qbservable, string clientId, string resourceName)
        {
            return qbservable.Subscribe(x =>
            {
                try
                {
                    Connection.Send(clientId, new EventWrapper<TItemType> { Resource = resourceName, Message = x, Type = EventWrapper<TItemType>.EventType.Message });
                }
                catch (Exception exception)
                {
                    // How should we handle send exceptions like serialization etc? Error the stream of ignore the poison message?
                    logger.Log(Logger.LogLevel.Error, exception, "Error sending message");
                }
            },
            ex =>
            {
                try
                {
                    Connection.Send(clientId, new EventWrapper<TItemType> { Resource = resourceName, ErrorMessage = ex.Message, Type = EventWrapper<TItemType>.EventType.Error });
                }
                catch (Exception exception)
                {
                    // How should we handle send exceptions like serialization etc? Error the stream of ignore the poison message?
                    logger.Log(Logger.LogLevel.Error, exception, "Error sending message");
                }
            }
            , () =>
            {
                try
                {
                    Connection.Send(clientId, new EventWrapper<TItemType> { Resource = resourceName, Type = EventWrapper<TItemType>.EventType.Completed });
                }
                catch (Exception exception)
                {
                    logger.Log(Logger.LogLevel.Error, exception, "Error sending complete message");
                }
            });
        }

        /// <summary>
        /// Occurs when the client disconnects
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="connectionId">The connection id.</param>
        /// <returns></returns>
        protected override Task OnDisconnected(IRequest request, string connectionId)
        {
            subscriptionManager.TryRemoveAllSubscriptions(connectionId);
            logger.Log(Logger.LogLevel.Debug, string.Format("Client {0} has disconnected from server. Number of remaining connected clients {1}", connectionId, subscriptionManager.SubscriptionCount));
            return base.OnDisconnected(request, connectionId);
        }

        /// <summary>
        /// Occurs when the client sends updated filter criteria
        /// </summary>
        /// <param name="request"></param>
        /// <param name="connectionId"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        protected override Task OnReceived(IRequest request, string connectionId, string data)
        {
            return Task.Factory.StartNew(() =>
            {
                if (request.Url == null)
                {
                    return;
                }

                var uri = new UriBuilder(request.Url);
                uri.Path = uri.Path.Replace("/send", "");

                string resourceName;
                string filter = null;

                if (data != null && data.Contains(";;;"))
                {
                    var parts = data.Split(new[] { ";;;" }, StringSplitOptions.None);
                    if (parts.Length < 2)
                    {
                        return;
                    }
                    resourceName = parts[0];
                    filter = parts[1];
                }
                else
                {
                    resourceName = data ?? string.Empty;
                }

                logger.Log(Logger.LogLevel.Debug, "{0}:{1}={2}", connectionId, resourceName, filter ?? "Unfiltered");
                uri.Path += "/" + resourceName;
                var queryItems = HttpUtility.ParseQueryString(uri.Query);

                if (filter != null)
                {
                    queryItems["$filter"] = filter.Replace(" ", "+");
                }

                var newQueryBuilder = new StringBuilder();
                foreach (var key in queryItems.AllKeys)
                {
                    newQueryBuilder.AppendFormat("{0}={1}&", HttpUtility.UrlEncode(key), HttpUtility.UrlEncode(queryItems[key]));
                }
                if (newQueryBuilder.Length > 0)
                {
                    newQueryBuilder.Length -= 1;
                    uri.Query = newQueryBuilder.ToString();
                }
                else
                {
                    uri.Query = string.Empty;
                }

                PropertyInfo propertyInfo = typeof(T).GetProperty(resourceName);
                if (propertyInfo == null)
                {
                    throw new NotImplementedException("Need exception type");
                }

                Type messageType = propertyInfo.PropertyType.GetInterfaces().Concat(new[] { propertyInfo.PropertyType }).Where(
                    iface => iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IQbservable<>)).Select(iface => iface.GetGenericArguments()[0]).FirstOrDefault();

                if (messageType == null)
                {
                    throw new NotImplementedException("Need exception type");
                }

                IQbservable qbservable = queryDeserializer.Deserialize(propertyInfo.GetValue(context, null) as IQbservable, messageType, uri.Uri);

                Func<IQbservable<int>, string, string, IDisposable> dummyCreateSubscription = CreateSubscription;

                var subscription = dummyCreateSubscription.Method.GetGenericMethodDefinition().MakeGenericMethod(new[] { messageType }).Invoke(this, new object[] { qbservable, connectionId, resourceName }) as IDisposable;

                subscriptionManager.AddOrUpdateSubscription(connectionId, resourceName, subscription);

                logger.Log(Logger.LogLevel.Debug, string.Format("New client {0} connected to server. Total connected clients {1}", connectionId, subscriptionManager.SubscriptionCount));
            });
        }
    }
}

