# Eight-room Unity demo

Open `Assets/Scenes/PhaseDemo1.unity` and press Play. Crossing the right edge
advances to the next room with the existing pixel slide. Each room starts red,
with full stamina and dash, at its own checkpoint. Death resets that room's
mechanisms. The eighth room ends on a resting platform. There are no game labels,
tutorial messages, menus or added controls.

| Room | Question | Intended solution |
| --- | --- | --- |
| 1 — 換色階梯 | Which landing exists after a jump? | Jump onto blue, green, then red. |
| 2 — 低頂渡船 | When should the ferry move? | Board first, then dash; the ferry carries the player and the color stays blue. |
| 3 — 齒輪抬橋 | How can a parked platform bridge the corridor? | Climb to move the wall and platform, release to park them, then cross the low ceiling gap. |
| 4 — 五連跳 | Can the complete color sequence be read ahead? | Five rising landings require B/G/R/B/G; the reference route uses five jumps and no dashes. |
| 5 — 保色穿刺 | When is another jump the wrong action? | Keep blue through the low corridor using dash; another jump activates the green spikes and removes the blue landings. |
| 6 — 三站轉乘 | Is another dash useful or does it overshoot? | Stop the ferry at its middle station, jump onto cyan, then onto yellow. Extra dashes can cycle the ferry back. |
| 7 — 相位卡齒 | Why did the linked mechanism stop? | Change to blue before climbing so the red obstruction is absent; the crossing jump turns green, making the blue spikes safe and the exit solid. |
| 8 — 色序預算 | Which moves should preserve color? | Prepare blue, position the gear and ferry, jump green to board, dash through blue spikes, then dash upward onto the yellow exit. A magenta lower ledge offers recovery while blue. |

The first three rooms isolate the core causes; later rooms combine color,
mechanism position and landing decisions. Single-color and mixed-color surfaces
use the existing phase masks. Spikes have the same mask meanings. A mask never
adds a new player action.

`Assets/Phase/DemoRooms.json` is the authored geometry and reference-input source.
`PhaseSandboxBuilder.CreateDemoRooms()` creates the eight saved scenes through
the connected Editor. It puts them first in Build Settings and preserves the
original sandbox and standalone gear scene. Changing the JSON requires rebuilding
the scenes. The original finalized HTML demo is a separate, unchanged artifact.

## Gear geometry

The horizontal rack occupies the last three pixels **inside** the platform's
collision rectangle, with exactly the platform's width. The 48-pixel platform
travels 24 pixels. Its fixed output pinion stays within the overlap of both end
positions, with a full tooth of margin. Three adjacent gears turn in alternating
directions at matching pitch. The dark fixed guide and end marks sit behind the
platform; they do not form another ledge. The vertical rack has a fixed length
and may enter the lower guide housing. Mechanism decoration has no collision.

`ClimbGear` rejects unmatched travel or a rack that would disengage. The neutral
wall and platform remain the only moving colliders.

## Verification

`DemoRoomTests` replays every authored route against the production controller,
checking the final landing, color, action counts and mechanism position with no
deaths. The exit ledges also support wider viewports. Pixel tests check the whole
gear travel, attached rack width and stationary gears. These witnesses prove
solvability; they do not prove there is only one solution.

For live verification, open room 1 in Play and run `Verification/DemoLiveInput.cs`
through Pipeline, entry `DemoLiveInput.Run`. It queues the room's input on a
temporary keyboard, stops at the exit landing before crossing the boundary,
checks advancing frame count/controller time, and saves
captures to `TestResults/DemoRooms/`. Entry `DemoLiveInput.Advance` walks across
the exit and checks the next checkpoint. Repeat for all eight rooms. Both methods
restore background/input settings and remove their keyboards. Inspect the Game
View and Console as described in [Development](Development.md).

Difficulty, visual readability and creative alternative routes still need human
playtesting. The Unity scenes do not by themselves update the hosted Web preview.
