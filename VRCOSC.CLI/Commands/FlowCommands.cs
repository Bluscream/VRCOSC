// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using Color = Spectre.Console.Color;
using VRCOSC.App.Nodes;
using VRCOSC.App.Nodes.Serialisation.V2;

namespace VRCOSC.CLI.Commands;

internal static class FlowCommands
{
    public static void DisplayFlows()
    {
        var nodeManager = NodeManager.GetInstance();
        var graphs = nodeManager.Graphs;

        var table = CliTheme.CreateTable("Flows / Node Graphs", "Status", "Graph Name", "ID", "Elements", "Connections");

        if (graphs.Count == 0)
        {
            table.AddRow("[dim]None[/]", "[dim]No node graphs found[/]", "-", "0", "0");
        }
        else
        {
            foreach (var g in graphs)
            {
                var isEnabled = g.Enabled.Value;
                var isRunning = g.Running.Value;
                var statusTag = CliTheme.FormatStatusBadge(isRunning, isEnabled);

                table.AddRow(
                    statusTag,
                    Markup.Escape(g.Name.Value),
                    g.Id.ToString(),
                    g.Elements.Count.ToString(),
                    g.Connections.Count.ToString()
                );
            }
        }

        AnsiConsole.Write(table);
        ConsoleLogger.PrintPrompt();
    }

    public sealed class FlowsCommand : Command
    {
        protected override int Execute(CommandContext context, CancellationToken cancellationToken)
        {
            DisplayFlows();
            return 0;
        }
    }

    public sealed class FlowManageCommand : Command<FlowManageCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "[name_or_id]")]
            [Description("Flow graph name or GUID ID")]
            public string? Identifier { get; set; }

            [CommandArgument(1, "[action]")]
            [Description("Action: enable, disable, toggle, or info")]
            public string? Action { get; set; }
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var graphs = NodeManager.GetInstance().Graphs.ToList();
            if (graphs.Count == 0)
            {
                ConsoleLogger.WriteLog(ConsoleColor.Yellow, "WARN", "No node graphs available.");
                return 1;
            }

            string selectedIdentifier;
            if (string.IsNullOrWhiteSpace(settings.Identifier) && !Console.IsInputRedirected)
            {
                var choices = graphs.Select(g => $"{g.Name.Value} ({g.Id})").ToList();
                var selection = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[bold cyan]Select Flow Graph to Manage:[/]")
                        .PageSize(10)
                        .AddChoices(choices)
                );

                var parts = selection.Split('(');
                selectedIdentifier = parts.Length > 1 ? parts[^1].TrimEnd(')').Trim() : selection;
            }
            else
            {
                selectedIdentifier = settings.Identifier ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(selectedIdentifier))
            {
                DisplayFlows();
                return 0;
            }

            var action = string.IsNullOrWhiteSpace(settings.Action) ? "toggle" : settings.Action.ToLowerInvariant();
            var targetGraph = graphs.FirstOrDefault(g => string.Equals(g.Id.ToString(), selectedIdentifier, StringComparison.OrdinalIgnoreCase) || string.Equals(g.Name.Value, selectedIdentifier, StringComparison.OrdinalIgnoreCase));

            if (targetGraph is null)
            {
                ConsoleLogger.WriteLog(ConsoleColor.Red, "ERROR", $"Flow graph '{selectedIdentifier}' not found.");
                return 1;
            }

            switch (action)
            {
                case "enable":
                case "on":
                    targetGraph.Enabled.Value = true;
                    ConsoleLogger.WriteLog(ConsoleColor.Green, "FLOW", $"Flow graph '{targetGraph.Name.Value}' is now ENABLED.");
                    break;

                case "disable":
                case "off":
                    targetGraph.Enabled.Value = false;
                    ConsoleLogger.WriteLog(ConsoleColor.Yellow, "FLOW", $"Flow graph '{targetGraph.Name.Value}' is now DISABLED.");
                    break;

                case "toggle":
                    targetGraph.Enabled.Value = !targetGraph.Enabled.Value;
                    ConsoleLogger.WriteLog(ConsoleColor.Green, "FLOW", $"Flow graph '{targetGraph.Name.Value}' is now {(targetGraph.Enabled.Value ? "ENABLED" : "DISABLED")}.");
                    break;

                case "info":
                case "details":
                    var table = CliTheme.CreateTable($"Flow Details: {targetGraph.Name.Value}", "Property", "Value");

                    table.AddRow("ID", targetGraph.Id.ToString());
                    table.AddRow("Enabled", targetGraph.Enabled.Value ? "[green]True[/]" : "[red]False[/]");
                    table.AddRow("Running", targetGraph.Running.Value ? "[green]True[/]" : "[red]False[/]");
                    table.AddRow("Elements", targetGraph.Elements.Count.ToString());
                    table.AddRow("Connections", targetGraph.Connections.Count.ToString());

                    AnsiConsole.Write(table);

                    if (targetGraph.Elements.Count > 0)
                    {
                        var nodeTable = new Table().Border(TableBorder.Simple).Title("[bold yellow]Node Elements[/]");
                        nodeTable.AddColumn("Node Type");
                        nodeTable.AddColumn("Node ID");
                        foreach (var el in targetGraph.Elements.Values)
                        {
                            nodeTable.AddRow(Markup.Escape(el.GetType().Name), el.Id.ToString());
                        }
                        AnsiConsole.Write(nodeTable);
                    }
                    ConsoleLogger.PrintPrompt();
                    break;

                default:
                    ConsoleLogger.WriteLog(ConsoleColor.Yellow, "WARN", "Usage: flow <name|id> [enable|disable|toggle|info]");
                    break;
            }
            return 0;
        }
    }

    public sealed class CreateFlowCommand : Command<CreateFlowCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "[name]")]
            [Description("Name of new node flow graph")]
            public string? Name { get; set; }
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var name = string.IsNullOrWhiteSpace(settings.Name) ? "New CLI Flow" : settings.Name;
            var nodeManager = NodeManager.GetInstance();
            var graph = new NodeGraph
            {
                Name = { Value = name },
                Enabled = { Value = true }
            };
            nodeManager.Graphs.Add(graph);
            graph.Serialise();
            ConsoleLogger.WriteLog(ConsoleColor.Green, "FLOW", $"Created flow graph '{graph.Name.Value}' (ID: {graph.Id}).");
            return 0;
        }
    }

    public sealed class ImportFlowCommand : Command<ImportFlowCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<path>")]
            [Description("File path of flow graph JSON to import")]
            public string Path { get; set; } = string.Empty;
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            if (!File.Exists(settings.Path))
            {
                ConsoleLogger.WriteLog(ConsoleColor.Red, "ERROR", $"File not found: {settings.Path}");
                return 1;
            }

            try
            {
                var graph = NodeManager.GetInstance().ImportGraph(settings.Path);
                ConsoleLogger.WriteLog(ConsoleColor.Green, "FLOW", $"Imported flow graph '{graph.Name.Value}' (ID: {graph.Id}).");
                return 0;
            }
            catch (Exception e)
            {
                ConsoleLogger.WriteLog(ConsoleColor.Red, "ERROR", $"Failed to import flow graph: {e.Message}");
                return 1;
            }
        }
    }

    public sealed class ExportFlowCommand : Command<ExportFlowCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "<name_or_id>")]
            [Description("Flow graph name or GUID ID")]
            public string Identifier { get; set; } = string.Empty;

            [CommandArgument(1, "<path>")]
            [Description("Destination file path for JSON export")]
            public string Path { get; set; } = string.Empty;
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var targetGraph = NodeManager.GetInstance().Graphs.FirstOrDefault(g => string.Equals(g.Id.ToString(), settings.Identifier, StringComparison.OrdinalIgnoreCase) || string.Equals(g.Name.Value, settings.Identifier, StringComparison.OrdinalIgnoreCase));
            if (targetGraph is null)
            {
                ConsoleLogger.WriteLog(ConsoleColor.Red, "ERROR", $"Flow graph '{settings.Identifier}' not found.");
                return 1;
            }

            try
            {
                var serialisable = new SerialisableNodeGraph(targetGraph);
                var json = JsonConvert.SerializeObject(serialisable, Formatting.Indented);
                File.WriteAllText(settings.Path, json);
                ConsoleLogger.WriteLog(ConsoleColor.Green, "FLOW", $"Exported flow graph '{targetGraph.Name.Value}' to '{settings.Path}'.");
                return 0;
            }
            catch (Exception e)
            {
                ConsoleLogger.WriteLog(ConsoleColor.Red, "ERROR", $"Failed to export flow graph: {e.Message}");
                return 1;
            }
        }
    }

    public sealed class CreateTestFlowCommand : Command<CreateTestFlowCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "[name]")]
            [Description("Name for demo test flow graph")]
            public string? Name { get; set; }
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var name = string.IsNullOrWhiteSpace(settings.Name) ? "Test Flow Demo" : settings.Name;
            var graph = new NodeGraph
            {
                Name = { Value = name },
                Enabled = { Value = true }
            };

            var nodeTypes = NodeTypeManager.Data.Values.SelectMany(d => d.LinkedTypes).ToList();
            var startNodeType = nodeTypes.FirstOrDefault(t => t.Name == "OnStartNode") ?? nodeTypes.FirstOrDefault(t => t.Name.Contains("Start"));

            if (startNodeType is not null)
            {
                graph.AddNode(startNodeType);
            }

            NodeManager.GetInstance().Graphs.Add(graph);
            graph.Serialise();
            ConsoleLogger.WriteLog(ConsoleColor.Green, "FLOW", $"Created test flow '{graph.Name.Value}' with {graph.Elements.Count} nodes (ID: {graph.Id}).");
            return 0;
        }
    }
}
