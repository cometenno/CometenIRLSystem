# Publishing the GitHub Wiki

The repository contains the maintained Wiki source in `wiki/`.

GitHub stores the rendered Wiki in a separate Git repository:

```text
CometenIRLSystem.wiki.git
```

The Wiki feature is enabled and the Wiki has been initialized.

## Automatic publishing

The main repository now includes:

```text
.github/workflows/publish-wiki.yml
```

Any push to `main` that changes `wiki/**` automatically synchronizes the Markdown files into the GitHub Wiki repository.

The workflow:

1. checks out the main repository
2. clones `CometenIRLSystem.wiki.git`
3. replaces the Wiki Markdown files with the maintained files from `wiki/`
4. commits only when something changed
5. pushes the synchronized Wiki

The workflow can also be started manually with GitHub Actions `workflow_dispatch`.

## Source of truth

The detailed technical documentation under `docs/` remains the canonical reference for implementation and setup details.

The `wiki/` directory is the maintained navigation/front-door layer for GitHub Wiki. When behavior changes:

1. update the relevant canonical `docs/` page
2. update the matching Wiki page when needed
3. commit the changes to `main`
4. the Wiki publish workflow performs the Wiki sync automatically

## Local fallback publisher

A Windows/PowerShell fallback is retained:

```powershell
.\scripts\publish-wiki.ps1
```

It clones:

```text
https://github.com/la1ona/CometenIRLSystem.wiki.git
```

copies all Markdown files from `wiki/`, commits any changes and pushes them.

## Manual fallback

If automatic publishing is unavailable, the Wiki can still be updated manually:

```bash
git clone https://github.com/la1ona/CometenIRLSystem.wiki.git
cd CometenIRLSystem.wiki
```

Copy the Markdown files from the main repository `wiki/` directory, preserving names such as:

```text
Home.md
_Sidebar.md
Installation.md
Module-Overview.md
Commands.md
Security.md
Troubleshooting.md
```

Then:

```bash
git add -A
git commit -m "Sync Cometen IRL System Wiki"
git push
```

## Security

Never put private configuration, sender/receiver tokens, database passwords, BELABOX stream IDs or private Browser Source URLs in Wiki pages.
