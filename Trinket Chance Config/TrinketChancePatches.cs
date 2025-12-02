using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using System.Linq;

namespace TrinketChanceConfig
{
    internal static class TrinketChancePatches
    {
        // Apply Harmony patches for Trinket drop logic
        public static void Apply(Harmony harmony, ModEntry mod)
        {
            // Use a Trinket.TrySpawnTrinket transpiler
            try
            {
                var asm = typeof(Game1).Assembly;

                // Try full names first (just in case)
                Type trinketType =
                    asm.GetType("StardewValley.Trinket") ??
                    asm.GetType("StardewValley.Objects.Trinket");

                // Otherwise fall back to scanning all types by simple name
                if (trinketType == null)
                    trinketType = asm.GetTypes().FirstOrDefault(t => t.Name == "Trinket");

                if (trinketType == null)
                {
                    mod.Monitor.Log(
                        $"Failed to resolve Trinket type in assembly '{asm.FullName}' ",
                        LogLevel.Warn
                    );
                    return;
                }

                var trySpawnTrinketMethod = AccessTools.Method(trinketType, "TrySpawnTrinket");
                if (trySpawnTrinketMethod == null)
                {
                    mod.Monitor.Log("Failed to find Trinket.TrySpawnTrinket", LogLevel.Warn);
                }
                else
                {
                    var transpiler = new HarmonyMethod(
                        typeof(TrinketChancePatches),
                        nameof(Trinket_TrySpawnTrinket_Transpiler)
                    );

                    harmony.Patch(trySpawnTrinketMethod, transpiler: transpiler);
                }
            }
            catch (Exception ex)
            {
                mod.Monitor.Log($"Error applying Trinket.TrySpawnTrinket patch: {ex}", LogLevel.Error);
            }

            // Filter the vanilla trinkets from monster drops
            try
            {
                var monsterDrop = AccessTools.Method(typeof(GameLocation), "monsterDrop");
                if (monsterDrop != null)
                {
                    harmony.Patch(
                        monsterDrop,
                        postfix: new HarmonyMethod(
                            typeof(TrinketChancePatches),
                            nameof(GameLocation_monsterDrop_Postfix)
                        )
                    );
                }
                else
                {
                    mod.Monitor.Log(
                        "Failed to find GameLocation.monsterDrop.",
                        LogLevel.Warn
                    );
                }
            }
            catch (Exception ex)
            {
                mod.Monitor.Log($"Error applying GameLocation.monsterDrop patch: {ex}", LogLevel.Error);
            }

            // Patch BreakableContainer.releaseContents to remove vanilla trinkets from crate drops
            try
            {
                var breakableType = typeof(BreakableContainer);

                // Be careful of signature changes -> find any method named "releaseContents"
                var releaseContents = AccessTools
                    .GetDeclaredMethods(breakableType)
                    .FirstOrDefault(m => m.Name == "releaseContents");

                if (releaseContents == null)
                {
                    mod.Monitor.Log(
                        "Failed to find any BreakableContainer.releaseContents method.",
                        LogLevel.Warn
                    );
                }
                else
                {
                    harmony.Patch(
                        releaseContents,
                        postfix: new HarmonyMethod(
                            typeof(TrinketChancePatches),
                            nameof(BreakableContainer_releaseContents_Postfix)
                        )
                    );
                }
            }
            catch (Exception ex)
            {
                mod.Monitor.Log(
                    $"Error applying BreakableContainer.releaseContents postfix: {ex}",
                    LogLevel.Error
                );
            }
        }

        // Transpiler that wraps every call to Random.NextDouble() in Trinket.TrySpawnTrinket
        public static IEnumerable<CodeInstruction> Trinket_TrySpawnTrinket_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            var nextDouble = AccessTools.Method(typeof(Random), nameof(Random.NextDouble));
            var adjustMethod = AccessTools.Method(typeof(TrinketChancePatches), nameof(AdjustRandomSample));

            for (int i = 0; i < codes.Count; i++)
            {
                var code = codes[i];
                yield return code;

                // If it's a call to Random.NextDouble, inject a call to AdjustRandomSample(double)
                if ((code.opcode == OpCodes.Call || code.opcode == OpCodes.Callvirt) &&
                    code.operand is MethodInfo mi &&
                    mi == nextDouble)
                {
                    // Stack double from NextDouble
                    // Call AdjustRandomSample(double) -> transform it
                    yield return new CodeInstruction(OpCodes.Call, adjustMethod);
                }
            }
        }

        // Modify the random sample returned by Random.NextDouble() according to the config multiplier
        public static double AdjustRandomSample(double sample)
        {
            var config = ModEntry.Config;
            if (config == null)
                return sample;

            // Convert 0 - 200% to 0.0 - 2.0 multiplier
            double multiplier = Math.Max(0, config.TrinketChancePercentOfBase) / 100.0;

            // If multiplier is 0 -> force to 1.0 so "< p" checks never pass
            if (multiplier <= 0.0)
                return 1.0;

            // Scaling:
            // multiplier = 2.0 -> P = min(1, p * 2)
            // multiplier = 0.5 -> P = p * 0.5
            double adjusted = sample / multiplier;

            if (adjusted < 0.0)
                adjusted = 0.0;
            else if (adjusted > 1.0)
                adjusted = 1.0;

            return adjusted;
        }

        // Had to add filtering helpers to prevent vanilla trinkets from dropping via debris when containers are broken
        // This should be called after monsterDrop() runs
        public static void GameLocation_monsterDrop_Postfix(GameLocation __instance)
        {
            if (__instance == null)
                return;

            // Existing vanilla-trinket blocking
            FilterVanillaTrinketDebris(__instance);

            // Added duplicate penalty filtering
            FilterDuplicateTrinketDebris(__instance);
        }

        // Called after BreakableContainer.releaseContents runs
        public static void BreakableContainer_releaseContents_Postfix(BreakableContainer __instance, Farmer who)
        {
            if (who == null)
                return;

            var location = who.currentLocation;
            if (location == null)
                return;

            // Existing vanilla-trinket blocking
            FilterVanillaTrinketDebris(location);

            // Added duplicate penalty filtering
            FilterDuplicateTrinketDebris(location);
        }

        // Remove any debris in this location (for vanilla trinkets) only if the config toggle is enabled
        private static void FilterVanillaTrinketDebris(GameLocation location)
        {
            if (location == null || location.debris == null)
                return;

            var config = ModEntry.Config;
            if (config == null || !config.BlockVanillaTrinkets)
                return;

            if (location.debris.Count == 0)
                return;

            int removed = 0;

            // There's no "removeAll" or anything like that, so this is my attempt to walk it backwards
            for (int i = location.debris.Count - 1; i >= 0; i--)
            {
                var debris = location.debris[i];
                var item = debris?.item;
                if (item == null)
                    continue;

                // QualifiedItemId based on 1.6 but it's good practice to have a fallback to ItemId just in case
                string id = item.QualifiedItemId ?? item.ItemId;

                if (ModEntry.IsVanillaTrinketId(id))
                {
                    location.debris.RemoveAt(i);
                    removed++;
                }
            }
        }

        // Cull duplicate trinket debris based on the per-save "seen trinkets" data
        private static void FilterDuplicateTrinketDebris(GameLocation location)
        {
            if (location?.debris == null || location.debris.Count == 0)
                return;

            var config = ModEntry.Config;
            if (config == null)
                return;

            // If duplicates are allowed, do nothing
            if (!config.DisableDuplicateTrinketDrops)
                return;

            for (int i = location.debris.Count - 1; i >= 0; i--)
            {
                var debris = location.debris[i];
                if (debris?.item is StardewValley.Objects.Trinkets.Trinket trinket)
                {
                    // If this specific debris has already been evaluated once -> don't touch it again
                    // This prevents trinkets disappearing when monsterDrop runs multiple times (this ended up being a bug that I found and this was the fix)
                    var modData = trinket.modData;
                    if (modData != null && modData.ContainsKey(ModEntry.DuplicateCheckedModDataKey))
                        continue;

                    string id = trinket.QualifiedItemId ?? trinket.ItemId;

                    // ShouldKeepDuplicateTrinketDrop handles the per-save 'seen' logic
                    if (!ModEntry.ShouldKeepDuplicateTrinketDrop(id))
                    {
                        // This drop is a duplicate and should be culled
                        location.debris.RemoveAt(i);
                    }
                    else
                    {
                        // This drop is allowed -> mark it so it isn't evaluated again and accidentally removed
                        if (modData != null)
                            modData[ModEntry.DuplicateCheckedModDataKey] = "true";
                    }
                }
            }
        }

    }
}
