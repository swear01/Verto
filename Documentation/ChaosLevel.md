# Chaos level: segmented laser walls

A four-screen (160×22 cell) horizontal level that looks crowded but runs on one rule:
**a beam reaches only as far as the first block that is solid in its colour.**
The player only has to jump at the right places. The colour after each jump decides which beams are live.

## Layout
- **Laser walls.** White walls hang from the ceiling down to row 18. The player always runs under them.
  Each wall face has three emitter segments at rows 16, 17 and 18, one red, one blue and one green, and
  they fire sideways into the neighbouring zone. Grounded players sit in rows 19–20 and are never hit,
  so every segment matters only while the player is in the air.
- **Stoppers.** Coloured blocks at rows 16–18, some of them spiked, cut a beam of their own colour.
  A red block is solid exactly when red beams are live, so a red beam always ends at the first red block.
- **Floor spikes.** White one-cell spike blocks force most jumps. Two jumps have no spike. The player must
  take those anyway, or they reach the next spike in the wrong colour.

## Rule per jump
Every jump's arc must be clear of beams of the colour the jump gives, and is crossed by beams of both
other colours. A jump in the wrong colour, from an extra or a missing hop, is always fatal. The colours
keep the red → blue → green rhythm, and the player never waits in place.

## Tools
- `Verification/ChaosLevel.py [out.json]` generates the map (verto-map v3, one 4×1 room) and prints any
  jump that breaks the rule above.
- The stopper positions use the arc measured from the controller: a hop spans 62 px, and its apex is 27 px
  above the ground. The route survives takeoff offsets of ±8 px in a headless controller simulation.

## Ideas kept for other levels
- **Umbrella:** white laser rain from the ceiling, with coloured shields that let only your colour pass
  safely. Being built in its own level.
- **Pits:** a pit forces a jump at a fixed spot, so the player must count earlier hops to arrive in the
  right colour.
- **Moving shields:** jump platforms (one step per jump) as stoppers that travel with the player.
