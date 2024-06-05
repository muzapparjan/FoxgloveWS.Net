using System.Buffers.Binary;

namespace FoxgloveWS.Net;

public static class FoxgloveBinaryUtils
{
    public static bool IsMessageData(byte[] bytes) => OpcodeIs(bytes, 0x01);
    public static bool IsTime(byte[] bytes) => OpcodeIs(bytes, 0x02);
    public static bool IsServiceCallResponse(byte[] bytes) => OpcodeIs(bytes, 0x03);
    public static bool IsFetchAssetResponse(byte[] bytes) => OpcodeIs(bytes, 0x04);
    public static MessageData ParseMessageData(byte[] bytes) => new()
    {
        subscriptionId = BinaryPrimitives.ReadUInt32LittleEndian(new(bytes, 1, 4)),
        timestamp = BinaryPrimitives.ReadUInt64LittleEndian(new(bytes, 5, 8)),
        payload = bytes[13..]
    };
    public static Time ParseTime(byte[] bytes) => new()
    {
        timestamp = BinaryPrimitives.ReadUInt64LittleEndian(new(bytes, 1, 8))
    };
    public static ServiceCallResponse ParseServiceCallResponse(byte[] bytes) => new()
    {
        serviceId = BinaryPrimitives.ReadUInt32LittleEndian(new(bytes, 1, 4)),
        callId = BinaryPrimitives.ReadUInt32LittleEndian(new(bytes, 5, 4)),
        encodingLength = BinaryPrimitives.ReadUInt32LittleEndian(new(bytes, 9, 4)),
        encoding = System.Text.Encoding.UTF8.GetString(bytes[13..(int)BinaryPrimitives.ReadUInt32LittleEndian(new(bytes, 9, 4))]),
        payload = bytes[(13 + (int)BinaryPrimitives.ReadUInt32LittleEndian(new(bytes, 9, 4)))..]
    };
    public static FetchAssetResponse ParseFetchAssetResponse(byte[] bytes) => new()
    {
        requestId = BinaryPrimitives.ReadUInt32LittleEndian(new(bytes, 1, 4)),
        status = bytes[5],
        errorMessageLength = BinaryPrimitives.ReadUInt32LittleEndian(new(bytes, 6, 4)),
        errorMessage = System.Text.Encoding.UTF8.GetString(bytes[10..(int)BinaryPrimitives.ReadUInt32LittleEndian(new(bytes, 6, 4))]),
        data = bytes[(10 + (int)BinaryPrimitives.ReadUInt32LittleEndian(new(bytes, 6, 4)))..]
    };

    public static bool TryParse<T>(byte[] bytes, Func<byte[], T> parseFunc, out T parsed)
    {
        try
        {
            parsed = parseFunc(bytes);
            return true;
        }
        catch (Exception)
        {
            parsed = default;
            return false;
        }
    }
    public static bool TryParseMessageData(byte[] bytes, out MessageData messageData) => TryParse(bytes, ParseMessageData, out messageData);
    public static bool TryParseTime(byte[] bytes, out Time time) => TryParse(bytes, ParseTime, out time);
    public static bool TryParseServiceCallResponse(byte[] bytes, out ServiceCallResponse serviceCallResponse) => TryParse(bytes, ParseServiceCallResponse, out serviceCallResponse);
    public static bool TryParseFetchAssetResponse(byte[] bytes, out FetchAssetResponse fetchAssetResponse) => TryParse(bytes, ParseFetchAssetResponse, out fetchAssetResponse);

    public static byte[] ToBytes(ClientMessageData clientMessageData)
    {
        var length = clientMessageData.payload == null ? 5 : 5 + clientMessageData.payload.Length;
        var bytes = new byte[length];
        bytes[0] = clientMessageData.opcode;
        BinaryPrimitives.WriteUInt32LittleEndian(new(bytes, 1, 4), clientMessageData.channelId);
        if (clientMessageData.payload != null)
            for (var i = 0; i < clientMessageData.payload.Length; i++)
                bytes[i + 5] = clientMessageData.payload[i];
        return bytes;
    }
    public static byte[] ToBytes(ServiceCallRequest request)
    {
        var length = 13 + request.encodingLength + (request.payload == null ? 0 : request.payload.Length);
        var bytes = new byte[length];
        bytes[0] = request.opcode;
        BinaryPrimitives.WriteUInt32LittleEndian(new(bytes, 1, 4), request.serviceId);
        BinaryPrimitives.WriteUInt32LittleEndian(new(bytes, 5, 4), request.callId);
        BinaryPrimitives.WriteUInt32LittleEndian(new(bytes, 9, 4), request.encodingLength);
        var encodingBytes = System.Text.Encoding.UTF8.GetBytes(request.encoding);
        for (var i = 0; i < encodingBytes.Length; i++)
            bytes[i + 13] = encodingBytes[i];
        for (var i = 0; i < request.payload.Length; i++)
            bytes[i + encodingBytes.Length + 13] = request.payload[i];
        return bytes;
    }

    public static bool OpcodeIs(byte[] bytes, byte opcode) => bytes != null && bytes.Length > 0 && bytes[0] == opcode;
}