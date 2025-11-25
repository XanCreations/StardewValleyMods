using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using System.Linq;

namespace TrinketChanceConfig
{
    internal static class TrinketChancePatches
    {
        // Apply Harmony patches for Trinket drop logic
        public static void Apply(Harmony harmony, ModEntry mod)
        {
            try
            {
                var asm = typeof(Game1).Assembly;

                // Try full names first (just in case)
                Type trinketType =
                    asm.GetType("StardewValley.Trinket") ??
                    asm.GetType("StardewValley.Objects.Trinket");

                // Otherwise fall back to scanning all types by simple name
                if (trinketType == null)
                {
                    trinketType = asm.GetTypes().FirstOrDefault(t => t.Name == "Trinket");
                }

                if (trinketType == null)
                {
                    mod.Monitor.Log(
                        $"Failed to resolve Trinket type in assembly '{asm.FullName}' ",
                        LogLevel.Warn
                    );
                    return;
                }

                mod.Monitor.Log($"Resolved Trinket type as '{trinketType.FullName}'.", LogLevel.Trace);

                var trySpawnTrinketMethod = AccessTools.Method(trinketType, "TrySpawnTrinket");
                if (trySpawnTrinketMethod == null)
                {
                    mod.Monitor.Log("Failed to find Trinket.TrySpawnTrinket", LogLevel.Warn);
                    return;
                }

                var transpiler = new HarmonyMethod(typeof(TrinketChancePatches), nameof(Trinket_TrySpawnTrinket_Transpiler));

                harmony.Patch(trySpawnTrinketMethod, transpiler: transpiler);

                mod.Monitor.Log(
                    "Patched Trinket.TrySpawnTrinket to apply configurable drop chance multiplier.",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                mod.Monitor.Log($"Error applying Trinket.TrySpawnTrinket patch: {ex}", LogLevel.Error);
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

                // If this instruction is a call to Random.NextDouble, inject a call to AdjustRandomSample(double)
                if ((code.opcode == OpCodes.Call || code.opcode == OpCodes.Callvirt) &&
                    code.operand is MethodInfo mi &&
                    mi == nextDouble)
                {
                    // Stack double from NextDouble
                    // Call AdjustRandomSample(double) -> Essentially trying to transform it
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

            // Need to force to 1.0 so comparisons like "< p" don't pass
            if (multiplier <= 0.0)
                return 1.0;

            // Scaling
            // multiplier = 2.0 -> P = min(1, p * 2)
            // multiplier = 0.5 -> P = p * 0.5
            double adjusted = sample / multiplier;

            if (adjusted < 0.0)
                adjusted = 0.0;
            else if (adjusted > 1.0)
                adjusted = 1.0;

            return adjusted;
        }
    }
}
