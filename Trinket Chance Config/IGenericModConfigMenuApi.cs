using System;
using StardewModdingAPI;

namespace TrinketChanceConfig
{
    public interface IGenericModConfigMenuApi
    {
        // Register a mod config that can be edited
        void Register(
            IManifest mod,
            Action reset,
            Action save,
            bool titleScreenOnly = false
        );

        // Add a simple checkbox option
        void AddBoolOption(
            IManifest mod,
            Func<bool> getValue,
            Action<bool> setValue,
            Func<string> name = null,
            Func<string> tooltip = null,
            string fieldId = null
        );

        // Add a numeric option (slider)
        void AddNumberOption(
            IManifest mod,
            Func<int> getValue,
            Action<int> setValue,
            Func<string> name = null,
            Func<string> tooltip = null,
            int? min = null,
            int? max = null,
            int? interval = null,
            string fieldId = null
        );
    }
}
