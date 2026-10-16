# PHASE

[Play the Web preview](https://phase-gamejam1-20261016.swear01.chatgpt.site).

Unity **6000.4.7f1**. Open `Assets/Scenes/PhaseSandbox.unity` and press Play.
The two test rooms connect at the first room’s right edge and the second room’s
left edge. Cross to continue from the opposite side; the game view has no explanatory text.
In the Web preview, the top-right settings button lets you customize keyboard bindings.

The 320×180 pixel frame uses point sampling. Web fits the window at 16:9 and has a
fullscreen button; native Editor Game Views keep integer scaling. Room travel uses
a quick horizontal slide.
`PhaseTerrain` and `PhaseMovingPlatform` are reusable Inspector components.

Development uses **Unity CLI + Unity Pipeline** against a running Editor.
See [development and verification](Documentation/Development.md) and
[art direction](DESIGN.md) and [movement attribution](Documentation/ThirdPartyNotices.md).

For browser play, [build the Web preview or enable Web CI/Pages](Documentation/Web.md).
