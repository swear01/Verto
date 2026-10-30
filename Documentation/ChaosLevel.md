# Chaos level: segmented laser walls

`PhaseDemo15` is a four-screen (160×22 cell) horizontal level that looks crowded but runs on one rule:
**a beam reaches only as far as the first block solid in your colour.**
Every beat asks a different question about that rule, so an obstacle on the floor is not by itself a reason to jump.
Leaving the room to the right wins.

## Pieces
- **Laser walls.** White walls hang from the ceiling to three cells above the floor, at irregular spacing. Each face fires
  one red, one blue and one green segment sideways into its zone, over the highest floor in that zone. Grounded
  players are never hit, so the segments only matter in the air.
- **Stoppers.** Spiked coloured blocks in the beam rows cut a beam of their own colour. A red block is solid exactly
  when red beams are live, so a red beam always ends at the first red block.
- **Floor obstacles.** A white spike must be jumped. A coloured spike or block matters only in its own colour: in
  another colour you walk through it, and jumping it is the mistake.
- **Floor height.** A step up costs a jump; walking off a ledge keeps your colour.
- **Shields.** Three white ceiling lasers are stopped by a coloured block: under one you must be its colour.
- **Rain.** Three coloured ceiling lasers fall over stretches you walk in another colour; an extra jump there is fatal.
- **Moving platform.** A red jump platform crosses the pit. It alternates between two stops with every jump of the
  run, so it is under you only on the right count.

## Beats
1. A white spike: the first jump, to blue.
2. A no-jump zone: a red spike you walk through in blue, under green rain and beams in every colour.
3. A step up to a shelf (blue → green).
4. A green block on the shelf with a red stopper above it: jump before the block, not over it (green → red), then walk
   through it.
5. Walk off the shelf, then a red shield and a green shield with two jumps between them and nothing on the floor.
6. The pit: jump onto the red platform and off it.
7. A blue spike (jump it in blue), then a red spike (walk it in green).
8. One jump under a red shield and up to a shelf, past a cyan spike you walk in red, down again, and a last white spike.

## Tools
- `Verification/ChaosLevel.py [out.json]` generates the map (verto-map v3, one 4×1 room, saved as
  `Assets/Phase/Imported/chaos.json`). It places the stoppers from a hop arc measured in the controller and exits with
  an error when a route jump is hit in its own colour, a rule jump survives in a wrong colour, a listed naive jump
  survives, or a walk between jumps is hit.
- `CustomMapTests.ChaosRouteWinsAndJumpingOnSightDies` replays the route in the controller with the takeoffs shifted by
  up to ±6 px and checks that three naive jumps die. Keep its route in step with the generator.
- In PhaseDemo15 Play, `Verification/UmbrellaLiveInput.cs` (entry `UmbrellaLiveInput.Chaos`, script
  `--timeout_ms 60000`) plays the route with the virtual keyboard and saves a frame per screen to `TestResults/Chaos/`.
- Rebuild the scene with `unity command eval 'PhaseSandboxBuilder.CreateImportedLevels();'` and rebake its
  thumbnail with `LevelThumbnails.Run()`.
