using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace OBSPlugin
{
    internal class Svc
    {
        [PluginService]
        internal static IPluginLog PluginLog { get; private set; } = null!;

        [PluginService]
        internal static IDutyState DutyState { get; private set; } = null!;

        [PluginService]
        internal static IDataManager DataManager { get; private set; } = null!;

        [PluginService]
        internal static ICommandManager Commands { get; private set; } = null!;

        [PluginService]
        internal static IChatGui Chat { get; private set; } = null!;

        [PluginService]
        internal static IClientState ClientState { get; private set; } = null!;

        [PluginService]
        internal static IObjectTable ObjectTable { get; private set; } = null!;

        [PluginService]
        internal static IFramework Framework { get; private set; } = null!;

        [PluginService]
        internal static IGameGui GameGui { get; private set; } = null!;

        [PluginService]
        internal static ISigScanner SigScanner { get; private set; } = null!;

        [PluginService]
        internal static ICondition Condition { get; private set; } = null!;

        [PluginService]
        internal static IGameInteropProvider GameInteropProvider { get; private set; } = null!;
    }
}
