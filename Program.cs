using GrpcChatDemo;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddGrpc();

var app = builder.Build();
app.MapGrpcService<ChatService>();
app.Run();
