# Rooms, room changes and the open top

A map is a world of connected rooms. A room is the camera's range, so its size and shape set how the camera behaves: one screen is a fixed view, a wide room scrolls sideways, a tall room is for climbing or falling. Leaving a room slides the view into the next one.

Part A is the runtime world and map format v3; part B is the editor's world canvas.

## Decisions

- **Screens.** One screen is 40×22 cells (320×176 px). A room is 1–4 screens wide and 1–5 tall. The height limit of 5 lets the old 90-row maps migrate without losing content.
- **Designer-chosen.** The frame never grows with its content.
- **Position.** A room sits at a screen position `at`, which may be negative. The world has no size limit and rooms may touch on any side. Rooms must not overlap.
- **Top.** The top of a room is open air; the goal is an object.
- **Spawns and goal.** Each room has at least one spawn and may have many. The world has at most one goal.

## Position and room changes

- The player belongs to the room whose frame holds their center.
- A neighbor is a room that shares an edge with the current one. The controller gets its neighbors as rectangles in room-local pixels.
- The player's center entering a neighbor rectangle switches to that room.
- Leaving through the left or right side is allowed only where the player's center would be inside a neighbor rectangle; elsewhere the side is an invisible wall, at any height, including above the top.
- The top has no clamp, and no ceiling for corner nudges, ledge steps or platform pushes. The player can jump above the frame and fall back without dying; the camera does not follow.
- The bottom kills at room height + 14 px unless the center is inside a neighbor rectangle, in which case play moves to that room.
- On a change, position (translated by the room offset), velocity, phase, time, jumps, deaths and the air jump carry over. A solid the player arrives inside waits until they leave it, like after a color change.
- Play pauses for 0.18 s while the view slides up, down, left or right. A jump pressed during the pause stays buffered.
- On entry the respawn becomes the spawn in the new room nearest to the entry point, and the phase at entry is recorded. Death returns to that spawn with that phase.
- Entering a room resets its platforms to their first pivot and its crystals.
- The world starts in the first room by position (left to right, then top to bottom) at its leftmost spawn, in red.
- Scene levels and level select are unchanged. They only inherit the open top and the camera alignment; `exitLeft` and `exitRight` scene travel still works.
- Lasers stop at the room frame.

## Camera

- The camera stays inside the current room.
- A room shorter than the view is bottom-aligned with sky above. A room narrower than the view is centered horizontally.
- Neighboring rooms are not drawn outside the current room: the space shows the backdrop.

## Goal

- The goal is active in every color. Touching it wins and opens the victory menu; its retry entry restarts the world.
- It is a one-cell flag and may sit anywhere in any room. A map without a goal never ends.

## Map format v3

```json
{
  "format": "verto-map",
  "version": 3,
  "generator": "Verto 0.3",
  "name": "Custom map",
  "rooms": [
    { "at": [0, 0], "size": [3, 1], "terrain": ["..."],
      "objects": [{ "type": "spawn", "cell": [3, 19] }] },
    { "at": [3, -1], "size": [1, 2], "terrain": ["..."],
      "objects": [{ "type": "spawn", "cell": [4, 40] }, { "type": "goal", "cell": [30, 5] }] }
  ]
}
```

- `at` and `size` are in screens. Terrain rows and every object cell are room-local cells; rows may be shorter than the room.
- `spawn` and `goal` are objects (`{"type":"spawn","cell":[x,y]}`); the spawn cell is the top of the 8×11 player. Everything else is as in map format v2 (see [Jump platforms and map format](JumpPlatformSpec.md)).
- Validation: rooms do not overlap; each room has at least one spawn; the world has at most one goal; sizes are 1–4 by 1–5; every object and every platform stop with its attached rows lies inside its own room; the number bounds are unchanged.
- Upgrade from v1 and v2 (a v1 file goes through v2): one room at `[0, 0]`, its size rounded up to whole screens, the content bottom-aligned (the floor stays at the room's bottom) with extra columns on the right. The old spawn becomes a spawn object.
- A file newer than the game is refused with a message to update the game.

## Editor

- The canvas is the whole world, scrolls in any direction and shows faint screen gridlines.
- **Draw:** drawing in a screen that belongs to no room makes it a new 1×1 room.
- **Room tool** (click the seam between two screens): different rooms on both sides merge into one room whose frame is the enclosing rectangle, refused with a red flash if it would cover another room; the same room on both sides splits along the line.
- **Move:** click a room to move it by whole screens; Shift-click selects several; a drop that overlaps another room is refused.
- A room with all content erased disappears. A room without a spawn flashes red; a second goal is refused.
- Every room draws its frame, the selected one brighter; the shared stretch between two rooms is dashed. Terrain on both sides of a seam is drawn together, so floor heights and openings show whether they line up.
- Tab shows a world overview; a click scrolls there. The four size buttons are removed.
- Playtest starts at the nearest spawn of the room under the view.

## Official levels

Official levels become editor maps loaded by the same game scene, and the one-scene-per-level setup retires once the new levels exist. Level select lists every room by position and starts at the room's leftmost spawn.

## Later

Camera objects inside a room, such as locking an area or looking further ahead, wait until a level needs them.
