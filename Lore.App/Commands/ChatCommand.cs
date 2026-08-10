using System.Text;
using Lore.Common.Models;
using Lore.Core.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Lore.App.Commands;

internal class ChatCommand : AsyncCommand<ChatCommand.Settings>
{
    private const string UserPromptMarkup = "[bold blue]You:[/] ";
    private const int UserPromptWidth = 5;

    public class Settings : CommandSettings
    {
    }

    protected override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        IHost? host = null;

        try
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();

            // SUPPRESS CONSOLE LOGS: Clear default console logging providers so background logs don't overwrite CLI output
            builder.Logging.ClearProviders();

            builder.Services.AddAppServices();
            host = builder.Build();

            await host.StartAsync(cts.Token);

            var searchService = host.Services.GetRequiredService<ILoreChatService>();

            WriteBanner();

            var chatId = Guid.NewGuid();
            var history = new List<string>();

            while (!cts.Token.IsCancellationRequested)
            {
                var userPrompt = await ReadLineAsync(history, cts.Token);

                if (cts.Token.IsCancellationRequested || userPrompt == null)
                {
                    break;
                }

                EraseInputLine(userPrompt.Length);

                if (string.IsNullOrWhiteSpace(userPrompt))
                {
                    continue;
                }

                if (IsExitCommand(userPrompt))
                {
                    break;
                }

                if (TryHandleCommand(userPrompt, ref chatId))
                {
                    continue;
                }

                AnsiConsole.Write(RenderUserMessage(userPrompt));
                AnsiConsole.WriteLine();

                var request = new LoreChatRequest(chatId, userPrompt, true);
                StreamingSearchContextResult? searchResult = null;

                await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .StartAsync("Searching documents...", async _ =>
                    {
                        searchResult = await searchService.ChatAsync(request, cts.Token);
                    });

                var responseBuffer = new StringBuilder();

                await AnsiConsole.Live(RenderLorePanel("[grey]Thinking...[/]"))
                    .AutoClear(false)
                    .StartAsync(async ctx =>
                    {
                        await foreach (var token in searchResult!.LLMResponseStream.WithCancellation(cts.Token))
                        {
                            responseBuffer.Append(token);
                            ctx.UpdateTarget(RenderLorePanel(Markup.Escape(responseBuffer.ToString())));
                        }
                    });

                AnsiConsole.WriteLine();
            }
        }
        catch (OperationCanceledException)
        {
            // Clean exit
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
        finally
        {
            if (host != null)
            {
                using var stopCts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                try
                {
                    await host.StopAsync(stopCts.Token);
                }
                catch
                {
                    // Ignore stop timeout
                }
                finally
                {
                    host.Dispose();
                }
            }
        }

        return 0;
    }

    private static bool IsExitCommand(string input)
    {
        var command = input.Trim().ToLowerInvariant();
        return command is "/exit" or "/quit";
    }

    private static bool TryHandleCommand(string input, ref Guid chatId)
    {
        if (!input.StartsWith('/'))
        {
            return false;
        }

        var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var command = parts[0].ToLowerInvariant();

        switch (command)
        {
            case "/help":
                WriteHelp();
                return true;
            case "/clear":
                AnsiConsole.Clear();
                chatId = Guid.NewGuid();
                WriteBanner();
                return true;
            case "/new":
                chatId = Guid.NewGuid();
                AnsiConsole.MarkupLine("[grey]Started a new conversation.[/]");
                return true;
            default:
                AnsiConsole.MarkupLine($"[red]Unknown command:[/] {Markup.Escape(input)}");
                return true;
        }
    }

    private static void WriteBanner()
    {
        AnsiConsole.Write(
            new Rule("[bold green]Lore Chat[/]")
                .RuleStyle(new Style(Color.Green))
                .LeftJustified()
        );
        AnsiConsole.MarkupLine(
            "[grey]Ask questions about your documents. Type [bold]/help[/] for commands or [bold]Ctrl+C[/] to quit.[/]"
        );
        AnsiConsole.WriteLine();
    }

    private static void WriteHelp()
    {
        var panel = new Panel(
                new Markup(
                    "[bold yellow]Commands[/]\n"
                    + "[bold]/clear[/]    Clear the screen and start a new conversation\n"
                    + "[bold]/new[/]      Start a new conversation\n"
                    + "[bold]/help[/]     Show this help text\n"
                    + "[bold]/exit[/]     Exit the chat\n\n"
                    + "[bold yellow]Editing[/]\n"
                    + "[bold]↑ / ↓[/]     Navigate command history\n"
                    + "[bold]← / →[/]     Move the cursor\n"
                    + "[bold]Home/End[/]  Jump to start/end of the line\n"
                    + "[bold]Ctrl+U[/]    Clear the current input\n"
                )
                .LeftJustified()
            )
            .Header("[bold cyan] Help [/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Cyan))
            .Padding(new Padding(1, 1, 1, 1))
            .Expand();

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private static Panel RenderUserMessage(string text)
    {
        return new Panel(new Markup(Markup.Escape(text)).LeftJustified())
            .Header("[bold blue] You [/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Blue))
            .Padding(new Padding(1, 0, 1, 0))
            .Expand();
    }

    private static Panel RenderLorePanel(string contentMarkup)
    {
        return new Panel(new Markup(contentMarkup).LeftJustified())
            .Header("[bold yellow] Lore [/]")
            .Border(BoxBorder.Rounded)
            .BorderStyle(new Style(Color.Yellow))
            .Padding(new Padding(1, 0, 1, 0))
            .Expand();
    }

    private static void EraseInputLine(int inputLength)
    {
        var lines = InputLineCount(UserPromptWidth, inputLength);
        Console.Write($"\x1b[{lines}A\r\x1b[0J");
    }

    private static int InputLineCount(int promptWidth, int inputLength)
    {
        var total = promptWidth + inputLength;
        var width = Math.Max(Console.WindowWidth, 1);
        return total == 0 ? 1 : (total - 1) / width + 1;
    }

    private static async Task<string?> ReadLineAsync(List<string> history, CancellationToken cancellationToken)
    {
        var buffer = new StringBuilder();
        var cursor = 0;
        var historyIndex = history.Count;
        string? stashed = null;

        AnsiConsole.Markup(UserPromptMarkup);

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!Console.KeyAvailable)
            {
                await Task.Delay(15, cancellationToken);
                continue;
            }

            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                var line = buffer.ToString();
                if (!string.IsNullOrWhiteSpace(line) && !history.Contains(line))
                {
                    history.Add(line);
                }
                return line;
            }

            if (key.Key == ConsoleKey.Backspace)
            {
                if (cursor > 0)
                {
                    buffer.Remove(cursor - 1, 1);
                    cursor--;
                    if (cursor == buffer.Length)
                    {
                        Console.Write("\b \b");
                    }
                    else
                    {
                        RedrawInputLine(buffer, cursor);
                    }
                }
            }
            else if (key.Key == ConsoleKey.Delete)
            {
                if (cursor < buffer.Length)
                {
                    buffer.Remove(cursor, 1);
                    RedrawInputLine(buffer, cursor);
                }
            }
            else if (key.Key == ConsoleKey.LeftArrow)
            {
                if (cursor > 0)
                {
                    cursor--;
                    Console.Write("\x1b[1D");
                }
            }
            else if (key.Key == ConsoleKey.RightArrow)
            {
                if (cursor < buffer.Length)
                {
                    cursor++;
                    Console.Write("\x1b[1C");
                }
            }
            else if (key.Key == ConsoleKey.Home)
            {
                if (cursor > 0)
                {
                    Console.Write($"\x1b[{cursor}D");
                    cursor = 0;
                }
            }
            else if (key.Key == ConsoleKey.End)
            {
                if (cursor < buffer.Length)
                {
                    Console.Write($"\x1b[{buffer.Length - cursor}C");
                    cursor = buffer.Length;
                }
            }
            else if (key.Key == ConsoleKey.UpArrow)
            {
                if (historyIndex > 0)
                {
                    if (historyIndex == history.Count)
                    {
                        stashed = buffer.ToString();
                    }
                    historyIndex--;
                    LoadHistory(buffer, ref cursor, history[historyIndex]);
                }
            }
            else if (key.Key == ConsoleKey.DownArrow)
            {
                if (historyIndex < history.Count)
                {
                    historyIndex++;
                    var text = historyIndex == history.Count ? stashed ?? string.Empty : history[historyIndex];
                    LoadHistory(buffer, ref cursor, text);
                }
            }
            else if ((key.Modifiers & ConsoleModifiers.Control) != 0 && key.Key == ConsoleKey.U)
            {
                buffer.Clear();
                cursor = 0;
                RedrawInputLine(buffer, cursor);
            }
            else if (!char.IsControl(key.KeyChar))
            {
                if (cursor == buffer.Length)
                {
                    buffer.Append(key.KeyChar);
                    cursor++;
                    Console.Write(key.KeyChar);
                }
                else
                {
                    buffer.Insert(cursor, key.KeyChar);
                    cursor++;
                    RedrawInputLine(buffer, cursor);
                }
            }
        }

        return null;
    }

    private static void LoadHistory(StringBuilder buffer, ref int cursor, string text)
    {
        buffer.Clear();
        buffer.Append(text);
        cursor = buffer.Length;
        RedrawInputLine(buffer, cursor);
    }

    private static void RedrawInputLine(StringBuilder buffer, int cursor)
    {
        Console.Write("\r\x1b[2K");
        AnsiConsole.Markup(UserPromptMarkup);
        Console.Write(buffer);
        if (cursor < buffer.Length)
        {
            Console.Write($"\x1b[{buffer.Length - cursor}D");
        }
    }
}
