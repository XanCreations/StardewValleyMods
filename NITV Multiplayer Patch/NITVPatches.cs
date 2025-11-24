using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace NITVMultiplayerPatch
{
    internal static class NITVPatches
    {
        // Which farmer spawned which creature (per process)
        private static readonly Dictionary<object, long> CreatureOwners = new();

        // Which farmer owns creatures for a given location name (per process)
        private static readonly Dictionary<string, long> LocationOwners = new();

        // cached reflection info
        private static FieldInfo _creaturesField;
        private static FieldInfo _releasedField;
        private static MethodInfo _getLocationMethod;

        // How many creatures existed before a TrySpawnFromArea call (per thread)
        [ThreadStatic]
        private static int _creatureCountBeforeSpawn;

        // used for trace debugging -> this was mostly when I was testing each stage of all the fixes
        /*
        private static void Log(string message)
        {
            ModEntry.Instance?.Monitor.Log($"[NITV MP Patch] {message}", StardewModdingAPI.LogLevel.Trace);
        }
        */

        // Get the static NITV creatures list via reflection
        private static IList GetCreatureList()
        {
            if (ModEntry.NitvEntryType == null)
                return null;

            _creaturesField ??= AccessTools.Field(ModEntry.NitvEntryType, "creatures");
            return _creaturesField?.GetValue(null) as IList;
        }

        private static bool IsReleased(object creature)
        {
            if (creature == null)
                return false;

            if (_releasedField == null)
            {
                var type = creature.GetType();
                _releasedField = AccessTools.Field(type, "released"); // public bool
                if (_releasedField == null)
                    return false;
            }

            object value = _releasedField.GetValue(creature);
            return value is bool b && b;
        }

        private static GameLocation GetCreatureLocation(object creature)
        {
            if (creature == null)
                return null;

            if (_getLocationMethod == null)
            {
                var type = creature.GetType();
                _getLocationMethod = AccessTools.Method(type, "GetLocation", Type.EmptyTypes);
                if (_getLocationMethod == null)
                    return null;
            }

            return _getLocationMethod.Invoke(creature, null) as GameLocation;
        }

        private static long GetOwnerForCreature(object creature)
        {
            return creature != null && CreatureOwners.TryGetValue(creature, out var owner)
                ? owner
                : 0L;
        }

        private static long GetOwnerForLocation(GameLocation location)
        {
            if (location == null || string.IsNullOrEmpty(location.Name))
                return 0L;

            return LocationOwners.TryGetValue(location.Name, out var owner)
                ? owner
                : 0L;
        }

        private static void SetOwnerForLocation(GameLocation location, long farmerId)
        {
            if (location == null || string.IsNullOrEmpty(location.Name))
                return;

            if (farmerId == 0L)
                return;

            if (!LocationOwners.ContainsKey(location.Name))
            {
                LocationOwners[location.Name] = farmerId;
                //Log($"Location '{location.Name}' owner set to farmer {farmerId}.");
            }
        }

        private static void ClearOwnerForLocationIfAlone(GameLocation oldLocation, long leavingFarmerId)
        {
            if (oldLocation == null || string.IsNullOrEmpty(oldLocation.Name))
                return;

            if (!LocationOwners.TryGetValue(oldLocation.Name, out var owner) || owner != leavingFarmerId)
                return;

            // Check if anyone else still in that location
            bool someoneElseThere = Game1.getAllFarmers()
                .Any(f => f.UniqueMultiplayerID != leavingFarmerId &&
                          f.currentLocation != null &&
                          f.currentLocation.Name == oldLocation.Name);

            if (!someoneElseThere)
            {
                LocationOwners.Remove(oldLocation.Name);
                //Log($"Location '{oldLocation.Name}' owner cleared (farmer {leavingFarmerId} left and no one else was there).");
            }
        }

        // TRACKING CREATURES BASED ON OWNER

        // This remembers who spawned a creature when it spawns
        // Signature matches Instantiate(string name, Vector2 tile, GameLocation location)
        public static void Instantiate_Postfix(object __instance, string name, Microsoft.Xna.Framework.Vector2 tile, GameLocation location)
        {
            try
            {
                if (Game1.player == null)
                    return;

                long farmerId = Game1.player.UniqueMultiplayerID;

                var list = GetCreatureList();
                if (list == null || list.Count == 0)
                    return;

                // NITV pushes new creatures to the end of the list.
                object newest = list[list.Count - 1];

                // Track owner
                CreatureOwners[newest] = farmerId;
                //Log($"Creature '{name}' spawned in '{location?.Name}' owned by farmer {farmerId}.");

                // If the location has no owner yet, let the spawner claim it.
                if (location != null && !string.IsNullOrEmpty(location.Name))
                    SetOwnerForLocation(location, farmerId);

                // NOTE: I tried a "window of opportunity" where there was a rolling soft-cap logic applied so that players couldn't hog all the spawns
                // This was scrapped because it could reduce spawns to zero per location -> too many variables such as small maps, different cap settings, locations with no spawns etc.
            }
            catch (Exception ex)
            {
                ModEntry.Instance?.Monitor.Log(
                    $"Error in Instantiate_Postfix: {ex}",
                    StardewModdingAPI.LogLevel.Error
                );
            }
        }

        // NEXT SECTION ABOUT FIXING PER-OWNER DESPAWN BASED ON PLAYER WARP

        /// <summary>
        /// Replace NITV's ClearUnclaimedCreatures logic with a multiplayer safe version.
        /// Attempt to remove non-released creatures from locations that have no farmers present.
        /// Attempt to keep creatures in any location that has at least one farmer present.
        /// Attempt to avoid the "my friend warped and nuked all my spawns" issue
        /// unfortunately it seems global creatures list grows until it hits the cap
        /// </summary>
        public static bool ClearUnclaimedCreatures_Prefix(object __instance, GameLocation newlocation)
        {
            try
            {
                //let the original handle it if it goes haywire
                var list = GetCreatureList();
                if (list == null)
                    return false; // skip original

                // Build a set of all active locations (where any farmer currently is)
                var activeLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var farmer in Game1.getAllFarmers())
                {
                    var loc = farmer?.currentLocation;
                    if (loc != null && !string.IsNullOrEmpty(loc.Name))
                        activeLocations.Add(loc.Name);
                }

                // Walk the creature list backwards and remove anything in an empty location
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    object creature = list[i];
                    if (creature == null || IsReleased(creature))
                        continue;

                    GameLocation creatureLoc = GetCreatureLocation(creature);
                    string creatureLocName = creatureLoc?.Name;

                    // If there is at least one farmer in a location, don't nuke the creature list
                    if (creatureLocName != null && activeLocations.Contains(creatureLocName))
                        continue;

                    // safe to despawn
                    list.RemoveAt(i);
                    CreatureOwners.Remove(creature);
                    //Log($"Removed creature in empty location '{creatureLocName ?? "null"}'.");
                }

                // cleanup has been handled -> don't run the original method
                return false;
            }
            catch (Exception ex)
            {
                ModEntry.Instance?.Monitor.Log(
                    $"Error in ClearUnclaimedCreatures_Prefix: {ex}",
                    StardewModdingAPI.LogLevel.Error
                );

                // On error, let NITV's original method run
                return true;
            }
        }

        // Track which farmer owns a location's creatures when warping
        public static void OnWarp_Postfix(object __instance, object sender, WarpedEventArgs eventArgs)
        {
            try
            {
                if (eventArgs?.Player == null)
                    return;

                long farmerId = eventArgs.Player.UniqueMultiplayerID;

                if (eventArgs.NewLocation != null && !string.IsNullOrEmpty(eventArgs.NewLocation.Name))
                {
                    // The first farmer to enter a location claims it on this client
                    if (GetOwnerForLocation(eventArgs.NewLocation) == 0L)
                    {
                        SetOwnerForLocation(eventArgs.NewLocation, farmerId);
                    }
                    
                    // Essentially I want to avoid overwriting a stable per-location setup when other players are warping in and out
                }

                if (eventArgs.OldLocation != null && !string.IsNullOrEmpty(eventArgs.OldLocation.Name))
                {
                    ClearOwnerForLocationIfAlone(eventArgs.OldLocation, farmerId);
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance?.Monitor.Log(
                    $"Error in OnWarp_Postfix: {ex}",
                    StardewModdingAPI.LogLevel.Error
                );
            }
        }


        // Which spawn settings apply to each location name (per process)
        private class SpawnConfig
        {
            // Stabilise which creatures can spawn where
            // NITV's own cap, spawnChance, counters etc. should be left alone
            public object LocationalData; // NITV's Dictionary<string, List<string>>
            public object CreatureSpawnList; // NITV's List<string>
        }

        private static readonly Dictionary<string, SpawnConfig> SpawnConfigsByLocation = new();
        private static FieldInfo _locationalDataField;
        private static FieldInfo _creatureSpawnListField;


        private static void EnsureSpawnFieldInfos()
        {
            if (ModEntry.NitvEntryType == null)
                return;

            _locationalDataField ??= AccessTools.Field(ModEntry.NitvEntryType, "locationalData");
            _creatureSpawnListField ??= AccessTools.Field(ModEntry.NitvEntryType, "CreatureSpawnList");
        }

        private static void CaptureSpawnConfigForLocation(object nitvInstance, string locationName)
        {
            if (nitvInstance == null || string.IsNullOrEmpty(locationName))
                return;

            try
            {
                EnsureSpawnFieldInfos();

                var locData   = _locationalDataField    != null ? _locationalDataField.GetValue(nitvInstance)    : null;
                var spawnList = _creatureSpawnListField != null ? _creatureSpawnListField.GetValue(nitvInstance) : null;

                // If there aren't any spawnable creatures and the spawn list is empty -> don't cache this as the "official" config for the location
                // It's important not to permanently lock an area into having no spawns
                if (locData is IDictionary dict && dict.Count == 0 &&
                    (spawnList is IList list && list.Count == 0))
                {
                    //Log($"Not caching empty spawn config for '{locationName}'.");
                    return;
                }

                var cfg = new SpawnConfig
                {
                    LocationalData    = locData,
                    CreatureSpawnList = spawnList
                };

                SpawnConfigsByLocation[locationName] = cfg;
                //Log($"Captured spawn config for '{locationName}'.");
            }
            catch (Exception ex)
            {
                ModEntry.Instance?.Monitor.Log(
                    $"Error capturing spawn config for '{locationName}': {ex}",
                    StardewModdingAPI.LogLevel.Error
                );
            }
        }

        private static void ApplySpawnConfigForCurrentLocation(object nitvInstance)
        {
            var loc = Game1.currentLocation;
            if (loc == null || string.IsNullOrEmpty(loc.Name))
                return;

            if (!SpawnConfigsByLocation.TryGetValue(loc.Name, out var cfg))
                return;

            try
            {
                EnsureSpawnFieldInfos();

                // Only restore which creatures can spawn in this area.
                _locationalDataField    ?.SetValue(nitvInstance, cfg.LocationalData);
                _creatureSpawnListField ?.SetValue(nitvInstance, cfg.CreatureSpawnList);
            }
            catch (Exception ex)
            {
                ModEntry.Instance?.Monitor.Log(
                    $"Error applying spawn config for '{loc.Name}': {ex}",
                    StardewModdingAPI.LogLevel.Error
                );
            }
        }

        // Before NITV's OneSecond logic runs, make sure the spawn fields match the current location on this screen
        public static void OneSecond_Prefix(object __instance, object sender, OneSecondUpdateTickedEventArgs e)
        {
            try
            {
                ApplySpawnConfigForCurrentLocation(__instance);
            }
            catch (Exception ex)
            {
                ModEntry.Instance?.Monitor.Log(
                    $"Error in OneSecond_Prefix: {ex}",
                    StardewModdingAPI.LogLevel.Error
                );
            }
        }

        // After NITV actually spawns from a location, refresh that location's config -> I am trying to make sure that SpawnedInLoc and any internal lists stay in sync
        public static void TrySpawnFromArea_Postfix(object __instance, GameLocation location)
        {
            try
            {
                if (location == null || string.IsNullOrEmpty(location.Name))
                    return;

                // Only update spawn config if this call actually spawned something
                var list = GetCreatureList();
                if (list == null || list.Count <= _creatureCountBeforeSpawn)
                {
                    // No new creatures were added -> don't overwrite the location's config
                    return;
                }

                CaptureSpawnConfigForLocation(__instance, location.Name);
            }
            catch (Exception ex)
            {
                ModEntry.Instance?.Monitor.Log(
                    $"Error in TrySpawnFromArea_Postfix: {ex}",
                    StardewModdingAPI.LogLevel.Error
                );
            }
        }


        // I noticed that all the trees, bushes, stumps and water positions are cached
        // This is an attempt to fix how creatures spawn in positions cached from one screen but then appear on the other screen instead

        private static FieldInfo _treePosField;
        private static FieldInfo _bushPosField;
        private static FieldInfo _stumpPosField;
        private static FieldInfo _waterPosField;

        private static MethodInfo _getTreesMethod;
        private static MethodInfo _getBushesMethod;
        private static MethodInfo _getStumpsMethod;
        private static MethodInfo _getWaterMethod;

        private static void EnsureEnvironmentInfos()
        {
            if (ModEntry.NitvEntryType == null)
                return;

            _treePosField  ??= AccessTools.Field(ModEntry.NitvEntryType, "treePos");
            _bushPosField  ??= AccessTools.Field(ModEntry.NitvEntryType, "bushPos");
            _stumpPosField ??= AccessTools.Field(ModEntry.NitvEntryType, "stumpPos");
            _waterPosField ??= AccessTools.Field(ModEntry.NitvEntryType, "waterPos");

            _getTreesMethod  ??= AccessTools.Method(ModEntry.NitvEntryType, "GetTrees",  new[] { typeof(GameLocation) });
            _getBushesMethod ??= AccessTools.Method(ModEntry.NitvEntryType, "GetBushes", new[] { typeof(GameLocation) });
            _getStumpsMethod ??= AccessTools.Method(ModEntry.NitvEntryType, "GetStumps", new[] { typeof(GameLocation) });
            _getWaterMethod  ??= AccessTools.Method(ModEntry.NitvEntryType, "GetWater",  new[] { typeof(GameLocation) });
        }

        public static void TrySpawnFromArea_Prefix(object __instance, GameLocation location)
        {
            try
            {
                if (__instance == null || location == null)
                    return;

                // Remember how many creatures existed before this spawn attempt
                var list = GetCreatureList();
                _creatureCountBeforeSpawn = list?.Count ?? 0;

                EnsureEnvironmentInfos();

                // If reflection failed, bail without touching anything
                if (_getTreesMethod == null || _treePosField == null)
                    return;

                // Recompute environment anchors for this location
                var trees  = (List<Vector2>)_getTreesMethod?.Invoke(__instance, new object[] { location });
                var bushes = (List<Vector2>)_getBushesMethod?.Invoke(__instance, new object[] { location });
                var stumps = (List<Vector2>)_getStumpsMethod?.Invoke(__instance, new object[] { location });
                var water  = (List<Vector2>)_getWaterMethod?.Invoke(__instance, new object[] { location });

                _treePosField ?.SetValue(__instance, trees);
                _bushPosField ?.SetValue(__instance, bushes);
                _stumpPosField?.SetValue(__instance, stumps);
                _waterPosField?.SetValue(__instance, water);
            }
            catch (Exception ex)
            {
                ModEntry.Instance?.Monitor.Log(
                    $"Error in TrySpawnFromArea_Prefix: {ex}",
                    LogLevel.Error
                );
            }
        }
    }
}
