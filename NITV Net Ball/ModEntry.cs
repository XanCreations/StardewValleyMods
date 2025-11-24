using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace NITVNetBall
{
    public class ModEntry : Mod
    {
        public static ModEntry Instance { get; private set; }

        public override void Entry(IModHelper helper)
        {
            Instance = this;

            try
            {
                var harmony = new Harmony(this.ModManifest.UniqueID);
                harmony.Patch(
                    original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.checkAction)),
                    prefix: new HarmonyMethod(
                        typeof(GameLocation_checkAction_NetBallPatch),
                        nameof(GameLocation_checkAction_NetBallPatch.Prefix)
                    )
                );
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Harmony patch failed: {ex}", LogLevel.Error);
            }
        }
    }
}
