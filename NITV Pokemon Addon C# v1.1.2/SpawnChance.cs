using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using Microsoft.Xna.Framework;


namespace NITVPokemonAddon
{
    internal static class SpawnChance
    {
        // Fields wired at runtime
        private static Dictionary<string, double> _spawnChances;
        private static FieldInfo _locationalDataField;
        private static FieldInfo _spawnListField;
        private static MethodInfo _handleRarityMethod;

        // NITV reflection (for Net Ball integration)
        private static Type _nivEntryType;
        private static object _nivEntryInstance;
        private static MethodInfo _creaturesForAreaMethod;
        private static MethodInfo _instantiateMethod;

        // Pokemon with chance ≤ are considered as a "rare pass" first
        private const double RareThreshold = 0.20;

        // Multiply all "_Shiny" entries before rolling
        // 0.1 -> 10x rarer than listed chance
        private const double ShinyMultiplier = 0.1;

        // Rough pattern: every Nth spawn-list slot is considered "Pokemon first"
        // So every 3rd slot will spawn 1 Pokemon -> The rest are NITV creatures
        private const int PokemonSlotInterval = 3;


        // Public entry point from ModEntry
        public static void Initialize(IModHelper helper, Harmony harmony)
        {
            try
            {
                // Load SpawnChanceList json with configurable spawn chances for each Pokemon spawn
                _spawnChances = helper.Data.ReadJsonFile<Dictionary<string, double>>("assets/SpawnChanceList.json")
                                ?? new Dictionary<string, double>();

                if (_spawnChances.Count == 0)
                {
                    ModEntry.Instance.Monitor.Log(
                        "SpawnChance: SpawnChanceList.json is empty or missing.",
                        LogLevel.Trace
                    );
                    return;
                }

                var entryType = AccessTools.TypeByName("NatureInTheValley.NatureInTheValleyEntry, NatureInTheValley");
                if (entryType == null)
                {
                    ModEntry.Instance.Monitor.Log(
                        "SpawnChance: Couldn't find NatureInTheValleyEntry.",
                        LogLevel.Warn
                    );
                    return;
                }

                _locationalDataField = AccessTools.Field(entryType, "locationalData");
                _spawnListField      = AccessTools.Field(entryType, "CreatureSpawnList");
                _handleRarityMethod  = AccessTools.Method(
                    entryType,
                    "HandleRarityChance",
                    new[] { typeof(Dictionary<string, List<string>>) }
                );

                if (_locationalDataField == null || _spawnListField == null || _handleRarityMethod == null)
                {
                    ModEntry.Instance.Monitor.Log(
                        "SpawnChance: missing required NITV fields/methods.",
                        LogLevel.Warn
                    );
                    return;
                }

                //MakeCreatueSpawnList -> no "r" (there are some typos in NITV framework)
                var makeListMethod = AccessTools.Method(entryType, "MakeCreatueSpawnList", Type.EmptyTypes);
                if (makeListMethod == null)
                {
                    ModEntry.Instance.Monitor.Log(
                        "SpawnChance: Couldn't find MakeCreatueSpawnList.",
                        LogLevel.Warn
                    );
                    return;
                }

                // Patch MakeCreatueSpawnList to build the list
                harmony.Patch(
                    original: makeListMethod,
                    prefix: new HarmonyMethod(typeof(SpawnChance), nameof(MakeCreatueSpawnListPrefix))
                );

                ModEntry.Instance.Monitor.Log(
                    $"SpawnChance: loaded spawn chances for {_spawnChances.Count} creatures.",
                    LogLevel.Trace
                );
                // If Net Ball mod is installed, hook its spawn pipeline so Net Balls respect Pokemon spawn chances (including shinies).
                TryRegisterNetBallHook(helper);

            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"SpawnChance.Initialize failed: {ex}", LogLevel.Error);
            }
        }

        // Harmony prefix that replaces MakeCreatueSpawnList
        public static bool MakeCreatueSpawnListPrefix(object __instance)
        {
            try
            {
                if (_spawnChances == null ||
                    _locationalDataField == null ||
                    _spawnListField == null)
                {
                    // otherwise let vanilla run instead
                    return true;
                }

                var locationalData = _locationalDataField.GetValue(__instance) as Dictionary<string, List<string>>;
                var spawnList = _spawnListField.GetValue(__instance) as List<string>;

                if (locationalData == null || spawnList == null || locationalData.Count == 0)
                    return true;

                //  Pokemon: keys that exist in SpawnChanceList.json
                //  Non-Pokemon: everything else (still uses NITV rarity and list)
                var pokemon = new List<(string Key, double Chance)>();
                var nonPokemon = new Dictionary<string, List<string>>();

                foreach (var kv in locationalData)
                {
                    string bareKey = kv.Key; // e.g. "Pidgey_Roam" or "Pidgey_Roam_Shiny"
                    string fullKey = "NatInValley.Creature." + bareKey;

                    if (_spawnChances.TryGetValue(fullKey, out double chance) && chance > 0.0)
                    {
                        // Added an extra nerf for shinies based on name to have greater control
                        if (bareKey.Contains("_Shiny", StringComparison.OrdinalIgnoreCase))
                        {
                            chance *= ShinyMultiplier;
                        }

                        // Might be some strangeness with the spawnchance json when testing, so this is just to keep it clamped down
                        if (chance > 1.0)
                            chance = 1.0;
                        else if (chance < 0.0)
                            chance = 0.0;

                        pokemon.Add((bareKey, chance));
                    }
                    else
                    {
                        nonPokemon[bareKey] = kv.Value;
                    }
                }

                // If no Pokemon entries exist for this location, let vanilla NITV handle it
                if (pokemon.Count == 0)
                    return true;

                // Check if there are any normal NITV creatures
                bool hasNonPokemon = _handleRarityMethod != null && nonPokemon.Count > 0;

                spawnList.Clear();

                for (int i = 0; i < 30; i++)
                {
                    // If there are no normal NITV creatures for this location then keep the pokemon spawn behaviour
                    if (!hasNonPokemon)
                    {
                        string pickOnlyPokemon = RollPokemon(pokemon);
                        if (!string.IsNullOrEmpty(pickOnlyPokemon))
                            spawnList.Add(pickOnlyPokemon);

                        // If RollPokemon returns null, then the spawn slot is just left empty
                        continue;
                    }

                    // There are both Pokemon and NITV creatures -> Decide which group gets priority for this spawn slot
                    bool pokemonSlot = (i % PokemonSlotInterval == 0);

                    if (pokemonSlot)
                    {
                        // Try Pokemon first for this spawn slot
                        string pick = RollPokemon(pokemon);
                        if (!string.IsNullOrEmpty(pick))
                        {
                            spawnList.Add(pick);
                            continue;
                        }

                        // If no Pokemon succeeded this slot, try vanilla NITV creatures
                        var args = new object[] { nonPokemon };
                        string fallback = _handleRarityMethod.Invoke(__instance, args) as string;
                        if (!string.IsNullOrEmpty(fallback))
                            spawnList.Add(fallback);
                    }
                    else
                    {
                        // This is a non-Pokemon slot -> try vanilla NITV creatures first
                        var args = new object[] { nonPokemon };
                        string fallback = _handleRarityMethod.Invoke(__instance, args) as string;
                        if (!string.IsNullOrEmpty(fallback))
                        {
                            spawnList.Add(fallback);
                            continue;
                        }

                        // If vanilla didn't pick anything, fall back to Pokemon
                        string pick = RollPokemon(pokemon);
                        if (!string.IsNullOrEmpty(pick))
                            spawnList.Add(pick);
                    }
                }

                // skip the original method
                return false;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"SpawnChance.MakeCreatueSpawnListPrefix failed: {ex}", LogLevel.Error);
                return true; // fail-safe -> let vanilla run
            }
        }

        //Hook the Net Ball mod so when it spawns Pokemon it uses Pokemon SpawnChance rules
        private static void TryRegisterNetBallHook(IModHelper helper)
        {
            try
            {
                // Find the type by name across all loaded assemblies
                Type nbType = AccessTools.TypeByName("NITVNetBall.NetBallMethods");
                if (nbType == null)
                {
                    ModEntry.Instance.Monitor.Log("[SpawnChance] NetBall not found (NITVNetBall.NetBallMethods missing).", LogLevel.Trace);
                    return;
                }

                FieldInfo field = AccessTools.Field(nbType, "ExternalSpawnResolver");
                if (field == null)
                {
                    ModEntry.Instance.Monitor.Log("[SpawnChance] NetBallMethods.ExternalSpawnResolver field not found.", LogLevel.Warn);
                    return;
                }

                MethodInfo handler = AccessTools.Method(typeof(SpawnChance), nameof(HandleNetBallSpawn));
                if (handler == null)
                {
                    ModEntry.Instance.Monitor.Log("[SpawnChance] HandleNetBallSpawn method not found.", LogLevel.Error);
                    return;
                }

                // Create a delegate of Func<> type declared from Net Ball
                Delegate del = Delegate.CreateDelegate(field.FieldType, handler);
                field.SetValue(null, del);

                ModEntry.Instance.Monitor.Log("[SpawnChance] Installed Net Ball ExternalSpawnResolver hook successfully.", LogLevel.Info);
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log("[SpawnChance] Failed to install Net Ball hook: " + ex, LogLevel.Error);
            }
        }

        // Called by the Net Ball mod when its chance/proximity checks have passed
        // Return true if a Pokemon was spawned
        private static bool HandleNetBallSpawn(GameLocation location, Vector2 landingPos, Farmer who, string usedBallName, Vector2 spawnTile)
        {
            try
            {
                if (!EnsureNivReady(ModEntry.Instance.Helper))
                    return false;

                // Get NITV locationalData, filter to water creatures and then use RollPokemon with ShinyMultiplier.
                var locationalData = GetLocationalDataForLocation(location); // use existing helper/reflection wrapper

                // Build candidates that are water spawns (localSpawnCode == "3")
                var candidates = BuildPokemonCandidatesFromLocationalData(locationalData, waterOnly: true);

                if (candidates.Count == 0)
                    return false;

                // Independent rolls per Pokemon, RareThreshold logic, ShinyMultiplier
                string chosenKey = RollPokemon(candidates);

                if (string.IsNullOrEmpty(chosenKey))
                    return false;

                // Spawn via NITV Instantiate (same signature Net Ball uses)
                InstantiateViaNiv(chosenKey, spawnTile, location); // <-- use your existing Instantiate reflection helper

                ModEntry.Instance.Monitor.Log($"[SpawnChance] Net Ball resolver spawned '{chosenKey}' at {spawnTile} using PokemonAddon SpawnChance.", LogLevel.Trace);
                return true;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log("[SpawnChance] HandleNetBallSpawn crashed: " + ex, LogLevel.Error);
                return false;
            }
        }

        private static List<(string Key, double Chance)> BuildPokemonCandidatesFromLocationalData(
            Dictionary<string, List<string>> locationalData,
            bool waterOnly
        )
        {
            var pokemon = new List<(string Key, double Chance)>();
            if (locationalData == null || _spawnChances == null)
                return pokemon;

            foreach (var kv in locationalData)
            {
                string bareKey = kv.Key; // e.g. "Pidgey_Roam" or "Pidgey_Roam_Shiny"
                var vals = kv.Value;

                if (waterOnly && !IsWaterSpawn(vals))
                    continue;

                string fullKey = "NatInValley.Creature." + bareKey;

                if (!_spawnChances.TryGetValue(fullKey, out double chance) || chance <= 0.0)
                    continue;

                // Extra nerf for shinies (10x rarer by default)
                if (bareKey.Contains("_Shiny", StringComparison.OrdinalIgnoreCase))
                    chance *= ShinyMultiplier;

                // Clamp
                if (chance > 1.0) chance = 1.0;
                else if (chance < 0.0) chance = 0.0;

                pokemon.Add((bareKey, chance));
            }

            return pokemon;
        }

        // Matches Net Ball's water-only rule (localSpawnCode is at index 17)
        private static bool IsWaterSpawn(List<string> vals)
        {
            if (vals == null)
                return false;

            string code = vals.Count > 17 ? Convert.ToString(vals[17]) : null;
            bool isWater = (code == "3") || (code != "0" && code != "1" && code != "2" && code != "4");
            return isWater;
        }

        private static bool EnsureNivReady(IModHelper helper)
        {
            if (_nivEntryType == null)
                _nivEntryType = AccessTools.TypeByName("NatureInTheValley.NatureInTheValleyEntry, NatureInTheValley");

            if (_nivEntryType == null)
                return false;

            _creaturesForAreaMethod ??= _nivEntryType.GetMethod(
                "CreaturesForArea",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(GameLocation) },
                null
            );

            _instantiateMethod ??= _nivEntryType.GetMethod(
                "Instantiate",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(Vector2), typeof(GameLocation) },
                null
            );

            if (_creaturesForAreaMethod == null || _instantiateMethod == null)
                return false;

            if (_nivEntryInstance != null)
                return true;

            return TryResolveNivEntryInstance(helper, out _nivEntryInstance);
        }

        private static bool TryResolveNivEntryInstance(IModHelper helper, out object entry)
        {
            entry = null;

            try
            {
                object staticHelper = _nivEntryType
                    .GetField("staticHelper", BindingFlags.Public | BindingFlags.Static)
                    ?.GetValue(null);

                if (staticHelper != null && TryCrawlForInstance(staticHelper, _nivEntryType, maxDepth: 3, out entry))
                    return true;

                foreach (var info in helper.ModRegistry.GetAll())
                {
                    if (info != null && TryCrawlForInstance(info, _nivEntryType, maxDepth: 3, out entry))
                        return true;
                }
            }
            catch
            {
                // ignore
            }

            return false;
        }

        private static bool TryCrawlForInstance(object root, Type targetType, int maxDepth, out object found)
        {
            found = null;
            if (root == null || targetType == null)
                return false;

            var visited = new HashSet<object>(RefEqComparer.Instance);
            var queue = new Queue<(object Obj, int Depth)>();

            visited.Add(root);
            queue.Enqueue((root, 0));

            while (queue.Count > 0)
            {
                var (obj, depth) = queue.Dequeue();
                if (obj == null)
                    continue;

                if (targetType.IsInstanceOfType(obj))
                {
                    found = obj;
                    return true;
                }

                if (depth >= maxDepth)
                    continue;

                Type t = obj.GetType();

                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    object val = null;
                    try { val = f.GetValue(obj); } catch { }
                    if (val != null && visited.Add(val))
                        queue.Enqueue((val, depth + 1));
                }

                foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!p.CanRead)
                        continue;

                    if (p.GetIndexParameters().Length != 0)
                        continue;

                    object val = null;
                    try { val = p.GetValue(obj); } catch { }
                    if (val != null && visited.Add(val))
                        queue.Enqueue((val, depth + 1));
                }
            }

            return false;
        }

        private sealed class RefEqComparer : IEqualityComparer<object>
        {
            public static readonly RefEqComparer Instance = new RefEqComparer();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        // Core Pokemon roll -> two passes
        private static string RollPokemon(List<(string Key, double Chance)> candidates)
        {
            if (candidates == null || candidates.Count == 0)
                return null;

            var random = Game1.random;

            // Pass 1: rare-only (Chance ≤ 20%) -> Everything rolls
            var rareWinners = new List<string>();
            foreach (var c in candidates)
            {
                if (c.Chance <= RareThreshold && c.Chance > 0)
                {
                    if (random.NextDouble() <= c.Chance)
                        rareWinners.Add(c.Key);
                }
            }

            if (rareWinners.Count > 0)
            {
                int idx = random.Next(rareWinners.Count);
                return rareWinners[idx];
            }

            // Pass 2: all Pokemon (0–100%) -> Everything rolls
            var winners = new List<string>();
            foreach (var c in candidates)
            {
                if (c.Chance > 0)
                {
                    if (random.NextDouble() <= c.Chance)
                        winners.Add(c.Key);
                }
            }

            if (winners.Count > 0)
            {
                int idx = random.Next(winners.Count);
                return winners[idx];
            }

            // Neither pass spawned anything
            return null;
        }

        private static Dictionary<string, List<string>> GetLocationalDataForLocation(GameLocation location)
        {
            if (location == null)
                return null;

            if (!EnsureNivReady(ModEntry.Instance.Helper))
                return null;

            try
            {
                // NITV: CreaturesForArea(GameLocation) -> Dictionary<string, List<string>
                return _creaturesForAreaMethod?.Invoke(_nivEntryInstance, new object[] { location })
                    as Dictionary<string, List<string>>;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log("[SpawnChance] GetLocationalDataForLocation failed: " + ex, LogLevel.Trace);
                return null;
            }
        }

        private static void InstantiateViaNiv(string key, Vector2 spawnTile, GameLocation location)
        {
            if (string.IsNullOrEmpty(key) || location == null)
                return;

            if (!EnsureNivReady(ModEntry.Instance.Helper))
                return;

            try
            {
                object target = _instantiateMethod.IsStatic ? null : _nivEntryInstance;
                _instantiateMethod.Invoke(target, new object[] { key, spawnTile, location });
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log("[SpawnChance] InstantiateViaNiv failed: " + ex, LogLevel.Error);
            }
        }

    }
}
