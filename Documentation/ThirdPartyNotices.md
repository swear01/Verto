# References

PHASE controller is ported from the local three-color HTML prototype
(SHA-256 `263b80ae0014b042603f5ec44b6a42b3216142b6e6ad25e1978d5ac8be598934`).
The current Wick character, connected stone terrain, riveted platform and night
backdrop are ported from the user's [published demo](https://phase-three-color.swear01.chatgpt.site/).
The renderer SHA-256 is `b7178e0f6c0a613a10a92d276a2b6f9c37db75420a0ec342654504278a982c1d`;
`Verification/ArtReference.json` and `.png` preserve its independent reference frames.
The hood now uses the scarf dash-charge palette at the user's request. Pixel tests
explicitly remap the hood, spent scarf and permanent geometry palettes, and
tint low-stamina body pixels. The user-requested character warning replaces the
previous detached stamina gauge; its progressive tint/shake and sustained30px/s
exhausted-grab slide are PHASE-specific tuning. Permanent stone, moving platforms,
routes and stops now use white and gray at the user's request; the immutable
reference images and all remaining pixels stay checked.
The phase-mask mapping, mixed-phase inlays, four-direction spike glyphs and swept
3-pixel root-strip collision are also ported from the frozen local HTML v10.
That HTML file remains unchanged; Unity adds the authoring components separately.
No commercial game art or music is included.

Movement behavior was informed by [Celeste Player.cs](https://github.com/NoelFB/Celeste/blob/1b0ce45c75e05649ae91b44a8bb6b196684e4352/Source/Player/Player.cs).
Its MIT notice is preserved below. The existing immutable reference fixtures come
from [celeste-next-gym](https://github.com/std-microblock/celeste-next-gym/tree/2e133ef7803c2f74b6914a477393218f2b9abaef),
whose Cargo workspace declares MIT. No Rust runtime or game assets are vendored.
The fixtures identify their revision and generator; they are a source-informed
subset, not proof of full Celeste parity.

MIT License

Copyright (c) 2018 Noel Berry

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
