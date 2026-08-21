# Publishing the GitHub Wiki

The repository contains Wiki-ready source files in `wiki/`.

GitHub stores the actual Wiki in a separate Git repository named:

```text
CometenIRLAlerts.wiki.git
```

The main repository currently needs the Wiki feature enabled and initialized before these pages can be pushed there.

## 1. Enable Wiki

On GitHub:

```text
Repository -> Settings -> General -> Features -> Wikis
```

Enable **Wikis**.

## 2. Initialize the Wiki once

Open the new **Wiki** tab and create the first Home page. The content can be temporary; the files from `wiki/` will replace it.

This creates the separate `.wiki.git` repository.

## 3. Publish from a machine with GitHub credentials

From a temporary directory:

```bash
git clone https://github.com/la1ona/CometenIRLAlerts.wiki.git
cd CometenIRLAlerts.wiki
```

Copy all Markdown files from the main repository's `wiki/` directory into the cloned Wiki repository, preserving names such as:

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
git add .
git commit -m "Publish Cometen IRL Alerts English wiki"
git push
```

## Keeping it in sync

The canonical detailed documentation remains under `docs/` in the main repository. The Wiki is intended as an easier navigation/front-door layer.

When module behavior changes:

1. update the canonical `docs/` page first
2. update the matching Wiki summary if needed
3. publish the Wiki changes

Do not put private configuration, tokens, Browser Source URLs or BELABOX stream IDs in Wiki pages.
