namespace TrinketChanceConfig
{
    internal class ModConfig
    {
        /// <summary>
        /// Multiplier for the vanilla trinket drop chance, expressed as a percentage
        /// 0 = 0% (no drops)
        /// 100 = 100% (vanilla)
        /// 200 = 200% (double chance)
        /// 1000 = 1000% (expanded to such a large number because "DisableDuplicateTrinketDrops" being set to true compounds the chance the spawn a trinket the less trinkets there are to find)
        /// </summary>
        public int TrinketChancePercentOfBase { get; set; } = 100;

        /// <summary>
        /// If set to true -> disable the specific vanilla trinket IDs from dropping
        /// This allows custom trinkets like those made from TrinketTinker to drop normally
        /// </summary>
        public bool BlockVanillaTrinkets { get; set; } = false;

        /// <summary>
        /// If set to true -> any player that has obtained a trinket -> will prevent that trinket dropping again
        /// If set to false -> trinkets drop like normal
        /// </summary>
        public bool DisableDuplicateTrinketDrops { get; set; } = false;



    }
}
