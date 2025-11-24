using System;
using Microsoft.Xna.Framework; // for Rectangle and Vector2
using StardewValley;
using xTile.Dimensions; // for the Location type

namespace NITVNetBall
{
    /// <summary>
    /// Harmony prefix for GameLocation.checkAction.
    /// If the player is holding an object named "Net Ball" and clicked on water, then run NetBallMethods.ThrowNetBall
    /// Otherwise, return true to let the game handle it normally.
    /// </summary>
    public static class GameLocation_checkAction_NetBallPatch
    {
        public static bool Prefix(
            GameLocation __instance,
            Location tileLocation, // xTile.Dimensions.Location
            Microsoft.Xna.Framework.Rectangle viewport, // 1.6 signature includes this
            Farmer who,
            ref bool __result
        )
        {
            // If the player has no active object, or it's not "Net Ball", let vanilla run:
            if (who?.ActiveObject == null 
            || (!who.ActiveObject.Name.Equals("Net Ball", StringComparison.OrdinalIgnoreCase)
                && !who.ActiveObject.Name.Equals("Net Ball=", StringComparison.Ordinal)))
            {
                return true;
            }

            // Get the tile the player clicked from Game1.currentCursorTile:
            Vector2 cursor = Game1.currentCursorTile;
            int tileX = (int)cursor.X;
            int tileY = (int)cursor.Y;

            // If that tile is not on the map or not water, let vanilla run:
            if (!__instance.isTileOnMap(tileX, tileY) 
             || !__instance.isWaterTile(tileX, tileY))
            {
                return true;
            }

            // If a Net Ball is already in flight, ignore this click entirely
            if (NetBallMethods.IsInFlight)
            {
            __result = true; // mark “handled”
                return false; // skip vanilla and skip consumption
            }

            // Face the farmer toward the clicked tile right away
            int faceDir = NetBallMethods.GetDirectionTowardTile(who, cursor);
            who.faceDirection(faceDir);

            // capture BEFORE consuming the item
            string usedBallName = who.ActiveObject.Name;

            // Schedule the throw for the *next* tick so that facing shows first
            void DelayedThrow(object sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
            {
                ModEntry.Instance.Helper.Events.GameLoop.UpdateTicked -= DelayedThrow;
                NetBallMethods.ThrowNetBall(who, __instance, cursor, usedBallName);
            }
            ModEntry.Instance.Helper.Events.GameLoop.UpdateTicked += DelayedThrow;

            // Consume one Net Ball from the player's stack
            who.ActiveObject.Stack--;
            if (who.ActiveObject.Stack <= 0)
                who.removeItemFromInventory(who.ActiveObject);

            // Tell the game the click has been handled
            __result = true;
            return false;
        }
    }
}
