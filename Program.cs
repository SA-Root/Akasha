namespace Akasha
{
    static class Launcher
    {
        public static void Main(string[] args)
        {

            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();

            app.UseWebSockets();
            app.Map("/ws", async context =>
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    Console.WriteLine($"Connected to '{context.Connection.RemoteIpAddress?.ToString()}'");
                    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    var rand = new Random();

                    while (true)
                    {
                        var now = DateTime.Now;
                        byte[] data = Encoding.ASCII.GetBytes($"{now}");
                        await webSocket.SendAsync(data, WebSocketMessageType.Text,
                            true, CancellationToken.None);
                        await Task.Delay(1000);

                        long r = rand.NextInt64(0, 10);

                        if (r <= 3)
                        {
                            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure,
                                "random closing", CancellationToken.None);

                            return;
                        }
                    }
                }
                else
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                }
            });

            app.Run("http://localhost:8888");

        }
    }
}