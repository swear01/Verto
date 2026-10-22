# Jump platforms, spike cover and map format v2

Jump platforms replace the removed dash platforms: every jump advances every platform one stop. Any block can be fully covered in spikes, and map files carry a version.

## Jump platforms

### Movement

- Every jump (ground, wall and crystal air jumps) moves all platforms one pivot forward.
- A step slides for about 0.3 s. A jump during a slide queues its step, which starts when the slide ends.
- The pivot count is free, so a platform's cycle is independent of the 3-color cycle. A 4-pivot platform needs 12 jumps to line up with the colors again.
- Clockwise follows the placement order; counter-clockwise walks it backwards. The direction can be flipped.
- Death and entering a room return every platform to its first pivot.

### Shape and color

- Horizontal platforms are 1 cell high, vertical ones 1 cell wide. The length is odd and the middle cell sits on the pivot; platforms never turn.
- Colors work like terrain (7 masks). A platform is solid, carries the player and blocks lasers only in its own color; it keeps moving with every jump while dark.

### Attached spikes

- Any side (up, down, left, right) can carry spikes. They occupy the cells beside that side, never the body, with one spike cell per cell of the side.
- They behave like ordinary spikes (only the 3 px root is lethal), move with the platform and share its color.

### Interaction with the player

- Standing on a platform carries the player.
- A platform that pushes the player into a wall never kills: the player stays put and the two pass through each other until the player is clear of the platform. This is the same handling as being inside a block after a color change.
- Attached spikes kill as usual.

## Spike cover

- There is no separate spike-block object: any block, every terrain cell and every platform, can be fully covered in spikes.
- A covered block is lethal on every face, on contact.
- It is still a block: solid, lethal and laser-blocking only in its own color, harmless and passable while dark. A covered block that turns solid around the player waits until the player leaves it, like any other block.
- A covered platform is a moving spike block and cannot be stood on.
- Ordinary and attached spikes sit in cells beside a block and only their root is lethal; a covered block is lethal over its whole body.

## Editor

- The panel gains the platform and spike-cover types.
- Placing a platform: click the first pivot (the centre cell); click a cell on the same row (horizontal) or column (vertical) as the body end, which makes the length centre-to-end distance x 2 + 1 (the centre itself is length 1); click further pivots in order; click the first pivot again to close the loop, which needs at least 2 pivots. Escape or right-click cancels an open loop.
- Attached spikes: with a spike tool (R/E turns it), click the platform to toggle a spike row on the side the tool points to. Erasing a spike row removes it; erasing the body removes the platform.
- Spike cover: the spike-cover tool toggles terrain or a platform; on an empty cell it paints covered terrain.
- F with the cursor on a platform flips its direction. Paths are drawn with arrows showing the direction.
- A platform's start (including attached spikes) and the spawn may not overlap other objects. The path may cross other objects but every stop stays inside the room.

## Map format v2

Map format v3 replaces `size` and `spawn` with rooms; see [Rooms](Rooms.md). The rest of this section still applies room by room.

```json
{
  "format": "verto-map",
  "version": 2,
  "generator": "Verto 0.2",
  "name": "Custom map",
  "size": [40, 22],
  "spawn": [3, 19],
  "terrain": ["........", "....77GG", "...."],
  "objects": [
    { "type": "spike", "cell": [10, 18], "dir": "up", "mask": 1 },
    { "type": "laser", "cell": [20, 0], "dir": "down", "mask": 2 },
    { "type": "crystal", "cell": [15, 9], "mask": 4 },
    { "type": "platform", "orientation": "horizontal", "length": 3,
      "pivots": [[8, 12], [16, 12], [16, 6]], "reverse": false, "mask": 7,
      "spiked": false, "spikes": ["up"] }
  ]
}
```

- All units are cells.
- `terrain` has one string per row: `.` is empty, `1`-`7` plain terrain in that color, `A`-`G` spike-covered terrain (A = 1 ... G = 7). Rows may be shorter than `size`; the rest is empty.
- A platform's `spiked` covers its body; `spikes` lists the sides with attached spike rows.
- Everything else is an object with a `type`; new objects add new types without changing the top level. Directions are `up`, `down`, `left`, `right`.
- `size` is 40x22 to 160x90 cells; `spawn` is a cell.

### Versions and compatibility

- `version` is an integer. It increases whenever a change alters game behavior (new objects or fields).
- Older files are upgraded step by step (v1, then v2, ...) on load. Files without a version are v1: rectangles of terrain, pixel positions and numeric directions.
- A file newer than the game is refused with a message to update the game; nothing is silently dropped.
- Saving always writes the latest version; `generator` records which Verto wrote it.
- Editor drafts (PlayerPrefs `phase.editor.v1`) hold the same JSON and go through the same upgrade. Drafts from before the format change still load.

## Unity

- `PhaseJumpPlatform` (horizontal or vertical, length, pivot offsets, direction, color, spike cover, attached sides) with the `JumpPlatform` prefab; `PhaseTerrain` gains a spike-cover option. Both can be placed in level scenes.
