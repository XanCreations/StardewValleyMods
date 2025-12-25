using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Menus;
using StardewValley.SpecialOrders;

namespace NITVPokemonAddon
{
    internal static class PokemonQuestBoardManager
    {
        private const string TileActionName = "NITVPokemonQuestBoard";
        private const string OrderType = "NITVPokemonSO";
        private const string LogPrefix = "[PkmnBoard] ";

        private static IModHelper _helper = null!;
        private static IMonitor _monitor = null!;

        private static bool _overrideActive;
        private static readonly List<string> _overrideKeys = new();

        // Weekly cache (local-only, recomputable)
        private static readonly List<string> _weeklyKeys = new();
        private static int _weekStamp = -1;

        internal static void Initialize(IMod mod, IModHelper helper, Harmony harmony)
        {
            _helper = helper;
            _monitor = mod.Monitor;

            GameLocation.RegisterTileAction(TileActionName, OnTileAction);
            _helper.Events.Display.MenuChanged += OnMenuChanged;
            _helper.Events.GameLoop.DayStarted += OnDayStarted;

            InstallPatches(harmony);

            _monitor.Log($"{LogPrefix}initialized (action='{TileActionName}', type='{OrderType}')", LogLevel.Info);
        }

        private static void OnDayStarted(object sender, DayStartedEventArgs e)
        {
            try
            {
                // Host-only for weekly reset (multiplayer)
                if (!Context.IsMainPlayer)
                    return;

                // Let vanilla tick durations
                SpecialOrder.UpdateAvailableSpecialOrders(OrderType, forceRefresh: false);

                bool isMonday = Game1.dayOfMonth % 7 == 1;
                if (!isMonday)
                    return;

                // Clear last week and rebuild offers
                SpecialOrder.RemoveAllSpecialOrders(OrderType);
                SpecialOrder.UpdateAvailableSpecialOrders(OrderType, forceRefresh: true);

                // Compute weekly keys from (uniqueID, week)
                _weeklyKeys.Clear();
                _weeklyKeys.AddRange(BuildChosenKeys(max: 2));
                _weekStamp = (int)((Game1.stats?.DaysPlayed ?? 0) / 7);

                _monitor.Log($"{LogPrefix}Monday reset -> new board offers: {string.Join(", ", _weeklyKeys)}", LogLevel.Info);
            }
            catch (Exception ex)
            {
                _monitor.Log($"{LogPrefix}DayStarted weekly board logic failed: {ex}", LogLevel.Error);
            }
        }

        private static bool OnTileAction(GameLocation location, string[] args, Farmer who, Point clickedTile)
        {
            try
            {
                // Recompute if no local cache (e.g. client joining mid-week, SP reload)
                if (_weeklyKeys.Count == 0)
                {
                    _weeklyKeys.Clear();
                    _weeklyKeys.AddRange(BuildChosenKeys(max: 2));
                    _weekStamp = (int)((Game1.stats?.DaysPlayed ?? 0) / 7);
                    _monitor.Log($"{LogPrefix}weekly keys recomputed locally: {string.Join(", ", _weeklyKeys)}", LogLevel.Trace);
                }

                // Use weekly keys
                SetOverride(_weeklyKeys.ToList());

                var board = TryCreateBoardWithType(OrderType) ?? new SpecialOrdersBoard();

                // Use custom textures for the special orders board
                try
                {
                    Texture2D tex = null;
                    try { tex = Game1.content.Load<Texture2D>("LooseSprites/NITV_SpecialOrdersBoard"); } catch { }
                    if (tex == null)
                    {
                        try { tex = _helper.ModContent.Load<Texture2D>("assets/NITV_SpecialOrdersBoard.png"); } catch { }
                    }
                    if (tex != null)
                        _helper.Reflection.GetField<Texture2D>(board, "billboardTexture", true)?.SetValue(tex); // because it's a private field
                }
                catch { /* cosmetic only */ }

                Game1.activeClickableMenu = board;
                _monitor.Log($"{LogPrefix}opened board (weekly keys): {string.Join(", ", _overrideKeys)}", LogLevel.Info);
                return true;
            }
            catch (Exception ex)
            {
                _monitor.Log($"{LogPrefix}failed to open board: {ex}", LogLevel.Error);
                return false;
            }
        }

        private static void OnMenuChanged(object sender, MenuChangedEventArgs e)
        {
            if (!_overrideActive) return;
            if (e.OldMenu is SpecialOrdersBoard && e.NewMenu is not SpecialOrdersBoard)
            {
                ClearOverride();
                _monitor.Log($"{LogPrefix}board closed -> override off", LogLevel.Info);
            }
        }

        private static void SetOverride(List<string> keys)
        {
            _overrideKeys.Clear();
            _overrideKeys.AddRange(keys);
            _overrideActive = _overrideKeys.Count > 0;
        }

        private static void ClearOverride()
        {
            _overrideActive = false;
            _overrideKeys.Clear();
        }

        internal static bool OverrideActive => _overrideActive;
        internal static IReadOnlyList<string> OverrideKeys => _overrideKeys;

        private static List<string> BuildChosenKeys(int max)
        {
            var team = Game1.player?.team;
            if (team == null)
                return new List<string>();

            var all = LoadSpecialOrdersDict();
            if (all == null)
                return new List<string>();

            var candidates = new List<string>();
            foreach (DictionaryEntry de in all)
            {
                var key = de.Key?.ToString();
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                var entry = de.Value;
                if (!IsOurType(entry, OrderType))
                    continue;

                // skip if already active
                if (team.specialOrders.Any(o => string.Equals(o?.questKey?.Value, key, StringComparison.Ordinal)))
                    continue;

                // skip if completed and not repeatable
                if (!IsRepeatable(entry) && team.completedSpecialOrders.Contains(key))
                    continue;

                candidates.Add(key);
            }

            // Deterministic across host/clients -> (game unique ID, current week number)
            long seed = unchecked((long)Game1.uniqueIDForThisGame); // explicit cast from ulong
            long week = ((long)(Game1.stats?.DaysPlayed ?? 0)) / 7;
            var rng = Utility.CreateRandom(seed, week);

            return candidates
                .OrderBy(_ => rng.Next())
                .Take(Math.Max(0, max))
                .ToList();
        }

        private static IDictionary LoadSpecialOrdersDict()
        {
            try { return Game1.content.Load<IDictionary>("Data/SpecialOrders"); }
            catch (Exception ex)
            {
                _monitor.Log($"{LogPrefix}failed to load Data/SpecialOrders: {ex}", LogLevel.Error);
                return null;
            }
        }

        private static bool IsOurType(object entry, string want)
        {
            var type = GetFieldString(entry, "OrderType");
            return string.Equals(type, want, StringComparison.Ordinal);
        }

        private static bool IsRepeatable(object entry)
        {
            var val = GetField(entry, "Repeatable");
            if (val is bool b) return b;
            if (val is string s && bool.TryParse(s, out var pb)) return pb;
            return false; // default non-repeatable
        }

        private static string GetFieldString(object entry, string name)
        {
            var v = GetField(entry, name);
            return v?.ToString() ?? string.Empty;
        }

        private static object GetField(object entry, string name)
        {
            if (entry == null) return null;

            if (entry is IDictionary dict && dict.Contains(name))
                return dict[name];

            var t = entry.GetType();
            var prop = t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
            if (prop != null) return prop.GetValue(entry);

            var field = t.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.IgnoreCase);
            if (field != null) return field.GetValue(entry);

            return null;
        }

        private static SpecialOrdersBoard TryCreateBoardWithType(string boardType)
        {
            var ctor = typeof(SpecialOrdersBoard).GetConstructor(new[] { typeof(string) });
            if (ctor != null)
            {
                try { return (SpecialOrdersBoard)ctor.Invoke(new object[] { boardType }); }
                catch { /* ignore */ }
            }

            try
            {
                var menu = new SpecialOrdersBoard();
                try { _helper.Reflection.GetField<string>(menu, "boardType", true)?.SetValue(boardType); }
                catch { /* ignore */ }
                return menu;
            }
            catch { return null; }
        }

        private static void InstallPatches(Harmony harmony)
        {
            try
            {
                var soType = typeof(SpecialOrder);

                var mGeneral = AccessTools.Method(soType, "GetAvailableSpecialOrderKeys");
                if (mGeneral != null)
                    harmony.Patch(mGeneral, prefix: new HarmonyMethod(typeof(SOPrefixes), nameof(SOPrefixes.KeysGeneral_Prefix)));

                var mForType = AccessTools.Method(soType, "GetAvailableSpecialOrderKeysForOrderType");
                if (mForType != null)
                    harmony.Patch(mForType, prefix: new HarmonyMethod(typeof(SOPrefixes), nameof(SOPrefixes.KeysForType_Prefix)));
            }
            catch (Exception ex)
            {
                _monitor.Log($"{LogPrefix}failed to install patches: {ex}", LogLevel.Error);
            }
        }

        private static class SOPrefixes
        {
            public static bool KeysGeneral_Prefix(ref List<string> __result)
            {
                if (!OverrideActive) return true;
                __result = OverrideKeys.ToList();
                return false;
            }

            public static bool KeysForType_Prefix(ref List<string> __result)
            {
                if (!OverrideActive) return true;
                __result = OverrideKeys.ToList();
                return false;
            }
        }
    }
}
