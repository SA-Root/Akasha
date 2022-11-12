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
            var diUDB = new FileInfo(UserDBPath);
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

        async Task SendResponseAsync(WebSocket ws, uint code, string msg = "")
        {
            var response = new WSResponse { Code = code, Msg = msg };
            var msgResponseJson = JsonSerializer.SerializeToUtf8Bytes<WSMessage>(response);
            await ws.SendAsync(msgResponseJson, WebSocketMessageType.Binary,
                    true, CancellationToken.None);
        }
        async Task RegisterHandler(WebSocket webSocket, WSRegister msgReg)
        {
            var uid = xUserDB.NextUID++;
            xUserDB.UserDic[uid] = new UserInfo
            {
                UserName = msgReg.UserName,
                SecPassword = msgReg.SecPassword
            };
            var task = SaveUserDBAsync();
            Console.WriteLine($"[INFO]New Sign up: {msgReg.UserName}({uid})");
            await SendResponseAsync(webSocket, uid);
            await task;
        }
        async Task<uint> LoginHandler(WebSocket webSocket, WSLogin msgLogin)
        {
            var uid = msgLogin.UID;
            var pwd = msgLogin.SecPassword.ToString() ?? "";
            string tpwd;
            try
            {
                tpwd = xUserDB.UserDic[uid].SecPassword.ToString() ?? "";
            }
            catch (KeyNotFoundException)
            {
                await SendResponseAsync(webSocket, 0);
                return 0;
            }
            if (pwd == tpwd)
            {
                if (UserStateDic.ContainsKey(uid))
                {
                    UserStateDic[uid].isOnline = true;
                    UserStateDic[uid].WSConnection = webSocket;
                }
                else
                {
                    UserStateDic.TryAdd(uid, new UserState
                    {
                        isOnline = true,
                        WSConnection = webSocket
                    });
                }
                await SendResponseAsync(webSocket, uid, xUserDB.UserDic[uid].UserName);
                Console.WriteLine($"[INFO]{xUserDB.UserDic[uid].UserName}({uid}) has signed in");
                return uid;
            }
            else
            {
                await SendResponseAsync(webSocket, 0);
                return 0;
            }
        }
        async Task ChatMsgHandler(WebSocket webSocket, WSChatMessage msgChat)
        {
            if (UserStateDic.ContainsKey(msgChat.ToUID))
            {
                if (UserStateDic[msgChat.ToUID].isOnline == true)
                {
                    var msgJson = JsonSerializer.SerializeToUtf8Bytes<WSMessage>(msgChat);
                    if (UserStateDic[msgChat.ToUID].WSConnection?.State == WebSocketState.Open)
                    {
                        Task t;
                        lock (UserStateDic[msgChat.ToUID].WSSendLock)
                        {
                            t = UserStateDic[msgChat.ToUID].WSConnection.SendAsync(msgJson, WebSocketMessageType.Binary,
                                true, CancellationToken.None);
                        }
                        await t;
                    }
                }
            }
        }
        async Task ChatRequestHandler(WebSocket webSocket, WSChatRequest msgChatRequest)
        {
            if (UserStateDic.ContainsKey(msgChatRequest.FromUID) && UserStateDic.ContainsKey(msgChatRequest.ToUID))
            {
                if (UserStateDic[msgChatRequest.FromUID].isOnline == true
                    && UserStateDic[msgChatRequest.FromUID].ChatingWithUID <= 100_000
                    && UserStateDic[msgChatRequest.ToUID].isOnline == true
                    && UserStateDic[msgChatRequest.ToUID].ChatingWithUID <= 100_000)
                {
                    await SendResponseAsync(UserStateDic[msgChatRequest.ToUID].WSConnection,
                        msgChatRequest.ToUID, xUserDB.UserDic[msgChatRequest.ToUID].UserName);
                }
            }
        }
        // async Task ChatRequestResponseHandler(WSResponse msgResp, uint fuid)
        // {
        //     if (msgResp.Code > 100_000)
        //     {
        //         var respJson = JsonSerializer.SerializeToUtf8Bytes<WSMessage>(msgResp);
        //         if (UserStateDic[msgResp.Code].WSConnection?.State == WebSocketState.Open)
        //         {
        //             await UserStateDic[msgResp.Code].WSConnection.SendAsync(respJson, WebSocketMessageType.Binary, true, CancellationToken.None);
        //         }
        //         UserStateDic[fuid].ChatingWithUID = msgResp.Code;
        //         UserStateDic[msgResp.Code].ChatingWithUID = fuid;

        //     }
        // }

        async Task WSRequestHandler(HttpContext context)
        {
            if (context.WebSockets.IsWebSocketRequest)
            {
                Console.WriteLine($"[INFO]Connected to client '{GetIPAddressWithPort(context.Connection)}'");
                using var webSocket = await context.WebSockets.AcceptWebSocketAsync();

                byte[] buf = new byte[4096];
                uint uid = 0;

                while (webSocket.State == WebSocketState.Open)
                {
                    var result = await webSocket.ReceiveAsync(buf, CancellationToken.None);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        UserStateDic[uid].isOnline = false;
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None);
                        Console.WriteLine($"[INFO]Client '{GetIPAddressWithPort(context.Connection)}' terminated connection");
                    }
                    else
                    {
                        var msg = JsonSerializer.Deserialize<WSMessage>(
                            new ReadOnlySpan<byte>(buf, 0, result.Count));
                        if (msg is WSRegister msgReg)
                        {
                            await RegisterHandler(webSocket, msgReg);
                        }
                        else if (msg is WSLogin msgLogin)
                        {
                            uid = await LoginHandler(webSocket, msgLogin);
                        }
                        else if (msg is WSChatMessage msgChat)
                        {
                            await ChatMsgHandler(webSocket, msgChat);
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
            app.UseExceptionHandler(exceptionHandlerApp =>
            {
                exceptionHandlerApp.Run(async context =>
                {
                    Console.WriteLine("[ERROR]Exception thrown");
                });
            });

            app.Run("http://localhost:8888");

        }
        async Task SaveUserDBAsync() => await Task.Run(() =>
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
        });
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