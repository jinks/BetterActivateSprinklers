using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace BetterActivateSprinklers
{
    public class ModEntry : Mod
    {
        private ModConfig Config;
        private object BetterSprinklersApi, PrismaticToolsApi;
        private bool LineSprinklersIsLoaded;

        public override void Entry(IModHelper helper)
        {
            Config = Helper.ReadConfig<ModConfig>();

            Helper.Events.GameLoop.GameLaunched += this.OnGameLaunch;

            if (Config.ActivateOnAction)
            {
                Helper.Events.Input.ButtonPressed += this.OnButtonPressed;
            }

            if (Config.ActivateOnPlacement)
            {
                Helper.Events.World.ObjectListChanged += this.OnWorld_ObjectListChanged;
            }
        }

        private void OnGameLaunch(object sender, GameLaunchedEventArgs e)
        {
            if (Helper.ModRegistry.IsLoaded("Speeder.BetterSprinklers"))
            {
                BetterSprinklersApi = Helper.ModRegistry.GetApi("Speeder.BetterSprinklers");
            }

            if (Helper.ModRegistry.IsLoaded("stokastic.PrismaticTools"))
            {
                PrismaticToolsApi = Helper.ModRegistry.GetApi("stokastic.PrismaticTools");
            }

            LineSprinklersIsLoaded = Helper.ModRegistry.IsLoaded("hootless.LineSprinklers");
        }

        private void OnWorld_ObjectListChanged(object sender, ObjectListChangedEventArgs e)
        {
            foreach (var pair in e.Added)
            {
                ActivateSprinkler(pair.Value);
            }
        }

        private void OnButtonPressed(object sender, ButtonPressedEventArgs e)
        {
            // Ignore event if world is not loaded and player is not interacting with the world
            if (!Context.IsPlayerFree)
                return;
                
            if (e.Button.IsActionButton())
            {
                var tile = e.Cursor.GrabTile;
                if (tile == null) return;

                var obj = Game1.currentLocation.getObjectAtTile((int) tile.X, (int) tile.Y);
                if (obj == null) return;

                ActivateSprinkler(obj);
            }
        }

        private void ActivateSprinkler(StardewValley.Object sprinkler)
        {
            if (sprinkler == null) return;

            if (sprinkler.Name.Contains("Sprinkler"))
            {
                if (LineSprinklersIsLoaded && sprinkler.Name.Contains("Line"))
                {
                    ActivateLineSprinkler(sprinkler);
                }
                else if (PrismaticToolsApi != null && sprinkler.Name.Contains("Prismatic"))
                {
                    ActivatePrismaticSprinkler(sprinkler);
                }
                else if (BetterSprinklersApi != null)
                {
                    ActivateBetterSprinkler(sprinkler);
                }
                else
                {
                    ActivateVanillaSprinkler(sprinkler);
                }
            }
        }

        private void ActivateVanillaSprinkler(StardewValley.Object sprinkler)
        {
            if (!sprinkler.IsSprinkler()) return;

            foreach (Vector2 current in sprinkler.GetSprinklerTiles())
            {
                sprinkler.ApplySprinkler(Game1.currentLocation, current);
            }

            ApplySprinklerAnimation(Game1.currentLocation, sprinkler);
        }

        private void ActivateBetterSprinkler(StardewValley.Object sprinkler)
        {
            IDictionary<int, Vector2[]> coverageList = Helper.Reflection.GetMethod(BetterSprinklersApi, "GetSprinklerCoverage").Invoke<IDictionary<int, Vector2[]>>();
            Vector2[] coverage = coverageList[sprinkler.ParentSheetIndex];
            Vector2 sprinklerTile = sprinkler.TileLocation;

            foreach (Vector2 v in coverage)
            {
                WaterTile(sprinklerTile + v);
            }
        }

        private void ActivateLineSprinkler(StardewValley.Object sprinkler)
        {
            Vector2 waterTile = sprinkler.TileLocation;
            int range;

            if (sprinkler.Name.Contains("Quality")) range = 8;
            else if (sprinkler.Name.Contains("Iridium")) range = 24;
            else range = 4;

            if (sprinkler.Name.Contains("(U)"))
            {
                for (int i = 0; i < range; i++)
                {
                    waterTile.Y--;
                    WaterTile(waterTile);
                }
            }
            else if (sprinkler.Name.Contains("(L)"))
            {
                for (int i = 0; i < range; i++)
                {
                    waterTile.X--;
                    WaterTile(waterTile);
                }
            }
            else if (sprinkler.Name.Contains("(R)"))
            {
                for (int i = 0; i < range; i++)
                {
                    waterTile.X++;
                    WaterTile(waterTile);
                }
            }
            else if (sprinkler.Name.Contains("(D)"))
            {
                for (int i = 0; i < range; i++)
                {
                    waterTile.Y++;
                    WaterTile(waterTile);
                }
            }
        }

        private void ActivatePrismaticSprinkler(StardewValley.Object sprinkler)
        {
            Vector2 sprinklerTile = sprinkler.TileLocation;
            IEnumerable<Vector2> coverage = Helper.Reflection.GetMethod(PrismaticToolsApi, "GetSprinklerCoverage").Invoke<IEnumerable<Vector2>>(sprinklerTile);

            foreach (Vector2 v in coverage)
            {
                WaterTile(v);
            }
        }

        private void WaterTile(Vector2 tile)
        {
            TerrainFeature terrainFeature;
            HoeDirt hoeDirt;

            if (Game1.currentLocation.terrainFeatures.TryGetValue(tile, out terrainFeature) && (hoeDirt = terrainFeature as HoeDirt) != null)
            {
                hoeDirt.state.Value = 1;
            }
        }

        public void ApplySprinklerAnimation(GameLocation location, StardewValley.Object sprinkler)
        {
            int radius = sprinkler.GetModifiedRadiusForSprinkler();
            if (radius >= 0)
            {
                int delay = 0;
                int numberOfLoops = 6;
                switch (radius)
                {
                    case 0:
                        {
                            location.temporarySprites.Add(new TemporaryAnimatedSprite(29, sprinkler.tileLocation.Value * 64f + new Vector2(0f, -48f), Color.White * 0.5f, 4, flipped: false, 60f, numberOfLoops)
                            {
                                delayBeforeAnimationStart = delay,
                                id = sprinkler.tileLocation.X * 4000f + sprinkler.tileLocation.Y
                            });
                            location.temporarySprites.Add(new TemporaryAnimatedSprite(29, sprinkler.tileLocation.Value * 64f + new Vector2(48f, 0f), Color.White * 0.5f, 4, flipped: false, 60f, numberOfLoops)
                            {
                                rotation = (float)Math.PI / 2f,
                                delayBeforeAnimationStart = delay,
                                id = sprinkler.tileLocation.X * 4000f + sprinkler.tileLocation.Y
                            });
                            location.temporarySprites.Add(new TemporaryAnimatedSprite(29, sprinkler.tileLocation.Value * 64f + new Vector2(0f, 48f), Color.White * 0.5f, 4, flipped: false, 60f, numberOfLoops)
                            {
                                rotation = (float)Math.PI,
                                delayBeforeAnimationStart = delay,
                                id = sprinkler.tileLocation.X * 4000f + sprinkler.tileLocation.Y
                            });
                            location.temporarySprites.Add(new TemporaryAnimatedSprite(29, sprinkler.tileLocation.Value * 64f + new Vector2(-48f, 0f), Color.White * 0.5f, 4, flipped: false, 60f, numberOfLoops)
                            {
                                rotation = 4.712389f,
                                delayBeforeAnimationStart = delay,
                                id = sprinkler.tileLocation.X * 4000f + sprinkler.tileLocation.Y
                            });
                            break;
                        }
                    case 1:
                        location.temporarySprites.Add(new TemporaryAnimatedSprite("TileSheets\\animations", new Microsoft.Xna.Framework.Rectangle(0, 1984, 192, 192), 60f, 3, numberOfLoops, sprinkler.tileLocation.Value * 64f + new Vector2(-64f, -64f), flicker: false, flipped: false)
                        {
                            color = Color.White * 0.4f,
                            delayBeforeAnimationStart = delay,
                            id = sprinkler.tileLocation.X * 4000f + sprinkler.tileLocation.Y
                        });
                        break;
                    default:
                        {
                            float scale = (float)radius / 2f;
                            location.temporarySprites.Add(new TemporaryAnimatedSprite("TileSheets\\animations", new Microsoft.Xna.Framework.Rectangle(0, 2176, 320, 320), 60f, 4, numberOfLoops, sprinkler.tileLocation.Value * 64f + new Vector2(32f, 32f) + new Vector2(-160f, -160f) * scale, flicker: false, flipped: false)
                            {
                                color = Color.White * 0.4f,
                                delayBeforeAnimationStart = delay,
                                id = sprinkler.tileLocation.X * 4000f + sprinkler.tileLocation.Y,
                                scale = scale
                            });
                            break;
                        }
                }
            }
        }
    }
}
