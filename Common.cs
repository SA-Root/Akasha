namespace Akasha
{
    namespace Common.Server
    {
        public class UserInfo
        {
            public string? UserName { get; set; }
            public uint UID { get; set; }
            public byte[]? SecPassword { get; set; }
            public bool isOnline { get; set; }
            public DateTime LastPingTime { get; set; }
            public uint ChatingWithUID { get; set; }
            [JsonIgnore]
            public WebSocket? WSConnection { get; set; }
        }
    }
    namespace Common
    {
        [JsonDerivedType(typeof(WSMessage), typeDiscriminator: "base")]
        [JsonDerivedType(typeof(WSRegister), typeDiscriminator: "MsgRegister")]
        [JsonDerivedType(typeof(WSLogin), typeDiscriminator: "MsgLogin")]
        [JsonDerivedType(typeof(WSResponse), typeDiscriminator: "MsgResponse")]
        [JsonDerivedType(typeof(WSChatRequest), typeDiscriminator: "MsgChatRequest")]
        [JsonDerivedType(typeof(WSChatMessage), typeDiscriminator: "MsgChatMessage")]
        public class WSMessage
        {

        }
        public sealed class WSRegister : WSMessage
        {
            public string? UserName { get; set; }
            public uint UID { get; set; }
            public byte[]? SecPassword { get; set; }
        }
        public sealed class WSLogin : WSMessage
        {
            public uint UID { get; set; }
            public byte[]? SecPassword { get; set; }
        }
        public sealed class WSResponse : WSMessage
        {
            public uint Code { get; set; }
        }
        public sealed class WSChatRequest : WSMessage
        {
            public uint FromUID { get; set; }
            public uint ToUID { get; set; }
        }
        public sealed class WSChatMessage : WSMessage
        {
            public string? FromUserName { get; set; }
            public uint FromUID { get; set; }
            public uint ToUID { get; set; }
            public string? Content { get; set; }
            public string? Timestamp { get; set; }
        }
        public static class Extensions
        {
            public static byte[] GetMD5(this string data) => MD5.HashData(Encoding.UTF8.GetBytes(data));
        }
    }
}