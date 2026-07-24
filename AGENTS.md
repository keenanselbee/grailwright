AGENTS.md
=========

This file provides general working rules for coding agents in this repository.
It extends the shared template at `C:\Repositories\AGENTS.md` with Grailwright-specific mod packaging, build, and staging rules.


Command Speed Rules
-------------------

- Zero-tool commands must not inspect files, run shell commands, check git status, summarize context, or add extra explanation.
- `help` is the only zero-tool command. Reply immediately from the command list in the Keyword Commands section.
- Direct-action commands should skip unrelated repo inspection, git status checks, diff reading, and planning. Execute only their defined workflow, then report the result.
- Direct-action commands are `AUDIT`, `COMMIT`, `DIFF`, and `MSG`.


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


SKSE Plugin Repositories
------------------------

- For Skyrim SKSE plugin work, prefer the repository's documented wrapper script, such as `tools/build-skse-plugin.ps1`, over raw `cl`, `cmake`, `ninja`, `xmake`, or Visual Studio commands.
- Do not assume `cl`, `cmake`, or `ninja` are on the shell PATH. Let the repo wrapper locate MSVC and any repo-local or shared xmake tooling.
- Do not copy Iron Soul's dev environment into another repository unless the user explicitly asks. A repo may instead declare a symlinked SKSE project or shared xmake tool/cache path in its own `AGENTS.md`.
- Follow symlinked project roots only when the repo-specific `AGENTS.md` explicitly names them as intentional editable paths. Otherwise treat symlink targets outside the repo as external and read-only.
- Verify-only native builds must not refresh shipped DLLs. DLL refresh builds may copy only the successful release DLL into `mod/SKSE/plugins`.
- Keep refreshed DLLs in the same commit as the native source change that produced them unless the user explicitly asks for a DLL-only refresh.
- Do not run dependency upgrades, delete package caches, or change global Git `safe.directory` settings unless the user explicitly asks.


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
MSG     Generate a commit message for staged files.
```


Command Behavior
----------------

- `AUDIT`: Perform a read-only audit of recent substantial changes. Inspect relevant status, diffs, affected files, missed call sites, stale docs/config, missing generated artifacts, unsafe file operations, and verification gaps. Do not edit, stage, commit, build, format, generate files, or launch external editors. Report findings first by severity with file/line references when possible; if no issues are found, say so clearly and list any residual risk or checks not run.
- `DIFF`: Read current git status, diff stats, and important changed files without modifying the worktree. Propose intelligent commit groups with file lists and commit messages. Use multiple commits when changes are independently useful or independently revertible. Follow the repository's existing commit style when obvious; otherwise use concise conventional-style subjects. State that `COMMIT` will execute the proposal if the worktree is unchanged.
- `COMMIT`: Execute the latest `DIFF` proposal only if it still matches the worktree. If no current proposal exists, or the worktree has changed since the proposal, run `DIFF` behavior and stop instead of committing. When executing, stage only the proposed files for each commit, run `git diff --cached --check` before each commit, commit with the proposed messages, and report commit hashes plus final status.
- `MSG`: Inspect only staged files and the staged diff needed to understand them. Generate a commit message that follows the repository's existing style when obvious; otherwise use concise conventional-style wording. Do not inspect unstaged changes, edit files, stage, commit, build, format, generate files, or launch external editors. If nothing is staged, say so and stop.


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


Repository-Specific Notes
-------------------------

- Grailwright is the shared development repo for Keenan's Tainted Grail: The Fall of Avalon BepInEx 5 Mono mods.
- Treat `mods/` as the source of truth for authored mod packages.
- Keep each mod's authored source, package metadata, docs, and runtime assets under its own folder in `mods/`.
- Keep KS Fixes suite components as individual mod folders under `mods/KSAddons/`, not as loose files at the repository root.
- Keep shared tooling under `tools/`; do not copy build, export, audio, or staging scripts back into individual mod folders.
- Prefer updating a mod's `mod.json` when its version, DLL name, package name, source file, package layout, or Nexus-facing identity changes.
- Use `ks.tgfoa.<mod-slug>` for BepInEx plugin GUIDs and generated config filenames. Do not use personal-name prefixes or bare `tgfoa` GUIDs for authored mods.
- When a plugin GUID changes, update the source constant, `mod.json`, README, changelog, Nexus description, and any template/docs references together.
- Any Grailwright BepInEx plugin that binds config options must define and bind `ConfigSchemaVersion`, back up stale or unversioned configs beside the active `.cfg`, clear/reload the config, and save regenerated defaults. Increment the schema only when settings are added, removed, renamed, reinterpreted, or defaults materially change. Prefer schema reset over carrying old per-setting migration code.
- Do not add or use a repo-local `dist` folder. Release zips should go to the user's Desktop by default.
- When exporting a new version to the Desktop, remove older same-package version zips so the Desktop keeps only the latest built zip for each mod.
- Release zip filenames should use the readable display name plus version, for example `No Player Light 1.0.2.zip`; the zip payload should still use the compact package folder from `mod.json`.
- Exported zips must contain exactly one top-level mod folder with the DLL, README/changelog docs, and runtime assets inside it. This prevents Vortex from flattening conflicting README or changelog files across mods.
- Exported zips must not contain `src`, `tools`, `mod.json`, `API.txt`, `nexus-desc.txt`, `nexus-page-summary.txt`, `nexus-file-summary.txt`, `.codex-temp`, git files, or repo-only build/publishing scaffolding.
- Keep top-level `README.txt` files as packaged installed-user quick references. They should summarize what the mod does, the current version, config path, defaults, custom assets, compatibility notes, and troubleshooting only when relevant.
- Keep folder-specific README files only when the folder needs runtime instructions, such as audio or custom asset naming rules.
- Keep full Nexus page copy in `nexus-desc.txt`; do not duplicate the whole Nexus description into README files.
- Keep `nexus-file-summary.txt` as a stable one- or two-sentence file-row description of what the mod does. Do not use it for version-specific changelog notes; those belong only in `CHANGELOG.txt` and Nexus changelog entries.
- Keep `CHANGELOG.txt` plain text, newest first, and Nexus-pasteable. Version blocks must use `Version X.Y.Z`, immediately followed by change lines with no blank line after the version header, one blank line between version blocks, and no Markdown bullets.
- Do not add changelog entries whose only purpose is to say something did not change, such as "No audio, loop, or music behavior changes." Changelogs should list meaningful user-facing or technical changes, not absence-of-change reassurance.
- Build and export with `tools/Build-Mod.ps1` or `tools/Build-All.ps1` unless a more specific repo tool is documented.
- Use `-SkipCompile` only when intentionally repacking docs/assets around an already-built DLL.
- Use `-StageToVortex` only when the user asks to stage a built mod into Vortex.
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
