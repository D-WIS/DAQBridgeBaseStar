using DWIS.DAQBridge.BaseStar.BlackboardSink;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
