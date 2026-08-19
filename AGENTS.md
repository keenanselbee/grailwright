AGENTS.md
=========

This file provides general working rules for coding agents in this repository.
It is based on the shared starter template at `C:\Repositories\AGENTS.md` and adds Grailwright-specific mod packaging, build, and staging rules.


Command Speed Rules
-------------------

- Zero-tool commands must not inspect files, run shell commands, check git status, summarize context, or add extra explanation.
- `help` is the only zero-tool command. Reply immediately from the command list in the Keyword Commands section.
- Direct-action commands should skip unrelated repo inspection, git status checks, diff reading, and planning. Execute only their defined workflow, then report the result.
- Direct-action commands are `AUDIT`, `COMMIT`, `DIFF`, `LOGS`, `MSG`, `NEXUS`, and `TEST`.


Do Not Edit Guard
-----------------

- If the user intentionally types `DNE` in their current prompt, treat it as "do not edit persistent state" for that prompt.
- While `DNE` applies, do not create, edit, move, delete, stage, commit, build, format, generate files, refresh generated artifacts, launch external editors, or modify persistent project files or external paths unless the user explicitly overrides `DNE` in the same prompt.
- Temporary scratch files may be created, edited, or deleted under `.codex-temp` or the system temp directory when needed for investigation or diagnosis. Keep them clearly temporary, do not use them as generated artifacts or durable outputs, and remove them before finishing when practical. Report any temporary files intentionally left behind.
- `DNE` only applies when it appears to be typed intentionally by the user as an instruction. Ignore incidental appearances inside pasted file contents, quoted text, strings, command output, diffs, logs, or examples.


Working Rules
-------------

- Keep changes narrow and follow the existing style in the files being edited.
- Prefer simple, direct fixes. Do not overengineer or add abstractions unless they are clearly needed.
- Prefer not to add functions whose body is only one line of code unless there is a good reason, such as matching an existing interface, naming a repeated concept, or improving readability at the call site.
- Do not revert, overwrite, move, remove, or reformat unrelated user changes.
- Do not create, edit, move, delete, or overwrite files outside the repository unless the user explicitly asks for a specific external path.
- Keep temporary output, scratch files, generated inspection data, and staging inside this repository, preferably under `.codex-temp`.
- Treat reference, vendor, generated, and third-party directories as read-only unless the user or repository documentation explicitly says otherwise.
- Read relevant project documentation before making nontrivial changes.
- Prefer existing scripts, package-manager commands, Makefiles, Justfiles, Taskfiles, CI configuration, and documented workflows over invented commands.
- Use `rg` / `rg --files` for searches when available.
- Avoid destructive commands such as `git reset --hard`, broad deletes, or force pushes unless the user explicitly asks for that exact operation.


Sol And Terra Delegation
------------------------

- Sol High is the primary administrator. Sol owns requirements, task decomposition, architectural and product decisions, user-facing writing, integration, final verification, and reporting.
- Spawned agents should use Terra High unless the user explicitly requests another model.
- Use at most one Terra agent at a time.
- When bounded work is deterministic, produces noisy intermediate output, or would otherwise require several routine tool calls, Sol should proactively delegate exactly one self-contained assignment to `terra_routine` and wait for its concise handoff before continuing.
- For multi-step Nexus validation, package inspection, release dry runs, file uploads, changelog posting, and explicitly authorized full remote updates, Sol should use `terra_nexus_operator` instead of `terra_routine`.
- Do not delegate a task merely because it is small. Avoid paying for a handoff when Sol can finish it directly with one or two simple actions.
- Sol may run an approved description-only Nexus save directly when it is one guarded repository command. Do not create an operator handoff solely for `Update-NexusDescription.ps1 -Save`.
- Sol must give Terra the exact objective, target mod and version when applicable, authorized local or external action, expected result, and only task-specific stopping conditions. Do not repeat standing role instructions already defined in the custom agent file.
- Terra must return a concise result. Sol should review the result and relevant diff instead of repeating Terra's complete investigation.
- Suitable Terra work includes targeted searches, inventories, builds, tests, log summaries, metadata validation, package inspection, consistency checks, and fully specified mechanical edits.
- Keep ambiguous diagnosis, feature design, behavioral implementation, configuration-schema decisions, version decisions, user-facing release writing, changelog consolidation, and final review with Sol.
- For Nexus work, Sol writes and approves all descriptions and changelog text. Terra may validate, dry-run, operate the existing Nexus tools, and verify the remote result only when the current user request explicitly authorizes that exact remote operation. A full-update assignment should authorize the normal same-version fallback: if the target file is already the active primary version and its changelog exactly matches the approved local payload, retain both and complete only the description save instead of stopping or duplicating remote data.
- External publishing, commits, pushes, destructive operations, and live-game changes are never implied by delegation.


Shell Reliability
-----------------

- The shell may start outside the repository even when a workspace root is provided.
- Before broad searches, recursive commands, builds, tests, or git operations, verify the current location or target the repository root explicitly.
- Prefer commands that set their working directory explicitly, such as `git -C <repo> ...`, when the repository path is known.
- Do not assume relative paths resolve from the repository root unless the command sets location itself.
- If a command unexpectedly lands outside the repository, stop and rerun it with an explicit repository path.


Project Discovery
-----------------

- Identify the repository root from git, workspace context, or the nearest relevant project manifest.
- Treat `README.md` as the likely main project document when present.
- Also check relevant local documentation such as `CONTRIBUTING.md`, `docs/`, package manifests, build files, CI workflows, and tool configuration when needed.
- Determine build, test, lint, format, and typecheck commands from project docs or configuration before running them.
- If multiple plausible commands exist and the right one matters, report the candidates and ask or choose the smallest clearly relevant one.
- Do not assume a language, framework, package manager, build system, or test runner that is not present in the repository.


Local Paths And Tooling
-----------------------

- Current local Tainted Grail: The Fall of Avalon game root: `G:\Steam\steamapps\common\Tainted Grail FoA`.
- Game managed assemblies are under `G:\Steam\steamapps\common\Tainted Grail FoA\Fall of Avalon_Data\Managed`; the primary game assembly for BepInEx mod inspection is `TG.Main.dll`.
- Game addressable assets are under `G:\Steam\steamapps\common\Tainted Grail FoA\Fall of Avalon_Data\StreamingAssets\aa`.
- Do not edit the live game install. It may be read for compile references, diagnostics, and game-file inspection only unless the user explicitly asks for a specific live-game change.
- .NET SDK is installed at `C:\Program Files\dotnet\sdk\10.0.302`; prefer the x64 `dotnet` host at `C:\Program Files\dotnet\dotnet.exe`.
- Build scripts still resolve the game root from `-GameRoot`, then `TAINTED_GRAIL_FOA_DIR`, then the known local Steam path. Prefer passing `-GameRoot` only when the auto-detected path is wrong.
- Game inspection tools live under `tools/game-inspection/`.
- Use `tools/game-inspection/Invoke-ILSpy.ps1` for managed assembly decompilation and metadata inspection.
- Use `tools/game-inspection/Invoke-UnityPython.ps1` for UnityPy, dnfile, pefile, and other repo-local Python inspection packages.
- Use `tools/game-inspection/Invoke-AssetRipper.ps1` for AssetRipper. Prefer `--headless` when launching it for non-interactive investigation.
- Treat `tools/game-inspection/downloads/`, extracted tool package directories, and generated inspection output as tool/cache material. Do not hand-edit downloaded tool contents unless replacing or upgrading the tool intentionally.


Keyword Commands
----------------

Codex chat messages may trigger generic keyword commands.

- A keyword command triggers only when the full user message clearly invokes one of the supported commands.
- Clear invocations include a command on its own line, a command followed by `:`, `-`, or context, or phrasing such as "run DIFF", "please DIFF", "do AUDIT", or "use COMMIT".
- Context may appear before or after the command token, or in nearby plain-text lines. Use it to narrow the command's behavior, such as ignored files, focus areas, or commit-plan preferences.
- Do not trigger commands from quoted text, pasted output, file contents, diffs, examples, fenced code blocks, command lists, questions about a command, or incidental prose where the user is discussing a command rather than asking to run it.
- `help` is the only lowercase command. All supported command names are uppercase.
- If an unknown uppercase single-word command is received, reply with `Unknown command. Type help.`
- Commands must still follow all safety, staging, commit, verification, and external-path rules in this file.

`help` prints this command list quickly, alphabetically, with one short line per command:

```text
AUDIT   Audit recent changes end to end without editing.
COMMIT  Execute the latest DIFF commit proposal.
DIFF    Show current changes and propose commit splits.
LOGS    Inspect the newest game log for the requested mod.
MSG     Generate a commit message for staged files.
NEXUS   Compare live Nexus state with local releases and offer needed updates.
TEST    List or update the requested mod's in-game tests.
```


Command Behavior
----------------

- `AUDIT`: Perform a read-only audit of recent substantial changes. Inspect relevant status, diffs, affected files, missed call sites, stale docs/config, missing generated artifacts, unsafe file operations, and verification gaps. Do not edit, stage, commit, build, format, generate files, or launch external editors. Report findings first by severity with file/line references when possible; if no issues are found, say so clearly and list any residual risk or checks not run.
- `DIFF`: Read current git status, diff stats, and important changed files without modifying the worktree. Propose intelligent commit groups with file lists and commit messages. Use multiple commits when changes are independently useful or independently revertible. Follow the repository's existing commit style when obvious; otherwise use concise conventional-style subjects. State that `COMMIT` will execute the proposal if the worktree is unchanged.
- `COMMIT`: Execute the latest `DIFF` proposal only if it still matches the worktree. If no current proposal exists, or the worktree has changed since the proposal, run `DIFF` behavior and stop instead of committing. When executing, stage only the proposed files for each commit, run `git diff --cached --check` before each commit, commit with the proposed messages, and report commit hashes plus final status.
- `LOGS [mod]`: Inspect the newest available BepInEx session log for one mod without modifying any file. The mod argument is optional, so `LOGS` is a complete standalone command. Resolve an explicit mod from its display name, package or folder name, plugin GUID, or an unambiguous common abbreviation such as `EITD`, `GFT`, `GUI`, or `UW`. Without an explicit mod, make a best-effort contextual choice: prefer the mod most recently mentioned or worked on, then the single clear mod in the active discussion. State the inferred scope briefly and proceed; ask which mod only when the conversation provides no reasonable candidate. Read the current log under `G:\Steam\steamapps\common\Tainted Grail FoA\BepInEx`, confirm the logged plugin version when possible, and inspect matching startup lines, diagnostics, warnings, exceptions, and adjacent stack-trace context. Distinguish mod findings from unrelated log noise, lead with actionable errors or warnings, and say clearly when the requested mod or expected version is absent. `LOGS` is diagnostic only: do not edit test matrices, source, config, the live game, or any other persistent state.
- `MSG`: Inspect only staged files and the staged diff needed to understand them. Generate a commit message that follows the repository's existing style when obvious; otherwise use concise conventional-style wording. Do not inspect unstaged changes, edit files, stage, commit, build, format, generate files, or launch external editors. If nothing is staged, say so and stop.
- `NEXUS [mod]`: Audit Nexus publishing state without changing Nexus. With no mod argument, include every locally authored mod that has a configured Nexus page and file group; with an argument, resolve it using the same rules as `LOGS`. Start with one `tools/Get-NexusLiveState.ps1` comparison pass. Reuse fresh verified observations, then refresh only stale or unknown surfaces needed for the result, querying each unique Nexus file group once and reviewing each unique Nexus page once so shared pages such as KS Addons are not reopened per add-on. Never treat stale or unknown evidence as proof that a surface is current or needs an update. Report a compact table sorted by display name with `Mod`, `Nexus version`, `Local version`, `File`, and `Full description`; use `Current`, `Update`, or `Verify` for the last two columns, and add concise notes only for independently drifting short descriptions, file descriptions, changelogs, missing metadata, or blocked verification. Do not inspect unrelated git changes, build, package, upload, post changelogs, or save descriptions during this audit. If every verified surface is current, say `No Nexus updates needed.` and do not request confirmation. If one or more updates are verified, list the exact proposed mod/surface updates and end with exactly `Please reply with yes to update.`
- As part of the `NEXUS` audit, resolve each file group's live Nexus version as the feature-review baseline and review every newer local `CHANGELOG.txt` block through the target version, using the same final-state editorial judgment as a consolidated Nexus changelog. Identify durable major features, integrations, compatibility changes, and player-facing behavior that remain true in the target release; omit superseded implementations, routine fixes, tuning, and version-history narration. Check whether `nexus-full-desc.txt` accurately communicates those major additions even when its text currently matches Nexus. When it does not, mark `Full description` as `Update` and propose only the lightest edits needed to fold the final behavior into the most relevant existing sections while preserving the page's voice, structure, and useful copy. Do not edit the local description files during the audit; apply the proposed editorial changes after the user gives the general update go-ahead.
- Prefer to leave `nexus-short-desc.txt` and `nexus-file-desc.txt` unchanged. A major new feature alone is not enough to rewrite either pitch. Update one only when the cumulative release changes the mod's central identity, player fantasy, primary use case, or core promise enough that the existing text has become materially misleading or incomplete. Ordinary feature growth, new settings, integrations, fixes, compatibility work, and tuning belong in the full description and changelog instead. When an identity-level rewrite is justified, keep the short and file descriptions distinct, retain their existing length and style constraints, and include each proposed rewrite explicitly in the `NEXUS` update list; otherwise do not touch them.
- Treat the user's next clear affirmative reply to an unresolved `NEXUS` audit as the general go-ahead to update. Accept natural replies such as `yes`, `go ahead`, `update them`, or `do it`, including replies that add or narrow instructions; do not require a bare `yes`, an exact phrase, or unchanged wording. Reread the current local metadata and live Nexus state, apply any instructions in that reply, and proceed without asking again when the update intent remains clear. Ask only when the new reply makes the target materially ambiguous or requires a new content or version decision. Use the repository's existing Nexus validation, dry-run, publishing, and verification tools; apply the normal full-update behavior when a file version is behind, use a description-only save when the file is already current, and never duplicate an already-current file version or changelog. Report completed, skipped, and failed items, including any partial remote success. An affirmative reply with no identifiable pending Nexus update must not publish anything; explain that `NEXUS` must be run first.
- `TEST [mod]`: Find the mod's authored `TEST-MATRIX.md` and show the in-game checks that are not `Passed`. The mod argument is optional, so `TEST` is a complete standalone command. Resolve its explicit or inferred scope using the same best-effort rules as `LOGS`, including briefly stating an inferred mod and proceeding whenever the context supplies a reasonable candidate. Default to the focused release-smoke table when one exists; `TEST [mod] ALL` shows every non-passed matrix row instead. If no authored matrix exists, say so and stop rather than creating one. Number the displayed rows from 1 for this response and include each permanent test ID, description or expected result, and current status. Listing tests is read-only and must not infer results from logs or automated checks.
- `TEST` result updates apply only to the most recently displayed numbered list in the current conversation. Accept explicit grouped results such as `TEST: pass 1,2,3; almost 4; adjust 5` or equivalent unambiguous wording. Map `pass` to `Passed`, `almost` to `Almost working`, and `adjust` to `Needs adjustment`. Update only the numbered rows explicitly named; omitted rows and detailed rows merely referenced by a displayed smoke test remain unchanged. If there is no current numbered list, a number is outside that list, the intended result is ambiguous, or the matrix changed after the list was displayed, do not guess or partially apply the update: redisplay the current needed tests and request a corrected result line. After a successful update, report the permanent IDs and old-to-new statuses changed plus the number of tests still not passed. `Almost working` and `Needs adjustment` remain in later needed-test lists.


Verification
------------

- Run the smallest relevant check for the files changed.
- Prefer verification commands documented by the project.
- If no verification command exists, say so clearly.
- If verification cannot be run, report why.
- Do not run formatters, linters with autofix, code generators, migrations, or other write-producing checks unless the user requested that action or the repository instructions require it.


Commits
-------

- Follow the repository's existing commit style.
- If no style is obvious, use concise conventional-style subjects, for example:
  - `fix: handle empty config`
  - `docs: clarify setup steps`
  - `test: cover parser fallback`
- Split commits when changes are independently useful or independently revertible.
- Keep generated artifacts in the same commit as the source change that produced them unless repository instructions say otherwise.
- Do not stage or commit unrelated changes.


Grailwright Mod Development And Publishing
-------------------------------------------

- Grailwright is the shared development repo for Keenan's Tainted Grail: The Fall of Avalon BepInEx 5 Mono mods.
- Treat `mods/` as the source of truth for authored mod packages.
- Keep each mod's authored source, package metadata, docs, and runtime assets under its own folder in `mods/`.
- Keep KS Fixes suite components as individual mod folders under `mods/KSAddons/`, not as loose files at the repository root.
- Keep shared tooling under `tools/`; do not copy build, export, audio, or staging scripts back into individual mod folders.
- Prefer updating a mod's `mod.json` when its version, DLL name, package name, source file, package layout, or Nexus-facing identity changes.
- When a mod's `mod.json` version, display name, package path, Nexus status, or published Nexus URL changes, update the top-level `README.md` Current Mods table in the same change. Before `DIFF`, `COMMIT`, or release packaging, compare changed mod versions against the README table and report any mismatch. Do not commit a mod version bump with a stale README row unless the user explicitly excludes README updates.
- Grailwright authored mod versions must use `MAJOR.MINOR.PATCH`, where `MAJOR` may contain one or more digits and both `MINOR` and `PATCH` must be single digits from 0 through 9. Versions such as `11.1.1` are valid; versions such as `1.10.0` and `1.1.10` are invalid. When a patch sequence would roll from `X.Y.9` to `X.Y.10`, bump the minor version instead, such as `2.0.9` to `2.1.0`. When a minor sequence would roll from `X.9.9`, bump the major version and reset both following components, such as `1.9.9` to `2.0.0`.
- Use `ks.tgfoa.<mod-slug>` for BepInEx plugin GUIDs and generated config filenames. Do not use personal-name prefixes or bare `tgfoa` GUIDs for authored mods.
- When a plugin GUID changes, update the source constant, `mod.json`, README, changelog, Nexus description, and any template/docs references together.
- Any Grailwright BepInEx plugin that binds config options must define and bind `ConfigSchemaVersion`. Treat it as a cleanup/default compatibility boundary, not as a general layout or release counter.
- Do not prefix config section names with numbers. Give every player-visible setting explicit FoA Mod Manager metadata for both `SectionOrder` and `Order`; bind order alone does not control the manager, whose fallback is alphabetical. Put the primary enable or mode controls first, keep related everyday settings together, and, when those sections apply to the mod, place `Diagnostics` second-to-last and the explicit `Import Previous Settings` section last. Hidden schema markers do not need display-order metadata.
- Do not increment `ConfigSchemaVersion` when only adding settings. Let `Config.Bind` add each new setting with its current default while retaining the existing config.
- Increment `ConfigSchemaVersion` when a setting is removed, renamed, moved to another section, given a materially different default, given a different type or meaning, or can no longer interpret an old value safely. Description, ordering, visibility, and other metadata-only changes do not require an increment. Compatible range expansions do not require an increment; range changes that invalidate old values do.
- Every schema increment must state its specific reset reason in the changelog. Do not increment the schema without one of the qualifying reasons above.
- Every config-owning mod must compile `tools/shared/ConfigPreviousSettingsRecovery.cs`, define a fixed `ConfigRecoveryBaselineSchema`, define its per-schema `ConfigRecoveryKeepCurrentDefaultRules`, define its `ConfigRecoveryPermanentExclusions`, and bind recovery after its normal settings. Set the baseline to the current schema when recovery is first introduced and do not advance it on later schema changes. The explicit final FoA Mod Manager tab named `Import Previous Settings` must always remain available, show the current and newest compatible backup schemas, and keep its one-shot import action safe when no compatible backup exists.
- On a schema mismatch, back up the stale or unversioned config beside the active `.cfg`, clear/reload the config, bind and save regenerated defaults, then automatically restore only the mod's approved durable settings that differ from their recorded previous defaults. Use exact current section and setting names and current types, apply the shared per-schema safety rules, clamp numeric values to current supported ranges, and skip invalid values.
- Manual previous-settings import must remain conservative and transactional: use the newest supported pre-schema backup, import only values that differ from that backup's recorded defaults, require an exact current section/key and type, apply current acceptable-value clamping, skip new/removed/renamed/invalid settings, create a pre-import backup, and leave current defaults in place when compatibility is uncertain.
- Add a `ConfigRecoveryKeepCurrentDefaultRule` for an exact same-name setting whenever a schema change gives it a new meaning, makes old customized values unsafe, or requires the new default even for users who customized the old value. A normal default change does not need a rule when old customized values remain valid: an untouched old default is already excluded from recovery. Removed and renamed settings are skipped naturally.
- Permanently exclude one-shot actions, pseudo-buttons, preset triggers, and derived status or informational entries from automatic preservation and manual import. Keep the per-mod exclusion array explicit even when it is empty.
- Keep automatic durable-setting preservation and manual import on the same shared typed customization and safety-rule policy. Automatic preservation must retrieve typed customized values with the shared profile and restore them through the shared helper so current `AcceptableValues` perform validation and clamping; do not duplicate stale-value parsers, raw-string comparisons, or range-clamping logic in individual mods. The recovery contract must dynamically discover every authored mod that binds config, verify normal settings are bound before recovery, and verify exact permanent-exclusion wiring so a new owner or unsafe setting cannot silently omit the infrastructure. Run `tools/Test-ConfigRecoveryContracts.ps1` and `tools/Test-ConfigPreservationContracts.ps1` after changing schemas, recovery rules, config manifests, or preservation behavior.
- Prefer schema reset over carrying old per-setting migration code. Do not add backwards-compatible migrations unless the user explicitly requests them.
- Do not add or use a repo-local `dist` folder.
- Use `tools/Build-Mod.ps1` or `tools/Build-All.ps1` for builds so `.codex-temp\locks\mod-<package>.lock` coordinates concurrent work on the same mod.
- Use `tools/Publish-NexusMod.ps1` and `tools/Update-NexusDescription.ps1` for Nexus updates so `.codex-temp\locks\nexus.lock` serializes remote uploads, changelogs, short descriptions, and full descriptions across Codex threads.
- Successful Nexus version uploads must retain their ignored `nexus-release-receipts.local.json` entry with the immutable v3 version ID, Vortex `game_scoped_id` when available, Nexus page and file-group IDs, and exact archive fingerprints. Do not create receipts for dry runs or description-only saves, fabricate a missing Vortex file ID, discard an existing receipt during cleanup, or retry an upload merely because receipt enrichment or changelog posting failed after the remote version was created.
- If a Grailwright lock conflict appears, stop and report the owner details. Use `-LockWaitSeconds` only when waiting is appropriate, and use `-ForceStaleLock` only after confirming the recorded owner process is gone or stale.
- Every ordinary mod build must stage to Vortex, including implementation, diagnostic, verification, and iteration builds. `Build-Mod.ps1` and `Build-All.ps1` do this by default and keep their intermediate zips under `.codex-temp`; do not redirect an ordinary build to the Desktop or suppress staging.
- Do not remove, replace, rename, overwrite, or otherwise clean up older mod versions from Vortex staging unless the user explicitly asks for that exact cleanup. Staging a newer version does not authorize removal of any existing staged version; leave parallel staged versions available for rollback and comparison.
- A Desktop export is an explicit exception only when the user says "send to Desktop", "Desktop only", or equivalent. Use `-DesktopOnly`; do not stage that build to Vortex.
- Never remove or clean up older package archives from the Desktop or another destination unless the user explicitly requests that exact cleanup.
- Use `-PackageOnly` with an explicit `-DestinationDirectory` only for workflows such as Nexus publishing that need an archive without Vortex staging. Do not use it for ordinary or user-testable builds.
- Release zip filenames should use the readable display name plus version, for example `No Player Light 1.0.2.zip`; the zip payload should still use the compact package folder from `mod.json`.
- Exported zips must contain exactly one top-level mod folder with the DLL, README/changelog docs, and runtime assets inside it. This prevents Vortex from flattening conflicting README or changelog files across mods.
- Exported zips must not contain `src`, `tools`, `mod.json`, `API.txt`, `nexus-full-desc.txt`, `nexus-short-desc.txt`, `nexus-file-desc.txt`, `nexus-changelog.txt`, `.codex-temp`, git files, or repo-only build/publishing scaffolding.
- Keep top-level `README.txt` files as packaged installed-user quick references. They should summarize what the mod does, the current version, config path, defaults, custom assets, compatibility notes, and troubleshooting only when relevant.
- Keep folder-specific README files only when the folder needs runtime instructions, such as audio or custom asset naming rules.
- Keep full Nexus page copy in `nexus-full-desc.txt`; do not duplicate the whole Nexus description into README files.
- Keep `nexus-short-desc.txt` as the Nexus page short description, 350 characters max.
- Keep `nexus-file-desc.txt` as a stable, concise, and persuasive file-row pitch. Favor flavorful plain language that sells the player's fantasy, experience, or payoff over a dry inventory of mechanics, settings, and technical terms, while still making the mod's purpose clear. It must be shorter than `nexus-short-desc.txt` and must not be copied from, or lightly reworded from, the short description. Do not use it for version-specific changelog notes; those belong only in `CHANGELOG.txt` and Nexus changelog entries.
- Nexus description files use Nexus BBCode, not Markdown fenced code blocks. Do not use language-qualified code tags such as `[code=plaintext]`; Nexus may render them literally.
- Use plain ASCII punctuation in `nexus-full-desc.txt`, `nexus-short-desc.txt`, and `nexus-file-desc.txt`. Use straight apostrophes (`'`) and quotation marks (`"`) instead of curly variants such as `’`, and avoid typographic dashes, ellipses, nonbreaking spaces, and other unusual Unicode punctuation.
- Nexus descriptions should describe config schema behavior generically. Avoid listing specific schema numbers, GUID history, or individual config-change reasons unless the user asks for that detail.
- In Nexus descriptions, link the first nearby mention of each published mod name to its Nexus page with `[url=...]Mod Name[/url]`; leave repeated mentions in the same paragraph, list, or nearby section unlinked or bold. Avoid self-linking the page title/current mod unless it genuinely helps a bundled-mod page.
- In Nexus Compatibility sections, list linked Grailwright mods first under `[b]Grailwright mods[/b]`, followed by third-party mods under `[b]Other mods[/b]`. Alphabetize each group by displayed mod name and omit a group when it would be empty.
- Do not include version numbers or phrases such as `or newer` on Grailwright mod entries in Compatibility. Put a genuinely required minimum API or dependency version in Requirements or the relevant integration details instead.
- Use this standard opening for the Grail Floating Text bullet in Compatibility when it accurately describes the mod: `[url=https://www.nexusmods.com/taintedgrailthefallofavalon/mods/247]Grail Floating Text[/url] can show compatibility conflicts, critical load errors, and useful debug info in-game when diagnostics are enabled.` This sentence is generally enough. Expand the bullet only with concise behavior that is useful for players to know; keep implementation detail and extensive integration guidance in the relevant feature, configuration, or requirements section. Omit the bullet from Grail Floating Text's own page and from mods that do not integrate with it.
- For tabular data in Nexus descriptions, use pipe tables inside plain `[code]...[/code]` blocks with leading/trailing pipes and a separator row. Prefer table form for aligned settings, presets, tuning values, and comparison data.
- When the user asks to "update the Nexus mod", "upload to Nexus", "publish to Nexus", or similar without narrowing the scope, treat it as the full Nexus update: upload/update the mod file/version with `nexus-file-desc.txt`, update the Nexus short description from `nexus-short-desc.txt`, update the full page description from `nexus-full-desc.txt`, add the current changelog when applicable, and verify the uploaded file/version and saved descriptions afterward.
- KS Addons share one Nexus page across unrelated add-ons. For an individual mod under `mods/KSAddons/`, each Nexus changelog payload must begin with that add-on's `mod.json` display name on its own line, followed immediately by the normal change lines. `Publish-NexusMod.ps1` adds this heading automatically; do not duplicate it in `CHANGELOG.txt` or `nexus-changelog.txt`. Ordinary mods keep the standard unprefixed changelog format.
- Keep the KS Addons `nexus-full-desc.txt` Included Addons list, addon detail sections, and Install path examples alphabetical by parent mod name.
- When a Nexus upload advances past one or more local versions, resolve the file group's current Nexus version from its configured `GroupId` immediately before upload and collect every newer local `CHANGELOG.txt` block through the target version, newest first. Post the result as one Nexus changelog entry under the target version only: never include intermediate `Version X.Y.Z` headings or the already-published baseline block in the Nexus payload.
- Consolidated Nexus changelogs require a light editorial pass. When several local versions revise the same feature, describe its final released behavior once and omit superseded intermediate implementations, temporary fixes, and repeated wording. Preserve distinct user-facing changes plus compatibility, configuration-reset, or migration consequences that still apply. Do not mechanically concatenate redundant history.
- Keep the reviewed current-release consolidation in `nexus-changelog.txt` beside the mod when an upload spans multiple local versions. Its first two nonblank lines must be `TargetVersion=X.Y.Z` and `BaselineVersion=A.B.C`, followed only by the final Nexus change lines. Do not use Markdown bullets, embedded version headings, work-in-progress markers, or duplicate lines. `Publish-NexusMod.ps1` must reject a missing or stale reviewed consolidation for multi-version uploads and may generate a raw candidate under `.codex-temp` for review. Keep `CHANGELOG.txt` unchanged as the complete version-by-version history.
- Keep `CHANGELOG.txt` plain text, newest first, and Nexus-pasteable. Version blocks must use `Version X.Y.Z`, immediately followed by change lines with no blank line after the version header, one blank line between version blocks, and no Markdown bullets.
- Do not add changelog entries whose only purpose is to say something did not change, such as "No audio, loop, or music behavior changes." Changelogs should list meaningful user-facing or technical changes, not absence-of-change reassurance.
- Build and export with `tools/Build-Mod.ps1` or `tools/Build-All.ps1` unless a more specific repo tool is documented.
- Use `-SkipCompile` only when intentionally repacking docs/assets around an already-built DLL.
- `Build-Mod.ps1` and `Build-All.ps1` stage to Vortex by default. `-StageToVortex` remains an explicit compatibility switch, while `-DesktopOnly` and `-PackageOnly` are the only non-staging output modes.
- Vortex staging means copying the exported package into the Vortex mods directory as a new folder for a new version. It must not edit Vortex metadata, profile state, enablement, deployment state, or `state.v2`.
- Staging must refuse to overwrite an existing same-version Vortex mod folder. Bump the mod version or ask the user before replacing an existing staged folder.
- Do not deploy Vortex mods, change active variants, or enable/disable Vortex mods unless the user explicitly asks.
- Do not edit live game install folders unless the user explicitly gives a specific external path and asks for that change.
- Keep temporary repo work under `.codex-temp` and remove it after verification when practical.
- When validating package output, inspect zip contents for top-level folder shape and excluded source/tool files.
- When validating audio assets, keep runtime WAVs in mod asset folders and shared conversion/normalization scripts in `tools/audio/`.


Nexus Publishing Secrets
------------------------

- Never store Nexus API keys, bearer tokens, passwords, or upload credentials in `mod.json`, README files, changelogs, Nexus description files, scripts, examples, or committed configuration.
- Nexus publishing tools must read credentials from the `NEXUS_API_KEY` environment variable only.
- Local secret files such as `.env`, `*.secret`, `*.secrets`, `*.local.json`, and `NexusApiKey.txt` are ignored and must remain untracked.
- Nexus mod IDs, game domains, game-scoped mod IDs, and file/group IDs may be stored as publishing metadata because they are not credentials.
- Per-mod `API.txt` files may store local non-secret Nexus publishing metadata such as `NexusUrl`, `ModId`, `GameDomain`, and `GroupId`, but must not store personal API keys or tokens.
