# Input tolerance report

Window = frames (60 Hz) an input may move and still win; tight ≤ 3, moderate 4–12, free ≥ 13. A window stops growing at 17 frames or ±20; ≤/≥ mark such bounds. See Documentation/Tolerance.md.

| Level | Verdict | Jumps | Steps | Free | Moderate | Tight | Tightest step |
|---|---|---|---|---|---|---|---|
| PhaseDemo1 | in band | 3 | 6 | 5 | 1 | 0 | jump at x=114: 5f |
| PhaseDemo1.1 | in band | 16 | 36 | 33 | 3 | 0 | jump at x=146: 10f |
| PhaseDemo13 | in band | 19 | 61 | 55 | 6 | 0 | release at x=33: 5f |
| PhaseDemo14 | in band | 10 | 22 | 17 | 5 | 0 | release at x=106: 7f |
| PhaseDemo15 | in band | 10 | 22 | 20 | 2 | 0 | release at x=398: 10f |
| PhaseUmbrella | in band | 15 | 32 | 25 | 7 | 0 | jump at x=1051: 9f |
| PhaseLadder | in band | 7 | 24 | 23 | 1 | 0 | jump at x=162: 8f |
| PhaseTurnaround | in band | 13 | 38 | 37 | 1 | 0 | jump at x=164: 11f |
| PhaseGear | in band | 8 | 23 | 16 | 7 | 0 | jump at x=97: 4f |

## PhaseDemo1

Route 201 frames (3.4s), 3 jumps; from a 6-frame search, the most forgiving of 3 routes (237 searched states, 0s).

Histogram: 1–3f 0, 4–6f 1, 7–9f 0, 10–12f 0, 13+f 5

| # | Frame | Input | x | Contact | Color | Window (frames) | Window (px) | Grade |
|---|---|---|---|---|---|---|---|---|
| 1 | 0 | jump | 24 | ground | blue | 17 (≤-12..+4) | 3 | free |
| 2 | 51 | release | 99 | ground |  | 17 (≤-8..+8) | 24 | free |
| 3 | 61 | jump | 114 | ground | green | 5 (-2..+2) | 6 | moderate |
| 4 | 89 | release | 146 | air |  | 17 (≤-8..+8) | 24 | free |
| 5 | 96 | jump | 156 | ground | red | 13 (≤-6..+6) | 18 | free |
| 6 | 108 | release | 173 | air |  | 17 (≤-8..≥+8) | 20 | free |

## PhaseDemo1.1

Route 1034 frames (17.2s), 16 jumps; from the recorded route, the most forgiving of 1 routes (180000 searched states, 44s).

Histogram: 1–3f 0, 4–6f 0, 7–9f 0, 10–12f 3, 13+f 33

| # | Frame | Input | x | Contact | Color | Window (frames) | Window (px) | Grade |
|---|---|---|---|---|---|---|---|---|
| 1 | 58 | jump | 90 | ground | blue | 12 (-6..+5) | 16 | moderate |
| 2 | 70 | release | 111 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 3 | 93 | jump | 146 | ground | green | 10 (-5..+4) | 14 | moderate |
| 4 | 105 | release | 167 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 5 | 178 | jump | 276 | ground | red | 17 (-8..≥+8) | 24 | free |
| 6 | 196 | release | 292 | air |  | 17 (≤-8..≥+8) | 22 | free |
| 7 | 215 | left | 304 | ground |  | 17 (≤-8..≥+8) | 0 | free |
| 8 | 215 | jump | 304 | ground | blue | 17 (≤-8..≥+8) | 9 | free |
| 9 | 234 | release | 277 | air |  | 17 (-8..≥+8) | 24 | free |
| 10 | 272 | jump | 220 | ground | green | 17 (≤-8..+8) | 24 | free |
| 11 | 284 | release | 198 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 12 | 333 | jump | 125 | ground | red | 15 (-7..+7) | 21 | free |
| 13 | 345 | release | 103 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 14 | 390 | jump | 36 | ground | blue | 17 (-8..≥+8) | 24 | free |
| 15 | 408 | release | 21 | air |  | 17 (≤-8..≥+8) | 22 | free |
| 16 | 420 | right | 8 | ground |  | 17 (≤-8..≥+8) | 7 | free |
| 17 | 420 | jump | 8 | ground | green | 17 (≤-8..≥+8) | 2 | free |
| 18 | 439 | release | 35 | air |  | 17 (-8..≥+8) | 24 | free |
| 19 | 471 | jump | 83 | ground | red | 17 (≤-8..+8) | 24 | free |
| 20 | 483 | release | 105 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 21 | 530 | jump | 175 | ground | blue | 11 (-5..+5) | 15 | moderate |
| 22 | 542 | release | 197 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 23 | 595 | jump | 276 | ground | green | 17 (-8..≥+8) | 24 | free |
| 24 | 613 | release | 291 | air |  | 17 (≤-8..≥+8) | 22 | free |
| 25 | 625 | left | 304 | ground |  | 17 (≤-8..≥+8) | 7 | free |
| 26 | 625 | jump | 304 | ground | red | 17 (≤-8..≥+8) | 2 | free |
| 27 | 644 | release | 277 | air |  | 17 (-8..≥+8) | 24 | free |
| 28 | 705 | jump | 185 | ground | blue | 17 (≤-8..≥+8) | 24 | free |
| 29 | 717 | release | 164 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 30 | 743 | jump | 125 | ground | green | 17 (≤-8..+8) | 24 | free |
| 31 | 755 | release | 103 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 32 | 800 | jump | 36 | ground | red | 17 (-8..≥+8) | 24 | free |
| 33 | 818 | release | 21 | air |  | 17 (≤-8..≥+8) | 22 | free |
| 34 | 830 | right | 8 | ground |  | 17 (≤-8..≥+8) | 7 | free |
| 35 | 830 | jump | 8 | ground | blue | 17 (≤-8..≥+8) | 2 | free |
| 36 | 849 | release | 35 | air |  | 17 (-8..≥+8) | 24 | free |

## PhaseDemo13

Route 739 frames (12.3s), 19 jumps; from the recorded route, the most forgiving of 1 routes (180000 searched states, 18s).

Histogram: 1–3f 0, 4–6f 4, 7–9f 1, 10–12f 1, 13+f 55

| # | Frame | Input | x | Contact | Color | Window (frames) | Window (px) | Grade |
|---|---|---|---|---|---|---|---|---|
| 1 | 16 | jump | 27 | ground | blue | 17 (≤-8..≥+8) | 24 | free |
| 2 | 19 | release | 33 | air |  | 5 (≤-2..+2) | 8 | moderate |
| 3 | 52 | jump | 84 | ground | green | 15 (-7..+7) | 21 | free |
| 4 | 65 | release | 107 | air |  | 17 (-8..≥+8) | 25 | free |
| 5 | 80 | stop | 129 | air |  | 17 (-8..≥+8) | 24 | free |
| 6 | 89 | right | 135 | ground |  | 17 (≤-8..≥+8) | 4 | free |
| 7 | 96 | jump | 142 | ground | red | 16 (-7..+8) | 19 | free |
| 8 | 108 | release | 163 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 9 | 121 | stop | 183 | ground |  | 17 (≤-8..≥+8) | 24 | free |
| 10 | 130 | right | 186 | ground |  | 17 (≤-8..≥+8) | 2 | free |
| 11 | 140 | jump | 198 | ground | blue | 13 (-6..+6) | 18 | free |
| 12 | 151 | release | 218 | air |  | 17 (-8..≥+8) | 26 | free |
| 13 | 173 | stop | 248 | air |  | 17 (≤-8..≥+8) | 21 | free |
| 14 | 177 | right | 251 | ground |  | 17 (≤-3..≥+13) | 2 | free |
| 15 | 180 | jump | 254 | ground | green | 17 (≤-8..≥+8) | 20 | free |
| 16 | 196 | release | 272 | air |  | 17 (-8..≥+8) | 20 | free |
| 17 | 200 | stop | 278 | air |  | 17 (≤-16..+0) | 15 | free |
| 18 | 209 | right | 283 | ground |  | 17 (≤-8..≥+8) | 4 | free |
| 19 | 212 | jump | 285 | ground | red | 14 (-7..+6) | 11 | free |
| 20 | 224 | release | 292 | air |  | 17 (≤-8..≥+8) | 15 | free |
| 21 | 234 | stop | 304 | wall |  | 17 (≤-8..≥+8) | 10 | free |
| 22 | 243 | left | 304 | ground |  | 17 (≤-8..≥+8) | 0 | free |
| 23 | 250 | jump | 297 | ground | blue | 17 (-8..≥+8) | 19 | free |
| 24 | 253 | release | 291 | air |  | 5 (≤-2..+2) | 8 | moderate |
| 25 | 266 | right | 270 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 26 | 275 | left | 266 | ground |  | 17 (≤-8..≥+8) | 10 | free |
| 27 | 293 | jump | 247 | ground | green | 17 (≤-8..≥+8) | 24 | free |
| 28 | 305 | release | 226 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 29 | 320 | right | 203 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 30 | 329 | left | 198 | ground |  | 17 (≤-8..≥+8) | 6 | free |
| 31 | 337 | jump | 191 | ground | red | 17 (-8..≥+8) | 19 | free |
| 32 | 340 | release | 185 | air |  | 5 (≤-2..+2) | 8 | moderate |
| 33 | 353 | right | 164 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 34 | 362 | left | 160 | ground |  | 17 (≤-8..≥+8) | 10 | free |
| 35 | 374 | jump | 150 | ground | blue | 16 (-8..+7) | 21 | free |
| 36 | 394 | release | 117 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 37 | 409 | right | 94 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 38 | 417 | left | 91 | ground |  | 15 (≤-7..+7) | 8 | free |
| 39 | 431 | jump | 77 | ground | green | 17 (≤-8..≥+8) | 24 | free |
| 40 | 443 | release | 56 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 41 | 461 | right | 29 | air |  | 17 (-8..≥+8) | 24 | free |
| 42 | 470 | left | 25 | ground |  | 17 (≤-8..≥+8) | 8 | free |
| 43 | 470 | jump | 25 | ground | red | 17 (≤-8..≥+8) | 8 | free |
| 44 | 482 | release | 21 | air |  | 17 (≤-8..≥+8) | 14 | free |
| 45 | 499 | right | 8 | wall |  | 17 (≤-8..≥+8) | 1 | free |
| 46 | 509 | jump | 16 | ground | blue | 11 (-5..+5) | 14 | moderate |
| 47 | 521 | release | 19 | air |  | 17 (≤-8..≥+8) | 14 | free |
| 48 | 539 | jump | 45 | ground | green | 17 (≤-8..+8) | 24 | free |
| 49 | 551 | release | 66 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 50 | 578 | jump | 107 | ground | red | 15 (-7..+7) | 21 | free |
| 51 | 581 | release | 113 | air |  | 5 (≤-2..+2) | 8 | moderate |
| 52 | 601 | jump | 145 | ground | blue | 16 (-8..+7) | 22 | free |
| 53 | 613 | release | 166 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 54 | 625 | stop | 184 | air |  | 16 (-8..+7) | 23 | free |
| 55 | 634 | right | 190 | air |  | 17 (≤-8..≥+8) | 4 | free |
| 56 | 645 | jump | 202 | ground | green | 15 (-7..+7) | 21 | free |
| 57 | 665 | release | 236 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 58 | 689 | jump | 272 | ground | red | 17 (≤-8..≥+8) | 24 | free |
| 59 | 696 | release | 280 | wall |  | 7 (-3..+3) | 1 | moderate |
| 60 | 717 | jump | 296 | ground | blue | 17 (≤-8..≥+8) | 2 | free |
| 61 | 729 | release | 299 | air |  | 17 (≤-8..≥+8) | 14 | free |

## PhaseDemo14

Route 461 frames (7.7s), 10 jumps; from the recorded route, the most forgiving of 1 routes (180000 searched states, 27s).

Histogram: 1–3f 0, 4–6f 0, 7–9f 4, 10–12f 1, 13+f 17

| # | Frame | Input | x | Contact | Color | Window (frames) | Window (px) | Grade |
|---|---|---|---|---|---|---|---|---|
| 1 | 9 | jump | 32 | ground | blue | 17 (≤-8..≥+8) | 8 | free |
| 2 | 24 | release | 58 | air |  | 17 (-8..≥+8) | 24 | free |
| 3 | 47 | jump | 93 | ground | green | 14 (-7..+6) | 20 | free |
| 4 | 54 | release | 106 | air |  | 7 (-3..+3) | 10 | moderate |
| 5 | 76 | jump | 139 | ground | red | 16 (-8..+7) | 23 | free |
| 6 | 82 | release | 151 | air |  | 8 (-3..+4) | 12 | moderate |
| 7 | 106 | jump | 188 | ground | blue | 15 (-7..+7) | 21 | free |
| 8 | 113 | release | 202 | air |  | 7 (-3..+3) | 10 | moderate |
| 9 | 128 | jump | 224 | ground | green | 17 (-8..≥+8) | 24 | free |
| 10 | 137 | release | 240 | wall |  | 17 (≤-8..≥+8) | 14 | free |
| 11 | 146 | left | 240 | wall |  | 17 (-8..≥+8) | 0 | free |
| 12 | 146 | jump | 240 | wall | red | 14 (≤-8..+5) | 2 | free |
| 13 | 164 | release | 210 | air |  | 17 (-8..≥+8) | 24 | free |
| 14 | 194 | jump | 165 | ground | blue | 14 (-7..+6) | 19 | free |
| 15 | 201 | release | 151 | air |  | 7 (-3..+3) | 11 | moderate |
| 16 | 218 | jump | 125 | ground | green | 17 (≤-8..+8) | 24 | free |
| 17 | 232 | release | 101 | air |  | 17 (-8..≥+8) | 24 | free |
| 18 | 253 | jump | 69 | ground | red | 17 (-8..≥+8) | 24 | free |
| 19 | 259 | release | 58 | air |  | 10 (≤-5..+4) | 16 | moderate |
| 20 | 268 | jump | 48 | wall | blue | 17 (≤-8..≥+8) | 8 | free |
| 21 | 274 | right | 43 | air |  | 17 (≤-8..≥+8) | 5 | free |
| 22 | 280 | release | 43 | air |  | 17 (≤-8..≥+8) | 8 | free |

## PhaseDemo15

Route 836 frames (13.9s), 10 jumps; from a 4-frame search, the most forgiving of 3 routes (1216 searched states, 18s).

Histogram: 1–3f 0, 4–6f 0, 7–9f 0, 10–12f 2, 13+f 20

| # | Frame | Input | x | Contact | Color | Window (frames) | Window (px) | Grade |
|---|---|---|---|---|---|---|---|---|
| 1 | 21 | jump | 42 | ground | blue | 17 (≤-8..+8) | 24 | free |
| 2 | 36 | release | 68 | air |  | 17 (-8..≥+8) | 24 | free |
| 3 | 148 | jump | 236 | ground | green | 17 (≤-8..+8) | 24 | free |
| 4 | 160 | release | 257 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 5 | 247 | jump | 388 | ground | red | 17 (≤-8..≥+8) | 16 | free |
| 6 | 252 | release | 398 | air |  | 10 (≤-4..+5) | 16 | moderate |
| 7 | 373 | jump | 580 | ground | blue | 17 (≤-8..+8) | 24 | free |
| 8 | 380 | release | 594 | air |  | 14 (≤-6..+7) | 22 | free |
| 9 | 383 | stop | 599 | air |  | 13 (-9..+3) | 20 | free |
| 10 | 400 | right | 604 | ground |  | 17 (-8..≥+8) | 0 | free |
| 11 | 401 | jump | 605 | ground | green | 12 (-6..+5) | 6 | moderate |
| 12 | 410 | release | 618 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 13 | 450 | jump | 678 | ground | red | 17 (≤-8..+8) | 24 | free |
| 14 | 462 | release | 699 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 15 | 488 | jump | 738 | ground | blue | 17 (≤-8..+8) | 24 | free |
| 16 | 501 | release | 761 | air |  | 17 (-8..≥+8) | 25 | free |
| 17 | 566 | jump | 859 | ground | green | 17 (≤-8..+8) | 24 | free |
| 18 | 575 | release | 876 | air |  | 17 (≤-8..≥+8) | 27 | free |
| 19 | 651 | jump | 990 | ground | red | 17 (≤-8..+8) | 24 | free |
| 20 | 663 | release | 1011 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 21 | 769 | jump | 1170 | ground | blue | 17 (≤-8..+8) | 24 | free |
| 22 | 784 | release | 1196 | air |  | 17 (-8..≥+8) | 24 | free |

## PhaseUmbrella

Route 836 frames (13.9s), 15 jumps; from a 4-frame search, the most forgiving of 3 routes (18992 searched states, 556s).

Histogram: 1–3f 0, 4–6f 0, 7–9f 1, 10–12f 6, 13+f 25

| # | Frame | Input | x | Contact | Color | Window (frames) | Window (px) | Grade |
|---|---|---|---|---|---|---|---|---|
| 1 | 18 | jump | 38 | ground | blue | 17 (≤-8..+8) | 24 | free |
| 2 | 54 | release | 95 | ground |  | 17 (≤-8..≥+8) | 24 | free |
| 3 | 61 | jump | 106 | ground | green | 12 (≤-6..+5) | 16 | moderate |
| 4 | 141 | release | 229 | ground |  | 17 (≤-8..≥+8) | 24 | free |
| 5 | 163 | jump | 262 | ground | red | 17 (≤-8..+8) | 24 | free |
| 6 | 175 | release | 283 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 7 | 221 | jump | 352 | ground | blue | 14 (-7..+6) | 19 | free |
| 8 | 268 | release | 426 | ground |  | 17 (≤-8..+8) | 24 | free |
| 9 | 274 | jump | 435 | ground | green | 10 (≤-5..+4) | 13 | moderate |
| 10 | 312 | release | 496 | ground |  | 17 (≤-8..+8) | 24 | free |
| 11 | 319 | jump | 506 | ground | red | 12 (≤-6..+5) | 17 | moderate |
| 12 | 368 | release | 583 | air |  | 17 (≤-8..+8) | 24 | free |
| 13 | 375 | jump | 594 | ground | blue | 11 (-5..+5) | 15 | moderate |
| 14 | 392 | release | 623 | air |  | 17 (-8..≥+8) | 24 | free |
| 15 | 413 | jump | 654 | ground | green | 17 (-3..≥+13) | 22 | free |
| 16 | 425 | release | 675 | air |  | 17 (-8..≥+8) | 25 | free |
| 17 | 446 | jump | 707 | ground | red | 17 (≤-8..≥+8) | 17 | free |
| 18 | 462 | release | 720 | air |  | 17 (≤-8..≥+8) | 20 | free |
| 19 | 498 | jump | 774 | ground | blue | 17 (≤-8..+8) | 24 | free |
| 20 | 510 | release | 795 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 21 | 600 | jump | 930 | ground | green | 11 (-5..+5) | 15 | moderate |
| 22 | 613 | release | 953 | air |  | 17 (-8..≥+8) | 24 | free |
| 23 | 646 | jump | 1003 | ground | red | 17 (-8..≥+8) | 24 | free |
| 24 | 658 | release | 1024 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 25 | 676 | jump | 1051 | ground | blue | 9 (-4..+4) | 12 | moderate |
| 26 | 708 | release | 1103 | air |  | 17 (≤-8..+8) | 24 | free |
| 27 | 715 | jump | 1113 | ground | green | 13 (≤-6..+6) | 18 | free |
| 28 | 741 | release | 1156 | air |  | 17 (-8..≥+8) | 24 | free |
| 29 | 756 | stop | 1178 | air |  | 11 (-5..+5) | 15 | moderate |
| 30 | 763 | jump | 1183 | ground | red | 17 (-8..≥+8) | 6 | free |
| 31 | 772 | right | 1183 | air |  | 17 (≤-8..≥+8) | 0 | free |
| 32 | 778 | release | 1187 | air |  | 17 (-8..≥+8) | 15 | free |

## PhaseLadder

Route 382 frames (6.4s), 7 jumps; from the recorded route, the most forgiving of 1 routes (180000 searched states, 27s).

Histogram: 1–3f 0, 4–6f 0, 7–9f 1, 10–12f 0, 13+f 23

| # | Frame | Input | x | Contact | Color | Window (frames) | Window (px) | Grade |
|---|---|---|---|---|---|---|---|---|
| 1 | 54 | jump | 108 | ground | blue | 17 (≤-8..≥+8) | 24 | free |
| 2 | 74 | release | 141 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 3 | 79 | stop | 149 | ground |  | 17 (≤-8..≥+8) | 24 | free |
| 4 | 88 | right | 152 | ground |  | 17 (≤-8..≥+8) | 2 | free |
| 5 | 88 | jump | 152 | ground | green | 17 (≤-8..≥+8) | 11 | free |
| 6 | 100 | release | 168 | wall |  | 17 (≤-8..≥+8) | 21 | free |
| 7 | 128 | left | 208 | ground |  | 17 (≤-8..≥+8) | 24 | free |
| 8 | 128 | jump | 208 | ground | red | 17 (≤-8..≥+8) | 14 | free |
| 9 | 140 | release | 202 | wall |  | 17 (≤-8..≥+8) | 19 | free |
| 10 | 153 | right | 183 | ground |  | 17 (≤-8..≥+8) | 24 | free |
| 11 | 162 | left | 182 | ground |  | 17 (≤-8..≥+8) | 12 | free |
| 12 | 179 | right | 166 | ground |  | 17 (≤-8..≥+8) | 24 | free |
| 13 | 179 | jump | 166 | ground | blue | 17 (≤-8..≥+8) | 14 | free |
| 14 | 191 | release | 168 | wall |  | 17 (≤-8..≥+8) | 9 | free |
| 15 | 218 | left | 203 | ground |  | 17 (≤-8..≥+8) | 24 | free |
| 16 | 218 | jump | 203 | ground | green | 17 (≤-8..≥+8) | 14 | free |
| 17 | 230 | release | 200 | wall |  | 17 (≤-8..≥+8) | 15 | free |
| 18 | 243 | right | 182 | ground |  | 17 (≤-8..≥+8) | 24 | free |
| 19 | 252 | left | 181 | ground |  | 17 (≤-8..≥+8) | 12 | free |
| 20 | 269 | right | 165 | ground |  | 17 (≤-8..≥+8) | 24 | free |
| 21 | 272 | jump | 162 | ground | red | 8 (-3..+4) | 3 | moderate |
| 22 | 289 | release | 171 | air |  | 17 (-8..≥+8) | 14 | free |
| 23 | 320 | jump | 216 | ground | blue | 17 (≤-8..≥+8) | 24 | free |
| 24 | 332 | release | 238 | air |  | 17 (≤-8..≥+8) | 25 | free |

## PhaseTurnaround

Route 602 frames (10.0s), 13 jumps; from the recorded route, the most forgiving of 1 routes (180000 searched states, 50s).

Histogram: 1–3f 0, 4–6f 0, 7–9f 0, 10–12f 1, 13+f 37

| # | Frame | Input | x | Contact | Color | Window (frames) | Window (px) | Grade |
|---|---|---|---|---|---|---|---|---|
| 1 | 70 | jump | 116 | ground | blue | 17 (≤-8..≥+8) | 24 | free |
| 2 | 90 | release | 149 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 3 | 98 | left | 161 | ground |  | 17 (-8..≥+8) | 24 | free |
| 4 | 103 | jump | 164 | ground | green | 11 (-5..+5) | 0 | moderate |
| 5 | 123 | release | 146 | air |  | 17 (≤-8..≥+8) | 18 | free |
| 6 | 134 | right | 129 | ground |  | 17 (≤-8..≥+8) | 24 | free |
| 7 | 141 | jump | 126 | ground | red | 15 (-7..+7) | 6 | free |
| 8 | 161 | release | 156 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 9 | 167 | left | 165 | air |  | 17 (-8..≥+8) | 24 | free |
| 10 | 172 | jump | 169 | ground | blue | 17 (-8..≥+8) | 0 | free |
| 11 | 192 | release | 141 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 12 | 202 | right | 126 | ground |  | 16 (-8..+7) | 22 | free |
| 13 | 210 | jump | 124 | ground | green | 17 (-8..≥+8) | 9 | free |
| 14 | 230 | release | 154 | air |  | 17 (-3..≥+13) | 24 | free |
| 15 | 236 | left | 163 | ground |  | 17 (-8..≥+8) | 24 | free |
| 16 | 240 | jump | 167 | ground | red | 17 (-8..≥+8) | 3 | free |
| 17 | 260 | release | 142 | air |  | 17 (≤-8..≥+8) | 22 | free |
| 18 | 270 | right | 127 | ground |  | 17 (≤-8..+8) | 24 | free |
| 19 | 277 | jump | 125 | ground | blue | 16 (-7..+8) | 8 | free |
| 20 | 297 | release | 155 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 21 | 303 | left | 164 | ground |  | 17 (-8..≥+8) | 24 | free |
| 22 | 307 | jump | 167 | ground | green | 17 (-8..≥+8) | 2 | free |
| 23 | 327 | release | 140 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 24 | 336 | right | 126 | ground |  | 14 (-6..+7) | 19 | free |
| 25 | 344 | jump | 124 | ground | red | 17 (-8..≥+8) | 10 | free |
| 26 | 364 | release | 155 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 27 | 369 | left | 162 | air |  | 17 (-8..≥+8) | 24 | free |
| 28 | 373 | jump | 166 | ground | blue | 16 (-7..+8) | 2 | free |
| 29 | 393 | release | 142 | air |  | 17 (≤-8..≥+8) | 22 | free |
| 30 | 403 | right | 127 | ground |  | 16 (-8..+7) | 22 | free |
| 31 | 410 | jump | 125 | ground | green | 16 (-7..+8) | 8 | free |
| 32 | 430 | release | 155 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 33 | 435 | left | 162 | air |  | 17 (-8..≥+8) | 24 | free |
| 34 | 439 | jump | 166 | ground | red | 16 (-7..+8) | 2 | free |
| 35 | 459 | release | 142 | air |  | 17 (≤-8..≥+8) | 22 | free |
| 36 | 469 | right | 127 | ground |  | 16 (-8..+7) | 22 | free |
| 37 | 476 | jump | 125 | ground | blue | 16 (-7..+8) | 8 | free |
| 38 | 496 | release | 155 | air |  | 17 (≤-8..≥+8) | 24 | free |

## PhaseGear

Route 221 frames (3.7s), 8 jumps; from a 4-frame search, the most forgiving of 1 routes (163429 searched states, 52s).

Histogram: 1–3f 0, 4–6f 2, 7–9f 3, 10–12f 2, 13+f 16

| # | Frame | Input | x | Contact | Color | Window (frames) | Window (px) | Grade |
|---|---|---|---|---|---|---|---|---|
| 1 | 0 | jump | 16 | ground | blue | 17 (≤-15..+1) | 0 | free |
| 2 | 47 | release | 85 | ground |  | 17 (≤-8..+8) | 24 | free |
| 3 | 55 | jump | 97 | ground | green | 4 (-2..+1) | 4 | moderate |
| 4 | 75 | release | 130 | air |  | 12 (-8..+3) | 17 | moderate |
| 5 | 82 | stop | 141 | air |  | 7 (-3..+3) | 9 | moderate |
| 6 | 85 | jump | 144 | wall | red | 11 (-4..+6) | 7 | moderate |
| 7 | 101 | release | 160 | wall |  | 17 (≤-8..≥+8) | 3 | free |
| 8 | 105 | jump | 160 | wall | blue | 7 (≤-3..+3) | 0 | moderate |
| 9 | 109 | right | 168 | air |  | 17 (≤-8..≥+8) | 14 | free |
| 10 | 114 | release | 176 | air |  | 17 (≤-8..≥+8) | 21 | free |
| 11 | 118 | stop | 182 | wall |  | 17 (≤-8..≥+8) | 14 | free |
| 12 | 118 | jump | 182 | wall | green | 14 (≤-3..+10) | 7 | free |
| 13 | 122 | release | 182 | wall |  | 17 (≤-3..≥+13) | 5 | free |
| 14 | 124 | jump | 184 | wall | red | 7 (≤-1..+5) | 0 | moderate |
| 15 | 140 | left | 198 | air |  | 17 (≤-8..≥+8) | 2 | free |
| 16 | 140 | release | 198 | air |  | 14 (-10..+3) | 3 | free |
| 17 | 143 | jump | 197 | air | blue | 5 (≤-2..+2) | 3 | moderate |
| 18 | 148 | right | 199 | air |  | 16 (≤-7..+8) | 15 | free |
| 19 | 150 | release | 203 | air |  | 13 (≤-6..+6) | 17 | free |
| 20 | 157 | press | 214 | air |  | 13 (≤-6..+6) | 19 | free |
| 21 | 164 | release | 225 | air |  | 14 (≤-6..+7) | 19 | free |
| 22 | 172 | jump | 237 | ground | green | 15 (≤-7..+7) | 21 | free |
| 23 | 186 | release | 261 | air |  | 17 (-8..≥+8) | 24 | free |
