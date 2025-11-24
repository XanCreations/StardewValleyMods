using HarmonyLib;
using System.Reflection;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Tools;
using StardewValley.TerrainFeatures;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using StardewValley.Menus;
using StardewModdingAPI.Utilities;
using System;
using Microsoft.Xna.Framework.Graphics;
using StardewValley.Triggers;

namespace NITVPokemonAddon
{
    internal static class Patches
    {

        private static readonly string[] Stage1Ids = new[]
        {
            "Pidgey_Roam","Geodude_Attack","Zubat_Attack","Caterpie_Bush","Weedle_Bush",
            "Rattata_Roam","Magikarp_Swim","Metapod_Tree","Kakuna_Tree"
        };

        private static readonly string[] Stage2Ids = new[]
        {
            "Butterfree_Roam","Beedrill_Attack","Ekans_Attack","Poliwag_Swim","Tentacool_Swim",
            "Growlithe_Roam","Venonat_Stump","Paras_Roam","Diglett_Roam","Sandshrew_Roam"
        };

        private static readonly string[] Stage3Ids = new[]
        {
            "Spearow_Attack","Koffing_Attack","Oddish_Stump","Bellsprout_Roam","Grimer_Roam",
            "Cubone_Roam","Rhyhorn_Attack","Shellder_Swim","Goldeen_Swim","Psyduck_Swim"
        };

        public static void EntryPostfix(IModHelper helper)
        {
            // bail if sprite override is disabled
            if (ModEntry.Instance?.Config?.PokemonNetSpriteEnabled != true
                || ModEntry.Instance.netTextures == null)
                return;

            var type = AccessTools.TypeByName("NatureInTheValley.NatureInTheValleyEntry, NatureInTheValley");
            var field = type.GetField("netTexture", BindingFlags.Static | BindingFlags.NonPublic);
            if (field != null)
            {
                var tex = ModEntry.Instance.netTextures[type.FullName];
                field.SetValue(null, tex);
            }
        }

        public static void NetCtorPostfix(object __instance)
        {
            if (ModEntry.Instance?.Config?.PokemonNetSpriteEnabled != true
                || ModEntry.Instance.netTextures == null)
                return;

            var type = __instance.GetType();
            var field = type.GetField("texture", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
            {
                var tex = ModEntry.Instance.netTextures[type.FullName];
                field.SetValue(null, tex);
            }
        }

        public static void NamePostfix(ref string __result, StardewValley.Object __instance)
        {
            {
                if (ModEntry.Instance?.Config?.PokemonNetSpriteEnabled != true)
                    return;
                switch (__instance.ItemId)
                {
                    case "NIVNet":
                        __result = ModEntry.Instance.Helper.Translation.Get("NetName");
                        break;
                    case "NIVSaphNet":
                        __result = ModEntry.Instance.Helper.Translation.Get("SaphNetName");
                        break;
                    case "NIVJadeNet":
                        __result = ModEntry.Instance.Helper.Translation.Get("JadeNetName");
                        break;
                    case "NIVGoldNet":
                        __result = ModEntry.Instance.Helper.Translation.Get("GoldenNetName");
                        break;
                    default:
                        return;
                }
            }
        }

        public static void DescriptPostfix(ref string __result, StardewValley.Object __instance)
        {
            {
                if (ModEntry.Instance?.Config?.PokemonNetSpriteEnabled != true)
                    return;
                switch (__instance.ItemId)
                {
                    case "NIVNet":
                        __result = ModEntry.Instance.Helper.Translation.Get("NetDescript");
                        break;
                    case "NIVSaphNet":
                        __result = ModEntry.Instance.Helper.Translation.Get("SaphNetDescript");
                        break;
                    case "NIVJadeNet":
                        __result = ModEntry.Instance.Helper.Translation.Get("JadeNetDescript");
                        break;
                    case "NIVGoldNet":
                        __result = ModEntry.Instance.Helper.Translation.Get("GoldenNetDescript");
                        break;
                    default:
                        return;
                }
            }
        }
        // Prevent using axe logic in Safari Map
        public static bool BlockAnyChopOnSafari(Tool t, int tileX, int tileY, GameLocation __instance, ref bool __result)
        {
            if (__instance.Name == "NITVPokemon_SafariZone" && t is Axe)
            {
                __result = false;
                return false; // cancel original method
            }

            if (__instance.Name == "NITVPokemon_SafariZone2" && t is Axe)
            {
                __result = false;
                return false; // cancel original method
            }

            return true;
        }

        public static bool BlockTreeChop(Tool t, int explosion, Vector2 tileLocation, Tree __instance, ref bool __result)
        {
            if (Game1.currentLocation?.Name == "NITVPokemon_SafariZone")
            {
                __result = false;
                return false;
            }

            if (Game1.currentLocation?.Name == "NITVPokemon_SafariZone2")
            {
                __result = false;
                return false;
            }

            return true;
        }

        public static bool BlockBushChop(Tool t, int explosion, Vector2 tileLocation, Bush __instance, ref bool __result)
        {
            if (Game1.currentLocation?.Name == "NITVPokemon_SafariZone")
            {
                __result = false;
                return false;
            }

            if (Game1.currentLocation?.Name == "NITVPokemon_SafariZone2")
            {
                __result = false;
                return false;
            }

            return true;
        }

        /// <summary>
        /// Check donation progress and display the appropriate Spiritomb dialogue.
        /// </summary>
        private static void ShowDonationResult(NPC spiritomb, IModHelper helper)
        {
            const string DonationPrefix = "NatureInTheValley/Donated/";
            const string Stage1AllShownKey = "NITV.Spiritomb.Stage1AllShown"; // one-time Q3_all gate
            const string Stage2AllShownKey = "NITV.Spiritomb.Stage2AllShown"; // one-time Q4_all gate

            bool Has(string id)
            {
                string k = DonationPrefix + id;

                if (Game1.player?.modData.ContainsKey(k) == true)
                    return true;

                foreach (var L in Game1.locations)
                    if (L?.modData.ContainsKey(k) == true)
                        return true;

                return false;
            }

            (int count, List<string> seen, List<string> missing) SeenIds(IEnumerable<string> ids)
            {
                var seen = new List<string>();
                var missing = new List<string>();
                foreach (var id in ids)
                {
                    if (Has(id)) seen.Add(id);
                    else missing.Add(id);
                }
                return (seen.Count, seen, missing);
            }

            var s1 = SeenIds(Stage1Ids);
            var s2 = SeenIds(Stage2Ids);
            var s3 = SeenIds(Stage3Ids);

            ModEntry.Instance.Monitor.Log(
                $"[Spiritomb] S1 {s1.count}/{Stage1Ids.Length} | seen=[{string.Join(",", s1.seen)}] | missing=[{string.Join(",", s1.missing)}]",
                StardewModdingAPI.LogLevel.Debug
            );
            ModEntry.Instance.Monitor.Log(
                $"[Spiritomb] S2 {s2.count}/{Stage2Ids.Length} | seen=[{string.Join(",", s2.seen)}] | missing=[{string.Join(",", s2.missing)}]",
                StardewModdingAPI.LogLevel.Debug
            );
            ModEntry.Instance.Monitor.Log(
                $"[Spiritomb] S3 {s3.count}/{Stage3Ids.Length} | seen=[{string.Join(",", s3.seen)}] | missing=[{string.Join(",", s3.missing)}]",
                StardewModdingAPI.LogLevel.Debug
            );

            bool p2Unlocked = Game1.player.modData.ContainsKey("NITV.Spiritomb.P2Unlocked");
            bool p3Unlocked = Game1.player.modData.ContainsKey("NITV.Spiritomb.P3Unlocked");
            bool stage1AllShown  = Game1.player.modData.ContainsKey(Stage1AllShownKey);
            bool stage2AllShown = Game1.player.modData.ContainsKey(Stage2AllShownKey);

            ModEntry.Instance.Monitor.Log(
                $"[Spiritomb] Flags -> P2Unlocked={p2Unlocked}, P3Unlocked={p3Unlocked}, Stage1AllShown={stage1AllShown}, Stage2AllShown={stage2AllShown}",
                StardewModdingAPI.LogLevel.Debug
            );


            // Sync flags with actual progress
            if (s1.count == Stage1Ids.Length && !p2Unlocked)
            {
                Game1.player.modData["NITV.Spiritomb.SafariUnlocked"] = "yes";
                Game1.player.modData["NITV.Spiritomb.P2Unlocked"] = "yes";
                p2Unlocked = true;
            }
            if (s2.count == Stage2Ids.Length && !p3Unlocked)
            {
                Game1.player.modData["NITV.Spiritomb.Stage2Complete"] = "yes";
                Game1.player.modData["NITV.Spiritomb.P3Unlocked"] = "yes";
                p3Unlocked = true;
            }

            // Need to get around the stale P3 flag if Stage 2 isn't complete
            if (p3Unlocked && s2.count < Stage2Ids.Length)
            {
                ModEntry.Instance.Monitor.Log("[Spiritomb] Detected P3Unlocked=true but Stage 2 not complete; clearing stale flag.", StardewModdingAPI.LogLevel.Warn);
                Game1.player.modData.Remove("NITV.Spiritomb.P3Unlocked");
                p3Unlocked = false;
            }

            // Pick dialogue key
            string key;

            // If Stage 1 complete but we haven't shown the completion line yet -> show Q3_all ONCE
            if (s1.count == Stage1Ids.Length && !stage1AllShown)
            {
                key = "Q3_all";
                // unlock safari + gate stage 2 (same as your sync above, but we ensure the line shows once)
                Game1.player.modData["NITV.Spiritomb.SafariUnlocked"] = "yes";
                Game1.player.modData["NITV.Spiritomb.P2Unlocked"] = "yes";
                Game1.player.modData[Stage1AllShownKey] = "yes";
            }
            // Stage 1 not done -> Q3_none/some
            else if (s1.count < Stage1Ids.Length)
            {
                key = s1.count > 0 ? "Q3_some" : "Q3_none";
            }
            // Stage 2 ongoing -> Q4
            else if (s2.count < Stage2Ids.Length)
            {
                key = s2.count > 0 ? "Q4_some" : "Q4_none";
            }
            // Stage 2 complete -> show Q4_all ONCE; thereafter progress to Stage 3
            else if (!stage2AllShown)
            {
                key = "Q4_all";
                Game1.player.modData[Stage2AllShownKey] = "yes";
                Game1.player.modData["NITV.Spiritomb.P3Unlocked"] = "yes";
                p3Unlocked = true;
            }
            // Stage 3 -> Q5
            else
            {
                bool all3 = s3.count == Stage3Ids.Length;
                key = all3 ? "Q5_all" : (s3.count > 0 ? "Q5_some" : "Q5_none");
                if (all3)
                    Game1.player.modData["NITV.Spiritomb.Stage3Complete"] = "yes";
            }

            ModEntry.Instance.Monitor.Log($"[Spiritomb] Dialogue key chosen -> {key}", StardewModdingAPI.LogLevel.Debug);

            string assetKey = $"Characters/Dialogue/Spiritomb:{key}";
            var dlg = new Dialogue(spiritomb, assetKey);
            Game1.activeClickableMenu = new DialogueBox(dlg);
        }

        /// <summary>
        /// Prefix on NPC.checkAction() -> next click opens custom menu.
        /// Returns true on first visit (lets CP run). False afterwards.
        /// </summary>
        public static bool SpiritombDialoguePrefix(NPC __instance)
        {
            // Spiritomb located in the OuterInsec map
            if (Game1.currentLocation?.Name != "NIVOuterInsec" || __instance.Name != "Spiritomb")
                return true;
            const string firstTalkKey = "NITV.Spiritomb.FirstTalk";
            var helper = ModEntry.Instance.Helper;
            var log = ModEntry.Instance.Monitor;

            // first visit: record visit -> go to CP dialogue
            if (!Game1.player.modData.ContainsKey(firstTalkKey))
            {
                Game1.player.modData[firstTalkKey] = "yes";
                return true;
            }

            log.Log("Spiritomb -> showing repeat-visit menu", LogLevel.Trace);

            // only show “Go to Safari Zone?” once they've seen Q3_all
            bool safariUnlocked = Game1.player.modData.ContainsKey("NITV.Spiritomb.SafariUnlocked");

            // build responses
            var answers = new List<Response> {
                new Response("check",  helper.Translation.Get("Menu_checkprogress"))
            };
            if (safariUnlocked)
                answers.Add(new Response("safari", helper.Translation.Get("Menu_safari")));
            answers.Add(new Response("nevermind", helper.Translation.Get("Menu_nevermind")));

            // display the menu
            Game1.currentLocation.createQuestionDialogue(
                helper.Translation.Get("Menu_prompt"),
                answers.ToArray(),
                (farmer, choice) =>
                {
                    log.Log($"Spiritomb menu choice: {choice}", LogLevel.Trace);
                    if (choice == "check")
                    {
                        ShowDonationResult(__instance, helper);
                    }
                    else if (choice == "safari")
                    {
                        // magic-warp effect -> delay -> warp
                        Game1.playSound("wand");
                        Game1.flashAlpha = 1f;
                        Game1.delayedActions.Add(new DelayedAction(500, () =>
                        {
                            Game1.warpFarmer("NITVPokemon_SafariZone", 52, 32, false);
                        }));
                    }
                    // "nevermind" just closes the box
                }
            );

            // skip the game's built-in dialogue
            return false;
        }
        public static bool ShadyTraderDialoguePrefix(NPC __instance)
        {
            if (__instance?.Name != "ShadyTrader")
                return true;

            var helper = ModEntry.Instance.Helper;
            var log = ModEntry.Instance.Monitor;
            var farmer = Game1.player;

            bool isFriday = SDate.Now().DayOfWeek == DayOfWeek.Friday;
            const string seenTodayKey = "NITV.ShadyTrader.FridaySeen";
            const string boughtKey = "NITV.ShadyTrader.BoughtCharmander";
            bool bought = farmer.modData.ContainsKey(boughtKey) || Game1.player.mailReceived.Contains("NITV_ShadyTrader_Bought");
            if (bought)
            {
                Game1.drawObjectDialogue(helper.Translation.Get("ShadyTrader_Refund"));
                return false;
            }

            // If already bought, just say “no refunds.”
            if (farmer.modData.ContainsKey(boughtKey))
            {
                Game1.drawObjectDialogue(helper.Translation.Get("ShadyTrader_Refund"));
                return false;
            }

            // First Friday talk —> show CP dialogue
            if (isFriday && !farmer.modData.ContainsKey(seenTodayKey))
            {
                farmer.modData[seenTodayKey] = "yes";
                string assetKey = $"Characters/Dialogue/ShadyTrader:Fri";
                var dlg = new Dialogue(__instance, assetKey);
                Game1.activeClickableMenu = new DialogueBox(dlg);
                return false;
            }

            // ─── Repeat menu (fallback) ───
            log.Log("ShadyTrader -> showing repeat menu", LogLevel.Trace);

            var answers = new List<Response>
            {
                new Response("yes", helper.Translation.Get("Menu_yes")),
                new Response("no", helper.Translation.Get("Menu_nevermind"))
            };

            Game1.currentLocation.createQuestionDialogue(
                helper.Translation.Get("Menu_prompt"),
                answers.ToArray(),
                (who, answer) =>
                {
                    if (answer == "yes")
                    {
                        if (farmer.Money >= 20000)
                        {
                            var item = ItemRegistry.Create("NITVPokemon_CharLOLTrinket");
                            if (item != null)
                            {
                                Game1.playSound("purchase");
                                farmer.Money -= 20000;
                                farmer.addItemByMenuIfNecessaryElseHoldUp(item);
                                farmer.modData[boughtKey] = "yes";
                                Game1.drawObjectDialogue(helper.Translation.Get("ShadyTrader_Success"));
                                TriggerActionManager.TryRunAction("AddMail All NITV_ShadyTrader_Bought received", out _, out _);
                            }
                            else
                            {
                                log.Log("Failed to create Charmander trinket item!", LogLevel.Warn);
                                Game1.drawObjectDialogue("There was a problem giving you the item.");
                            }
                        }
                        else
                        {
                            Game1.drawObjectDialogue(helper.Translation.Get("ShadyTrader_NoMoney"));
                        }
                    }
                    else if (answer == "no")
                    {
                        Game1.drawObjectDialogue(helper.Translation.Get("ShadyTrader_Cancel"));
                    }
                }
            );

            return false;
        }

        public static bool TerrariumGateBypassPrefix(StardewValley.Item item, ref bool __result)
        {
            try
            {
                // Only consider NITV creature objects
                if (item == null || item.Category != -81 || string.IsNullOrEmpty(item.ItemId))
                    return true;

                const string prefix = "NatInValley.Creature.";
                int mark = item.ItemId.IndexOf(prefix, StringComparison.Ordinal);
                if (mark < 0)
                    return true;

                string creatureKey = item.ItemId.Substring(mark + prefix.Length);
                if (string.IsNullOrEmpty(creatureKey))
                    return true;

                // Get NatureInTheValleyEntry.staticCreatureData (Dictionary<string, List<string>>)
                var entryType = AccessTools.TypeByName("NatureInTheValley.NatureInTheValleyEntry, NatureInTheValley");
                var dataField = entryType?.GetField("staticCreatureData", BindingFlags.Public | BindingFlags.Static);
                var dataMap = dataField?.GetValue(null) as Dictionary<string, List<string>>;
                if (dataMap == null || !dataMap.TryGetValue(creatureKey, out var row))
                    return true;

                // Terrarium menu checks index 42 == "true" for isTerrariumable
                // Donation menu checks index 43 == "true" for isDonatable
                bool isTerrariumable = (row.Count <= 43) || string.Equals(row[42], "true", StringComparison.OrdinalIgnoreCase);
                bool isDonatable = (row.Count <= 44) || string.Equals(row[43], "true", StringComparison.OrdinalIgnoreCase);

                if (isTerrariumable && !isDonatable)
                {
                    __result = true;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ModEntry.Instance?.Monitor.Log($"TerrariumGateBypassPrefix failed: {ex}", LogLevel.Trace);
                return true; // a fail safe just incase
            }
        }

        // Fix for the invalid donations to prevent players accidentally donating creatures
        public static bool PreventInvalidDonationPrefix(object __instance, int x, int y, bool playSound)
        {
            try
            {
                // get the item under the cursor (InventoryMenu.getItemAt)
                var getItemAt = AccessTools.Method(__instance.GetType(), "getItemAt", new[] { typeof(int), typeof(int) });
                var itemAt = (Item)getItemAt?.Invoke(__instance, new object[] { x, y });

                // let it run like normal -> closes the menu
                if (itemAt == null)
                    return true;

                // call the CreatureDonationMenu.CheckDonated
                var check = AccessTools.Method(__instance.GetType(), "CheckDonated", new[] { typeof(Item) });
                bool canDonate = (bool)(check?.Invoke(null, new object[] { itemAt }) ?? false);

                if (!canDonate)
                {
                    Game1.playSound("cancel");
                    return false; //do not let it run like normal since donate=false
                }

                // otherwise run normally
                return true;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"PreventInvalidDonationPrefix failed: {ex}", LogLevel.Warn);
                // a fail safe just incase
                return true;
            }
        }

        // Force "fake" location values to add custom Terrarium aesthetics
        private static readonly (string term, string suffix)[] CustomBaseByLocationKeyword = new[]
        {
            ("ForceBeach", "ForceBeach"),
            ("ForceCave", "ForceCave"),
            ("ForceDesert", "ForceDesert"),
            ("ForceVolcano", "ForceVolcano"),
            ("ForceForest", "ForceForest"),
            ("ForcePlains", "ForcePlains"),
            ("ForceDark", "ForceDark"),
            ("ForceElectric", "ForceElectric"),
            ("ForceSewer", "ForceSewer"),
            ("ForceIce", "ForceIce"),
            ("ForceSwamp", "ForceSwamp"),
            // add more here
        };

        // Need some simple caches otherwise it loads repeatedly
        private static readonly Dictionary<string, Texture2D> _baseCache = new();
        private static readonly Dictionary<string, Texture2D> _backBackCache = new();

        // Read the packed data list via Terrarium.GetData()
        private static List<string> GetTerrariumData(object terrarium)
        {
            var m = AccessTools.Method(terrarium.GetType(), "GetData");
            return (List<string>)m.Invoke(terrarium, Array.Empty<object>());
        }

        // Find a custom suffix from locations (index 11)
        private static bool TryGetCustomSuffix(object terrarium, out string suffix)
        {
            suffix = null;
            List<string> data = GetTerrariumData(terrarium);
            if (data is null || data.Count <= 11) return false;

            string locationsBlob = data[11] ?? string.Empty;

            // Find the first keyword in the location key list
            foreach (var (term, sfx) in CustomBaseByLocationKeyword)
            {
                if (locationsBlob.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    suffix = sfx;
                    return true;
                }
            }
            return false;
        }

        // Prefix on Terrarium.GetBackTexture()
        public static bool CustomBackPrefix(object __instance, ref Texture2D __result)
        {
            if (!TryGetCustomSuffix(__instance, out string suffix))
                return true; // no match, run original

            // Load the images from assets folder
            if (!_baseCache.TryGetValue(suffix, out var tex))
            {
                tex = ModEntry.Instance.Helper.ModContent.Load<Texture2D>($"assets/Base{suffix}.png");
                _baseCache[suffix] = tex;
            }
            __result = tex;
            return false;
        }

        // Prefix on Terrarium.GetBackBackTexture()
        public static bool CustomBackBackPrefix(object __instance, ref Texture2D __result)
        {
            if (!TryGetCustomSuffix(__instance, out string suffix))
                return true; // no match, run original

            if (!_backBackCache.TryGetValue(suffix, out var tex))
            {
                tex = ModEntry.Instance.Helper.ModContent.Load<Texture2D>($"assets/BackBack{suffix}.png");
                _backBackCache[suffix] = tex;
            }
            __result = tex;
            return false;
        }

    }
}
