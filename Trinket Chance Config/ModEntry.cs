using System;
using System.Collections.Generic;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley.GameData;

namespace TrinketChanceConfig
{
    public class ModEntry : Mod
    {
        internal static ModEntry Instance { get; private set; }
        internal static ModConfig Config { get; private set; }
        internal const string DuplicateCheckedModDataKey = "Xan.TrinketChanceConfig/DuplicateChecked";


        // Save the data key and memory set for "trinkets any player has ever obtained in this save"
        private const string SeenTrinketsSaveKey = "SeenTrinkets";
        private static readonly HashSet<string> SeenTrinketIds = new(StringComparer.OrdinalIgnoreCase);


        //This list can be expanded if CA adds more vanilla trinkets later
        private static readonly HashSet<string> VanillaTrinketIds = new(StringComparer.OrdinalIgnoreCase)
        {
            "FairyBox",
            "ParrotEgg",
            "FrogEgg",
            "IridiumSpur",
            "IceRod",
            "BasiliskPaw",
            "MagicQuiver",
            "MagicHairDye"
        };

        private Harmony Harmony { get; set; }

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            Config = helper.ReadConfig<ModConfig>();

            this.Harmony = new Harmony(this.ModManifest.UniqueID);

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;
            helper.Events.Content.AssetRequested += this.OnAssetRequested;

            // Added helper to load seen trinkets when a save is loaded
            helper.Events.GameLoop.SaveLoaded += this.OnSaveLoaded;

            // Added a tracker when any player obtains trinkets
            helper.Events.Player.InventoryChanged += this.OnInventoryChanged;

            TrinketChancePatches.Apply(this.Harmony, this);
        }

        private void OnSaveLoaded(object sender, SaveLoadedEventArgs e)
        {
            LoadSeenTrinkets();
        }

        private void OnInventoryChanged(object sender, InventoryChangedEventArgs e)
        {
            if (!Context.IsWorldReady)
                return;

            // Items being added (obtained)
            foreach (var item in e.Added)
            {
                if (item is StardewValley.Objects.Trinkets.Trinket trinket)
                {
                    string id = trinket.QualifiedItemId ?? trinket.ItemId;
                    MarkTrinketSeen(id);
                }
            }
        }

        // Apply Harmony patches
        private void ApplyPatches()
        {
            try
            {
                TrinketChancePatches.Apply(this.Harmony, this);
            }
            catch (System.Exception ex)
            {
                this.Monitor.Log($"Harmony patches failed: {ex}", LogLevel.Error);
            }
        }

        // Register config UI with GMCM
        private void OnGameLaunched(object sender, GameLaunchedEventArgs e)
        {
            var gmcm = this.Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null)
            {
                return;
            }

            gmcm.Register(
                mod: this.ModManifest,
                reset: this.ResetConfig,
                save: this.SaveConfig,
                titleScreenOnly: false
            );

            gmcm.AddNumberOption(
                mod: this.ModManifest,
                getValue: () => Config.TrinketChancePercentOfBase,
                setValue: value =>
                {
                    if (value < 0)
                        value = 0;
                    if (value > 1000)
                        value = 1000;

                    Config.TrinketChancePercentOfBase = value;
                },
                name: () => this.Helper.Translation.Get("config.drop_chance.name"),
                tooltip: () => this.Helper.Translation.Get("config.drop_chance.tooltip"),
                min: 0,
                max: 1000,
                interval: 10,
                fieldId: "TrinketChancePercentOfBase"
            );

            gmcm.AddBoolOption(
            mod: this.ModManifest,
            getValue: () => ModEntry.Config.BlockVanillaTrinkets,
            setValue: value =>
            {
                if (ModEntry.Config.BlockVanillaTrinkets == value)
                    return;

                ModEntry.Config.BlockVanillaTrinkets = value;

                this.Helper.GameContent.InvalidateCache("Data/Trinkets");
            },
            name: () => this.Helper.Translation.Get("config.block_vanilla.name"),
            tooltip: () => this.Helper.Translation.Get("config.block_vanilla.tooltip")
            );

            gmcm.AddBoolOption(
            mod: this.ModManifest,
            getValue: () => ModEntry.Config.DisableDuplicateTrinketDrops,
            setValue: value => Config.DisableDuplicateTrinketDrops = value,
            name: () => this.Helper.Translation.Get("config.disable_duplicates.name"),
            tooltip: () => this.Helper.Translation.Get("config.disable_duplicates.tooltip")
            );

        }

        private void ResetConfig()
        {
            Config = new ModConfig();
            this.SaveConfig();
        }

        private void SaveConfig()
        {
            this.Helper.WriteConfig(Config);
        }

        private void OnAssetRequested(object sender, AssetRequestedEventArgs e)
        {
            if (!e.NameWithoutLocale.IsEquivalentTo("Data/Trinkets"))
                return;

            // Make sure to edit late so Content Patcher has added all the custom trinkets first
            e.Edit(asset =>
            {
                // If disabled (set to false), do nothing
                if (!Config.BlockVanillaTrinkets)
                    return;

                try
                {
                    var data = asset.AsDictionary<string, TrinketData>().Data;

                    foreach (var pair in data)
                    {
                        string id = pair.Key;
                        TrinketData entry = pair.Value;
                        if (entry is null)
                            continue;

                        // If this is one of the vanilla trinkets, block it from dropping
                        if (VanillaTrinketIds.Contains(id))
                            entry.DropsNaturally = false;
                    }
                }
                catch (Exception ex)
                {
                    this.Monitor.Log(
                        $"Error editing Data/Trinkets for BlockVanillaTrinkets: {ex}",
                        LogLevel.Error
                    );
                }
            }, AssetEditPriority.Late);
        }

        
        // Returns whether the given item ID or qualified item ID refers to a vanilla trinket
        internal static bool IsVanillaTrinketId(string idOrQualifiedId)
        {
            if (string.IsNullOrWhiteSpace(idOrQualifiedId))
                return false;

            string id = idOrQualifiedId;

            // Normalize the IDs since trinkets start with "(TR)"
            if (id.StartsWith("(TR)", StringComparison.OrdinalIgnoreCase))
                id = id.Substring(4);

            return VanillaTrinketIds.Contains(id);
        }

        // Load the set of obtained trinket IDs from this save
        private void LoadSeenTrinkets()
        {
            SeenTrinketIds.Clear();

            try
            {
                var data = this.Helper.Data.ReadSaveData<List<string>>(SeenTrinketsSaveKey);
                if (data != null)
                {
                    foreach (string id in data)
                    {
                        if (!string.IsNullOrWhiteSpace(id))
                            SeenTrinketIds.Add(id);
                    }
                }
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Error loading seen trinket data: {ex}", LogLevel.Error);
                SeenTrinketIds.Clear();
            }
        }

        // Save the set of obtained trinket IDs
        private void SaveSeenTrinkets()
        {
            try
            {
                this.Helper.Data.WriteSaveData(
                    SeenTrinketsSaveKey,
                    new List<string>(SeenTrinketIds)
                );
            }
            catch (Exception ex)
            {
                this.Monitor.Log($"Error saving seen trinket data: {ex}", LogLevel.Error);
            }
        }

        // Mark a trinket as "seen" in this save when any player obtains it
        internal static void MarkTrinketSeen(string qualifiedId)
        {
            if (string.IsNullOrWhiteSpace(qualifiedId))
                return;

            if (SeenTrinketIds.Add(qualifiedId))
            {
                // only log/save if this is a new entry
                Instance?.Monitor.Log(
                    $"Marked trinket '{qualifiedId}' as seen in this save.",
                    LogLevel.Trace
                );
                Instance?.SaveSeenTrinkets();
            }
        }

        // Check if any player in this save ever obtained a trinket with this qualified id
        internal static bool HasAnyPlayerSeenTrinket(string qualifiedId)
        {
            if (string.IsNullOrWhiteSpace(qualifiedId))
                return false;

            return SeenTrinketIds.Contains(qualifiedId);
        }

        // Decide whether to keep this duplicate drop based on the config
        internal static bool ShouldKeepDuplicateTrinketDrop(string qualifiedId)
        {
            var config = Config;
            if (config == null)
                return true;

            // If duplicates are allowed -> run like normal
            if (!config.DisableDuplicateTrinketDrops)
                return true;

            // If duplicates are disabled and if the trinket is in the save, allow it and mark it as seen
            // If the player has obtained it, then block it from dropping
            if (!HasAnyPlayerSeenTrinket(qualifiedId))
            {
                MarkTrinketSeen(qualifiedId);
                return true;
            }

            // Already obtained the trinket and duplicates disabled
            return false;
        }

    }
}
