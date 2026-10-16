# PHASE

[Play the Web preview](https://phase-gamejam1-20261016.swear01.chatgpt.site).

Unity **6000.4.7f1**. Open `Assets/Scenes/PhaseSandbox.unity` and press Play.
The two test rooms connect at the first room’s right edge and the second room’s
left edge. Cross to continue from the opposite side; the game view has no explanatory text.
In the Web preview, the top-right settings button lets you customize keyboard bindings.

The pixel view fills the window at any aspect ratio, keeping 180 native pixels
high with point sampling. Wide rooms extend the quarry and walkable floor; narrow
views follow the player. Web has a fullscreen button, and room travel uses a quick
horizontal slide.
`PhaseTerrain` and `PhaseMovingPlatform` are reusable Inspector components.

Development uses **Unity CLI + Unity Pipeline** against a running Editor.
See [development and verification](Documentation/Development.md) and
[art direction](DESIGN.md) and [movement attribution](Documentation/ThirdPartyNotices.md).

For browser play, [build the Web preview or enable Web CI/Pages](Documentation/Web.md).
