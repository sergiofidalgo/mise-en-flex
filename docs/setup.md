# Setup

How to get the project running on a development machine (macOS, Apple Silicon). The Unity project already exists in this repository; there's nothing to create.

**Unity version:** 6.3 LTS, **6000.3.27f2** (Apple Silicon). See `decisions.md`.

---

## 1. Tools

1. **Git identity.** This repository is configured locally as `sergiofidalgo` / `serfidalgo@outlook.com`. On a new machine, set it again:
   ```bash
   git config user.name "sergiofidalgo"
   ```
   ```bash
   git config user.email "serfidalgo@outlook.com"
   ```
2. **GitHub access over SSH.** The remote is `git@github.com:sergiofidalgo/mise-en-flex.git`. Each machine needs its own SSH key added at GitHub → Settings → SSH and GPG keys.
3. **Git LFS.** Download the macOS arm64 zip from https://git-lfs.com (or the `git-lfs/git-lfs` GitHub releases), unzip it, and run its `install.sh` with `sudo`. Then, inside the repository:
   ```bash
   git lfs install
   ```
4. **Unity Hub**, from https://unity.com/download. Sign in and add a **Personal** license (Settings → Licenses → Add license).
5. In Unity Hub → **Installs → Install Editor**, install **Unity 6.3 LTS (6000.3.27f2)** with no extra modules. Install it from the Hub window, not the command line: a command-line install was interrupted when the Hub window opened.

## 2. Open the project

1. Unity Hub → **Projects → Add → Add project from disk** → select the `mise_en_flex` folder.
2. Open it with 6000.3.27f2. The first import takes a few minutes.
3. Check the Console: no compile errors.

The project already has the right settings (from the 3D URP template): Force Text serialization, Visible Meta Files, and Active Input Handling set to the Input System package.

## 3. Smart merging of Unity files (optional, recommended)

Lets git merge scenes and prefabs more cleanly:

```bash
git config merge.unityyamlmerge.driver '"/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/Tools/UnityYAMLMerge" merge -p %O %B %A %A'
```

## 4. Running tests from the command line

Close the Editor first (batch mode can't run on a project that's open):

```bash
"/Applications/Unity/Hub/Editor/6000.3.27f2/Unity.app/Contents/MacOS/Unity" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults ./TestResults.xml
```

A `[Licensing::Module] Error: Access token is unavailable` line in the log is harmless as long as the run completes.

## Known issue

The project path contains a folder named `SMFIDALGO ` with a **trailing space**. Unity and batch mode work with it, but trailing spaces can break shell scripts and some tools. If anything path-related misbehaves, renaming that folder is the first thing to try.
