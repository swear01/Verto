# Level course

Start from `Assets/Scenes/PhaseTitle.unity`, or open `PhaseDemo1.unity` and press Play. Crossing the right edge
advances to the next level with the pixel slide; the left edge returns to the previous one. Each level starts red
at its own checkpoint. Leaving the last level to the right shows the victory screen. There are no game labels or
tutorial messages.

| Select | Scene | Source |
| --- | --- | --- |
| 1 | `PhaseDemo1` — 換色階梯 | `Assets/Phase/DemoRooms.json`, rebuilt with `PhaseSandboxBuilder.CreateDemoRooms()` |
| 2 | `PhaseDemo1.1` | classmate color room, hand-authored scene |
| 3 | `PhaseDemo13` | `Assets/Phase/Imported/level-a.json` |
| 4 | `PhaseDemo14` | `Assets/Phase/Imported/level-b.json` |
| 5 | `PhaseDemo15` | [chaos level](ChaosLevel.md), `Assets/Phase/Imported/chaos.json` |
| 6 | `PhaseUmbrella` | [Umbrella](Umbrella.md), `Assets/Phase/Imported/umbrella.json` |
| 7 | `PhaseGear` | `Assets/Phase/Imported/level-gear.json` |

`PhaseSandbox.Levels` defines this order, the level select and both horizontal links.
`PhaseSandboxBuilder.CreateImportedLevels()` rebuilds levels 3–7 through the connected Editor.

Level 3 (level-a) keeps the classmates' premise: three floors striped red→blue→green, with a white laser
along each upper floor that colored terrain stops only in its own color, so its reach depends on the
player's color, and red, green and blue lasers falling from the ceiling. Climbing was its way up, so the
upper floors now sit one and two cells lower, the middle floor gains a trailing blue segment like the
bottom one, and three red steps lead up the right side: each floor ends green, a jump turns red onto the
step, the next jump turns blue onto the floor above, right of where the white laser reaches. The exit
gate moved down one cell so the last jump clears it from the right of the blue ceiling laser.
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

The jam rooms Demo2–8, Crystal1–3 and the classmate dash-platform room Demo12 were removed: they were built
around dash, climbing, gears and dash crystals, which Verto no longer has.
