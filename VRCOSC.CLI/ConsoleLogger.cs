// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Spectre.Console;
using Color = Spectre.Console.Color;
using VRCOSC.App;
using VRCOSC.App.OSC.VRChat;
using VRCOSC.App.SDK.VRChat.Logs;
using VRCOSC.App.SDK.VRChat.Logs.Handlers;
using VRCOSC.App.Utils;

namespace VRCOSC.CLI;

internal static class ConsoleLogger
{
    private static readonly Lock consoleLock = new();
    private static readonly StringBuilder inputBuffer = new();

    public static LogLevel MinLogLevel { get; set; } = LogLevel.Verbose;
    public static bool ShowOscLogs { get; set; } = false;
    public static bool ShowModuleDebugLogs { get; set; } = false;
    public static bool ShowTerminalLogs { get; set; } = false;

    public static StringBuilder InputBuffer => inputBuffer;

    public static void WriteHeader()
    {
        lock (consoleLock)
        {
            var panel = new Panel(new Markup($"[bold cyan]{Markup.Escape(AppManager.APP_NAME)} CLI[/] - [dim]v{AppManager.Version}[/]\nFull VRCOSC engine loaded with live log output & interactive commands.\nType [yellow]'help'[/] for commands, or send OSC parameters & chat directly."))
            {
                Header = new PanelHeader("[bold yellow] VRCOSC Engine [/]"),
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Cyan1)
            };
            AnsiConsole.Write(panel);
            Console.WriteLine();
        }
    }

    public static void PrintPrompt()
    {
        lock (consoleLock)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write("\r[VRCOSC] > ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write(inputBuffer.ToString());
        }
    }

    public static void WriteLog(ConsoleColor labelColor, string label, string message, ConsoleColor msgColor = ConsoleColor.Gray)
    {
        lock (consoleLock)
        {
            // Clear prompt line
            Console.Write("\r" + new string(' ', getWindowWidth()) + "\r");

            var timestamp = DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{timestamp}] ");

            Console.ForegroundColor = labelColor;
            Console.Write($"[{label}] ");

            Console.ForegroundColor = msgColor;
            Console.WriteLine(message);

            Console.ResetColor();

            // Restore prompt
            PrintPrompt();
        }
    }

    public static void OnLoggerNewEntry(LogEntry entry)
    {
        if (entry.Level < MinLogLevel) return;

        var isTerminalTarget = entry.Target == LoggingTarget.Terminal;
        if (isTerminalTarget && !ShowTerminalLogs) return;

        var isModuleDebugFile = string.Equals(entry.LoggerName, "module-debug", StringComparison.OrdinalIgnoreCase);

        // module-debug file log duplicates are suppressed unless ShowModuleDebugLogs is explicitly enabled
        if (isModuleDebugFile && !ShowModuleDebugLogs) return;

        var isModuleLog = isTerminalTarget
                       || isModuleDebugFile
                       || (entry.LoggerName is not null && entry.LoggerName.Contains("module", StringComparison.OrdinalIgnoreCase));

        var color = entry.Level switch
        {
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Important => ConsoleColor.Yellow,
            LogLevel.Verbose => ConsoleColor.DarkCyan,
            LogLevel.Debug => ConsoleColor.DarkGray,
            _ => ConsoleColor.Gray
        };

        var label = entry.Target?.ToString().ToUpperInvariant() ?? entry.LoggerName?.ToUpperInvariant() ?? entry.Level.ToString().ToUpperInvariant();
        var msg = entry.Message ?? string.Empty;
        if (entry.Exception is not null)
        {
            msg += MinLogLevel == LogLevel.Debug
                ? $"\n{entry.Exception}"
                : $" | Exception: {entry.Exception.Message}";
        }
        WriteLog(color, label, msg, entry.Level == LogLevel.Error ? ConsoleColor.Red : ConsoleColor.Gray);
    }

    public static Task OnOSCMessageReceived(VRChatOSCMessage message)
    {
        if (!ShowOscLogs) return Task.CompletedTask;

        if (message.Address.StartsWith("/avatar/parameters/"))
        {
            var paramName = message.Address["/avatar/parameters/".Length..];
            WriteLog(ConsoleColor.DarkGreen, "OSC IN", $"{paramName} = {FormatOscValue(message.ParameterValue)}", ConsoleColor.Green);
        }
        else
        {
            WriteLog(ConsoleColor.DarkGreen, "OSC IN", $"{message.Address} ({message.Arguments.Length} args)", ConsoleColor.Green);
        }

        return Task.CompletedTask;
    }

    public static void OnOSCMessageSent(VRChatOSCMessage message)
    {
        if (!ShowOscLogs) return;

        if (message.Address == VRChatOSCConstants.ADDRESS_CHATBOX_INPUT)
        {
            WriteLog(ConsoleColor.DarkCyan, "CHAT OUT", $"\"{message.ParameterValue}\"", ConsoleColor.Cyan);
        }
        else if (message.Address.StartsWith("/avatar/parameters/"))
        {
            var paramName = message.Address["/avatar/parameters/".Length..];
            WriteLog(ConsoleColor.DarkCyan, "OSC OUT", $"{paramName} = {FormatOscValue(message.ParameterValue)}", ConsoleColor.Cyan);
        }
        else
        {
            WriteLog(ConsoleColor.DarkCyan, "OSC OUT", $"{message.Address} = {FormatOscValue(message.ParameterValue)}", ConsoleColor.Cyan);
        }
    }

    public static void HandleClientEvent(IVRChatClientEvent @event)
    {
        switch (@event)
        {
            case UserAuthenticatedClientEvent ev:
                WriteLog(ConsoleColor.Blue, "VRC LOG", $"User Authenticated: {ev.User.Username}", ConsoleColor.White);
                break;

            case InstanceJoinedClientEvent ev:
                WriteLog(ConsoleColor.Blue, "VRC LOG", $"Joined Instance: {ev.Instance.World?.Name ?? "Unknown World"} ({ev.Instance.Id ?? "No ID"})", ConsoleColor.White);
                break;

            case InstanceLeftClientEvent:
                WriteLog(ConsoleColor.Blue, "VRC LOG", "Left Instance", ConsoleColor.White);
                break;

            case UserJoinedClientEvent ev:
                WriteLog(ConsoleColor.Blue, "VRC LOG", $"User Joined: {ev.User.Username}", ConsoleColor.White);
                break;

            case UserLeftClientEvent ev:
                WriteLog(ConsoleColor.Blue, "VRC LOG", $"User Left: {ev.User.Username}", ConsoleColor.White);
                break;

            case AvatarPreChangeClientEvent:
                WriteLog(ConsoleColor.Blue, "VRC LOG", "Avatar change initiated", ConsoleColor.White);
                break;
        }
    }

    public static string FormatOscValue(object? value)
    {
        return value switch
        {
            null => "null",
            bool b => b ? "True" : "False",
            float f => f.ToString("0.000", CultureInfo.InvariantCulture),
            double d => d.ToString("0.000", CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty
        };
    }

    private static int getWindowWidth()
    {
        try
        {
            var width = Console.WindowWidth;
            return width > 1 ? width - 1 : 80;
        }
        catch
        {
            return 80;
        }
    }
}
