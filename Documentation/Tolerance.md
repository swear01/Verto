# Input tolerance

`PhaseTolerance` (`Assets/Phase/Tests/Editor/PhaseTolerance.cs`) grades how precise a level's inputs must be, not just
whether it can be beaten. It drives the real `PhaseController` with keyboard input, so color cycling, jump platforms,
lasers, spikes, the jump buffer, coyote time, corner nudges and ledge steps all count exactly as in Play.

## Target band

Each input on the winning route gets a **window**: how many frames (60 Hz) it may move and still win.

| Grade | Window | Meaning |
|---|---|---|
| tight | ≤ 3 frames (≤ 50 ms) | near frame-perfect |
| moderate | 4–12 frames | **the target band**: some precision |
| free | ≥ 13 frames (> 200 ms) | no precision needed |

A level is **in band** when it has moderate steps and no tight ones, **too tight** with any tight step, and **too loose**
when every step is free. Most of our levels should be in band. Free steps between moderate ones are fine and expected:
releases after a full jump and runs on long floors are usually free.
For scale: the controller's coyote time is 6 frames and its jump buffer 5, close to Celeste's; a running hop over a
48 px pit has a 16-frame takeoff window, and an 84 px pit (near the longest running jump) leaves 3.

## How it works

1. **Search.** Weighted A* over held inputs (left/still/right × jump held or not) that change every 4 or 6 frames,
   merging states on position, rounded velocity, color, contact, platform positions, crystals and deferred blocks.
   Its cost counts frames, jumps and direction changes, and its heuristic is the distance to the nearest landing on the
   right edge. Hazards change only on jumps, not on time, so this state is complete enough to merge on.
2. **Simplify.** Drop jumps the route does not need, hold each jump through the variable-height window, and drop every
   detour (a stop or step back with its return, or a release with the next press) whose removal still wins. Later
   inputs keep their positions when a detour is dropped.
3. **Measure.** Each input (jump press, release, direction change) moves 1, 2, … frames earlier and later, one at a
   time. The inputs after it adapt like a player: a press or turn fires when the player reaches the spot where it
   happened on the route (along the main direction of travel there, within 16 px across it, and with the same ground or
   wall contact for a press); a release keeps its hold time. A move counts when the player survives the next 2 jumps
   and half a second after them, or wins. A window stops growing at 17 frames or 20 to one side, shown as ≥/≤; a move
   also cannot pass the previous input on the same key, shown as ≤.
4. **Relax.** Before measuring the next input, the measured one moves to the middle of its window, as a careful
   player would aim for the middle of a gap.
5. **Choose.** Three search settings give up to three routes; the report keeps the one with the fewest tight steps,
   then the widest tightest window. A level is only as hard as its easiest route.

## Running it

In the connected Editor (Edit Mode, scenes saved):

```sh
unity command run_script --project-path "$PWD" --timeout 7200 -- --file Verification/ToleranceReport.cs --entry ToleranceReport.Run --timeout_ms 7200000
unity command run_script --project-path "$PWD" --timeout 900 -- --file Verification/ToleranceReport.cs --entry ToleranceReport.Smoke --timeout_ms 900000
```

`PhaseTolerance.WriteReport(path, levels…)` takes other paths and level lists. `eval` cannot run it: Pipeline stops
main-thread work after 5 s, so the report builds the controllers on the main thread and analyzes them on a worker thread.
Do not recompile while a report runs: the domain reload waits for the worker thread, so the Editor stalls until it ends.
`Run` opens each level scene, builds its controller as Play does, reopens the scene that was open and writes
`Verification/ToleranceReport.md`; `Smoke` writes PhaseDemo1 and the chaos level to `Temp/tolerance-smoke.md`: a summary
table, then per level the route length, a window histogram and one row per input with its frame, x, contact, the
color a jump gives, the window in frames and in pixels and its grade. Without level names it covers
`PhaseSandbox.Levels` minus the classmate levels. A level takes seconds to about ten minutes; the Umbrella level is the
slowest because each of its frames checks 160 lasers. `ToleranceTests` checks that a 48 px pit grades free and an
84 px one tight.

## Limits

- The windows belong to the routes the search found. A designer's intended route can be easier or harder; a tight
  step on a route full of wall jumps or mid-air stops may be a search trick rather than the intended line.
- A move is only tested for the 2 jumps after it; a mistake that kills much later counts as recovered.
- Inputs are moved one at a time; a run of moderate steps in a row is harder than its parts.
- Only single scene levels are covered, not multi-room custom worlds.
