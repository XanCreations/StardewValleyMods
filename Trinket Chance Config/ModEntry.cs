using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace TrinketChanceConfig
{
    public class ModEntry : Mod
    {
        internal static ModEntry Instance { get; private set; }
        internal static ModConfig Config { get; set; }

        private Harmony Harmony { get; set; }

        public override void Entry(IModHelper helper)
        {
            Instance = this;
            Config = helper.ReadConfig<ModConfig>();

            this.Harmony = new Harmony(this.ModManifest.UniqueID);

            helper.Events.GameLoop.GameLaunched += this.OnGameLaunched;

            this.ApplyPatches();
        }

        // Apply Harmony patches
        private void ApplyPatches()
        {
            try
            {
                TrinketChancePatches.Apply(this.Harmony, this);
                this.Monitor.Log("Applied TrinketChanceConfig patches.", LogLevel.Trace);
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
                this.Monitor.Log("Generic Mod Config Menu not found, config UI disabled.", LogLevel.Trace);
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
                    if (value > 200)
                        value = 200;

                    Config.TrinketChancePercentOfBase = value;
                },
                name: () => "Trinket drop chance",
                tooltip: () =>
                    "Scales the drop chance of trinkets from monsters and crates.\n" +
                    "100 -> the default chance\n" +
                    "200 -> double the default chance\n" +
                    "50 -> half the default chance\n" +
                    "0 -> no trinket drops at all.",
                min: 0,
                max: 200,
                interval: 5,
                fieldId: "TrinketChancePercentOfBase"
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
    }
}
