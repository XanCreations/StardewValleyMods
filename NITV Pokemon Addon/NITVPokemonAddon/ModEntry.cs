using System;
using System.Reflection;
using StardewModdingAPI;
using Microsoft.Xna.Framework.Graphics;
using HarmonyLib;
using System.Collections.Generic;
using StardewValley;
using Microsoft.Xna.Framework;
using StardewValley.TerrainFeatures;


namespace NITVPokemonAddon
{
    public class ModEntry : Mod
    {
        // all the crop IDs found in Data/Crops
        private const string CROP_GRASS = "SafariGrass";
        private const string CROP_PECHA = "PechaFruit";
        private const string CROP_ORAN  = "OranFruit";
        private const string CROP_CHERI = "CheriFruit";

        internal static ModEntry Instance { get; private set; }
        internal ModConfig Config;

        // Need this for net name and description
        internal Dictionary<string, Texture2D> netTextures;

        public override void Entry(IModHelper helper)
        {
            Instance = this;

            Config = helper.ReadConfig<ModConfig>();

            // Needed for Pokemon Crops on specified maps
            Helper.Events.GameLoop.DayStarted += PlacePokemonCrops;

            Helper.Events.GameLoop.GameLaunched += OnGameLaunched;          


            // prepare a list of (typeName, assetPath) pairs
            var mappings = new (string typeName, string assetPath)[]
            {
                ("NatureInTheValley.NatureInTheValleyEntry, NatureInTheValley", "assets/PokeNet1.png"),
                ("NatureInTheValley.NatInValeyNet, NatureInTheValley", "assets/PokeNet1.png"),
                ("NatureInTheValley.NatInValeySaphNet, NatureInTheValley", "assets/GreatNet1.png"),
                ("NatureInTheValley.NatInValeyJadeNet, NatureInTheValley", "assets/UltraNet1.png"),
                ("NatureInTheValley.NatInValeyGoldenNet, NatureInTheValley","assets/MasterNet1.png")
            };

            // build the dictionary at runtime
            this.netTextures = null;
            if (Config.PokemonNetSpriteEnabled)
            {
                this.netTextures = new Dictionary<string, Texture2D>();
                foreach (var (typeName, path) in mappings)
                {
                    var type = AccessTools.TypeByName(typeName);
                    if (type != null)
                        this.netTextures[type.FullName] = helper.ModContent.Load<Texture2D>(path);
                    else
                        this.Monitor.Log($"Couldn't find type {typeName}", LogLevel.Warn);
                }
            }

            var harmony = new Harmony(this.ModManifest.UniqueID);

            // patch the NITV Entry override
            {
                var entryType = AccessTools.TypeByName("NatureInTheValley.NatureInTheValleyEntry, NatureInTheValley");
                var entryMethod = AccessTools.Method(entryType, "Entry", new Type[] { typeof(IModHelper) });
                if (entryMethod != null)
                    harmony.Patch(
                        entryMethod,
                        postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.EntryPostfix))
                    );
            }

            // patch each Net class's constructor so the OG 'texture' gets replaced
            foreach (string className in new[]
            {
                "NatureInTheValley.NatInValeyNet, NatureInTheValley",
                "NatureInTheValley.NatInValeySaphNet, NatureInTheValley",
                "NatureInTheValley.NatInValeyJadeNet, NatureInTheValley",
                "NatureInTheValley.NatInValeyGoldenNet, NatureInTheValley"
            })
            {
                var type = AccessTools.TypeByName(className);
                var ctor = AccessTools.Constructor(type, new Type[0]);
                if (ctor != null)
                    harmony.Patch(
                        ctor,
                        postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.NetCtorPostfix))
                    );
            }
            // If NITV.Entry already ran, force-apply the texture
            {
                if (Config.PokemonNetSpriteEnabled && this.netTextures != null)
                {
                    var entryType = AccessTools.TypeByName("NatureInTheValley.NatureInTheValleyEntry, NatureInTheValley");
                    var field = entryType?.GetField("netTexture", BindingFlags.Static | BindingFlags.NonPublic);
                    if (field != null && this.netTextures.TryGetValue(entryType.FullName, out var tex))
                        field.SetValue(null, tex);
                }
            }

            // patch each Net class's own loadDisplayName/getDescription
            foreach (string className in new[]
            {
                    "NatureInTheValley.NatInValeyNet, NatureInTheValley",
                    "NatureInTheValley.NatInValeySaphNet, NatureInTheValley",
                    "NatureInTheValley.NatInValeyJadeNet, NatureInTheValley",
                    "NatureInTheValley.NatInValeyGoldenNet, NatureInTheValley"
                })
            {
                var type = AccessTools.TypeByName(className);
                if (type == null)
                    continue;

                // patch the net's override of loadDisplayName()
                var nameMethod = AccessTools.Method(type, "loadDisplayName", Type.EmptyTypes);
                if (nameMethod != null)
                    harmony.Patch(
                        nameMethod,
                        postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.NamePostfix))
                    );

                // patch the net's override of getDescription()
                var descMethod = AccessTools.Method(type, "getDescription", Type.EmptyTypes);
                if (descMethod != null)
                    harmony.Patch(
                        descMethod,
                        postfix: new HarmonyMethod(typeof(Patches), nameof(Patches.DescriptPostfix))
                    );
            }

            // Prevent chopping trees/stumps/bushes in Safari Map
            harmony.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.performToolAction), new[] { typeof(Tool), typeof(int), typeof(int) }),
                prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.BlockAnyChopOnSafari))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Tree), nameof(Tree.performToolAction)),
                prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.BlockTreeChop))
            );

            harmony.Patch(
                original: AccessTools.Method(typeof(Bush), nameof(Bush.performToolAction)),
                prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.BlockBushChop))
            );

            // Spiritomb dialogue -> right-clicks show repeat‐visit menu
            {
                // patch the parameterless NPC.checkAction()
                var original = AccessTools.Method(typeof(NPC), nameof(NPC.checkAction), new[] { typeof(Farmer), typeof(GameLocation) });
                if (original != null)
                    harmony.Patch(
                        original,
                        prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.SpiritombDialoguePrefix))
                    );
                else
                    this.Monitor.Log("Couldn't find NPC.checkAction(Farmer,GameLocation)", LogLevel.Error);
            }

            {
                var original = AccessTools.Method(typeof(NPC), nameof(NPC.checkAction), new[] { typeof(Farmer), typeof(GameLocation) });
                if (original != null)
                    harmony.Patch(
                        original,
                        prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.ShadyTraderDialoguePrefix))
                    );
                else
                    this.Monitor.Log("Couldn't find NPC.checkAction(Farmer,GameLocation)", LogLevel.Error);
            }

            // Bypass the donation requirement for the terrarium
            {
                var type = AccessTools.TypeByName("NatureInTheValley.CreatureTerrariumMenu, NatureInTheValley");
                var gate = AccessTools.Method(type, "CheckDonated", new[] { typeof(StardewValley.Item) }); // static bool CheckDonated(Item)
                if (gate != null)
                    harmony.Patch(
                        original: gate,
                        prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.TerrariumGateBypassPrefix))
                    );
                else
                    this.Monitor.Log("Could not find CreatureTerrariumMenu.CheckDonated(Item).", LogLevel.Warn);
            }

            // Patch a bug where Terrarium=true and donation=false still allows donation
            {
                var type = AccessTools.TypeByName("NatureInTheValley.CreatureDonationMenu, NatureInTheValley");
                var method = AccessTools.Method(type, "receiveLeftClick", new[] { typeof(int), typeof(int), typeof(bool) });
                if (method != null)
                    harmony.Patch(
                        original: method,
                        prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.PreventInvalidDonationPrefix))
                    );
                else
                    this.Monitor.Log("Couldn't find CreatureDonationMenu.receiveLeftClick(int,int,bool)", LogLevel.Error);
            }

            // Force change terrarium base and backgrounds based on specific terms
            {
                var terrariumType = AccessTools.TypeByName("NatureInTheValley.Terrarium, NatureInTheValley");
                if (terrariumType != null)
                {
                    var back = AccessTools.Method(terrariumType, "GetBackTexture");
                    if (back != null)
                        harmony.Patch(back, prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.CustomBackPrefix)));

                    var backback = AccessTools.Method(terrariumType, "GetBackBackTexture");
                    if (backback != null)
                        harmony.Patch(backback, prefix: new HarmonyMethod(typeof(Patches), nameof(Patches.CustomBackBackPrefix)));
                }
            }

            // Simple initializer for the Pokemon Quest Board
            PokemonQuestBoardManager.Initialize(this, helper, harmony);

            this.Monitor.Log("Pokemon Addon Mod Loaded", LogLevel.Info);

        }
        
        // Crop setup on Safari Maps. This will be useful for early access of Pokemon crops with no maintenance
        private void PlacePokemonCrops(object sender, StardewModdingAPI.Events.DayStartedEventArgs e)
        {
            foreach (var kv in SafariCropLayouts.Maps)
            {
                string zoneName = kv.Key;
                var layout = kv.Value;

                GameLocation location = Game1.getLocationFromName(zoneName);
                if (location is null)
                    continue;

                void PlantCrops(IEnumerable<Vector2> tiles, string rowId)
                {
                    foreach (var t in tiles)
                        EnsureCropPlanted(location, rowId, t);
                }

                PlantCrops(layout.SafariGrass, "SafariGrass");
                PlantCrops(layout.Pecha, "PechaFruit");
                PlantCrops(layout.Oran, "OranFruit");
                PlantCrops(layout.Cheri, "CheriFruit");
            }
        }

        // Added a helper for the auto crop system
        private static void EnsureCropPlanted(GameLocation location, string cropRowId, Vector2 tile)
        {
            // Make sure there is tilled soil
            if (!location.terrainFeatures.TryGetValue(tile, out var tf) || tf is not HoeDirt)
            {
                var dirt = new HoeDirt();
                location.terrainFeatures[tile] = dirt;
                tf = dirt;
            }

            // Only plant if empty
            var hoeDirt = (HoeDirt)tf;
            if (hoeDirt.crop == null)
                hoeDirt.crop = new Crop(cropRowId, (int)tile.X, (int)tile.Y, location);
        }

        private void OnGameLaunched(object sender, StardewModdingAPI.Events.GameLaunchedEventArgs e)
        {
            var gmcm = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (gmcm == null)
                return;

            gmcm.Register(
                this.ModManifest,
                reset: () =>
                {
                    Config = new ModConfig();
                    Helper.WriteConfig(Config);
                },
                save: () =>
                {
                    Helper.WriteConfig(Config);
                    // Config reads at startup and uses guards
                }
            );

            gmcm.AddBoolOption(
                this.ModManifest,
                getValue: () => Config.PokemonNetSpriteEnabled,
                setValue: v => { Config.PokemonNetSpriteEnabled = v; },
                name: () => "Use Pokemon Net sprite",
                tooltip: () => "Disable to use original net. Requires reload."
            );
        }
    }
}