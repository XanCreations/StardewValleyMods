using Microsoft.Xna.Framework;
using StardewValley;
using StardewModdingAPI.Events;
using System.Reflection;
using System;
using StardewModdingAPI;
using System.Collections;
using StardewModdingAPI.Utilities;

namespace NITVNetBall
{
    public static partial class NetBallMethods
    {
        // Prevent more than one Net Ball at a time
        private static readonly PerScreen<bool> _netBallInFlight = new(() => false);
        public static bool IsInFlight => _netBallInFlight.Value; // True if any screen currently has a Net Ball being thrown

        public static void ThrowNetBall(Farmer who, GameLocation location, Vector2 clickedTile, string usedBallName)
        {
            if (_netBallInFlight.Value)
                return;
            _netBallInFlight.Value = true;

            // Face throw direction
            who.faceDirection(GetDirectionTowardTile(who, clickedTile));

            // Landing position (center of clicked tile)
            Vector2 landingPos = new(
                clickedTile.X * Game1.tileSize + Game1.tileSize / 2f,
                clickedTile.Y * Game1.tileSize + Game1.tileSize / 2f
            );

            // Sprite chosen by Content Patcher
            string textureName = string.Equals(usedBallName, "Net Ball=", StringComparison.Ordinal)
                ? "TileSheets/NetBall2"
                : "TileSheets/NetBall";
            Rectangle sourceRect = new(0, 0, Game1.tileSize, Game1.tileSize);

            // Start near farmer's hand
            Vector2 startPos = who.getStandingPosition() + new Vector2(0, -Game1.tileSize / 2f);

            float scale = 3f;
            float layerDepth = (startPos.Y + 128f) / 10000f;

            // Simple parabolic arc
            int totalTicks = 48;
            float gravity = 0.2f;
            Vector2 accel = new(0f, gravity);
            Vector2 delta = landingPos - startPos;
            Vector2 initVel = new(
                delta.X / totalTicks,
                (delta.Y - 0.5f * gravity * totalTicks * totalTicks) / totalTicks
            );

            var sprite = new TemporaryAnimatedSprite(
                textureName, sourceRect, 9999f, 1, 0,
                startPos, false, false, layerDepth,
                0f, Color.White, scale, 0f, 0f, 0f, false
            )
            {
                motion = initVel,
                acceleration = accel
            };
            location.temporarySprites.Add(sprite);
            Game1.playSound("NetBallThrow");

            // Tick until impact
            int ticksLeft = totalTicks;
            int myScreen = Context.ScreenId;

            void onTick(object s, UpdateTickedEventArgs e)
            {
                if (Context.ScreenId != myScreen)
                    return;

                ticksLeft--;
                if (ticksLeft > 0)
                    return;

                Game1.playSound("dropItemInWater");

                var splash = new TemporaryAnimatedSprite(
                    "TileSheets\\animations",
                    new Rectangle(0, 832, 64, 64),
                    50f, 10, 1,
                    new Vector2(landingPos.X - 8f, landingPos.Y - 8f),
                    false, false,
                    (landingPos.Y + 16f) / 10000f,
                    0f, Color.White, 0.8f, 0f, 0f, 0f, false
                )
                {
                    layerDepth = (landingPos.Y + 16f) / 10000f,
                };
                location.temporarySprites.Add(splash);

                // Only works on host - this still works for co-op/multiplayer
                if (Context.IsOnHostComputer)
                {
                    bool caught = CheckForCreatureAndCatch(location, landingPos, who);
                    // Block spawn chance if a catch just happened
                    if (!caught)
                        NetBallMethods.TryNetBallSpawnChance(location, landingPos, who, usedBallName);
                }

                location.temporarySprites.Remove(sprite);
                _netBallInFlight.Value = false;

                ModEntry.Instance.Helper.Events.GameLoop.UpdateTicked -= onTick;
            }
            ModEntry.Instance.Helper.Events.GameLoop.UpdateTicked += onTick;
        }

        public static int GetDirectionTowardTile(Farmer player, Vector2 targetTile)
        {
            Vector2 standing = player.getStandingPosition();
            Vector2 center = new(
                (standing.X + Game1.tileSize / 2f) / Game1.tileSize,
                (standing.Y + Game1.tileSize / 2f) / Game1.tileSize
            );

            float dx = targetTile.X - center.X;
            float dy = targetTile.Y - center.Y;
            float angle = (float)Math.Atan2(dy, dx);

            if (angle >= -Math.PI / 4f && angle < Math.PI / 4f) return 1;
            if (angle >= Math.PI / 4f && angle < 3f * Math.PI / 4f) return 2;
            if (angle >= 3f * Math.PI / 4f || angle < -3f * Math.PI / 4f) return 3;
            return 0;
        }

        // Returns true if a creature was caught
        private static bool CheckForCreatureAndCatch(GameLocation location, Vector2 landingPos, Farmer player)
        {
            var log = ModEntry.Instance.Monitor;

            // dynamic grab radius (controller vs KB/mouse)
            float baseRadius = Game1.options.gamepadControls
                ? 0.7f * Game1.tileSize
                : 0.3f * Game1.tileSize;

            // special Net Ball= variant boosts range
            float grabRadius = baseRadius;
            if (player?.ActiveObject?.Name == "Net Ball=")
                grabRadius *= 2.5f;

            log.Log($"[NetBall] grabRadius = {grabRadius} pixels", LogLevel.Debug);

            // reflect to get the NatureInTheValley creatures list
            Type entryType = Type.GetType("NatureInTheValley.NatureInTheValleyEntry, NatureInTheValley");

            var creaturesField = entryType?.GetField("creatures", BindingFlags.Public | BindingFlags.Static);
            if (creaturesField == null)
            {
                log.Log("[NetBall] creaturesField is null!", LogLevel.Error);
                return false;
            }
            var list = creaturesField.GetValue(null) as IList;
            if (list == null)
            {
                log.Log("[NetBall] creatures list cast failed!", LogLevel.Error);
                return false;
            }
            log.Log($"[NetBall] creaturesList contains {list.Count} entries", LogLevel.Debug);

            int i = 0;
            foreach (object natCreature in list)
            {
                var getPos = natCreature.GetType().GetMethod("GetEffectivePosition");
                if (getPos == null) continue;

                Vector2 pos = (Vector2)getPos.Invoke(natCreature, null);
                float dist = Vector2.Distance(pos, landingPos);
                log.Log($"[NetBall] Creature #{i++}: pos={pos}, dist={dist}", LogLevel.Trace);

                if (dist <= grabRadius)
                {
                    log.Log($"[NetBall] Catching creature #{i - 1}", LogLevel.Info);
                    var tryCatch = entryType.GetMethod("TryCatch", BindingFlags.Public | BindingFlags.Static);
                    Vector2 origPos = player.Position;
                    int origFacing = player.FacingDirection;

                    // convert creature world pos to tile
                    Vector2 creatureTile = new(
                        (int)(pos.X / Game1.tileSize),
                        (int)(pos.Y / Game1.tileSize)
                    );

                    // face creature
                    int facing = GetDirectionTowardTile(player, creatureTile);
                    player.FacingDirection = facing;

                    // back player up 96px opposite direction to make sure it reaches
                    Vector2 offset = facing switch
                    {
                        0 => new Vector2(0, 96),
                        1 => new Vector2(-96, 0),
                        2 => new Vector2(0, -96),
                        3 => new Vector2(96, 0),
                        _ => Vector2.Zero
                    };
                    player.Position = pos + offset;

                    log.Log($"[NetBall] Facing {facing}, playerPos={player.Position}, creaturePos={pos}", LogLevel.Debug);
                    log.Log($"[NetBall] Calling TryCatch for screen {Context.ScreenId}…", LogLevel.Debug);
                    log.Log($"[NetBall] Overriding Game1.player for screen {Context.ScreenId}", LogLevel.Debug);

                    // NITV.TryCatch expects Game1.player to set to this specific farmer
                    var playerField = typeof(Game1).GetField("_player", BindingFlags.Static | BindingFlags.NonPublic);
                    if (playerField == null)
                    {
                        log.Log("[NetBall] ERROR: Could not find Game1._player field!", LogLevel.Error);
                        // restore position/facing before exit
                        player.Position = origPos;
                        player.FacingDirection = origFacing;
                        return false;
                    }

                    var original = (Farmer)playerField.GetValue(null);
                    playerField.SetValue(null, player);
                    try
                    {
                        tryCatch?.Invoke(null, new object[] { player });
                        log.Log($"[NetBall] TryCatch invoked successfully for {player.Name}", LogLevel.Debug);
                    }
                    catch (Exception ex)
                    {
                        log.Log($"[NetBall] ERROR during TryCatch.Invoke: {ex}", LogLevel.Error);
                        // ensure state is restored
                        player.Position = origPos;
                        player.FacingDirection = origFacing;
                        playerField.SetValue(null, original);
                        return false;
                    }
                    finally
                    {
                        playerField.SetValue(null, original);
                    }

                    // restore the player state
                    player.Position = origPos;
                    player.FacingDirection = origFacing;
                    return true;
                }
            }

            return false;
        }
    }
}
