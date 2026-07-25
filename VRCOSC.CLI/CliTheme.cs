// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using Spectre.Console;
using Spectre.Console.Rendering;
using Color = Spectre.Console.Color;

namespace VRCOSC.CLI;

internal static class CliTheme
{
    public static readonly Color Primary = Color.Cyan1;
    public static readonly Color Secondary = Color.Yellow;
    public static readonly Color Success = Color.Green;
    public static readonly Color Error = Color.Red;
    public static readonly Color Warning = Color.Orange1;
    public static readonly Color Muted = Color.Grey;

    public static Table CreateTable(string title, params string[] columns)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Primary)
            .Title($"[bold cyan]{Markup.Escape(title)}[/]");

        foreach (var col in columns)
        {
            table.AddColumn($"[bold yellow]{Markup.Escape(col)}[/]");
        }

        return table;
    }

    public static Panel CreatePanel(string title, string content, Color? borderColor = null)
    {
        return new Panel(new Markup(content))
        {
            Header = new PanelHeader($"[bold cyan] {Markup.Escape(title)} [/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(borderColor ?? Primary)
        };
    }

    public static Panel CreatePanel(string title, IRenderable content, Color? borderColor = null)
    {
        return new Panel(content)
        {
            Header = new PanelHeader($"[bold cyan] {Markup.Escape(title)} [/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(borderColor ?? Primary)
        };
    }

    public static string FormatStatusBadge(bool isRunning, bool isEnabled)
    {
        if (isRunning) return "[bold green]RUNNING[/]";
        if (isEnabled) return "[yellow]ENABLED[/]";
        return "[dim]OFF[/]";
    }

    public static string FormatState(string state)
    {
        return state.ToLowerInvariant() switch
        {
            "started" => $"[bold green]{state}[/]",
            "waiting" => $"[bold yellow]{state}[/]",
            "stopping" => $"[bold orange1]{state}[/]",
            _ => $"[bold red]{state}[/]"
        };
    }
}
