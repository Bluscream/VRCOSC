// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using Spectre.Console;
using Spectre.Console.Cli;
using Color = Spectre.Console.Color;
using VRCOSC.App;
using VRCOSC.App.Profiles;

namespace VRCOSC.CLI.Commands;

internal static class ProfileCommands
{
    public static void DisplayProfiles()
    {
        var profileManager = ProfileManager.GetInstance();
        var active = profileManager.ActiveProfile.Value;

        var table = CliTheme.CreateTable("Available Profiles", "Active", "Profile Name", "Profile ID");

        foreach (var p in profileManager.Profiles)
        {
            var isCurrent = p == active;
            table.AddRow(
                isCurrent ? "[bold green]*[/]" : " ",
                isCurrent ? $"[bold green]{Markup.Escape(p.Name.Value)}[/]" : Markup.Escape(p.Name.Value),
                p.ID.ToString()
            );
        }

        AnsiConsole.Write(table);
        ConsoleLogger.PrintPrompt();
    }

    public sealed class ProfilesCommand : Command
    {
        protected override int Execute(CommandContext context, CancellationToken cancellationToken)
        {
            DisplayProfiles();
            return 0;
        }
    }

    public sealed class ProfileSwitchCommand : Command<ProfileSwitchCommand.Settings>
    {
        public sealed class Settings : CommandSettings
        {
            [CommandArgument(0, "[name_or_id]")]
            [Description("Profile Name or GUID ID to switch to")]
            public string? Identifier { get; set; }
        }

        protected override int Execute(CommandContext context, Settings settings, CancellationToken cancellationToken)
        {
            var profileManager = ProfileManager.GetInstance();
            var profiles = profileManager.Profiles.ToList();

            if (profiles.Count == 0)
            {
                ConsoleLogger.WriteLog(ConsoleColor.Yellow, "WARN", "No profiles available.");
                return 1;
            }

            string selectedIdentifier;

            if (string.IsNullOrWhiteSpace(settings.Identifier) && !Console.IsInputRedirected)
            {
                selectedIdentifier = AnsiConsole.Prompt(
                    new SelectionPrompt<string>()
                        .Title("[bold cyan]Select Profile to Activate:[/]")
                        .PageSize(10)
                        .AddChoices(profiles.Select(p => $"{p.Name.Value} ({p.ID})"))
                );

                var parts = selectedIdentifier.Split('(');
                if (parts.Length > 1)
                {
                    selectedIdentifier = parts[^1].TrimEnd(')').Trim();
                }
            }
            else
            {
                selectedIdentifier = settings.Identifier ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(selectedIdentifier))
            {
                DisplayProfiles();
                return 0;
            }

            var targetProfile = profiles.FirstOrDefault(p => string.Equals(p.ID.ToString(), selectedIdentifier, StringComparison.OrdinalIgnoreCase) || string.Equals(p.Name.Value, selectedIdentifier, StringComparison.OrdinalIgnoreCase));
            if (targetProfile is null)
            {
                ConsoleLogger.WriteLog(ConsoleColor.Red, "ERROR", $"Profile '{selectedIdentifier}' not found.");
                return 1;
            }

            ConsoleLogger.WriteLog(ConsoleColor.Green, "PROFILE", $"Switching profile to '{targetProfile.Name.Value}'...");
            AppManager.GetInstance().ChangeProfile(targetProfile);
            return 0;
        }
    }
}
