# Turnaround (level 7)

`PhaseTurnaround` is one room two screens tall (40×44 cells), built from `Assets/Phase/Imported/turnaround.json`, which
`python3 Verification/TurnaroundLevel.py` generates. It follows `PhaseUmbrella`; leaving its top-right shelf leads to `PhaseGear`.

## Rule

A colored beam kills only in its color, and every jump moves you one color on (red → blue → green).

Every row of the room carries a beam from each side wall, edge to edge, so the shaft looks completely blocked. Each row's
beams take a color you are never in on that row: while you stand on a ledge, the beams just above the next ledge are
your color and live; the jump that takes you there switches them off and makes the ledge solid. Wherever you land, one
jump back the other way is the path.

## Reading the room

- The ledges zigzag up a central shaft, three rows apart, colored in jump order: blue, green, red, …
- The first jump runs right off the end of the floor. From then on every landing is followed by a jump straight back the
  way you came (the turnaround).
- The ledge directly overhead is never your color during a jump, so the climb never hits its head.
- Beams come in bands: two rows of one color, then a ledge row whose two halves differ. The band colors rotate every
  three rows, so the sheet is striped diagonally.
- Ledges narrow on the way up: five cells for the first four, then four, then three. The last jump lands on a
  long white shelf at the top that runs out of the right edge.
- Jumping in place, walking off a ledge or jumping outward drops you through a band of your own color.

## Route and margins

The recorded route `Verification/Routes/PhaseTurnaround.json` holds right, jumps at x = 116 px onto the first ledge,
then reverses direction with every jump two frames after it lands, with full-height jumps: 13 jumps in about 9 seconds.
`dotnet run -c Release --project Verification/Tolerance -- PhaseTurnaround` grades it in band with no tight step;
see `Verification/ToleranceReport.md`.

## Editing

Change `Verification/TurnaroundLevel.py`, regenerate the map, re-record the route, rebuild the scene with
`unity command eval 'PhaseSandboxBuilder.CreateImportedLevels();'` and rebake the thumbnail with `LevelThumbnails.Run()`.
`LevelRouteTests` replays the route on the saved scene.
