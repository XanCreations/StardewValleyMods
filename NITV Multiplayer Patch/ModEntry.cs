using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewModdingAPI.Events;

namespace NITVMultiplayerPatch
{
    public class ModEntry : Mod
    {
        internal static ModEntry Instance { get; private set; }
        internal Harmony Harmony { get; private set; }

        // The Nature In The Valley main entry type, resolved at runtime via reflection
        internal static Type NitvEntryType { get; private set; }

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            Harmony = new Harmony(this.ModManifest.UniqueID);

            try
            {
                // Resolve the NatureInTheValley.NatureInTheValleyEntry
                NitvEntryType = AccessTools.TypeByName("NatureInTheValley.NatureInTheValleyEntry");
                if (NitvEntryType == null)
                {
                    this.Monitor.Log(
                        "Couldn't find type NatureInTheValley.NatureInTheValleyEntry. " +
                        "Is Nature In The Valley installed and enabled?",
                        LogLevel.Error
                    );
                    return;
                }

                // NatureInTheValleyEntry.Instantiate
                Harmony.Patch(
                    original: AccessTools.Method(
                        NitvEntryType,
                        "Instantiate",
                        new[] { typeof(string), typeof(Microsoft.Xna.Framework.Vector2), typeof(GameLocation) }
                    ),
                    postfix: new HarmonyMethod(
                        typeof(NITVPatches),
                        nameof(NITVPatches.Instantiate_Postfix)
                    )
                );

                // NatureInTheValleyEntry.ClearUnclaimedCreatures
                Harmony.Patch(
                    original: AccessTools.Method(
                        NitvEntryType,
                        "ClearUnclaimedCreatures",
                        new[] { typeof(GameLocation) }
                    ),
                    prefix: new HarmonyMethod(
                        typeof(NITVPatches),
                        nameof(NITVPatches.ClearUnclaimedCreatures_Prefix)
                    )
                );

                // NatureInTheValleyEntry.OnWarp
                Harmony.Patch(
                    original: AccessTools.Method(
                        NitvEntryType,
                        "OnWarp",
                        new[] { typeof(object), typeof(StardewModdingAPI.Events.WarpedEventArgs) }
                    ),
                    postfix: new HarmonyMethod(
                        typeof(NITVPatches),
                        nameof(NITVPatches.OnWarp_Postfix)
                    )
                );

                // NatureInTheValleyEntry.OneSecond
                Harmony.Patch(
                    original: AccessTools.Method(
                        NitvEntryType,
                        "OneSecond",
                        new[] { typeof(object), typeof(OneSecondUpdateTickedEventArgs) }
                    ),
                    prefix: new HarmonyMethod(
                        typeof(NITVPatches),
                        nameof(NITVPatches.OneSecond_Prefix)
                    )
                );

                // NatureInTheValleyEntry.TrySpawnFromArea
                var trySpawnFromArea = AccessTools.Method(
                    NitvEntryType,
                    "TrySpawnFromArea",
                    new[] { typeof(GameLocation) }
                );

                if (trySpawnFromArea != null)
                {
                    Harmony.Patch(
                        original: trySpawnFromArea,
                        prefix: new HarmonyMethod(
                            typeof(NITVPatches),
                            nameof(NITVPatches.TrySpawnFromArea_Prefix)
                        ),
                        postfix: new HarmonyMethod(
                            typeof(NITVPatches),
                            nameof(NITVPatches.TrySpawnFromArea_Postfix)
                        )
                    );
                }

            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Harmony patches failed: {ex}", LogLevel.Error);
            }
        }
    }
}
