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
        private const string TileActionName = "NITVPokemonQuestBoard"; // Map tile being used for the quest board
        private const string OrderType = "NITVPokemonSO"; // This is the OrderType in the GroupQuests.json - make sure to only use these
        private const string LogPrefix = "[PkmnBoard] ";

        private static IModHelper _helper = null!;
        private static IMonitor _monitor = null!;

        // override state
        private static bool _overrideActive;
        private static readonly List<string> _overrideKeys = new();

        internal static void Initialize(IMod mod, IModHelper helper, Harmony harmony)
        {
            _helper = helper;
            _monitor = mod.Monitor;

            // tile action -> open special orders board
            GameLocation.RegisterTileAction(TileActionName, OnTileAction);

            // turn it off when the player closes the board
            _helper.Events.Display.MenuChanged += OnMenuChanged;

            // do these patches when override is active
            InstallPatches(harmony);

            _monitor.Log($"{LogPrefix}initialized (action='{TileActionName}', type='{OrderType}')", LogLevel.Info);
        }

        private static bool OnTileAction(GameLocation location, string[] args, Farmer who, Point clickedTile)
        {
            try
            {
                // build list
                var keys = BuildChosenKeys(max: 2);
                if (keys.Count == 0)
                {
                    _monitor.Log($"{LogPrefix}found 0 '{OrderType}' orders — opening vanilla board", LogLevel.Warn);
                    Game1.activeClickableMenu = new SpecialOrdersBoard();
                    return true;
                }

                SetOverride(keys);

                // create quest board
                var menu = TryCreateBoardWithType(OrderType) ?? new SpecialOrdersBoard();

                // Add a custom special orders board image
                try
                {
                    Texture2D tex = null;

                    // Using content patcher to apply it under LooseSprites
                    try { tex = Game1.content.Load<Texture2D>("LooseSprites/NITV_SpecialOrdersBoard");}
                        catch { }

                    // Use a fallback as well
                    if (tex == null)
                    {
                        try { tex = _helper.ModContent.Load<Texture2D>("assets/NITV_SpecialOrdersBoard.png");}
                        catch { }
                    }

                    if (tex != null)
                        _helper.Reflection.GetField<Texture2D>(menu, "billboardTexture", true).SetValue(tex);
                }
                catch { /* ignore */ }


                Game1.activeClickableMenu = menu;
                _monitor.Log($"{LogPrefix}opened board (override {(_overrideActive ? "on" : "off")}) with {_overrideKeys.Count} keys", LogLevel.Info);
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
            if (!_overrideActive)
                return;

            // When the special orders board closes, drop the override
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
            _monitor.Log($"{LogPrefix}override set with {_overrideKeys.Count} keys: {string.Join(", ", _overrideKeys)}", LogLevel.Info);
        }

        private static void ClearOverride()
        {
            _overrideActive = false;
            _overrideKeys.Clear();
        }

        internal static bool OverrideActive => _overrideActive;
        internal static IReadOnlyList<string> OverrideKeys => _overrideKeys;

        // Actually building the list
        private static List<string> BuildChosenKeys(int max)
        {
            // Start with a clean slate otherwise it will use the vanilla quests
            var team = Game1.player.team;
            team.availableSpecialOrders.Clear();

            var all = LoadSpecialOrdersDict();
            if (all == null)
                return new List<string>();

            // Filter the quest list to the custom OrderType
            var candidates = new List<string>();
            foreach (DictionaryEntry de in all)
            {
                var key = de.Key?.ToString();
                if (string.IsNullOrEmpty(key)) continue;

                var entry = de.Value;
                if (!IsOurType(entry, OrderType)) continue;

                // not active already
                if (team.specialOrders.Any(o => string.Equals(o?.questKey?.Value, key, StringComparison.Ordinal)))
                    continue;

                // skip if non-repeatable and completed
                if (!IsRepeatable(entry) && team.completedSpecialOrders.Contains(key))
                    continue;

                candidates.Add(key);
            }

            // choose up to the max
            var rng = Utility.CreateRandom(Game1.uniqueIDForThisGame, Game1.stats.DaysPlayed / 7);
            var picks = candidates.OrderBy(_ => rng.Next()).Take(Math.Max(0, max)).ToList();

            // add to team.availableSpecialOrders (the code that reads it also sees my code)
            foreach (var key in picks)
            {
                var so = SpecialOrder.GetSpecialOrder(key, rng.Next());
                if (so != null)
                    team.availableSpecialOrders.Add(so);
            }

            _monitor.Log($"{LogPrefix}built {team.availableSpecialOrders.Count} orders for '{OrderType}'", LogLevel.Info);
            return picks;
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
            return true; // default to repeatable
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

        // Create the special orders board
        private static SpecialOrdersBoard TryCreateBoardWithType(string boardType)
        {
            // Prefer ctor(string) if present
            var ctor = typeof(SpecialOrdersBoard).GetConstructor(new[] { typeof(string) });
            if (ctor != null)
            {
                try { return (SpecialOrdersBoard)ctor.Invoke(new object[] { boardType }); }
                catch { /* fall through */ }
            }

            // The fallback is default ctor. Also need to set internal field if it exists
            try
            {
                var menu = new SpecialOrdersBoard();
                try
                {
                    var f = _helper.Reflection.GetField<string>(menu, "boardType", true);
                    f?.SetValue(boardType);
                }
                catch { /* ignore */ }
                return menu;
            }
            catch { return null; }
        }

        // I put all the Harmony patching here
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
            // This is called when the game asks for a list of order keys
            public static bool KeysGeneral_Prefix(ref List<string> __result)
            {
                if (!PokemonQuestBoardManager.OverrideActive)
                    return true; // run vanilla

                __result = PokemonQuestBoardManager.OverrideKeys.ToList();
                return false; // skip vanilla
            }

            // This is called when the game asks for a list of order keys for a specific type
            public static bool KeysForType_Prefix(ref List<string> __result)
            {
                if (!PokemonQuestBoardManager.OverrideActive)
                    return true;

                __result = PokemonQuestBoardManager.OverrideKeys.ToList();
                return false;
            }
        }
    }
}
