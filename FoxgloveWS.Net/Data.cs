namespace FoxgloveWS.Net;

public delegate bool TryParseBinaryDelegate<T>(byte[] bytes, out T parsed);

public sealed class Advertise
{
    public string op = "advertise";
    public Channel[] channels;
}
public sealed class AdvertisedService
{
    public string name;
    public string[] providerIds;
}
public sealed class AdvertiseServices
{
    public string op = "advertiseServices";
    public Service[] services;
}
public static class Capability
{
    public const string ClientPublish = "clientPublish";
    public const string Parameters = "parameters";
    public const string ParametersSubscribe = "parametersSubscribe";
    public const string Time = "time";
    public const string Services = "services";
    public const string ConnectionGraph = "connectionGraph";
    public const string Assets = "assets";
}
public sealed class Channel
{
    public int id;
    public string topic;
    public string encoding;
    public string schemaName;
    public string schema;
    public string schemaEncoding;
}
public sealed class ClientMessageData
{
    public byte opcode = 0x01;
    public uint channelId;
    public byte[] payload;
}
public sealed class ConnectionGraphUpdate
{
    public string op = "connectionGraphUpdate";
    public PublishedTopic[] publishedTopics;
    public SubscribedTopic[] subscribedTopics;
    public AdvertisedService[] advertisedServices;
    public string[] removedTopics;
    public string[] removedServices;
}
public static class Encoding
{
    public const string JSON = "json";
    public const string PROTOBUF = "protobuf";
    public const string ROS1 = "ros1";
    public const string CDR = "cdr";
}
public enum ServerBinaryOpcode
{
    MessageData = 1,
    Time = 2,
    ServiceCallResponse = 3,
    FetchAssetResponse = 4
}
public sealed class FetchAsset
{
    public string op = "fetchAsset";
    public string uri;
    public uint requestId;
}
public sealed class FetchAssetResponse
{
    public byte opcode = 0x04;
    public uint requestId;
    public byte status;
    public uint errorMessageLength;
    public string errorMessage;
    public byte[] data;
}
public sealed class GetParameters
{
    public string op = "getParameters";
    public string[] parameterNames;
    public string id;
}
public sealed class MessageData
{
    public byte opcode = 0x01;
    public uint subscriptionId;
    public ulong timestamp;
    public byte[] payload;
}
public sealed class Parameter
{
    public string name;
    public object value;
    public string type;
}
public sealed class ParameterValues
{
    public string op = "parameterValues";
    public Parameter[] parameters;
    public string id;
}
public sealed class PublishedTopic
{
    public string name;
    public string[] publisherIds;
}
public sealed class ServerInfo
{
    public string op = "serverInfo";
    public string name;
    public string[] capabilities;
    public string[] supportedEncodings;
    public Dictionary<string, object> metadata;
    public string sessionId;
}
public sealed class Service
{
    public int id;
    public string name;
    public string type;
    public string requestSchema;
    public string responseSchema;
}
public sealed class ServiceCallRequest
{
    public byte opcode = 0x02;
    public uint serviceId;
    public uint callId;
    public uint encodingLength;
    public string encoding;
    public byte[] payload;
}
public sealed class ServiceCallResponse
{
    public byte opcode = 0x03;
    public uint serviceId;
    public uint callId;
    public uint encodingLength;
    public string encoding;
    public byte[] payload;
}
public sealed class SetParameters
{
    public string op = "setParameters";
    public Parameter[] parameters;
    public string id;
}
public sealed class Status
{
    public string op = "status";
    public int level;
    public string message;
}
public enum StatusLevel
{
    Info = 0,
    Warning = 1,
    Error = 2
}
public sealed class Subscribe
{
    public string op = "subscribe";
    public Subscription[] subscriptions;
}
public sealed class SubscribeConnectionGraph
{
    public string op = "subscribeConnectionGraph";
}
public sealed class SubscribedTopic
{
    public string name;
    public string[] subscriberIds;
}
public sealed class SubscribeParameterUpdates
{
    public string op = "subscribeParameterUpdates";
    public string[] parameterNames;
}
public sealed class Subscription
{
    public int id;
    public int channelId;
}
public sealed class Time
{
    public byte opcode = 0x02;
    public ulong timestamp;
}
public sealed class Unadvertise
{
    public string op = "unadvertise";
    public int[] channelIds;
}
public sealed class UnadvertiseServices
{
    public string op = "unadvertiseServices";
    public int[] serviceIds;
}
public sealed class Unsubscribe
{
    public string op = "unsubscribe";
    public int[] subscriptionIds;
}
public sealed class UnsubscribeConnectionGraph
{
    public string op = "unsubscribeConnectionGraph";
}
public sealed class UnsubscribeParameterUpdates
{
    public string op = "unsubscribeParameterUpdates";
    public string[] parameterNames;
}