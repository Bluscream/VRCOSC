// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Spectre.Console.Cli;
using Color = Spectre.Console.Color;
using VRCOSC.App;
using VRCOSC.App.Modules;
using VRCOSC.App.OSC.VRChat;
using VRCOSC.App.Profiles;
using VRCOSC.App.Utils;

namespace VRCOSC.CLI.Commands;

internal static class EngineCommands
{
    public sealed class StatusCommand : Command
    {
        protected override int Execute(CommandContext context, CancellationToken cancellationToken)
        {
            var appManager = AppManager.GetInstance();
            var table = CliTheme.CreateTable("VRCOSC Engine Status", "Property", "Value");

            table.AddRow("Engine State", CliTheme.FormatState(appManager.State.Value.ToString()));
            table.AddRow("Active Profile", $"[bold green]{Markup.Escape(ProfileManager.GetInstance().ActiveProfile.Value?.Name.Value ?? "None")}[/]");
            table.AddRow("VRChat Running", appManager.VRChatClient.IsOpen ? "[green]Yes[/]" : "[red]No[/]");
            table.AddRow("OSC Endpoint", appManager.VRChatOscClient.SendEndpoint?.ToString() ?? "[dim]Disconnected[/]");

            if (appManager.VRChatClient.IsInAvatar)
            {
                var avatar = appManager.VRChatClient.Avatar;
                table.AddRow("Avatar", $"{Markup.Escape(avatar.Name)} ({avatar.Id})");
                table.AddRow("Avatar Parameters", avatar.Parameters.Count.ToString());
            }
            else
            {
                table.AddRow("Avatar", "[dim]Not in avatar[/]");
            }

            table.AddRow("Running Modules", ModuleManager.GetInstance().RunningModules.Count().ToString());

            AnsiConsole.Write(table);
            ConsoleLogger.PrintPrompt();
            return 0;
        }
    }

    public sealed class VRChatStatusCommand : Command
    {
        protected override int Execute(CommandContext context, CancellationToken cancellationToken)
        {
            var vrcClient = AppManager.GetInstance().VRChatClient;

            // 1. Process Panel
            var processInfo = vrcClient.IsOpen
                ? $"[green]Process Running[/]\n[dim]FPS:[/] [bold green]{vrcClient.FPS:F1}[/]"
                : "[red]Process Not Open[/]";
            var processPanel = CliTheme.CreatePanel("Process & Performance", processInfo);

            // 2. User Panel
            var userInfo = vrcClient.IsLoggedIn && vrcClient.User is not null
                ? $"[bold green]{Markup.Escape(vrcClient.User.Username)}[/]\n[dim]ID:[/] {Markup.Escape(vrcClient.User.Id)}"
                : "[dim]Not Logged In / Unknown[/]";
            var userPanel = CliTheme.CreatePanel("User Profile", userInfo);

            // 3. World & Instance Panel
            string instanceInfo;
            if (vrcClient.IsInInstance && vrcClient.Instance is not null)
            {
                var inst = vrcClient.Instance;
                var userNames = inst.Users.Count > 0
                    ? string.Join(", ", inst.Users.Select(u => Markup.Escape(u.Username)))
                    : "[dim]None[/]";

                instanceInfo = $"[bold green]{Markup.Escape(inst.World.Name)}[/]\n" +
                               $"[dim]World ID:[/] {Markup.Escape(inst.World.Id)}\n" +
                               $"[dim]Instance ID:[/] {Markup.Escape(inst.Id ?? "N/A")}\n" +
                               $"[dim]Type:[/] [yellow]{inst.Type}[/]\n" +
                               $"[dim]Region:[/] [cyan]{inst.Region}[/]\n" +
                               $"[dim]Players ({inst.Users.Count}):[/] {userNames}";
            }
            else
            {
                instanceInfo = "[dim]Not in World / Instance[/]";
            }
            var instancePanel = CliTheme.CreatePanel("World & Instance", instanceInfo);

            // 4. Avatar Panel
            string avatarInfo;
            if (vrcClient.IsInAvatar && vrcClient.Avatar is not null)
            {
                var av = vrcClient.Avatar;
                avatarInfo = $"[bold green]{Markup.Escape(av.Name)}[/]\n" +
                             $"[dim]ID:[/] {Markup.Escape(av.Id)}\n" +
                             $"[dim]Eye Height:[/] {av.EyeHeight:F2}m [dim]({av.EyeHeightMin:F2}m - {av.EyeHeightMax:F2}m)[/]\n" +
                             $"[dim]Scaling:[/] {(av.EyeHeightScalingAllowed ? "[green]Allowed[/]" : "[red]Disabled[/]")}\n" +
                             $"[dim]OSC Parameters:[/] [yellow]{av.Parameters.Count}[/]";
            }
            else
            {
                avatarInfo = "[dim]Not in Avatar[/]";
            }
            var avatarPanel = CliTheme.CreatePanel("Active Avatar", avatarInfo);

            // 5. User Camera Panel
            var cameraInfo = $"[dim]Is Streaming:[/] {(vrcClient.UserCamera.IsStreaming ? "[bold green]Yes[/]" : "[dim]No[/]")}";
            var cameraPanel = CliTheme.CreatePanel("User Camera", cameraInfo);

            // Grid Dashboard
            var grid = new Grid()
                .AddColumn(new GridColumn().PadRight(1))
                .AddColumn(new GridColumn());

            grid.AddRow(processPanel, userPanel);
            grid.AddRow(instancePanel, avatarPanel);
            grid.AddRow(cameraPanel, Text.Empty);

            var dashboardPanel = new Panel(grid)
            {
                Header = new PanelHeader("[bold cyan] VRChat Detailed Client SDK Dashboard [/]"),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Cyan1)
            };

            AnsiConsole.Write(dashboardPanel);
            ConsoleLogger.PrintPrompt();
            return 0;
        }
    }

    public sealed class StartCommand : AsyncCommand<StartCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandOption("-f|--force")]
            [Description("Force start engine modules immediately without waiting for VRCQuery auto-discovery")]
            public bool Force { get; set; }
        }

        protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var appManager = AppManager.GetInstance();

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(new Style(Color.Cyan1))
                .StartAsync(settings.Force ? "Force starting VRCOSC engine modules..." : "Requesting VRCOSC auto-connection startup...", async ctx =>
                {
                    if (settings.Force)
                    {
                        await appManager.ForceStart();
                        ConsoleLogger.WriteLog(ConsoleColor.Magenta, "STARTUP", "Engine force-started successfully.");
                    }
                    else
                    {
                        await appManager.RequestStart();
                        ConsoleLogger.WriteLog(ConsoleColor.Cyan, "STARTUP", "Engine auto-connection mode active.");
                    }
                });

            return 0;
        }
    }

    public sealed class StopCommand : AsyncCommand
    {
        protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(new Style(Color.Yellow))
                .StartAsync("Stopping VRCOSC engine...", async ctx =>
                {
                    await AppManager.GetInstance().StopAsync();
                    ConsoleLogger.WriteLog(ConsoleColor.Yellow, "ENGINE", "Engine stopped successfully.");
                });

            return 0;
        }
    }

    public sealed class RestartCommand : AsyncCommand
    {
        protected override async Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken)
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(new Style(Color.Yellow))
                .StartAsync("Restarting VRCOSC engine & rebinding OSC...", async ctx =>
                {
                    await AppManager.GetInstance().RestartAsync();
                    ConsoleLogger.WriteLog(ConsoleColor.Green, "ENGINE", "Engine restarted successfully.");
                });

            return 0;
        }
    }

    public sealed class ChatCommand : Command<ChatCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<message>")]
            [Description("Text message to send to VRChat chatbox")]
            public string Message { get; set; } = string.Empty;
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(settings.Message))
            {
                ConsoleLogger.WriteLog(ConsoleColor.Yellow, "WARN", "Usage: chat <message>");
                return 1;
            }

            AppManager.GetInstance().VRChatOscClient.Send(VRChatOSCConstants.ADDRESS_CHATBOX_INPUT, settings.Message, true, false);
            ConsoleLogger.WriteLog(ConsoleColor.Cyan, "CHAT SENT", $"\"{settings.Message}\"");
            return 0;
        }
    }

    public sealed class SendParamCommand : Command<SendParamCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<name>")]
            [Description("OSC Parameter Name (e.g. Mute, VRCOSC/Controls/ChatBox/Enabled)")]
            public string Name { get; set; } = string.Empty;

            [CommandArgument(1, "<value>")]
            [Description("Value to set (e.g. true, 1.0, hello)")]
            public string Value { get; set; } = string.Empty;
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var address = settings.Name.StartsWith("/") ? settings.Name : $"{VRChatOSCConstants.ADDRESS_AVATAR_PARAMETERS}/{settings.Name}";
            var parsedValue = ParseValue(settings.Value);

            AppManager.GetInstance().VRChatOscClient.Send(address, parsedValue);
            ConsoleLogger.WriteLog(ConsoleColor.Cyan, "OSC SENT", $"{address} = {ConsoleLogger.FormatOscValue(parsedValue)}");
            return 0;
        }
    }

    public sealed class LogConfigCommand : Command<LogConfigCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "[setting]")]
            [Description("Log option: debug, verbose, important, error, or osc")]
            public string? Setting { get; set; }
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(settings.Setting))
            {
                ConsoleLogger.WriteLog(ConsoleColor.Cyan, "LOG STATUS", $"Current log level: {ConsoleLogger.MinLogLevel}, OSC logging: {(ConsoleLogger.ShowOscLogs ? "ON" : "OFF")}");
                return 0;
            }

            switch (settings.Setting.ToLowerInvariant())
            {
                case "osc":
                case "toggle-osc":
                    ConsoleLogger.ShowOscLogs = !ConsoleLogger.ShowOscLogs;
                    ConsoleLogger.WriteLog(ConsoleColor.Green, "LOG", $"OSC message logging is now {(ConsoleLogger.ShowOscLogs ? "ENABLED" : "DISABLED")}.");
                    break;

                case "debug":
                    ConsoleLogger.MinLogLevel = LogLevel.Debug;
                    ConsoleLogger.WriteLog(ConsoleColor.Green, "LOG", "Log level set to DEBUG.");
                    break;

                case "verbose":
                    ConsoleLogger.MinLogLevel = LogLevel.Verbose;
                    ConsoleLogger.WriteLog(ConsoleColor.Green, "LOG", "Log level set to VERBOSE.");
                    break;

                case "important":
                    ConsoleLogger.MinLogLevel = LogLevel.Important;
                    ConsoleLogger.WriteLog(ConsoleColor.Green, "LOG", "Log level set to IMPORTANT.");
                    break;

                case "error":
                    ConsoleLogger.MinLogLevel = LogLevel.Error;
                    ConsoleLogger.WriteLog(ConsoleColor.Green, "LOG", "Log level set to ERROR.");
                    break;

                default:
                    ConsoleLogger.WriteLog(ConsoleColor.Yellow, "WARN", "Usage: log [debug|verbose|important|error|osc]");
                    break;
            }
            return 0;
        }
    }

    public static void ProcessDirectOscMessage(string input, AppManager appManager)
    {
        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var address = parts[0];

        if (parts.Length == 1)
        {
            appManager.VRChatOscClient.Send(address);
            ConsoleLogger.WriteLog(ConsoleColor.Cyan, "OSC SENT", address);
        }
        else
        {
            var parsedValue = ParseValue(parts[1]);
            appManager.VRChatOscClient.Send(address, parsedValue);
            ConsoleLogger.WriteLog(ConsoleColor.Cyan, "OSC SENT", $"{address} = {ConsoleLogger.FormatOscValue(parsedValue)}");
        }
    }

    public static object ParseValue(string raw)
    {
        if (bool.TryParse(raw, out var bVal)) return bVal;
        if (int.TryParse(raw, CultureInfo.InvariantCulture, out var iVal)) return iVal;
        if (float.TryParse(raw, CultureInfo.InvariantCulture, out var fVal)) return fVal;
        return raw;
    }
}
