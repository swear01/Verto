# Head bonk

Hitting a ceiling should read as "the jump was capped, I keep going", not "I was thrown down".

## What other games do

Only rows with a source were verified; the rest are unverified, not "none".

| Game | Vertical on bonk | Horizontal on bonk | Grace | Source |
|---|---|---|---|---|
| Celeste (`Player.cs`, `OnCollideV`) | `Speed.Y = 0` if no correction applies | untouched | Up to 4px sideways corner correction, tried toward the movement direction, then the jump continues; the variable-jump timer survives a bonk in the first `.05s` of a jump (`CeilingVarJumpGrace`) | [Player.cs](https://raw.githubusercontent.com/NoelFB/Celeste/master/Source/Player/Player.cs) |
| Celeste (design notes) | not stated | not stated | corner correction, coyote time, jump buffer, half gravity at the apex, no numbers | [Celeste & Forgiveness](https://maddymakesgames.com/articles/celeste_and_forgiveness/) |
| Super Mario Bros. (NES) | 0 on a block that survives, -2 on one that breaks, 1 on a hard block | not stated (subspeed unchanged in the bump entries) | none found | [SMBpedia](https://simplistic6502.github.io/smb1_tll/smbpedia_movement.html) |
| Super Mario 64 | 0 | not stated | none found | [Ukikipedia](https://ukikipedia.net/wiki/Quarter_Steps) (search snippet) |
| SMW, Mario Maker, Hollow Knight, Super Meat Boy, Dead Cells, Ori, Mega Man X, Shovel Knight | unverified | unverified | unverified | no ceiling source found |
| GMTK Platformer Toolkit, Swink's *Game Feel*, GDC talks | not researched | not researched | not researched | — |

## Pattern

- A true bonk zeroes vertical speed; none of the verified games reverses it (except Mario's -2 when a block breaks).
- Horizontal speed is left alone.
- Forgiveness comes before the bonk: corner correction turns near-misses into clean rises.

## Verto

- `vy = 0` on the bonk, never a bounce (already the case).
- Corner correction: up to 4px toward the movement direction (already the case).
- The variable-jump hold ends on any bonk. Celeste's `.05s` grace would keep pushing `vy` back up and re-bonk every frame.
- New: gravity pauses for `BonkHang` (0.1s, 6 frames) after a bonk, `vx` untouched, so the run carries along the ceiling. A jump (wall jump, air jump) clears it.
