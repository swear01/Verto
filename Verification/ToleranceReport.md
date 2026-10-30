# Input tolerance report

Window = frames (60 Hz) an input may move and still win; tight ≤ 3, moderate 4–12, free ≥ 13. A window stops growing at 17 frames or ±20; ≤/≥ mark such bounds. See Documentation/Tolerance.md.

| Level | Verdict | Jumps | Steps | Free | Moderate | Tight | Tightest step |
|---|---|---|---|---|---|---|---|
| PhaseDemo1 | in band | 2 | 5 | 4 | 1 | 0 | jump at x=114: 5f |
| PhaseDemo13 | in band | 10 | 30 | 23 | 7 | 0 | jump at x=148: 6f |
| PhaseDemo15 | in band | 10 | 22 | 20 | 2 | 0 | release at x=398: 10f |
| PhaseUmbrella | in band | 15 | 32 | 25 | 7 | 0 | jump at x=1051: 9f |
| PhaseGear | in band | 7 | 22 | 15 | 7 | 0 | jump at x=97: 4f |

## PhaseDemo1

Route 201 frames (3.4s), 2 jumps; from a 6-frame search, the most forgiving of 2 routes (210 searched states, 0s).

Histogram: 1–3f 0, 4–6f 1, 7–9f 0, 10–12f 0, 13+f 4

| # | Frame | Input | x | Contact | Color | Window (frames) | Window (px) | Grade |
|---|---|---|---|---|---|---|---|---|
| 1 | 51 | release | 99 | ground |  | 17 (≤-8..+8) | 24 | free |
| 2 | 61 | jump | 114 | ground | green | 5 (-2..+2) | 6 | moderate |
| 3 | 89 | release | 146 | air |  | 17 (≤-8..+8) | 24 | free |
| 4 | 96 | jump | 156 | ground | red | 13 (≤-6..+6) | 18 | free |
| 5 | 108 | release | 173 | air |  | 17 (≤-8..≥+8) | 20 | free |

## PhaseDemo13

Route 410 frames (6.8s), 10 jumps; from the recorded route, the most forgiving of 1 routes (180000 searched states, 19s).

Histogram: 1–3f 0, 4–6f 1, 7–9f 3, 10–12f 3, 13+f 23

| # | Frame | Input | x | Contact | Color | Window (frames) | Window (px) | Grade |
|---|---|---|---|---|---|---|---|---|
| 1 | 2 | jump | 25 | ground | blue | 8 (≤-1..+6) | 7 | moderate |
| 2 | 32 | release | 69 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 3 | 46 | jump | 90 | ground | green | 10 (-8..+1) | 14 | moderate |
| 4 | 76 | release | 139 | air |  | 9 (-3..+5) | 12 | moderate |
| 5 | 82 | jump | 148 | air | red | 6 (-1..+4) | 8 | moderate |
| 6 | 112 | stop | 186 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 7 | 112 | release | 186 | air |  | 17 (≤-8..≥+8) | 17 | free |
| 8 | 116 | right | 191 | ground |  | 17 (≤-3..≥+13) | 3 | free |
| 9 | 116 | jump | 191 | ground | blue | 15 (≤-3..+11) | 18 | free |
| 10 | 146 | stop | 236 | ground |  | 17 (≤-8..≥+8) | 24 | free |
| 11 | 146 | release | 236 | ground |  | 17 (≤-8..≥+8) | 15 | free |
| 12 | 152 | left | 239 | ground |  | 17 (≤-5..≥+11) | 2 | free |
| 13 | 152 | jump | 239 | ground | green | 17 (≤-5..≥+11) | 11 | free |
| 14 | 182 | release | 195 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 15 | 186 | right | 189 | air |  | 15 (-2..+12) | 21 | free |
| 16 | 192 | jump | 184 | ground | red | 14 (≤-9..+4) | 8 | free |
| 17 | 222 | stop | 213 | ground |  | 17 (≤-11..+5) | 24 | free |
| 18 | 222 | release | 213 | ground |  | 17 (≤-8..≥+8) | 16 | free |
| 19 | 228 | left | 217 | ground |  | 17 (≤-5..≥+11) | 2 | free |
| 20 | 228 | jump | 217 | ground | blue | 17 (-4..≥+12) | 14 | free |
| 21 | 258 | right | 173 | ground |  | 15 (-12..+2) | 21 | free |
| 22 | 258 | release | 173 | ground |  | 17 (≤-8..≥+8) | 14 | free |
| 23 | 264 | jump | 170 | ground | green | 8 (≤-5..+2) | 1 | moderate |
| 24 | 294 | release | 214 | air |  | 17 (≤-11..+5) | 24 | free |
| 25 | 300 | jump | 223 | air | red | 12 (-1..+10) | 16 | moderate |
| 26 | 330 | stop | 272 | ground |  | 13 (-11..+1) | 18 | free |
| 27 | 330 | release | 272 | ground |  | 17 (≤-8..≥+8) | 15 | free |
| 28 | 336 | right | 275 | ground |  | 17 (≤-5..≥+11) | 2 | free |
| 29 | 350 | jump | 288 | ground | blue | 12 (-10..+1) | 10 | moderate |
| 30 | 380 | release | 317 | air |  | 17 (≤-8..≥+8) | 非數值 | free |

## PhaseDemo15

Route 836 frames (13.9s), 10 jumps; from a 4-frame search, the most forgiving of 3 routes (1082 searched states, 23s).

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
| 9 | 380 | release | 594 | air |  | 13 (-6..+6) | 20 | free |
| 10 | 398 | right | 602 | air |  | 17 (≤-8..≥+8) | 0 | free |
| 11 | 401 | jump | 603 | ground | green | 11 (-5..+5) | 8 | moderate |
| 12 | 410 | release | 617 | air |  | 17 (≤-8..≥+8) | 24 | free |
| 13 | 450 | jump | 677 | ground | red | 17 (≤-8..+8) | 24 | free |
| 14 | 462 | release | 698 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 15 | 489 | jump | 739 | ground | blue | 17 (≤-8..+8) | 24 | free |
| 16 | 501 | release | 760 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 17 | 567 | jump | 859 | ground | green | 17 (≤-8..+8) | 24 | free |
| 18 | 576 | release | 876 | air |  | 17 (≤-8..≥+8) | 27 | free |
| 19 | 652 | jump | 990 | ground | red | 17 (≤-8..+8) | 24 | free |
| 20 | 664 | release | 1011 | air |  | 17 (≤-8..≥+8) | 25 | free |
| 21 | 770 | jump | 1170 | ground | blue | 17 (≤-8..+8) | 24 | free |
| 22 | 785 | release | 1196 | air |  | 17 (-8..≥+8) | 24 | free |

## PhaseUmbrella

Route 836 frames (13.9s), 15 jumps; from a 4-frame search, the most forgiving of 3 routes (14091 searched states, 358s).

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

## PhaseGear

Route 221 frames (3.7s), 7 jumps; from a 4-frame search, the most forgiving of 1 routes (144173 searched states, 64s).

Histogram: 1–3f 0, 4–6f 1, 7–9f 4, 10–12f 2, 13+f 15

| # | Frame | Input | x | Contact | Color | Window (frames) | Window (px) | Grade |
|---|---|---|---|---|---|---|---|---|
| 1 | 47 | release | 85 | ground |  | 17 (≤-8..+8) | 24 | free |
| 2 | 55 | jump | 97 | ground | green | 4 (-2..+1) | 4 | moderate |
| 3 | 75 | release | 130 | air |  | 12 (-8..+3) | 17 | moderate |
| 4 | 82 | stop | 141 | air |  | 7 (-3..+3) | 9 | moderate |
| 5 | 85 | jump | 144 | wall | red | 11 (-4..+6) | 7 | moderate |
| 6 | 101 | release | 160 | wall |  | 17 (≤-8..≥+8) | 3 | free |
| 7 | 105 | jump | 160 | wall | blue | 7 (≤-3..+3) | 0 | moderate |
| 8 | 109 | right | 168 | air |  | 17 (≤-8..≥+8) | 14 | free |
| 9 | 114 | release | 176 | air |  | 17 (≤-8..≥+8) | 21 | free |
| 10 | 118 | stop | 182 | wall |  | 17 (≤-8..≥+8) | 14 | free |
| 11 | 118 | jump | 182 | wall | green | 14 (≤-3..+10) | 7 | free |
| 12 | 122 | release | 182 | wall |  | 17 (≤-3..≥+13) | 5 | free |
| 13 | 124 | jump | 184 | wall | red | 7 (≤-1..+5) | 0 | moderate |
| 14 | 140 | left | 198 | air |  | 17 (≤-8..≥+8) | 2 | free |
| 15 | 140 | left | 198 | air |  | 14 (-7..+6) | 3 | free |
| 16 | 142 | jump | 198 | air | blue | 8 (≤-4..+3) | 3 | moderate |
| 17 | 149 | right | 201 | air |  | 17 (≤-8..≥+8) | 16 | free |
| 18 | 150 | release | 203 | air |  | 15 (≤-7..+7) | 17 | free |
| 19 | 157 | press | 214 | air |  | 13 (≤-6..+6) | 19 | free |
| 20 | 164 | release | 225 | air |  | 14 (≤-6..+7) | 19 | free |
| 21 | 172 | jump | 237 | ground | green | 15 (≤-7..+7) | 21 | free |
| 22 | 186 | release | 261 | air |  | 17 (-8..≥+8) | 24 | free |
