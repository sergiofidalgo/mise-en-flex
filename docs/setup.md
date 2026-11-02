# Setup

One-time setup for a new development machine (macOS, Apple Silicon). Steps marked **(once)** only happen when the project is first created.

---

## 1. Tools

1. **Git identity** (if not set yet):
   ```bash
   git config --global user.name "Your Name"
   ```
   ```bash
   git config --global user.email "you@example.com"
   ```
2. **Git LFS.** Either install Homebrew (https://brew.sh) and run `brew install git-lfs`, or download the macOS installer from https://git-lfs.com. Then, once per machine:
   ```bash
   git lfs install
   ```
3. **Unity Hub**, from https://unity.com/download.
4. In Unity Hub → **Installs → Install Editor**, pick the latest **Unity 6 LTS** (Apple Silicon). The default modules are enough; macOS build support is included.

## 2. Create the Unity project (once)

The repository already contains `Assets/_Project/` (folders and assembly definitions), docs and git files. Unity Hub won't create a project in a non-empty folder, so we create it next door and move its pieces in.

1. Unity Hub → **Projects → New project** → template **Universal 3D** (URP).
2. Project name: `mise_en_flex_unity`. Location: the folder that *contains* `mise_en_flex` (i.e. `smfidalgo-26`). Create it, wait for it to open, then **close Unity**.
3. Move the template's pieces into this repository (from the `smfidalgo-26` folder):
   ```bash
   cd "/Users/sergiofidalgo/Documents/SMFIDALGO /smfidalgo-26" && mv mise_en_flex_unity/Packages mise_en_flex_unity/ProjectSettings mise_en_flex/ && rsync -a mise_en_flex_unity/Assets/ mise_en_flex/Assets/
   ```
   Then delete the `mise_en_flex_unity` folder (Finder → Move to Bin).
4. Unity Hub → **Projects → Add → Add project from disk** → select `mise_en_flex`. Open it with Unity 6 LTS. The first import takes a few minutes.

## 3. Editor settings (once)

In the open project:

1. **Edit → Project Settings → Editor**:
   - **Asset Serialization → Mode:** Force Text
   - **Version Control → Mode:** Visible Meta Files
2. **Edit → Project Settings → Player → Other Settings → Active Input Handling:** *Input System Package (New)*. Unity restarts.
3. **Window → Package Manager**, check these are installed (install any that are missing from the Unity Registry):
   - Input System
   - Test Framework
   - Universal RP (comes with the template)
4. Check the Console: there should be **no compile errors**. Warnings about empty assemblies are fine for now.

## 4. Smart merging of Unity files (optional, recommended)

Lets git merge scenes and prefabs more cleanly. Replace `<version>` with the installed Unity version:

```bash
git config merge.unityyamlmerge.driver '"/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/Tools/UnityYAMLMerge" merge -p %O %B %A %A'
```

## 5. Running tests from the command line

Close the Editor first (batch mode can't run while it's open):

```bash
"/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/MacOS/Unity" -batchmode -projectPath . -runTests -testPlatform EditMode -testResults ./TestResults.xml
```

## Known issue

The project path contains a folder named `SMFIDALGO ` with a **trailing space**. Unity handles spaces, but trailing spaces can break shell scripts and some tools. If anything path-related misbehaves, renaming that folder is the first thing to try.
