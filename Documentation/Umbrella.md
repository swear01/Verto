# Umbrella (level 17)

`PhaseUmbrella` is one scrolling room four screens wide (160×22 cells), built from `Assets/Phase/Imported/umbrella.json`.
Leaving its right edge leads to `PhaseGear`.

## Rule

White light kills, and only blocks of your current color stop it.

Every ceiling column fires a white laser straight down onto the white terrain. Colored bars hang above the top of a
jump from the ground under them, so they are only shade, never ledges. Under a bar of your color you are dry; every
jump moves you to the next color in red → blue → green, so it changes the whole pattern of light at once.

## Reading the room

- The bar overhead is the color you must be under it.
- A bar's length is how long you hold that color. Where two bars overlap the cells take the mixed color (purple = red
  or blue, cyan = blue or green, yellow = green or red). That overlap is where to jump.
- The floor climbs, drops and breaks into pits. A step up or a pit forces a jump, so it costs a color; walking off a
  ledge does not, so a descent keeps one color all the way down. Bars hang at a fixed clearance over the ground below
  them and step up and down with it.
- You never wait in place: you keep moving right the whole way.
- Out-of-phase bars are drawn in front of the beams as dashed outlines, so the next colors stay visible through the rain.

## Screens

The room starts and ends under a short white awning, which is safe in every color.

1. **Hill.** Two steps up, each a jump, then two walk-offs down that keep green, and a pit.
2. **Pits.** Four pits between islands at different heights: up two cells, down four, up two again. Every jump clears a
   pit, so the colors come in a steady red → blue → green beat.
3. **Stairs.** Three steps up to a high plateau, a pit across the top, then a three-step stair down. The climb is three
   jumps on landing; the descent is one long blue bar walked under.
4. **Islands.** Three pits between narrow pillars that rise and fall, chained jump on landing, out to the white awning.

## Route and margins

The intended route holds right and jumps at x = 40, 100, 256, 352, 432, 496, 592, 656, 700, 768, 928, 1000, 1040,
1104 and 1168 px (the player's left edge), with full-height jumps. It takes about 14 seconds. In the headless
controller, 10 of 10 runs win with every jump moved by a random ±6 px, and the terrain alone allows ±8 px.
A tapped jump hops only 26 px instead of 62 and cannot clear a pit.

## Verification

In PhaseUmbrella Play, run `Verification/UmbrellaLiveInput.cs` (entry `UmbrellaLiveInput.Run`, script
`--timeout_ms 60000`). It plays the route with the virtual keyboard, holding jump while rising, and requires no
deaths and an exit at the right edge. It saves one frame per screen to `TestResults/Umbrella/`.

## Editing

Edit `umbrella.json` (map format v3; the in-game editor opens it as a custom map), then rebuild the scene with
`unity command eval 'PhaseSandboxBuilder.CreateImportedLevels();'` and rebake the level-select thumbnail with
`LevelThumbnails.Run()` (every build does too). The builder sizes the scene to the room, so imported rooms may be up to four
screens wide. Scene levels have no goal object; the right edge of the last level wins.
