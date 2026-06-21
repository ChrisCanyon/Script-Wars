Drop game art PNGs here. They are copied next to the built exe automatically
(see the <None Include="assets/**/*" .../> item in Client.csproj).

Expected files (each ~48x48 px; transparent background for player/bot):
  player.png  - drawn for entities with kind "player"
  bot.png     - drawn for entities with kind "bot"
  floor.png   - tiled across every grid cell

If a file is missing, the client falls back to colored rectangles
(player = blue, bot = red, floor = flat dark) so the game stays fully playable.
