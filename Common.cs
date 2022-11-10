namespace Akasha
{
    namespace Common.Server
    {
        public class UserState
        {
            public bool isOnline { get; set; }
            public DateTime LastPingTime { get; set; }
            public uint ChatingWithUID { get; set; }
            public WebSocket? WSConnection { get; set; }
            public UserState()
            {
                isOnline = false;
                LastPingTime = DateTime.MinValue;
                ChatingWithUID = 0;
            }
        }
        public class UserInfo
        {
            public required string UserName { get; set; }
            public required byte[] SecPassword { get; set; }
        }
        public class UserDB
        {
            public uint NextUID { get; set; }
            public ConcurrentDictionary<uint, UserInfo> UserDic { get; set; }
            public UserDB()
            {
                NextUID = 100001;
                UserDic = new();
            }
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
        public sealed class WSResponse : WSMessage
        {
            public required uint Code { get; set; }
        }
        //1
        public sealed class WSRegister : WSMessage
        {
            public required string UserName { get; set; }
            public required byte[] SecPassword { get; set; }
        }
        //2
        public sealed class WSLogin : WSMessage
        {
            public required uint UID { get; set; }
            public required byte[] SecPassword { get; set; }
        }
        //3
        public sealed class WSChatRequest : WSMessage
        {
            public required uint FromUID { get; set; }
            public required uint ToUID { get; set; }
        }
        //4
        public sealed class WSChatMessage : WSMessage
        {
            public required string FromUserName { get; set; }
            public required uint FromUID { get; set; }
            public required uint ToUID { get; set; }
            public required string Content { get; set; }
            public required string Timestamp { get; set; }
        }
        public static class Extensions
        {
            public static byte[] GetMD5(this string data) => MD5.HashData(Encoding.UTF8.GetBytes(data));
            public static string GetIPAddressWithPort(ConnectionInfo ci)
            {
                if (ci.RemoteIpAddress?.AddressFamily == AddressFamily.InterNetworkV6)
                    return $"[{ci.RemoteIpAddress}]:{ci.RemotePort}";
                else
                    return $"{ci.RemoteIpAddress}:{ci.RemotePort}";
            }
        }
    }
}