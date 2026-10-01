using Wardrobe.Configuration;

namespace Wardrobe.Services
{
    internal static class Log
    {
        public static void Always(string msg) => Wardrobe.Logger.LogInfo(msg);

        public static void Info(string msg)
        {
            if (PluginConfig.LoggingLevel.Value >= VerbosityLevel.Debug)
                Wardrobe.Logger.LogInfo(msg);
        }

        public static void Verbose(string msg)
        {
            if (PluginConfig.LoggingLevel.Value >= VerbosityLevel.Verbose)
                Wardrobe.Logger.LogDebug(msg);
        }
    }
}
