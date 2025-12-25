using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace NITVPokemonAddon
{
    public sealed class MapCropLayout
    {
        public List<Vector2> SafariGrass { get; init; } = new();
        public List<Vector2> Pecha { get; init; } = new();
        public List<Vector2> Oran { get; init; } = new();
        public List<Vector2> Cheri { get; init; } = new();
    }

    public static class SafariCropLayouts
    {
        // This is where all the crops will be specifically "mapped" as a layout per map
        public static readonly Dictionary<string, MapCropLayout> Maps = new()
        {
            // ──────────────────────────────
            // Map 1: NITVPokemon_SafariZone
            // ──────────────────────────────
            ["NITVPokemon_SafariZone"] = new MapCropLayout
            {
                SafariGrass = new()
                {
                    new Vector2(12, 7), new Vector2(13, 7), new Vector2(16, 7),
                    new Vector2(11, 8), new Vector2(12, 8), new Vector2(13, 8), new Vector2(14, 8), new Vector2(15, 8), new Vector2(16, 8), new Vector2(17, 8), new Vector2(18, 8),
                    new Vector2(11, 9), new Vector2(12, 9), new Vector2(13, 9), new Vector2(14, 9), new Vector2(15, 9), new Vector2(16, 9), new Vector2(17, 9), new Vector2(18, 9),
                    new Vector2(12, 10), new Vector2(13, 10), new Vector2(14, 10), new Vector2(15, 10), new Vector2(16, 10), new Vector2(17, 10),
                    new Vector2(12, 11), new Vector2(13, 11), new Vector2(14, 11), new Vector2(15, 11), new Vector2(16, 11), new Vector2(17, 11),
                    new Vector2(11, 12), new Vector2(12, 12), new Vector2(13, 12), new Vector2(14, 12), new Vector2(15, 12), new Vector2(16, 12), new Vector2(17, 12),
                    new Vector2(13, 13), new Vector2(14, 13), new Vector2(15, 13), new Vector2(16, 13), new Vector2(17, 13),
                    new Vector2(13, 14), new Vector2(16, 14), new Vector2(17, 14),
                    new Vector2(35, 8), new Vector2(35, 9), new Vector2(35, 10),
                    new Vector2(36, 8), new Vector2(36, 9), new Vector2(36, 10), new Vector2(36, 11), new Vector2(36, 12),
                    new Vector2(37, 7), new Vector2(37, 8), new Vector2(37, 9), new Vector2(37, 10), new Vector2(37, 11), new Vector2(37, 12), new Vector2(37, 13),
                    new Vector2(38, 6), new Vector2(38, 7), new Vector2(38, 8), new Vector2(38, 9), new Vector2(38, 10), new Vector2(38, 11), new Vector2(38, 12), new Vector2(38, 13), new Vector2(38, 14),
                    new Vector2(39, 6), new Vector2(39, 7), new Vector2(39, 8), new Vector2(39, 9), new Vector2(39, 10), new Vector2(39, 11), new Vector2(39, 12), new Vector2(39, 13), new Vector2(39, 14),
                    new Vector2(40, 6), new Vector2(40, 7), new Vector2(40, 8), new Vector2(40, 9), new Vector2(40, 10), new Vector2(40, 11), new Vector2(40, 12), new Vector2(40, 13), new Vector2(40, 14),
                    new Vector2(41, 6), new Vector2(41, 7), new Vector2(41, 8), new Vector2(41, 12), new Vector2(41, 13), new Vector2(41, 14),
                    new Vector2(42, 6), new Vector2(42, 7), new Vector2(42, 13), new Vector2(42, 14), new Vector2(43, 14),
                    new Vector2(22, 13), new Vector2(23, 13), new Vector2(24, 13),
                    new Vector2(22, 14), new Vector2(23, 14), new Vector2(24, 14),
                    new Vector2(22, 15), new Vector2(23, 15), new Vector2(24, 15), new Vector2(25, 15),
                    new Vector2(22, 16), new Vector2(23, 16), new Vector2(24, 16), new Vector2(25, 16),
                    new Vector2(22, 17), new Vector2(23, 17), new Vector2(24, 17),
                    new Vector2(20, 18), new Vector2(21, 18), new Vector2(22, 18), new Vector2(23, 18), new Vector2(24, 18), new Vector2(25, 18),
                    new Vector2(18, 19), new Vector2(19, 19), new Vector2(20, 19), new Vector2(21, 19), new Vector2(22, 19), new Vector2(23, 19), new Vector2(24, 19),
                    new Vector2(18, 20), new Vector2(19, 20), new Vector2(20, 20), new Vector2(21, 20), new Vector2(22, 20), new Vector2(23, 20),
                    new Vector2(19, 21), new Vector2(20, 21),
                    new Vector2(19, 24), new Vector2(20, 24), new Vector2(21, 24), new Vector2(22, 24),
                    new Vector2(17, 25), new Vector2(18, 25), new Vector2(19, 25), new Vector2(20, 25), new Vector2(21, 25), new Vector2(22, 25), new Vector2(23, 25),
                    new Vector2(16, 26), new Vector2(17, 26), new Vector2(18, 26), new Vector2(19, 26), new Vector2(20, 26), new Vector2(21, 26), new Vector2(22, 26), new Vector2(23, 26),
                    new Vector2(16, 27), new Vector2(17, 27), new Vector2(22, 27), new Vector2(23, 27),
                    new Vector2(16, 28), new Vector2(17, 28), new Vector2(22, 28), new Vector2(23, 28),
                    new Vector2(16, 29), new Vector2(17, 29), new Vector2(22, 29), new Vector2(17, 30), new Vector2(17, 31),
                    new Vector2(30, 26), new Vector2(31, 26), new Vector2(32, 26), new Vector2(33, 26), new Vector2(34, 26),
                    new Vector2(30, 27), new Vector2(31, 27), new Vector2(32, 27), new Vector2(33, 27), new Vector2(34, 27),
                    new Vector2(30, 28), new Vector2(31, 28), new Vector2(32, 28), new Vector2(33, 28), new Vector2(34, 28),
                    new Vector2(28, 29), new Vector2(29, 29), new Vector2(30, 29), new Vector2(31, 29), new Vector2(32, 29), new Vector2(33, 29), new Vector2(34, 29),
                    new Vector2(29, 30), new Vector2(30, 30), new Vector2(31, 30), new Vector2(32, 30), new Vector2(33, 30), new Vector2(34, 30),
                    new Vector2(29, 31), new Vector2(30, 31), new Vector2(31, 31), new Vector2(32, 31), new Vector2(33, 31), new Vector2(34, 31), new Vector2(35, 31), new Vector2(36, 31),
                    new Vector2(31, 32), new Vector2(32, 32), new Vector2(33, 32), new Vector2(34, 32), new Vector2(35, 32), new Vector2(36, 32), new Vector2(37, 32),
                    new Vector2(31, 33), new Vector2(32, 33), new Vector2(33, 33), new Vector2(34, 33), new Vector2(35, 33), new Vector2(36, 33), new Vector2(37, 33), new Vector2(33, 34)
                },

                Oran = new()
                { new Vector2(28, 30), new Vector2(18, 24), new Vector2(37, 6), new Vector2(39, 16) },

                Cheri = new()
                { new Vector2(14, 14), new Vector2(10, 33) },
                
            },

            // ───────────────────────────────
            // Map 2: NITVPokemon_SafariZone2
            // ───────────────────────────────
            ["NITVPokemon_SafariZone2"] = new MapCropLayout
            {
                SafariGrass = new()
                {
                    new Vector2(14, 7), new Vector2(15, 7), new Vector2(16, 7), new Vector2(17, 7), new Vector2(18, 7), new Vector2(19, 7), new Vector2(20, 7), new Vector2(21, 7), new Vector2(22, 7), new Vector2(23, 7), new Vector2(24, 7), new Vector2(25, 7), new Vector2(26, 7), new Vector2(27, 7), new Vector2(28, 7), new Vector2(29, 7), new Vector2(30, 7), new Vector2(31, 7),
                    new Vector2(15, 8), new Vector2(16, 8), new Vector2(17, 8), new Vector2(18, 8), new Vector2(19, 8), new Vector2(20, 8), new Vector2(21, 8), new Vector2(22, 8), new Vector2(23, 8), new Vector2(24, 8), new Vector2(25, 8), new Vector2(26, 8), new Vector2(27, 8), new Vector2(28, 8), new Vector2(29, 8), new Vector2(30, 8), new Vector2(31, 8),
                    new Vector2(16, 9), new Vector2(17, 9), new Vector2(18, 9), new Vector2(19, 9), new Vector2(20, 9), new Vector2(21, 9), new Vector2(22, 9), new Vector2(23, 9), new Vector2(24, 9), new Vector2(25, 9), new Vector2(26, 9), new Vector2(27, 9), new Vector2(28, 9),
                    new Vector2(19, 11), new Vector2(20, 11), new Vector2(16, 12), new Vector2(17, 12), new Vector2(18, 12), new Vector2(19, 12), new Vector2(20, 12), new Vector2(21, 12), new Vector2(22, 12), new Vector2(23, 12),
                    new Vector2(14, 13), new Vector2(15, 13), new Vector2(16, 13), new Vector2(17, 13), new Vector2(18, 13), new Vector2(19, 13), new Vector2(20, 13), new Vector2(21, 13), new Vector2(22, 13), new Vector2(23, 13),
                    new Vector2(11, 22), new Vector2(14, 22), new Vector2(10, 23), new Vector2(15, 23), new Vector2(10, 24), new Vector2(15, 24),
                    new Vector2(10, 25), new Vector2(11, 25), new Vector2(12, 25), new Vector2(13, 25), new Vector2(14, 25), new Vector2(15, 25),
                    new Vector2(10, 26), new Vector2(11, 26), new Vector2(12, 26), new Vector2(13, 26), new Vector2(14, 26),
                    new Vector2(13, 27), new Vector2(14, 27), new Vector2(14, 28), new Vector2(15, 28), new Vector2(13, 32), new Vector2(14, 32),
                    new Vector2(12, 33), new Vector2(13, 33), new Vector2(14, 33), new Vector2(15, 33), new Vector2(16, 33), new Vector2(17, 33), new Vector2(18, 33), new Vector2(19, 33), new Vector2(20, 33), new Vector2(21, 33), new Vector2(22, 33), new Vector2(23, 33), new Vector2(24, 33), new Vector2(25, 33), new Vector2(26, 33), new Vector2(27, 33), new Vector2(28, 33), new Vector2(29, 33), new Vector2(30, 33), new Vector2(31, 33), new Vector2(32, 33), new Vector2(33, 33), new Vector2(34, 33), new Vector2(35, 33), new Vector2(36, 33), new Vector2(45, 33), new Vector2(46, 33), new Vector2(47, 33),
                    new Vector2(11, 34), new Vector2(12, 34), new Vector2(13, 34), new Vector2(14, 34), new Vector2(15, 34), new Vector2(16, 34), new Vector2(17, 34), new Vector2(18, 34), new Vector2(19, 34), new Vector2(20, 34), new Vector2(21, 34), new Vector2(22, 34), new Vector2(23, 34), new Vector2(24, 34), new Vector2(25, 34), new Vector2(26, 34), new Vector2(27, 34), new Vector2(28, 34), new Vector2(29, 34), new Vector2(30, 34), new Vector2(31, 34), new Vector2(32, 34), new Vector2(33, 34), new Vector2(34, 34), new Vector2(35, 34), new Vector2(36, 34), new Vector2(37, 34), new Vector2(43, 34), new Vector2(44, 34), new Vector2(45, 34), new Vector2(46, 34), new Vector2(47, 34),
                    new Vector2(41, 26), new Vector2(41, 27), new Vector2(41, 28), new Vector2(41, 29), new Vector2(42, 23), new Vector2(42, 24), new Vector2(42, 25), new Vector2(42, 26), new Vector2(43, 23), new Vector2(43, 24),
                    new Vector2(44, 19), new Vector2(44, 20), new Vector2(44, 21), new Vector2(44, 22), new Vector2(44, 23), new Vector2(44, 24),
                    new Vector2(46, 10), new Vector2(46, 11), new Vector2(46, 12), new Vector2(46, 13), new Vector2(46, 14), new Vector2(46, 15), new Vector2(46, 16), new Vector2(46, 17), new Vector2(46, 18), new Vector2(46, 19), new Vector2(46, 20), new Vector2(46, 21),
                    new Vector2(47, 14), new Vector2(47, 15), new Vector2(47, 16), new Vector2(47, 17), new Vector2(47, 18), new Vector2(47, 20), new Vector2(47, 21), new Vector2(47, 22), new Vector2(47, 23)
                },

                Pecha = new()
                {
                    new Vector2(31, 19), new Vector2(16, 11), new Vector2(37, 33), new Vector2(47, 19)
                },

                Oran = new()
                {
                    new Vector2(44, 9), new Vector2(26, 12), new Vector2(9, 28)
                },

                Cheri = new()
                {
                    new Vector2(36, 14), new Vector2(30, 14), new Vector2(31, 17), new Vector2(43, 30)
                }

            }
        };
    }
}
