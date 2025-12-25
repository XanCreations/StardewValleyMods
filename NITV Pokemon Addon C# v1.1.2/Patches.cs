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

        // Mail flag based Spiritomb dialogue, no donation scanning / no modData flags (this was what I was doing before)
        private static void ShowDonationResult(NPC spiritomb, IModHelper helper)
        {
            bool HasMail(string id) => Game1.player.mailReceived.Contains(id);

            // progression flags set by trigger actions in the content JSON which are driven by donations
            bool g1 = HasMail("PokemonTrigger1");
            bool g2 = HasMail("PokemonTrigger2");
            bool g3 = HasMail("PokemonTrigger3");

            // reward/seen flags set by Spiritomb dialogue actions
            bool r1 = HasMail("Spiritomb_1st_reward");
            bool r2 = HasMail("Spiritomb_2nd_reward");
            bool r3 = HasMail("Spiritomb_3rd_reward");

            string key;

            // Stage 1: Not eligible -> Q3_none. Eligible -> show Q3_all, but don't repeat again
            if (!g1)
                key = "Q3_none";
            else if (!r1)
                key = "Q3_all";

            // Stage 2: After Q3_all has been seen, move to Q4_none unless eligible for Q4_all
            else if (!g2)
                key = "Q4_none";
            else if (!r2)
                key = "Q4_all";

            // Stage 3: After Q4_all has been seen, move to Q5_none unless eligible for Q5_all
            else if (!g3)
                key = "Q5_none";
            else if (!r3)
                key = "Q5_all";

            // Spiritomb has finished it's "intro" and rewards to the player -> now it will just use it's fallback dialogue since there is no more menu
            else
                key = "Spiritomb_fallback";

            ModEntry.Instance.Monitor.Log(
                $"[Spiritomb] Mail -> G1={g1},G2={g2},G3={g3} | R1={r1},R2={r2},R3={r3} | key={key}",
                LogLevel.Debug
            );

            string assetKey = $"Characters/Dialogue/Spiritomb:{key}";
            var dlg = new Dialogue(spiritomb, assetKey);
            Game1.activeClickableMenu = new DialogueBox(dlg);
        }

        // Prefix on NPC.checkAction() -> next click opens custom menu
        // Returns true on first visit (lets CP run). False afterwards
        public static bool SpiritombDialoguePrefix(NPC __instance)
        {
            // Spiritomb located in the OuterInsec map
            if (Game1.currentLocation?.Name != "NIVOuterInsec" || __instance.Name != "Spiritomb")
                return true;
            const string firstTalkKey = "NITV.Spiritomb.FirstTalk";
            var helper = ModEntry.Instance.Helper;
            var log = ModEntry.Instance.Monitor;

            // first visit -> record visit -> go to CP dialogue
            if (!Game1.player.modData.ContainsKey(firstTalkKey))
            {
                Game1.player.modData[firstTalkKey] = "yes";
                return true;
            }

            // After the final reward, let CP / vanilla dialogue run (no custom menu anymore)
            if (Game1.player.mailReceived.Contains("Spiritomb_3rd_reward"))
                return true;

            log.Log("Spiritomb -> showing repeat-visit menu", LogLevel.Trace);

            // only show safari zone option when the player has bought the first contract from Ivy via a mail flag
            bool safariUnlocked = Game1.player.mailReceived.Contains("PokemonTrigger1");

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

            // If already bought, just say 'no refunds.'
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

            // Repeat menu (fallback)
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
                    return false; //do not let it run like normal since donate = false
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

        public static bool PokeTeleporter_CheckForActionPrefix(
            StardewValley.Object __instance,
            Farmer who,
            bool justCheckingForActivity,
            ref bool __result)
        {
            if (__instance == null || who == null)
                return true;

            // Big craftables are (BC) qualified IDs
            if (!__instance.bigCraftable.Value)
                return true;

            if (__instance.QualifiedItemId != "(BC)Xan_PokeTeleporter" && __instance.ItemId != "Xan_PokeTeleporter")
                return true;

            var helper = ModEntry.Instance.Helper;
            var log = ModEntry.Instance.Monitor;

            // Let the game know the player can interact with it
            if (justCheckingForActivity)
            {
                __result = true;
                return false;
            }

            var answers = new List<Response>
            {
                new Response("safari", helper.Translation.Get("Menu_safari")),
                new Response("nevermind", helper.Translation.Get("Menu_nevermind"))
            };

            Game1.currentLocation.createQuestionDialogue(
                helper.Translation.Get("Teleporter_prompt"),
                answers.ToArray(),
                (farmer, choice) =>
                {
                    log.Log($"Safari Teleporter choice: {choice}", LogLevel.Trace);

                    if (choice == "safari")
                        WarpToSafariWithEffect();
                    // "Nevermind" does nothing
                }
            );

            __result = true;
            return false; // interaction completed
        }

        private static void WarpToSafariWithEffect() //same as what was used before for the Spiritomb dialogue
        {
            Game1.playSound("wand");
            Game1.flashAlpha = 1f;
            Game1.delayedActions.Add(new DelayedAction(500, () =>
            {
                Game1.warpFarmer("NITVPokemon_SafariZone", 52, 32, false);
            }));
        }

    }
}
