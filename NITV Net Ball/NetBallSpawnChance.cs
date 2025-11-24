using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace NITVNetBall
{
    public static partial class NetBallMethods
    {
        private static Type _nivType;
        private static FieldInfo _creaturesField;
        private static FieldInfo _staticDataField;

        private static MethodInfo _instantiateMi;
        private static object _nivInstance;

        public static void TryNetBallSpawnChance(GameLocation location, Vector2 landingPos, Farmer who, string usedBallName)
        {
            if (!Context.IsOnHostComputer || location is null) return;

            try
            {
                double chance = string.Equals(usedBallName, "Net Ball=", StringComparison.Ordinal) ? 0.40 : 0.20;
                MaybeSpawnWaterCreature(location, landingPos, chance);
            }
            catch (Exception ex)
            {
                ModEntry.Instance?.Monitor?.Log($"[NetBall] spawn attempt failed: {ex}", LogLevel.Error);
            }
        }

        // Spawns exactly 1 water creature based on probability rolls
        // choosing rarity via a single weighted roll (40/30/20/10/5) and re-rolling
        // Also needed to add a "no eligible water creatures" along with the blacklist + water filter
        private static void MaybeSpawnWaterCreature(GameLocation location, Vector2 landingPos, double chance)
        {
            var log = ModEntry.Instance?.Monitor;

            // reflection cache for NITV entry + fields + Instantiate
            if (!EnsureNivReflectionReady())
            {
                log?.Log("[NetBall] NITV reflection not ready.", LogLevel.Trace);
                return;
            }

            var creaturesList = _creaturesField?.GetValue(null) as IList;
            var staticData    = _staticDataField?.GetValue(null)  as IDictionary;
            if (creaturesList == null || staticData == null || staticData.Count == 0)
            {
                log?.Log("[NetBall] Missing creatures or staticCreatureData.", LogLevel.Trace);
                return;
            }

            const float radiusPx = 300f;

            // do not spawn if any creatures are already within radius in the water
            if (AnyCreatureWithinRadiusOnWater(location, creaturesList, landingPos, radiusPx))
            {
                log?.Log("[NetBall] Existing creature blocks spawn (within 300px).", LogLevel.Debug);
                return;
            }

            // main probability gate (20% or 40% depending on the ball used)
            double roll = Game1.random.NextDouble();
            if (roll >= chance)
            {
                log?.Log($"[NetBall] Spawn roll failed ({roll:0.000} ≥ {chance:0.00}).", LogLevel.Trace);
                return;
            }

            // quick availability check across all rarities (avoids pointless rerolls)
            bool anyAvailable = false;
            for (int r = 0; r <= 4; r++)
            {
                var pool = GetEligibleWaterKeysByRarity(staticData, r); // blacklist + water filter
                if (pool != null && pool.Count > 0) { anyAvailable = true; break; }
            }
            if (!anyAvailable)
            {
                log?.Log("[NetBall] No eligible water creatures exist in any rarity.", LogLevel.Trace);
                return;
            }

            // This is a safety cap since it may not always roll an eligible creature
            const int maxReRolls = 50;
            int attempts = 0;
            List<string> candidates = null;
            int chosenRarity = 0;

            do
            {
                chosenRarity = RollRarityWeighted(); // 0-40, 1-30, 2-20, 3-10, 4-5
                candidates = GetEligibleWaterKeysByRarity(staticData, chosenRarity);
                attempts++;
            }
            while ((candidates == null || candidates.Count == 0) && attempts < maxReRolls);

            if (candidates == null || candidates.Count == 0)
            {
                log?.Log("[NetBall] No eligible water creatures across rarities after re-rolls.", LogLevel.Trace);
                return;
            }

            // choose a random water tile within radius to spawn on
            if (!TryPickNearbyOpenWaterTile(location, landingPos, radiusPx, out Vector2 spawnTile))
            {
                log?.Log("[NetBall] No open-water tile within 300px.", LogLevel.Trace);
                return;
            }

            // choose 1 key from the winning rarity
            string key = candidates[Game1.random.Next(candidates.Count)];
            log?.Log($"[NetBall] Selected rarity={chosenRarity}, pool={candidates.Count}, key='{key}'", LogLevel.Debug);

            if (_instantiateMi == null)
            {
                log?.Log("[NetBall] NITV.Instantiate not found.", LogLevel.Error);
                return;
            }

            // ensure instance target if Instantiate is not static
            if (!_instantiateMi.IsStatic && _nivInstance == null && !ResolveNivInstanceDeep())
            {
                log?.Log("[NetBall] Instantiate is instance but no NITV instance could be resolved.", LogLevel.Error);
                return;
            }

            // spawn 1 creature
            _instantiateMi.Invoke(_instantiateMi.IsStatic ? null : _nivInstance, new object[] { key, spawnTile, location });
            Game1.addHUDMessage(new HUDMessage("A water creature appeared!", HUDMessage.newQuest_type));
            log?.Log($"[NetBall] Spawned '{key}' at {spawnTile} (chance={chance:P0}).", LogLevel.Debug);
        }

        private static bool AnyCreatureWithinRadiusOnWater(GameLocation location, IList creatures, Vector2 centerPx, float radiusPx)
        {
            foreach (object nat in creatures)
            {
                var t = nat.GetType();
                GameLocation natLoc = t.GetMethod("GetLocation")?.Invoke(nat, null) as GameLocation ?? t.GetField("currentLocation", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(nat) as GameLocation;
                if (natLoc != null && !ReferenceEquals(natLoc, location)) continue;

                var getPos = t.GetMethod("GetEffectivePosition");
                if (getPos == null) continue;
                Vector2 pos = (Vector2)getPos.Invoke(nat, null);

                if (Vector2.Distance(pos, centerPx) > radiusPx) continue;

                int tx = (int)(pos.X / Game1.tileSize);
                int ty = (int)(pos.Y / Game1.tileSize);
                if (location.isWaterTile(tx, ty)) return true;
            }
            return false;
        }

        private static string ChooseWaterCreatureKey(IDictionary staticData, int targetRarity)
        {
            var candidates = new List<string>();

            foreach (DictionaryEntry kv in staticData)
            {
                if (kv.Key is not string key) continue;

                // values are List<string> or string[] (IList)
                var vals = kv.Value as IList;
                if (vals == null) continue;

                // Water-only filter: localSpawnCode at index 17
                string code = vals.Count > 17 ? Convert.ToString(vals[17]) : null;
                bool isWater = (code == "3") || (code != "0" && code != "1" && code != "2" && code != "4");
                if (!isWater) continue;

                if (IsBlacklistedKey(key)) continue;

                // Rarity filter
                int rarity = GetRarity(vals); // index 0
                if (rarity == targetRarity)
                    candidates.Add(key);
            }

            ModEntry.Instance?.Monitor?.Log(
                $"[NetBall] Eligible water-creature keys for rarity={targetRarity} (after blacklist): {candidates.Count}",
                LogLevel.Debug
            );

            if (candidates.Count == 0) return null;
            return candidates[Game1.random.Next(candidates.Count)];
        }

        private static bool IsBlacklistedKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return true; // treat empty as ineligible
            // avoid spawning shinies and terrarium types
            return key.IndexOf("Shiny", StringComparison.OrdinalIgnoreCase) >= 0
                || key.IndexOf("Terrarium", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool TryPickNearbyOpenWaterTile(GameLocation loc, Vector2 centerPx, float radiusPx, out Vector2 tile)
        {
            tile = Vector2.Zero;
            if (loc == null) return false;

            int cx = (int)(centerPx.X / Game1.tileSize);
            int cy = (int)(centerPx.Y / Game1.tileSize);
            int rTiles = Math.Max(1, (int)Math.Ceiling(radiusPx / Game1.tileSize));

            int maxX = loc.Map.Layers[0].LayerWidth;
            int maxY = loc.Map.Layers[0].LayerHeight;

            var candidates = new List<Vector2>();
            for (int x = Math.Max(0, cx - rTiles); x <= Math.Min(maxX - 1, cx + rTiles); x++)
                for (int y = Math.Max(0, cy - rTiles); y <= Math.Min(maxY - 1, cy + rTiles); y++)
                {
                    if (!loc.isOpenWater(x, y)) continue;

                    Vector2 tileCenter = new(x * Game1.tileSize + Game1.tileSize / 2f,
                                             y * Game1.tileSize + Game1.tileSize / 2f);
                    if (Vector2.Distance(tileCenter, centerPx) <= radiusPx)
                        candidates.Add(new Vector2(x, y));
                }

            if (candidates.Count == 0) return false;
            tile = candidates[Game1.random.Next(candidates.Count)];
            return true;
        }

        private static bool EnsureNivReflectionReady()
        {
            if (_nivType != null && _creaturesField != null && _staticDataField != null && _instantiateMi != null)
                return true;

            _nivType = Type.GetType("NatureInTheValley.NatureInTheValleyEntry, NatureInTheValley");
            if (_nivType == null) return false;

            _creaturesField = _nivType.GetField("creatures", BindingFlags.Public | BindingFlags.Static);
            _staticDataField = _nivType.GetField("staticCreatureData", BindingFlags.Public | BindingFlags.Static);
            if (_creaturesField == null || _staticDataField == null) return false;

            // Find Instantiate (public or non-public)
            _instantiateMi = _nivType.GetMethod("Instantiate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, new[] { typeof(string), typeof(Vector2), typeof(GameLocation) }, null)
                          ?? _nivType.GetMethod("Instantiate", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(string), typeof(Vector2), typeof(GameLocation) }, null);
            return _instantiateMi != null;
        }


        // Crawl from NITV.staticHelper and all IModInfo entries until the NatureInTheValleyEntry instance is found
        private static bool ResolveNivInstanceDeep()
        {
            var log = ModEntry.Instance?.Monitor;

            try
            {
                // crawl starting at NITV's own staticHelper
                object helper = _nivType.GetField("staticHelper", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
                if (helper != null)
                {
                    if (TryCrawlForInstance(helper, _nivType, maxDepth: 3, out _nivInstance))
                    {
                        log?.Log("[NetBall] Resolved NITV instance via staticHelper crawl.", LogLevel.Debug);
                        return true;
                    }
                }

                // fallback crawl via ModRegistry public list
                var registry = ModEntry.Instance?.Helper?.ModRegistry;
                if (registry != null)
                {
                    foreach (var info in registry.GetAll())
                    {
                        if (TryCrawlForInstance(info, _nivType, maxDepth: 3, out _nivInstance))
                        {
                            log?.Log("[NetBall] Resolved NITV instance via ModRegistry crawl.", LogLevel.Debug);
                            return true;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                log?.Log("[NetBall] ResolveNivInstanceDeep failed: " + ex, LogLevel.Trace);
            }

            return _nivInstance != null;
        }

        private static bool TryCrawlForInstance(object root, Type targetType, int maxDepth, out object found)
        {
            found = null;
            if (root == null || maxDepth < 0) return false;

            var queue = new Queue<(object obj, int depth)>();
            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            queue.Enqueue((root, 0));
            visited.Add(root);

            while (queue.Count > 0)
            {
                var (obj, depth) = queue.Dequeue();
                if (obj == null) continue;

                if (targetType.IsInstanceOfType(obj))
                {
                    found = obj;
                    return true;
                }

                if (depth >= maxDepth) continue;

                Type t = obj.GetType();

                foreach (var p in t.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!p.CanRead) continue;
                    object val = null;
                    try { val = p.GetValue(obj); } catch { continue; }
                    if (val == null) continue;
                    if (visited.Add(val)) queue.Enqueue((val, depth + 1));
                }

                foreach (var f in t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    object val = null;
                    try { val = f.GetValue(obj); } catch { continue; }
                    if (val == null) continue;
                    if (visited.Add(val)) queue.Enqueue((val, depth + 1));
                }
            }
            return false;
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }

        // Weighted single-roll: 0-40, 1-30, 2-20, 3-10, 4-5 (total 105).
        // Roll an int in [0, 104] to preserve relative weights
        private static int RollRarityWeighted()
        {
            // weights aligned with your table
            int[] buckets = { 40, 30, 20, 10, 5 }; // indices 0 -> 4
            int total = 0;
            for (int i = 0; i < buckets.Length; i++) total += buckets[i]; // 105

            int r = Game1.random.Next(total); // 0 -> 104
            int acc = 0;
            for (int rarity = 0; rarity < buckets.Length; rarity++)
            {
                acc += buckets[rarity];
                if (r < acc) return rarity;
            }
            return 0;
        }

        // read rarity from the staticCreatureData list and fallback to rarity 0
        private static int GetRarity(IList vals)
        {
            if (vals == null || vals.Count == 0) return 0;
            if (int.TryParse(Convert.ToString(vals[0]), out int rarity))
                return Math.Clamp(rarity, 0, 4);
            return 0;
        }
        
        // Returns all eligible WATER keys of a given rarity after blacklist filtering.
        private static List<string> GetEligibleWaterKeysByRarity(IDictionary staticData, int targetRarity)
        {
            var list = new List<string>();
            foreach (DictionaryEntry kv in staticData)
            {
                if (kv.Key is not string key) continue;
                var vals = kv.Value as IList;
                if (vals == null) continue;

                // water filter via localSpawnCode at index 17 (0,1,2,4 are land types)
                string code = vals.Count > 17 ? Convert.ToString(vals[17]) : null;
                bool isWater = (code == "3") || (code != "0" && code != "1" && code != "2" && code != "4");
                if (!isWater) continue;

                if (IsBlacklistedKey(key)) continue;

                if (GetRarity(vals) == targetRarity) // rarity at index 0
                    list.Add(key);
            }
            return list;
        }

    }
}
