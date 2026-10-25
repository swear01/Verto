# Umbrella (level 17)

`PhaseUmbrella` is one scrolling room four screens wide (160×22 cells), built from `Assets/Phase/Imported/umbrella.json`.
It is the last level: leaving its right edge wins.

## Rule

White light kills, and only blocks of your current color stop it.

Every ceiling column fires a white laser straight down onto a white floor. Colored bars hang at mid height, above the
top of a jump, so they are only shade, never ledges. Under a bar of your color you are dry; every jump moves you to
the next color in red → blue → green, so it changes the whole pattern of light at once. The player causes every
change, and one jump shows how it works.

## Reading the room

- The bar overhead is the color you must be under it.
- A bar's length is how long you hold that color. A bar one hop long means jump again on landing. A longer bar means
  walk under it before the next jump.
- Where two bars overlap the cells take the mixed color (purple = red or blue, cyan = blue or green, yellow = green
  or red). That overlap is where to jump.
- You never wait in place: you keep moving right the whole way.
- Out-of-phase bars are drawn in front of the beams as dashed outlines, so the next colors stay visible through the rain.

## Screens

The room starts and ends under a short white awning, which is safe in every color.

1. **One bar, one hop.** Red, blue, green, red, blue: each bar is one hop long, so you chain jumps on landing.
2. **Long and short.** The bars change length. On a long bar you run on the ground and jump later.
3. **Pits.** Two four-cell pits force a jump at their edge. Before pit 1 a long yellow stretch lets you be green or
   red, but only green is safe: the edge jump must turn you red, the color over the pit. Before pit 2 a purple stretch
   lets you be red or blue, and only blue works, because the edge jump must turn you green. So you skip a jump before
   the first pit and add one before the second.
4. **Melee.** Bars at three heights (rows 8, 11 and 14). Each color of the route sits at a different height, a blue bar
   hangs where you cannot be blue, and a short green bar near the end is too short to hop under. The run ends with a
   third pit and the white awning.

## Route and margins

The intended route holds right and jumps at x = 80, 142, 204, 266, 400, 462, 590, 652, 780, 852, 916, 1000, 1062 and
1180 px (the player's left edge), with full-height jumps. It takes about 13 seconds. In the headless controller,
30 of 30 runs win with every jump moved by a random ±6 px. A tapped jump hops only 26 px instead of 62 and cannot
clear a pit.

## Verification

In PhaseUmbrella Play, run `Verification/UmbrellaLiveInput.cs` (entry `UmbrellaLiveInput.Run`, script
`--timeout_ms 60000`). It plays the route with the virtual keyboard, holding jump while rising, and requires no
deaths and the victory at the right edge. It saves one frame per screen to `TestResults/Umbrella/`.

## Editing

Edit `umbrella.json` (map format v3; the in-game editor opens it as a custom map), then rebuild the scene with
`unity command eval 'PhaseSandboxBuilder.CreateImportedLevels();'` and rebake the level-select thumbnail with
`LevelThumbnails.Run()` (every build does too). The builder sizes the scene to the room, so imported rooms may be up to four
screens wide. Scene levels have no goal object; the right edge of the last level wins.
