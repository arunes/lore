using Lore.App.Commands;
using Spectre.Console.Cli;

var cancellationTokenSource = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellationTokenSource.Cancel();
    Console.WriteLine("Cancellation requested...");
};

var cliApp = new CommandApp();
cliApp.Configure(config =>
{
    config.AddCommand<ChatCommand>("chat");
    config.AddCommand<UICommand>("ui");
});

return await cliApp.RunAsync(args, cancellationTokenSource.Token);