# Level course

Start from `Assets/Scenes/PhaseTitle.unity`, or open `PhaseDemo1.unity` and press Play. Crossing the right edge
advances to the next level with the pixel slide; the left edge returns to the previous one. Each level starts red
at its own checkpoint. Leaving the last level to the right shows the victory screen. There are no game labels or
tutorial messages.

| Select | Scene | Source |
| --- | --- | --- |
| 1 | `PhaseDemo1` — 換色階梯 | `Assets/Phase/DemoRooms.json`, rebuilt with `PhaseSandboxBuilder.CreateDemoRooms()` |
| 2 | `PhaseDemo1.1` | `Assets/Phase/Imported/level-stairs.json` |
| 3 | `PhaseDemo13` | `Assets/Phase/Imported/level-a.json` |
| 4 | `PhaseDemo14` | `Assets/Phase/Imported/level-b.json` |
| 5 | `PhaseDemo15` | [chaos level](ChaosLevel.md), `Assets/Phase/Imported/chaos.json` |
| 6 | `PhaseUmbrella` | [Umbrella](Umbrella.md), `Assets/Phase/Imported/umbrella.json` |
| 7 | `PhaseLadder` | `Assets/Phase/Imported/level-ladder.json` |
| 8 | `PhaseTurnaround` | [Turnaround](Turnaround.md), `Assets/Phase/Imported/turnaround.json` |
| 9 | `PhaseGear` | `Assets/Phase/Imported/level-gear.json` |

`PhaseSandbox.Levels` defines this order, the level select and both horizontal links.
`PhaseSandboxBuilder.CreateImportedLevels()` rebuilds levels 2–9 through the connected Editor.

Level 2 (stairs) keeps the classmates' color-room premise: a jump turns red→blue→green, so going back one color
takes two jumps. It used to stack five bars three cells apart and climb them by jumping up through the bar overhead;
that move now has its own level (7). The bars are now one staircase of three-cell steps rising from the bottom left
to a white pillar at the top right, each step one jump in color order, short hops along and big hops up. Where the
next step is the color before the current one (green to blue, red to green, blue to red), a two-cell bump of the
color in between sits on the seam, so the extra jump lands on it. Nothing hangs over a landing.
`Verification/Routes/PhaseDemo1.1.json` is its recorded route.

Level 3 (level-a) keeps the classmates' premise: a white laser's reach depends on the player's color, because
colored terrain stops it only in its own color. It keeps the original look, rows of floating red, blue and green
bars, without the red steps later added for run and jump. The rows are now four cells apart, so no row can be
reached from the one below, and the route runs right, left, then right, climbing at each end by a two-cell step.
Each upper row has a white laser along it, fired from the left, the right, then the left. A big jump crosses the
laser of the row above; only a bar of the player's color between the gun and the player stops it, so on each
row one color's jumps must stay low until its bar is passed, and the side doing the shielding flips with the
direction of travel. The top row holds only the shields and the white exit ledge.
`Verification/Routes/PhaseDemo13.json` is its recorded route.

Level 4 (level-b) keeps the classmates' premise: you climb between a red pillar and a green pillar past
red, blue and green lasers fired alternately from both sides, and a colored laser kills only in its own
color, so every jump swaps which beams are lethal. The pillars shield their own color: red beams only come
from the right and green beams only from the left. The climb used to scale the pillars without changing
color, so it was re-laid as a zigzag of three-cell steps colored in jump order: blue, green, red, blue up
to the green pillar, a wall jump off the green pillar (green while touching it, red after) back left onto
red, blue, green, and a wall jump off the red pillar onto a long blue walkway. The walkway runs under a
green beam, so the last stretch is walked, not jumped, through the green pillar to the white exit ledge.
A white wall right of the green pillar closes the drop below the ledge.
`Verification/Routes/PhaseDemo14.json` is its recorded route.

Level 7 (ladder) is built around the move level 2 dropped: jumping up through the bar overhead. Six bars three cells
apart span wall to wall in a running bond of six-cell red, blue and green bricks, so the only way up is through a
brick. A jump changes the color, so a brick of another color overhead lets the player through while the brick
beside it, in the new color, becomes the landing; a brick of the new color overhead blocks the jump. White ledges
at the top right lead out. `Verification/Routes/PhaseLadder.json` is its recorded route.

The jam rooms Demo2–8, Crystal1–3 and the classmate dash-platform room Demo12 were removed: they were built
around dash, climbing, gears and dash crystals, which Verto no longer has.
