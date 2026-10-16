# PHASE

[Play the Web preview](https://phase-gamejam1-20261016.swear01.chatgpt.site).

Unity **6000.4.7f1**. Open `Assets/Scenes/PhaseSandbox.unity` and press Play.
The two test rooms connect at the first room’s right edge and the second room’s
left edge. Cross to continue from the opposite side; there is no text interface.

The 320×180 pixel frame uses integer scaling; use a Game View of at least 320×180.
`PhaseTerrain` and `PhaseMovingPlatform` are reusable Inspector components.

Development uses **Unity CLI + Unity Pipeline** against a running Editor.
See [development and verification](Documentation/Development.md) and
[art direction](DESIGN.md) and [movement attribution](Documentation/ThirdPartyNotices.md).

For browser play, [build the Web preview or enable Web CI/Pages](Documentation/Web.md).
