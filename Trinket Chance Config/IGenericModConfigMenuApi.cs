using System;
using StardewModdingAPI;

namespace TrinketChanceConfig
{
    public interface IGenericModConfigMenuApi
    {
        /// <param name="mod"></param>
        /// <param name="reset"></param>
        /// <param name="save"></param>
        /// <param name="titleScreenOnly">
        /// </param>
        void Register(
            IManifest mod,
            Action reset,
            Action save,
            bool titleScreenOnly = false
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
