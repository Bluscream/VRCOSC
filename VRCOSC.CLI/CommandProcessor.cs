// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Spectre.Console.Cli;
using VRCOSC.App;

namespace VRCOSC.CLI.Commands;

internal static class CommandProcessor
{
    private static CommandApp commandApp = null!;

    public static void Initialize(AppManager appManager, Action onShutdownRequest)
    {
        commandApp = new CommandApp();
        commandApp.Configure(config =>
        {
            config.SetApplicationName("vrcosc");

            config.AddBranch<CommandSettings>("status", status =>
            {
                status.SetDescription("Display engine status or detailed VRChat SDK client data");
                status.SetDefaultCommand<EngineCommands.StatusCommand>();
                status.AddCommand<EngineCommands.VRChatStatusCommand>("vrc")
                      .WithAlias("vrchat")
                      .WithDescription("Display detailed VRChat SDK client data (User, World, Instance, Avatar, Eye Height)");
            });

            config.AddCommand<EngineCommands.VRChatStatusCommand>("vrcstatus")
                  .WithAlias("status-vrc")
                  .WithAlias("status-vrchat")
                  .WithDescription("Display detailed VRChat SDK client data (User, World, Instance, Avatar, Eye Height)");

            config.AddCommand<EngineCommands.StartCommand>("start")
                  .WithAlias("force-start")
                  .WithDescription("Start VRCOSC engine modules");

            config.AddCommand<EngineCommands.StopCommand>("stop")
                  .WithDescription("Stop VRCOSC engine execution");

            config.AddCommand<EngineCommands.RestartCommand>("restart")
                  .WithDescription("Restart engine and rebind OSC endpoints");

            config.AddCommand<EngineCommands.ChatCommand>("chat")
                  .WithAlias("say")
                  .WithDescription("Send text to VRChat chatbox");

            config.AddCommand<EngineCommands.SendParamCommand>("send")
                  .WithAlias("param")
                  .WithDescription("Send avatar OSC parameter");

            config.AddCommand<ProfileCommands.ProfilesCommand>("profiles")
                  .WithDescription("List all available profiles");

            config.AddCommand<ProfileCommands.ProfileSwitchCommand>("profile")
                  .WithDescription("Switch active profile");

            config.AddCommand<ModuleCommands.ModulesCommand>("modules")
                  .WithDescription("List loaded modules and status");

            config.AddCommand<ModuleCommands.ToggleModuleCommand>("toggle")
                  .WithDescription("Enable or disable a module");

            config.AddCommand<FlowCommands.FlowsCommand>("flows")
                  .WithAlias("graphs")
                  .WithDescription("List node flow graphs in active profile");

            config.AddCommand<FlowCommands.FlowManageCommand>("flow")
                  .WithAlias("graph")
                  .WithDescription("Manage flow graph (enable, disable, toggle, info)");

            config.AddCommand<FlowCommands.CreateFlowCommand>("create-flow")
                  .WithDescription("Create a new node flow graph");

            config.AddCommand<FlowCommands.ImportFlowCommand>("import-flow")
                  .WithDescription("Import a node graph JSON file");

            config.AddCommand<FlowCommands.ExportFlowCommand>("export-flow")
                  .WithDescription("Export node graph to JSON file");

            config.AddCommand<FlowCommands.CreateTestFlowCommand>("create-test-flow")
                  .WithDescription("Generate demo flow graph with node instances");

            config.AddCommand<EngineCommands.LogConfigCommand>("log")
                  .WithDescription("Configure log level or toggle OSC logging");
        });
    }

    public static async Task RunCommandLoop(AppManager appManager, Func<bool> isRunningCheck, Action onShutdownRequest)
    {
        Initialize(appManager, onShutdownRequest);

        while (isRunningCheck())
        {
            if (Console.IsInputRedirected)
            {
                var line = await Task.Run(() => Console.ReadLine());
                if (line is null)
                {
                    await Task.Delay(500);
                    continue;
                }

                line = line.Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    await ProcessCommand(line, appManager, onShutdownRequest);
                }
                continue;
            }

            try
            {
                var keyInfo = Console.ReadKey(intercept: true);

                if (keyInfo.Key == ConsoleKey.Enter)
                {
                    var input = ConsoleLogger.InputBuffer.ToString().Trim();
                    ConsoleLogger.InputBuffer.Clear();

                    Console.WriteLine();

                    if (!string.IsNullOrEmpty(input))
                    {
                        await ProcessCommand(input, appManager, onShutdownRequest);
                    }

                    ConsoleLogger.PrintPrompt();
                }
                else if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (ConsoleLogger.InputBuffer.Length > 0)
                    {
                        ConsoleLogger.InputBuffer.Remove(ConsoleLogger.InputBuffer.Length - 1, 1);
                        ConsoleLogger.PrintPrompt();
                    }
                }
                else if (keyInfo.Key == ConsoleKey.C && keyInfo.Modifiers.HasFlag(ConsoleModifiers.Control))
                {
                    onShutdownRequest();
                    break;
                }
                else if (keyInfo.KeyChar >= 32)
                {
                    ConsoleLogger.InputBuffer.Append(keyInfo.KeyChar);
                    ConsoleLogger.PrintPrompt();
                }
            }
            catch (InvalidOperationException)
            {
                var line = await Task.Run(() => Console.ReadLine());
                if (line is null)
                {
                    await Task.Delay(500);
                    continue;
                }

                line = line.Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    await ProcessCommand(line, appManager, onShutdownRequest);
                }
            }
        }
    }

    public static async Task ProcessCommand(string input, AppManager appManager, Action onShutdownRequest)
    {
        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();

        if (cmd is "exit" or "quit")
        {
            onShutdownRequest();
            return;
        }

        if (cmd is "cls" or "clear")
        {
            Console.Clear();
            ConsoleLogger.WriteHeader();
            return;
        }

        if (cmd.StartsWith("/"))
        {
            EngineCommands.ProcessDirectOscMessage(input, appManager);
            return;
        }

        var tokens = tokenize(input);
        if (tokens.Length > 0 && (tokens[0].Equals("help", StringComparison.OrdinalIgnoreCase) || tokens[0].Equals("?", StringComparison.OrdinalIgnoreCase)))
        {
            tokens = tokens.Length > 1 ? new[] { tokens[1], "--help" } : new[] { "--help" };
        }
        await commandApp.RunAsync(tokens);
    }

    private static string[] tokenize(string commandLine)
    {
        var tokens = new List<string>();
        var currentToken = new StringBuilder();
        var inQuotes = false;

        foreach (var c in commandLine)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (c == ' ' && !inQuotes)
            {
                if (currentToken.Length > 0)
                {
                    tokens.Add(currentToken.ToString());
                    currentToken.Clear();
                }
            }
            else
            {
                currentToken.Append(c);
            }
        }

        if (currentToken.Length > 0)
        {
            tokens.Add(currentToken.ToString());
        }

        return tokens.ToArray();
    }
}
