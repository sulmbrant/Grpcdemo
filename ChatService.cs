using Grpc.Core;
using GrpcChatDemo;
using System.Threading.Channels;

public class ChatService : Chat.ChatBase
{
    public override async Task ChatStream(
        IAsyncStreamReader<ChatMessage> requestStream,
        IServerStreamWriter<ChatMessage> responseStream,
        ServerCallContext context)
    {
        var queue = new ReliableMessageQueue<ChatMessage>(
            async (msg) =>
            {
                await responseStream.WriteAsync(msg);
                return true;
            });

        queue.StartProcessing(context.CancellationToken);

        await foreach (var message in requestStream.ReadAllAsync(context.CancellationToken))
        {
            Console.WriteLine($"Client: {message.Text}");
            await queue.EnqueueAsync(new ChatMessage { Text = $"Echo: {message.Text}" });
        }
    }
}

public class ReliableMessageQueue<T>
{
    private readonly Channel<T> _channel;
    private readonly Func<T, Task<bool>> _handler;
    private readonly TimeSpan _retryDelay;

    public ReliableMessageQueue(Func<T, Task<bool>> handler, int capacity = 100, TimeSpan? retryDelay = null)
    {
        _channel = Channel.CreateBounded<T>(capacity);
        _handler = handler;
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(1);
    }

    public async Task EnqueueAsync(T item)
    {
        await _channel.Writer.WriteAsync(item);
    }

    public void StartProcessing(CancellationToken cancellationToken)
    {
        _ = Task.Run(async () =>
        {
            await foreach (var item in _channel.Reader.ReadAllAsync(cancellationToken))
            {
                bool success = false;
                while (!success && !cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        success = await _handler(item);
                    }
                    catch
                    {
                        success = false;
                    }

                    if (!success)
                        await Task.Delay(_retryDelay, cancellationToken);
                }
            }
        }, cancellationToken);
    }
}
