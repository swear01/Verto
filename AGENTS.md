# BEGIN agents_rule-base
# Agent Rules

## Core Rules

- Default to autonomous execution: make and carry out the least-risky reasonable decisions for routine, reversible, in-scope work without asking.
- Own the requested outcome end to end. Do not stop after discovery, planning, partial implementation, or a progress update; continue through verification and safe in-scope fixes until complete or genuinely blocked.
- Necessary supporting changes to code, tests, fixtures, configuration, generated artifacts, and documentation are in scope. Unrelated refactors, cleanup, and features are not.
- Ask only for a concrete, material problem—missing input, a user-owned decision, or an unrequested destructive, irreversible, paid, or external action; never skip a required step for lack of approval—ask instead.
- Before asking, diagnose the issue and complete all safe, reversible, equivalent, in-scope work. If no concrete material problem exists, choose a reasonable default and continue.
- Do not ask merely because the work crosses files, functions, tests, fixtures, configuration, dependencies, or tools. Do not ask again for an action the user already explicitly requested or authorized.
- For substantive tasks, inspect relevant local files and instructions before modifying them; use authoritative docs or web sources when current or external facts matter. Explicit, trivial, unambiguous edits may proceed directly.
- If authoritative guidance is unavailable, use verified local evidence when sufficient; never invent code, paths, functions, APIs, behavior, or verification results.
- Equivalent tools and implementation methods may be substituted without asking when they preserve behavior, security, data safety, verification strength, target, and material cost. Otherwise report the issue and the decision needed.
- Never return dummy results, skip required verification, hide failures, or silently downgrade the requested scope.
- Retry an operation once when the failure appears transient; otherwise diagnose it and continue with safe, equivalent, in-scope alternatives when available.
- Match existing style, names, and patterns; keep changes minimal and add no dependencies or comments unless necessary for the requested result.
- Change requested behavior directly; do not add unrequested compatibility options.
- For explicit GitHub PR review, use `github-pr-review-loop`; local review is supplementary.

## Completion

- Run the smallest relevant verification that can prove the outcome, including live behavior when applicable; fix safe failures caused by the change.
- Never claim an unrun check passed. Report the concrete problem, safe alternatives tried, and exact decision needed when genuinely blocked.

## Memory and Documentation

- After substantive work, check for new durable, verified knowledge; use `shared-memory` only when there is useful knowledge to record.
- Update active docs only when durable behavior, API, CLI, or configuration changes; scan only affected documentation.
- Refresh and query-check QMD only when memory changes. Use `agents_rule archive` for intentional documentation archiving and `git mv` for tracked moves.
- Report files changed, documentation status, verification performed, and remaining risks or blockers.
# END agents_rule-base

## Unity workflow

- Use the official Unity CLI and `com.unity.pipeline` to drive a resident GUI Editor for project work and verification.
- Do not substitute `-batchmode`, `-executeMethod`, `unity run`, or `unity test` for live Pipeline commands. Diagnose connection failures and report concrete blockers instead of switching workflows silently.
- Edit C# source normally, then use `unity recompile` and Pipeline `run_tests`. Create or modify scenes and assets through the connected Editor.
- Verify Play with advancing frame count and controller time, live keyboard actions, a screen capture, and Console checks.
- Keep the game free of explanatory text and level progression.
- Commands and coordinate conventions: [Documentation/Development.md](Documentation/Development.md).

## Pull requests

- No review bot runs automatically. After every push to a PR, trigger one yourself with `"$(git rev-parse --path-format=absolute --git-common-dir)/info/trigger-pr-review" <pr> <head-sha>` (posts `/swear-review`), or post `/gemini review`. If neither answers, post `@claude review this PR`. Waiting without a trigger comment means no review ever arrives.
- Merge only when CI is green and a review you triggered passed on the latest head. Never merge on CI alone.
- Once only nits remain, reply/resolve and merge with `gh pr merge <n> --merge --match-head-commit <sha> --delete-branch`. If the auto-mode classifier blocks the merge, report it; do not work around it.

## Web publishing

- After merging a PR that changes the game (`Assets/`, `Packages/` or `ProjectSettings/`), publish <https://verto.5334601.xyz/> from a fresh checkout of the latest `origin/main`: run `Verification/BuildWeb.py`, deploy with `--commit-hash`, then check the live site with `WebLoadSmoke.cjs`. Skip it when the live deployment is already at that commit or a newer one. Steps: [Documentation/Web.md](Documentation/Web.md#online-play).
