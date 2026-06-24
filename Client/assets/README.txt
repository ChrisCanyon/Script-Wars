Drop game art PNGs here. They are copied next to the built exe automatically
(see the <None Include="assets/**/*" .../> item in Client.csproj).

Expected files (each source sprite is 256x256 px; transparent background for entities):
  player.png                  - fallback sprite for entities with kind "player"
  player/<direction>/idle-0.png
  player/<direction>/attack-0.png ... attack-3.png
  bot.png                     - drawn for entities with kind "bot"
  projectiles/arrow.png       - right-facing arrow; rotate at draw time for direction
  projectiles/firebolt.png    - right-facing firebolt; rotate at draw time for direction
  effects/hitsplats/*.png     - one-shot hit impact effects
  effects/ground/*.png        - 256x256 overlays drawn on top of floor tiles
  items/*.png                 - 64x64 pickup/loot sprites
  tiles/floor/*.png           - alternate floor terrain tiles
  floor.png                   - tiled across every grid cell

Player direction folders:
  player/down/
  player/up/
  player/left/
  player/right/

Player sprites use sentinel colors for runtime palette swaps:
  #000001 = cloth shadow
  #000002 = cloth midtone
  #000003 = cloth highlight

Attack variants:
  attack-* = idle pose with swipe arc overlay

Projectile notes:
  Keep arrows/projectiles as one right-facing source image when possible.
  Rotate the texture in code for up/down/left/right or diagonal shots.

Hit splats:
  effects/hitsplats/physical.png
  effects/hitsplats/magic.png
  effects/hitsplats/poison.png
  effects/hitsplats/fire.png
  effects/hitsplats/heal.png
  effects/hitsplats/critical.png

Ground overlays:
  effects/ground/poison-cloud.png
  effects/ground/fire-patch.png
  effects/ground/boulder.png
  effects/ground/rubble.png

Pickup items:
  items/coin.png
  items/coins.png
  items/potion-health.png
  items/potion-mana.png
  items/shield.png
  items/chest-closed.png
  items/chest-open.png
  items/key.png

Floor tile variants:
  tiles/floor/grass.png
  tiles/floor/dirt-path.png
  tiles/floor/stone.png
  tiles/floor/sand.png
  tiles/floor/mud.png

If a file is missing, the client falls back to colored rectangles
(player = blue, bot = red, floor = flat dark) so the game stays fully playable.
