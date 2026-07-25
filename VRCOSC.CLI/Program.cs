// Copyright (c) VolcanicArts. Licensed under the GPL-3.0 License.
// See the LICENSE file in the repository root for full license text.

using System;
using System.Linq;
using System.Threading.Tasks;
using VRCOSC.App;
using VRCOSC.App.ChatBox;
using VRCOSC.App.Dolly;
using VRCOSC.App.Modules;
using VRCOSC.App.Nodes;
using VRCOSC.App.Packages;
using VRCOSC.App.Profiles;
using VRCOSC.App.Router;
using VRCOSC.App.SDK.Handlers;
using VRCOSC.App.SDK.VRChat.Logs;
using VRCOSC.App.SDK.VRChat.Logs.Handlers;
using VRCOSC.App.Settings;
using VRCOSC.App.Startup;
using VRCOSC.App.Utils;
using VRCOSC.CLI.Commands;

namespace VRCOSC.CLI;

public class Program : IVRCClientEventHandler
{
    private static bool isRunning = true;
    private static Program instance = null!;

    public static async Task Main(string[] args)
    {
        try
        {
            try
            {
                Console.Title = $"{AppManager.APP_NAME} CLI {AppManager.Version}";
            }
            catch
            {
            }

            ConsoleLogger.WriteHeader();

            instance = new Program();

            // 1. Initialize Logger
            Logger.NewEntry += ConsoleLogger.OnLoggerNewEntry;

            // 2. Initialize Engine Managers
            Console.WriteLine("[CLI] Loading configuration and initializing managers...");
            SettingsManager.GetInstance().Load();

            var appManager = AppManager.GetInstance();
            appManager.Initialise();

            await PackageManager.GetInstance().Load();
            ProfileManager.GetInstance().Load();
            ModuleManager.GetInstance().LoadAllModules();
            NodeManager.GetInstance().Load();
            ChatBoxManager.GetInstance().Load();
            RouterManager.GetInstance().Load();
            DollyManager.GetInstance().Load();
            StartupManager.GetInstance().Load();

            appManager.InitialLoadComplete();

            // 3. Register Event Listeners
            appManager.VRChatOscClient.OnVRChatOSCMessageReceived += ConsoleLogger.OnOSCMessageReceived;
            appManager.VRChatOscClient.OnVRChatOSCMessageSent += ConsoleLogger.OnOSCMessageSent;

            // 4. Engine Connection Mode Startup
            var shouldForceStart = args.Any(a => string.Equals(a, "--start", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "--force-start", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-s", StringComparison.OrdinalIgnoreCase));

            if (shouldForceStart)
            {
                ConsoleLogger.WriteLog(ConsoleColor.Magenta, "STARTUP", "Force starting VRCOSC Engine modules (--start flag detected)...");
                _ = appManager.ForceStart();
            }
            else
            {
                ConsoleLogger.WriteLog(ConsoleColor.Cyan, "STARTUP", "Starting VRCOSC Engine in auto-connection mode (waiting for VRChat & VRCQuery)...");
                _ = appManager.RequestStart();
            }

            // 5. Run Interactive Command Loop
            ConsoleLogger.PrintPrompt();
            await CommandProcessor.RunCommandLoop(appManager, () => isRunning, () => isRunning = false);

            // 6. Graceful Shutdown
            ConsoleLogger.WriteLog(ConsoleColor.Yellow, "SHUTDOWN", "Stopping VRCOSC Engine...");
            await appManager.StopAsync();
            ConsoleLogger.WriteLog(ConsoleColor.Green, "SHUTDOWN", "VRCOSC CLI stopped gracefully.");
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[FATAL CLI EXCEPTION] {ex}\n");
            Console.ResetColor();
        }
    }

    public void HandleClientEvent(IVRChatClientEvent @event)
    {
        ConsoleLogger.HandleClientEvent(@event);
    }
}
