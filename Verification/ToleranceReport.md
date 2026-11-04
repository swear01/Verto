# Input tolerance report

Window = frames (60 Hz) an input may move and still win; tight ≤ 3, moderate 4–12, free ≥ 13. A window stops growing at 17 frames or ±20; ≤/≥ mark such bounds. See Documentation/Tolerance.md.

| Level | Verdict | Jumps | Steps | Free | Moderate | Tight | Tightest step |
|---|---|---|---|---|---|---|---|
| PhaseDemo1 | in band | 3 | 6 | 5 | 1 | 0 | jump at x=114: 5f |
| PhaseDemo1.1 | in band | 12 | 42 | 40 | 2 | 0 | jump at x=100: 10f |
| PhaseDemo13 | in band | 26 | 86 | 76 | 10 | 0 | release at x=296: 8f |
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

Route 461 frames (7.7s), 12 jumps; from the recorded route, the most forgiving of 1 routes (180000 searched states, 36s).

Histogram: 1–3f 0, 4–6f 0, 7–9f 0, 10–12f 2, 13+f 40

| # | Frame | Input | x | Contact | Color | Window (frames) | Window (px) | Grade |
|---|---|---|---|---|---|---|---|---|
| 1 | 4 | jump | 10 | ground | blue | 17 (≤-3..≥+13) | 20 | free |
| 2 | 16 | release | 28 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 3 | 20 | stop | 34 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 4 | 29 | right | 39 | air |  | 17 (≤-8..≥+8) | 4 | free |
| 5 | 33 | jump | 41 | ground | green | 17 (-4..≥+12) | 19 | free |
| 6 | 45 | release | 51 | air |  | 17 (≤-8..≥+8) | 15 | free |
| 7 | 49 | stop | 56 | air |  | 17 (≤-8..≥+8) | 20 | free |
| 8 | 59 | right | 61 | air |  | 17 (≤-8..≥+8) | 3 | free |
| 9 | 73 | stop | 78 | ground |  | 15 (-8..+6) | 21 | free |
| 10 | 73 | jump | 78 | ground | red | 17 (≤-10..+6) | 19 | free |
| 11 | 85 | release | 82 | air |  | 17 (≤-8..≥+8) | 2 | free |
| 12 | 105 | right | 82 | ground |  | 17 (≤-8..≥+8) | 0 | free |
| 13 | 105 | jump | 82 | ground | blue | 17 (≤-8..≥+8) | 8 | free |
| 14 | 113 | stop | 92 | air |  | 17 (≤-7..≥+9) | 23 | free |
| 15 | 117 | release | 97 | air |  | 17 (≤-8..≥+8) | 12 | free |
| 16 | 134 | right | 98 | air |  | 17 (0..≥+16) | 0 | free |
| 17 | 138 | jump | 100 | air | green | 10 (0..+9) | 12 | moderate |
| 18 | 150 | release | 113 | air |  | 17 (-7..≥+9) | 16 | free |
| 19 | 168 | jump | 136 | ground | red | 17 (≤-8..≥+8) | 24 | free |
| 20 | 180 | release | 139 | air |  | 17 (≤-8..≥+8) | 14 | free |
| 21 | 184 | stop | 144 | air |  | 17 (≤-8..≥+8) | 20 | free |
| 22 | 194 | right | 149 | air |  | 17 (≤-8..≥+8) | 3 | free |
| 23 | 208 | stop | 166 | ground |  | 17 (≤-10..+6) | 24 | free |
| 24 | 208 | jump | 166 | ground | blue | 17 (≤-8..≥+8) | 16 | free |
| 25 | 220 | release | 170 | air |  | 17 (≤-8..≥+8) | 2 | free |
| 26 | 240 | right | 170 | ground |  | 17 (≤-8..≥+8) | 0 | free |
| 27 | 240 | jump | 170 | ground | green | 17 (≤-8..≥+8) | 8 | free |
| 28 | 248 | stop | 180 | air |  | 17 (≤-7..≥+9) | 23 | free |
| 29 | 252 | release | 185 | air |  | 17 (≤-8..≥+8) | 12 | free |
| 30 | 269 | right | 186 | air |  | 17 (0..≥+16) | 0 | free |
| 31 | 273 | jump | 188 | air | red | 10 (0..+9) | 12 | moderate |
| 32 | 285 | release | 201 | air |  | 17 (-7..≥+9) | 16 | free |
| 33 | 303 | jump | 224 | ground | blue | 17 (≤-8..≥+8) | 24 | free |
| 34 | 315 | release | 227 | air |  | 17 (≤-8..≥+8) | 14 | free |
| 35 | 319 | stop | 232 | air |  | 17 (≤-8..≥+8) | 20 | free |
| 36 | 329 | right | 237 | air |  | 17 (≤-8..≥+8) | 3 | free |
| 37 | 343 | jump | 254 | ground | green | 17 (≤-8..≥+8) | 24 | free |
| 38 | 355 | release | 271 | air |  | 17 (≤-8..≥+8) | 23 | free |
| 39 | 399 | jump | 310 | ground | red | 17 (≤-8..≥+8) | 14 | free |
| 40 | 411 | release | 333 | air |  | 17 (≤-8..≥+8) | 13 | free |
| 41 | 427 | press | 357 | air |  | 17 (≤-8..≥+8) | 12 | free |
| 42 | 439 | release | 375 | air |  | 17 (≤-8..≥+8) | 12 | free |

## PhaseDemo13

Route 918 frames (15.3s), 26 jumps; from the recorded route, the most forgiving of 1 routes (180000 searched states, 57s).

Histogram: 1–3f 0, 4–6f 0, 7–9f 2, 10–12f 8, 13+f 76

| # | Frame | Input | x | Contact | Color | Window (frames) | Window (px) | Grade |
|---|---|---|---|---|---|---|---|---|
| 1 | 9 | jump | 16 | ground | blue | 17 (≤-8..≥+8) | 20 | free |
| 2 | 26 | release | 45 | air |  | 17 (-8..≥+8) | 24 | free |
| 3 | 37 | jump | 62 | ground | green | 15 (-7..+7) | 21 | free |
| 4 | 42 | release | 72 | air |  | 10 (≤-4..+5) | 16 | moderate |
| 5 | 47 | stop | 80 | air |  | 13 (-6..+6) | 19 | free |
| 6 | 66 | right | 86 | air |  | 17 (≤-8..≥+8) | 0 | free |
| 7 | 73 | jump | 92 | ground | red | 17 (≤-8..+8) | 18 | free |
| 8 | 90 | release | 117 | air |  | 17 (-8..≥+8) | 20 | free |
| 9 | 98 | stop | 129 | ground |  | 17 (≤-8..+8) | 24 | free |
| 10 | 107 | right | 132 | ground |  | 17 (≤-8..≥+8) | 2 | free |
| 11 | 108 | jump | 133 | ground | blue | 13 (-6..+6) | 8 | free |
| 12 | 119 | release | 149 | air |  | 17 (≤-8..+8) | 24 | free |
| 13 | 123 | stop | 155 | air |  | 10 (-5..+4) | 14 | moderate |
| 14 | 146 | right | 160 | ground |  | 17 (≤-8..≥+8) | 0 | free |
| 15 | 150 | jump | 163 | ground | green | 17 (≤-8..+8) | 15 | free |
| 16 | 167 | release | 191 | air |  | 17 (-8..≥+8) | 24 | free |
| 17 | 178 | jump | 207 | ground | red | 17 (≤-8..+8) | 24 | free |
| 18 | 186 | release | 223 | air |  | 15 (≤-7..+7) | 23 | free |
| 19 | 189 | stop | 227 | air |  | 15 (-10..+4) | 23 | free |
| 20 | 212 | right | 233 | air |  | 17 (-8..≥+8) | 0 | free |
| 21 | 219 | jump | 239 | ground | blue | 17 (-8..≥+8) | 18 | free |
| 22 | 236 | release | 259 | air |  | 17 (-8..≥+8) | 14 | free |
| 23 | 256 | jump | 288 | ground | green | 17 (≤-8..≥+8) | 24 | free |
| 24 | 268 | release | 291 | air |  | 17 (-8..≥+8) | 14 | free |
| 25 | 289 | left | 304 | wall |  | 17 (≤-8..≥+8) | 0 | free |
| 26 | 292 | jump | 304 | wall | red | 17 (-8..≥+8) | 10 | free |
| 27 | 297 | release | 296 | wall |  | 8 (≤-4..+3) | 6 | moderate |
| 28 | 309 | stop | 285 | ground |  | 17 (≤-8..+8) | 22 | free |
| 29 | 318 | left | 282 | ground |  | 17 (≤-8..≥+8) | 2 | free |
| 30 | 318 | jump | 282 | ground | blue | 17 (≤-8..≥+8) | 11 | free |
| 31 | 335 | release | 257 | air |  | 17 (-8..≥+8) | 24 | free |
| 32 | 344 | stop | 244 | ground |  | 17 (≤-8..≥+8) | 24 | free |
| 33 | 353 | left | 240 | ground |  | 17 (≤-8..≥+8) | 3 | free |
| 34 | 353 | jump | 240 | ground | green | 17 (≤-8..≥+8) | 11 | free |
| 35 | 359 | release | 233 | air |  | 11 (≤-5..+5) | 15 | moderate |
| 36 | 366 | stop | 222 | air |  | 11 (-5..+5) | 15 | moderate |
| 37 | 391 | left | 217 | ground |  | 17 (-8..≥+8) | 0 | free |
| 38 | 394 | jump | 215 | ground | red | 17 (-8..≥+8) | 14 | free |
| 39 | 411 | release | 189 | air |  | 17 (-8..≥+8) | 24 | free |
| 40 | 417 | stop | 180 | ground |  | 17 (≤-8..≥+8) | 24 | free |
| 41 | 426 | left | 176 | ground |  | 17 (≤-8..≥+8) | 3 | free |
| 42 | 428 | jump | 176 | ground | blue | 16 (-7..+8) | 12 | free |
| 43 | 440 | release | 158 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 44 | 445 | stop | 150 | air |  | 11 (-5..+5) | 15 | moderate |
| 45 | 470 | left | 145 | ground |  | 17 (≤-8..≥+8) | 0 | free |
| 46 | 475 | jump | 140 | ground | green | 16 (-7..+8) | 17 | free |
| 47 | 492 | release | 115 | air |  | 17 (-8..≥+8) | 21 | free |
| 48 | 493 | right | 113 | air |  | 17 (-5..≥+11) | 23 | free |
| 49 | 501 | left | 108 | ground |  | 15 (≤-7..+7) | 4 | free |
| 50 | 509 | jump | 100 | ground | red | 17 (-8..≥+8) | 20 | free |
| 51 | 515 | release | 88 | air |  | 11 (≤-5..+5) | 18 | moderate |
| 52 | 522 | stop | 77 | air |  | 10 (-5..+4) | 14 | moderate |
| 53 | 547 | left | 72 | ground |  | 17 (-8..≥+8) | 0 | free |
| 54 | 550 | jump | 70 | ground | blue | 17 (-8..≥+8) | 13 | free |
| 55 | 567 | release | 44 | air |  | 17 (-8..≥+8) | 24 | free |
| 56 | 573 | right | 35 | ground |  | 17 (≤-8..≥+8) | 24 | free |
| 57 | 581 | left | 33 | ground |  | 16 (≤-7..+8) | 10 | free |
| 58 | 595 | jump | 20 | ground | green | 17 (≤-8..≥+8) | 24 | free |
| 59 | 607 | release | 13 | air |  | 17 (≤-8..≥+8) | 14 | free |
| 60 | 617 | right | 0 | air |  | 17 (-8..≥+8) | 11 | free |
| 61 | 632 | jump | 7 | ground | red | 17 (≤-8..≥+8) | 19 | free |
| 62 | 637 | release | 8 | wall |  | 9 (≤-4..+4) | 1 | moderate |
| 63 | 652 | jump | 22 | ground | blue | 14 (-7..+6) | 19 | free |
| 64 | 669 | release | 47 | air |  | 17 (-8..≥+8) | 20 | free |
| 65 | 681 | jump | 65 | ground | green | 15 (-7..+7) | 21 | free |
| 66 | 686 | release | 75 | air |  | 10 (≤-4..+5) | 16 | moderate |
| 67 | 689 | stop | 80 | air |  | 13 (-6..+6) | 20 | free |
| 68 | 709 | right | 87 | air |  | 17 (-8..≥+8) | 0 | free |
| 69 | 716 | jump | 93 | ground | red | 15 (-7..+7) | 16 | free |
| 70 | 733 | release | 117 | air |  | 17 (-8..≥+8) | 20 | free |
| 71 | 745 | jump | 135 | ground | blue | 15 (-7..+7) | 21 | free |
| 72 | 753 | release | 150 | air |  | 15 (≤-7..+7) | 24 | free |
| 73 | 754 | stop | 152 | air |  | 13 (-6..+6) | 20 | free |
| 74 | 781 | right | 158 | ground |  | 17 (≤-8..≥+8) | 0 | free |
| 75 | 787 | jump | 164 | ground | green | 17 (≤-8..+8) | 18 | free |
| 76 | 804 | release | 189 | air |  | 17 (-8..≥+8) | 20 | free |
| 77 | 816 | jump | 207 | ground | red | 17 (≤-8..+8) | 24 | free |
| 78 | 824 | release | 222 | air |  | 15 (≤-7..+7) | 24 | free |
| 79 | 825 | stop | 224 | air |  | 13 (-6..+6) | 20 | free |
| 80 | 852 | right | 230 | ground |  | 17 (≤-8..≥+8) | 0 | free |
| 81 | 858 | jump | 236 | ground | blue | 17 (≤-8..≥+8) | 18 | free |
| 82 | 875 | release | 261 | air |  | 17 (-8..≥+8) | 20 | free |
| 83 | 879 | stop | 267 | air |  | 17 (≤-8..+8) | 23 | free |
| 84 | 888 | right | 272 | ground |  | 17 (≤-8..≥+8) | 4 | free |
| 85 | 888 | jump | 272 | ground | green | 16 (-8..+7) | 11 | free |
| 86 | 905 | release | 293 | air |  | 17 (-8..≥+8) | 21 | free |

## PhaseDemo14

Route 461 frames (7.7s), 10 jumps; from the recorded route, the most forgiving of 1 routes (180000 searched states, 33s).

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

Route 836 frames (13.9s), 10 jumps; from a 4-frame search, the most forgiving of 3 routes (1216 searched states, 22s).

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

Route 836 frames (13.9s), 15 jumps; from a 4-frame search, the most forgiving of 3 routes (18992 searched states, 610s).

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

Route 602 frames (10.0s), 13 jumps; from the recorded route, the most forgiving of 1 routes (180000 searched states, 52s).

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

Route 221 frames (3.7s), 8 jumps; from a 4-frame search, the most forgiving of 1 routes (163429 searched states, 54s).

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
