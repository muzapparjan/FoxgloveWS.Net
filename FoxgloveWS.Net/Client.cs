using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.WebSockets;

namespace FoxgloveWS.Net
{
    public sealed class FoxgloveWSClient : IDisposable
    {
        private readonly string[] subProtocols = new string[] { "foxglove.websocket.v1" };
        private readonly HashSet<string> supportedEncodings = new() { "json", "protobuf" };
        private readonly Dictionary<string, Action<JObject>> operationHandlers = new();

        public event Action<ServerInfo> OnReceivedServerInfo;
        public event Action<Status> OnReceivedStatus;
        public event Action<Advertise> OnReceivedAdvertise;
        public event Action<Unadvertise> OnReceivedUnadvertise;
        public event Action<ParameterValues> OnReceivedParameterValues;
        public event Action<AdvertiseServices> OnReceivedAdvertiseServices;
        public event Action<UnadvertiseServices> OnReceivedUnadvertiseServices;
        public event Action<ConnectionGraphUpdate> OnReceivedConnectionGraphUpdate;

        public event Action<MessageData> OnReceivedMessageData;
        public event Action<Time> OnReceivedTime;
        public event Action<ServiceCallResponse> OnReceivedServiceCallResponse;
        public event Action<FetchAssetResponse> OnReceivedFetchAssetResponse;

        public event Action<Channel> OnServerChannelAdded;
        public event Action<Channel> OnServerChannelRemoved;
        public event Action<Service> OnServerServiceAdded;
        public event Action<Service> OnServerServiceRemoved;

        public event Action<PublishedTopic> OnServerPublishedTopic;
        public event Action<SubscribedTopic> OnServerSubscribedTopic;
        public event Action<AdvertisedService> OnServerAdvertisedService;
        public event Action<PublishedTopic> OnServerRemovedPublishedTopic;
        public event Action<SubscribedTopic> OnServerRemovedSubscribedTopic;
        public event Action<AdvertisedService> OnServerRemovedAdvertisedServices;

        public string Host { get; private set; }
        public string Path { get; private set; }
        public int Port { get; private set; }

        public ServerInfo Server => serverInfo;
        public ulong Timestamp => serverTimestamp;
        public HashSet<Channel> Channels => serverChannels;
        public HashSet<Service> Services => serverServices;

        public Dictionary<string, PublishedTopic> PublishedTopics => publishedTopics;
        public Dictionary<string, SubscribedTopic> SubscribedTopics => subscribedTopics;
        public Dictionary<string, AdvertisedService> AdvertisedServices => advertisedServices;
        public Dictionary<int, Channel> ClientAdvertisedChannels => clientAdvertisedChannels;

        private ClientWebSocket ws;
        private ServerInfo serverInfo;
        private ulong serverTimestamp;
        private readonly HashSet<Channel> serverChannels = new();
        private readonly HashSet<Service> serverServices = new();
        private readonly object serverLock = new();

        private readonly Dictionary<string, PublishedTopic> publishedTopics = new();
        private readonly Dictionary<string, SubscribedTopic> subscribedTopics = new();
        private readonly Dictionary<string, AdvertisedService> advertisedServices = new();
        private readonly Dictionary<int, Channel> clientAdvertisedChannels = new();
        private readonly object connectionLock = new();

        public FoxgloveWSClient(string host, int port, string path = "")
        {
            Host = host;
            Path = path;
            Port = port;
            Reset();
            InitializeOperationHandlers();
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default, int timeout = 10000)
        {
            if (ws != null && ws.State == WebSocketState.Open)
                throw new WebSocketException($"[FoxgloveWSClient.ConnectAsync]: already connected to {Host}: {Port}");
            Reset();
            ws = new();
            if (subProtocols != null)
                foreach (var protocol in subProtocols)
                    ws.Options.AddSubProtocol(protocol);
            var uri = new UriBuilder()
            {
                Scheme = "ws",
                Port = Port,
                Host = Host,
                Path = Path,
            }.Uri;
            await Task.WhenAny(ws.ConnectAsync(uri, cancellationToken), Task.Delay(timeout, cancellationToken));
            if (ws.State != WebSocketState.Open)
            {
                Reset();
                throw new WebSocketException($"[FoxgloveWSClient.ConnectAsync]: failed to connect to {uri}");
            }
        }
        public async Task<WebSocketReceiveResult> ReceiveAsync(byte[] buffer, CancellationToken cancellationToken = default)
        {
            return await ws.ReceiveAsync(buffer, cancellationToken);
        }
        public bool ProcessMessage(WebSocketReceiveResult result, byte[] buffer)
        {
            switch (result.MessageType)
            {
                case WebSocketMessageType.Text:
                    ProcessTextMessage(buffer);
                    break;
                case WebSocketMessageType.Binary:
                    ProcessBinaryMessage(buffer);
                    break;
                case WebSocketMessageType.Close:
                    return true;
                default:
                    throw new WebSocketException("[FoxgloveWSClient.ConnectAsync]: unknown WebSocket Message Type");
            }
            return false;
        }
        public async Task SendAsync(ArraySegment<byte> buffer, WebSocketMessageType type, bool endOfMessage, CancellationToken cancellationToken = default)
        {
            await ws.SendAsync(buffer, type, endOfMessage, cancellationToken);
        }
        public async Task SendTextAsync(object obj, CancellationToken cancellationToken = default)
        {
            var settings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };
            var json = JsonConvert.SerializeObject(obj, settings);
            await SendTextAsync(json, cancellationToken);
        }
        public async Task SendTextAsync(string text, CancellationToken cancellationToken = default)
        {
            var buffer = System.Text.Encoding.UTF8.GetBytes(text);
            await SendAsync(buffer, WebSocketMessageType.Text, true, cancellationToken);
        }

        public async Task SendSubscribeAsync(int id, int channelId, CancellationToken cancellationToken = default)
        {
            var subscription = new Subscription()
            {
                id = id,
                channelId = channelId,
            };
            var subscribe = new Subscribe()
            {
                subscriptions = new Subscription[] { subscription }
            };
            await SendTextAsync(subscribe, cancellationToken);
        }
        public async Task SendSubscribeAsync(CancellationToken cancellationToken = default, params Subscription[] subscriptions)
        {
            if (subscriptions == null || subscriptions.Length == 0)
                throw new ArgumentNullException("[FoxgloveWSClient.SendSubscribeAsync]: should at least have one subscription");
            var subscribe = new Subscribe()
            {
                subscriptions = subscriptions
            };
            await SendTextAsync(subscribe, cancellationToken);
        }
        public async Task SendUnsubscribeAsync(int subscriptionId, CancellationToken cancellationToken = default)
        {
            var unsubscribe = new Unsubscribe()
            {
                subscriptionIds = new int[] { subscriptionId }
            };
            await SendTextAsync(unsubscribe, cancellationToken);
        }
        public async Task SendUnsubscribeAsync(CancellationToken cancellationToken = default, params int[] subscriptionIds)
        {
            if (subscriptionIds == null || subscriptionIds.Length == 0)
                throw new ArgumentNullException("[FoxgloveWSClient.SendUnsubscribeAsync]: should at least have one subscription id");
            var unsubscribe = new Unsubscribe()
            {
                subscriptionIds = subscriptionIds
            };
            await SendTextAsync(unsubscribe, cancellationToken);
        }
        public async Task SendClientAdvertiseAsync(CancellationToken cancellationToken = default, params Channel[] channels)
        {
            if (channels == null || channels.Length == 0)
                throw new ArgumentNullException("[FoxgloveWSClient.SendClientAdvertiseAsync]: should at least have one channel");
            var advertise = new Advertise()
            {
                channels = channels
            };
            lock (connectionLock)
            {
                foreach (var channel in channels)
                    clientAdvertisedChannels[channel.id] = channel;
            }
            await SendTextAsync(advertise, cancellationToken);
        }
        public async Task SendClientUnadvertiseAsync(CancellationToken cancellationToken = default, params int[] channelIds)
        {
            if (channelIds == null || channelIds.Length == 0)
                throw new ArgumentNullException("[FoxgloveWSClient.SendClientUnadvertiseAsync]: should at least have one channel id");
            var unadvertise = new Unadvertise()
            {
                channelIds = channelIds
            };
            lock (connectionLock)
            {
                foreach (var id in channelIds)
                    clientAdvertisedChannels.Remove(id);
            }
            await SendTextAsync(unadvertise, cancellationToken);
        }
        public async Task SendGetParametersAsync(string id = null, CancellationToken cancellationToken = default, params string[] parameterNames)
        {
            if (parameterNames == null || parameterNames.Length == 0)
                throw new ArgumentNullException("[FoxgloveWSClient.SendGetParametersAsync]: should at least have one parameter name");
            var request = new GetParameters()
            {
                id = id,
                parameterNames = parameterNames
            };
            await SendTextAsync(request, cancellationToken);
        }
        public async Task SendSetParametersAsync(string id = null, CancellationToken cancellationToken = default, params Parameter[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
                throw new ArgumentNullException("[FoxgloveWSClient.SetParametersAsync]: should at least have one parameter");
            var request = new SetParameters()
            {
                id = id,
                parameters = parameters
            };
            await SendTextAsync(request, cancellationToken);
        }
        public async Task SendSubscribeParameterUpdatesAsync(CancellationToken cancellationToken = default, params string[] parameterNames)
        {
            if (parameterNames == null || parameterNames.Length == 0)
                throw new ArgumentNullException("[FoxgloveWSClient.SendSubscribeParameterUpdatesAsync]: should at least have one parameter name");
            var request = new SubscribeParameterUpdates()
            {
                parameterNames = parameterNames
            };
            await SendTextAsync(request, cancellationToken);
        }
        public async Task SendUnsubscribeParameterUpdatesAsync(CancellationToken cancellationToken = default, params string[] parameterNames)
        {
            if (parameterNames == null || parameterNames.Length == 0)
                throw new ArgumentNullException("[FoxgloveWSClient.SendUnsubscribeParameterUpdatesAsync]: should at least have one parameter name");
            var request = new UnsubscribeParameterUpdates()
            {
                parameterNames = parameterNames
            };
            await SendTextAsync(request, cancellationToken);
        }
        public async Task SendSubscribeConnectionGraphUpdatesAsync(CancellationToken cancellationToken = default)
        {
            var request = new SubscribeConnectionGraph();
            await SendTextAsync(request, cancellationToken);
        }
        public async Task SendUnsubscribeConnectionGraphUpdatesAsync(CancellationToken cancellationToken = default)
        {
            var request = new UnsubscribeConnectionGraph();
            await SendTextAsync(request, cancellationToken);
        }
        public async Task SendFetchAssetAsync(string uri, uint requestId, CancellationToken cancellationToken = default)
        {
            var request = new FetchAsset()
            {
                uri = uri,
                requestId = requestId,
            };
            await SendTextAsync(request, cancellationToken);
        }

        public async Task SendClientMessageDataAsync(uint channelId, byte[] payload, CancellationToken cancellationToken = default)
        {
            var data = new ClientMessageData()
            {
                channelId = channelId,
                payload = payload
            };
            var bytes = FoxgloveBinaryUtils.ToBytes(data);
            await SendAsync(bytes, WebSocketMessageType.Binary, true, cancellationToken);
        }
        public async Task SendServiceCallRequestAsync(uint serviceId, uint callId, string encoding, byte[] payload, CancellationToken cancellationToken = default)
        {
            var request = new ServiceCallRequest()
            {
                serviceId = serviceId,
                callId = callId,
                encodingLength = (uint)encoding.Length,
                encoding = encoding,
                payload = payload
            };
            var bytes = FoxgloveBinaryUtils.ToBytes(request);
            await SendAsync(bytes, WebSocketMessageType.Binary, true, cancellationToken);
        }

        public void Dispose() => ws?.Dispose();

        public Channel FindChannelByTopic(string topic)
        {
            return Channels.Where(channel => channel.topic == topic).FirstOrDefault();
        }
        public Channel FindChannelById(int id)
        {
            return Channels.Where(channel => channel.id == id).FirstOrDefault();
        }

        private void InitializeOperationHandlers()
        {
            operationHandlers["serverInfo"] = json => HandleOperation<ServerInfo>(json, HandleServerInfo);
            operationHandlers["status"] = json => HandleOperation<Status>(json, HandleStatus);
            operationHandlers["advertise"] = json => HandleOperation<Advertise>(json, HandleAdvertise);
            operationHandlers["unadvertise"] = json => HandleOperation<Unadvertise>(json, HandleUnadvertise);
            operationHandlers["parameterValues"] = json => HandleOperation<ParameterValues>(json, HandleParameterValues);
            operationHandlers["advertiseServices"] = json => HandleOperation<AdvertiseServices>(json, HandleAdvertiseServices);
            operationHandlers["unadvertiseServices"] = json => HandleOperation<UnadvertiseServices>(json, HandleUnadvertiseServices);
            operationHandlers["connectionGraphUpdate"] = json => HandleOperation<ConnectionGraphUpdate>(json, HandleConnectionGraphUpdate);
        }
        private void Reset()
        {
            ws?.Dispose();
            ws = null;
            serverInfo = null;
            serverTimestamp = 0;
            serverChannels.Clear();
            serverServices.Clear();
        }

        private void ProcessTextMessage(byte[] buffer)
        {
            var jsonString = System.Text.Encoding.UTF8.GetString(buffer);
            var json = JObject.Parse(jsonString);
            if (!json.ContainsKey("op"))
                throw new WebSocketException($"[FoxgloveWSClient.ProcessTextMessage]: unknown text message:\n{json}");
            var operation = json.Value<string>("op");
            if (operationHandlers.TryGetValue(operation, out var handler))
                handler?.Invoke(json);
            else
                throw new WebSocketException($"[FoxgloveWSClient.ProcessTextMessage]: failed to handle message with unknown operation: {operation}");
        }
        private void ProcessBinaryMessage(byte[] buffer)
        {
            if (buffer == null || buffer.Length < 1)
                throw new WebSocketException($"[FoxgloveWSClient.ProcessBinaryMessage]: empty binary message");
            var opcode = buffer[0];
            switch ((int)opcode)
            {
                case (int)ServerBinaryOpcode.MessageData:
                    HandleBinary<MessageData>(buffer, FoxgloveBinaryUtils.IsMessageData, FoxgloveBinaryUtils.TryParseMessageData, HandleMessageData);
                    break;
                case (int)ServerBinaryOpcode.Time:
                    HandleBinary<Time>(buffer, FoxgloveBinaryUtils.IsTime, FoxgloveBinaryUtils.TryParseTime, HandleTime);
                    break;
                case (int)ServerBinaryOpcode.ServiceCallResponse:
                    HandleBinary<ServiceCallResponse>(buffer, FoxgloveBinaryUtils.IsServiceCallResponse, FoxgloveBinaryUtils.TryParseServiceCallResponse, HandleServiceCallResponse);
                    break;

                case (int)ServerBinaryOpcode.FetchAssetResponse:
                    HandleBinary<FetchAssetResponse>(buffer, FoxgloveBinaryUtils.IsFetchAssetResponse, FoxgloveBinaryUtils.TryParseFetchAssetResponse, HandleFetchAssetResponse);
                    break;

                default:
                    throw new WebSocketException($"[FoxgloveWSClient.ProcessBinaryMessage]: failed to handle binary message with unknown opcode: {opcode}");
            }
        }

        private void HandleOperation<T>(JObject json, Action<T> handler) => handler?.Invoke(json.ToObject<T>());
        private void HandleBinary<T>(byte[] buffer, Func<byte[], bool> validator, TryParseBinaryDelegate<T> parser, Action<T> handler)
        {
            if (!validator(buffer))
                throw new WebSocketException("[FoxgloveWSClient.HandleBinary]: failed to validate a binary message");
            if (!parser(buffer, out var parsed))
                throw new WebSocketException("[FoxgloveWSClient.HandleBinary]: failed to parse a binary message");
            handler(parsed);
        }

        private void HandleServerInfo(ServerInfo serverInfo)
        {
            lock (serverLock)
            {
                this.serverInfo = serverInfo;
            }
            OnReceivedServerInfo?.Invoke(serverInfo);
        }
        private void HandleStatus(Status status)
        {
            OnReceivedStatus?.Invoke(status);
        }
        private void HandleAdvertise(Advertise advertise)
        {
            lock (serverLock)
            {
                foreach (var channel in advertise.channels)
                    if (!serverChannels.Any(existingChannel => existingChannel.id == channel.id))
                    {
                        serverChannels.Add(channel);
                        OnServerChannelAdded?.Invoke(channel);
                    }
            }
            OnReceivedAdvertise?.Invoke(advertise);
        }
        private void HandleUnadvertise(Unadvertise unadvertise)
        {
            lock (serverLock)
            {
                foreach (var channelId in unadvertise.channelIds)
                {
                    var channel = serverChannels.Where(channel => channel.id == channelId).FirstOrDefault();
                    if (channel != null)
                    {
                        serverChannels.Remove(channel);
                        OnServerChannelRemoved?.Invoke(channel);
                    }
                }
            }
            OnReceivedUnadvertise?.Invoke(unadvertise);
        }
        private void HandleParameterValues(ParameterValues parameters)
        {
            OnReceivedParameterValues?.Invoke(parameters);
        }
        private void HandleAdvertiseServices(AdvertiseServices services)
        {
            lock (serverLock)
            {
                foreach (var service in services.services)
                    if (!serverServices.Any(existingService => existingService.id == service.id))
                    {
                        serverServices.Add(service);
                        OnServerServiceAdded?.Invoke(service);
                    }
            }
            OnReceivedAdvertiseServices?.Invoke(services);
        }
        private void HandleUnadvertiseServices(UnadvertiseServices unadvertiseServices)
        {
            lock (serverLock)
            {
                foreach (var serviceId in unadvertiseServices.serviceIds)
                {
                    var service = serverServices.Where(service => service.id == serviceId).FirstOrDefault();
                    if (service != null)
                    {
                        serverServices.Remove(service);
                        OnServerServiceRemoved?.Invoke(service);
                    }
                }
            }
            OnReceivedUnadvertiseServices?.Invoke(unadvertiseServices);
        }
        private void HandleConnectionGraphUpdate(ConnectionGraphUpdate connectionGraphUpdate)
        {
            lock (connectionLock)
            {
                foreach (var topic in connectionGraphUpdate.publishedTopics)
                {
                    if (!publishedTopics.ContainsKey(topic.name))
                    {
                        publishedTopics[topic.name] = topic;
                        OnServerPublishedTopic?.Invoke(topic);
                    }
                }
                foreach (var topic in connectionGraphUpdate.subscribedTopics)
                {
                    if (!subscribedTopics.ContainsKey(topic.name))
                    {
                        subscribedTopics[topic.name] = topic;
                        OnServerSubscribedTopic?.Invoke(topic);
                    }
                }
                foreach (var service in connectionGraphUpdate.advertisedServices)
                {
                    if (!advertisedServices.ContainsKey(service.name))
                    {
                        advertisedServices[service.name] = service;
                        OnServerAdvertisedService?.Invoke(service);
                    }
                }
                foreach (var topicName in connectionGraphUpdate.removedTopics)
                {
                    if (publishedTopics.Remove(topicName, out var publishedTopic))
                    {
                        OnServerRemovedPublishedTopic?.Invoke(publishedTopic);
                        if (subscribedTopics.Remove(topicName, out var subscribedTopic))
                            OnServerRemovedSubscribedTopic?.Invoke(subscribedTopic);
                    }
                }
                foreach (var serviceName in connectionGraphUpdate.removedServices)
                {
                    if (advertisedServices.Remove(serviceName, out var advertisedService))
                        OnServerRemovedAdvertisedServices?.Invoke(advertisedService);
                }
            }
            OnReceivedConnectionGraphUpdate?.Invoke(connectionGraphUpdate);
        }
        private void HandleMessageData(MessageData messageData)
        {
            OnReceivedMessageData?.Invoke(messageData);
        }
        private void HandleTime(Time time)
        {
            serverTimestamp = Math.Max(serverTimestamp, time.timestamp);
            OnReceivedTime?.Invoke(time);
        }
        private void HandleServiceCallResponse(ServiceCallResponse serviceCallResponse)
        {
            OnReceivedServiceCallResponse?.Invoke(serviceCallResponse);
        }
        private void HandleFetchAssetResponse(FetchAssetResponse fetchAssetResponse)
        {
            OnReceivedFetchAssetResponse?.Invoke(fetchAssetResponse);
        }
    }
}