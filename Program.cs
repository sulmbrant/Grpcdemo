using Grpc.Net.Client;
using GrpcChatDemo;

var channel = GrpcChannel.ForAddress("https://localhost:5001");
var client = new Chat.ChatClient(channel);

using var chat = client.ChatStream();

var readTask = Task.Run(async () =>
{
    await foreach (var message in chat.ResponseStream.ReadAllAsync())
    {
        Console.WriteLine($"Server: {message.Text}");
    }
});

for (int i = 0; i < 5; i++)
{
    await chat.RequestStream.WriteAsync(new ChatMessage { Text = $"Hej {i}" });
    await Task.Delay(500);
}

await chat.RequestStream.CompleteAsync();
await readTask;
