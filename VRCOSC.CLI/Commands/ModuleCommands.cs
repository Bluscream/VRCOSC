// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using Spectre.Console;
using Spectre.Console.Cli;
using Color = Spectre.Console.Color;
using VRCOSC.App.Modules;

namespace VRCOSC.CLI.Commands;

internal static class ModuleCommands
{
    public static void DisplayModules()
    {
        var moduleManager = ModuleManager.GetInstance();
        var modules = moduleManager.Modules.Values.SelectMany(m => m).ToList();

        var table = CliTheme.CreateTable("Loaded Modules", "Status", "Module Title", "Full Module ID");

        foreach (var m in modules)
        {
            var isRunning = moduleManager.RunningModules.Contains(m);
            var statusTag = CliTheme.FormatStatusBadge(isRunning, m.Enabled.Value);

            table.AddRow(
                statusTag,
                Markup.Escape(m.Title),
                Markup.Escape(m.FullID)
            );
        }

        AnsiConsole.Write(table);
        ConsoleLogger.PrintPrompt();
    }

    public sealed class ModulesCommand : Command
    {
        protected override int Execute(CommandContext context, CancellationToken cancellationToken)
        {
            DisplayModules();
            return 0;
        }
    }

    public sealed class ToggleModuleCommand : Command<ToggleModuleCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "[module_id]")]
            [Description("Module ID or Full ID to toggle on/off")]
            public string? ModuleId { get; set; }
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var allModules = ModuleManager.GetInstance().Modules.Values.SelectMany(m => m).ToList();
            if (allModules.Count == 0)
            {
                ConsoleLogger.WriteLog(ConsoleColor.Yellow, "WARN", "No modules loaded.");
                return 1;
            }

            string selectedModuleId;

            if (string.IsNullOrWhiteSpace(settings.ModuleId) && !Console.IsInputRedirected)
            {
                var choices = allModules.Select(m => $"{m.Title} ({m.FullID})").ToList();
                var promptSelection = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[bold cyan]Select Module to Toggle:[/]")
                        .PageSize(12)
                        .AddChoices(choices)
                );

                var parts = promptSelection.Split('(');
                selectedModuleId = parts.Length > 1 ? parts[^1].TrimEnd(')').Trim() : promptSelection;
            }
            else
            {
                selectedModuleId = settings.ModuleId ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(selectedModuleId))
            {
                DisplayModules();
                return 0;
            }

            var module = allModules.FirstOrDefault(m => string.Equals(m.FullID, selectedModuleId, StringComparison.OrdinalIgnoreCase) || string.Equals(m.ID, selectedModuleId, StringComparison.OrdinalIgnoreCase));
            if (module is null)
            {
                ConsoleLogger.WriteLog(ConsoleColor.Red, "ERROR", $"Module '{selectedModuleId}' not found.");
                return 1;
            }

            module.Enabled.Value = !module.Enabled.Value;
            ConsoleLogger.WriteLog(ConsoleColor.Green, "MODULE", $"Module '{module.FullID}' is now {(module.Enabled.Value ? "ENABLED" : "DISABLED")}.");
            return 0;
        }
    }
}
