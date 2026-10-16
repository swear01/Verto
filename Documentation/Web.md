# Web preview

[Play online](https://phase-gamejam1-20261016.swear01.chatgpt.site). This is a
manually published two-room preview; automatic CI/Pages still needs the owner
setup below.

Unity **6000.4.7f1**, Web Build Support and Unity CLI are required. Open the GUI
Editor as described in [Development](Development.md), then run:

```sh
python3 Verification/BuildWeb.py
cd Verification
npm ci
npx --no-install playwright install chromium --no-shell
cd ..
node Verification/WebSmoke.cjs Builds/Web
python3 -m http.server 8765 --bind 127.0.0.1 --directory Builds/Web
```

Open <http://127.0.0.1:8765/>. Click the canvas for keyboard focus: arrows/WASD
move, Space/Z jumps, X dashes, and C/Shift grabs. Cross the first room’s right
edge to continue, or the second room’s left edge to return.
Touch controls are not provided.
The game keeps integer scaling from 320×180; the browser
viewport must be at least 320×180. Loading failures remain visible.

The top-right settings button opens keyboard bindings and pauses the game. Select
an action, then press one key; Esc cancels capture or closes settings. Overrides
replace that action's default aliases. Duplicate keys and browser modifier shortcuts
are rejected. Changes apply immediately and are stored on this browser/origin;
blocked storage displays a warning while keeping the current session usable.
Release held keys after closing settings to resume. Restore defaults resets all
seven actions. Bindings survive room travel.
The settings interface lives in the Web page; the game view stays free of prose.

The custom `Phase` template uses gzip with Unity's decompression fallback, so a
static host does not need custom `Content-Encoding` headers. The 2D renderer has
post-processing disabled. A scoped WebGL guard returns no MSAA samples for the
six SNORM formats that Unity 6000.4 queries incorrectly in Chromium; ordinary
queries and warnings remain intact. Revisit it after an engine/browser fix.
The build warning about an absent `RuntimePipelineConfig` is expected: release
Players do not need Pipeline's remote-control server. Do not enable it to silence
the warning.

## Enable automatic builds and online play

`Web build and preview` runs only on `main`, after push or manual dispatch.
It uses Unity CLI + live Pipeline for 22 Editor tests, three Play Mode tests and
the Web build, then verifies browser rendering, keyboard input, both scene-edge
transitions, resizing, visible load failures, and absence of browser errors/warnings. Only a passing
build is uploaded and deployed. `web-verification` contains the reports/captures.

The repository owner must complete this setup first:

1. Register a **dedicated macOS ARM64 runner** under Settings → Actions → Runners,
   with the custom label `gamejam1-unity`. Run it in a logged-in desktop session
   with a licensed Unity 6000.4.7f1, Web Build Support, Python 3, and Unity CLI at
   `~/.unity/bin/unity`. Runner jobs execute the repository's `main` code.
2. Set Settings → Pages → Source to **GitHub Actions**. Pages from this private
   repository requires the owner's GitHub Pro or a qualifying paid plan; do not
   change repository visibility to work around this requirement.
3. Set the Actions repository variable **`UNITY_WEB_CI_ENABLED=true`**, then run
   `Web build and preview` on `main`. The successful deployment reports the
   playable URL in its `github-pages` environment.

Until this setup is complete the Web job is explicitly skipped. The existing
GitHub-hosted controller/compatibility checks still run on pull requests and
`main`. A local build passing does not prove that the runner or Pages is enabled.

References: [Unity Web deployment](https://docs.unity3d.com/6000.4/Documentation/Manual/webgl-deploying.html),
[runner registration](https://docs.github.com/en/actions/how-tos/manage-runners/self-hosted-runners/add-runners),
[Pages publishing](https://docs.github.com/en/pages/getting-started-with-github-pages/configuring-a-publishing-source-for-your-github-pages-site).
