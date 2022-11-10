namespace Akasha
{
    class Host
    {
        string UserDBPath = "UserDB.json";
        UserDB xUserDB { get; set; }
        ConcurrentDictionary<uint, UserState> UserStateDic { get; set; }
        WebApplication app { get; set; }
        object UserDBFileWriteLock = new();
        public Host()
        {
            LoadUserDB();
        }
        void LoadUserDB()
        {
            var diUDB = new DirectoryInfo(UserDBPath);
            if (diUDB.Exists)
            {
                using var sUDB = File.Open(diUDB.FullName, FileMode.Open);
                xUserDB = JsonSerializer.Deserialize<UserDB>(sUDB) ?? new();
            }
            else
            {
                xUserDB = new();
            }
            UserStateDic = new();
            foreach (var u in xUserDB.UserDic.Keys)
            {
                UserStateDic.TryAdd(u, new UserState());
            }
        }
        async Task WSRequestHandler(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                Console.WriteLine($"[INFO]Connected to client '{GetIPAddressWithPort(context.Connection)}'");
                using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                var rand = new Random();

                byte[] buf = new byte[4096];

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(buf, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                        Console.WriteLine($"[INFO]Client '{GetIPAddressWithPort(context.Connection)}' terminated connection");
                    }
                    else
                    {
                        var msg = JsonSerializer.Deserialize<WSMessage>(
                            new ReadOnlySpan<byte>(buf, 0, result.Count));
                        if (msg is WSRegister msgReg)
                        {
                            xUserDB.UserDic[xUserDB.NextUID++] = new UserInfo
                            {
                                UserName = msgReg.UserName,
                                SecPassword = msgReg.SecPassword
                            };
                            SaveUserDB();
                        }
                    }
                }
            }
            else
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            }
        }
        public void Activate(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);
            app = builder.Build();

            app.UseWebSockets();
            app.Map("/ws", WSRequestHandler);

            app.Run("http://localhost:8888");

        }
        void SaveUserDB()
        {
            lock (UserDBFileWriteLock)
            {
                var UserDBJson = JsonSerializer.SerializeToUtf8Bytes<UserDB>(xUserDB,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });
                using var fsUserDB = File.Open(UserDBPath, FileMode.Create);
                fsUserDB.Write(UserDBJson);
            }
        }
    }
    static class Launcher
    {
        public static void Main(string[] args)
        {
            var h = new Host();
            h.Activate(args);
        }
    }
}