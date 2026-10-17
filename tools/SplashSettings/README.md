# Working Unity splash settings patch

This tool reproduces the startup settings confirmed working in The Spirit of the Samurai, Unity 2021.3.45f2. It does not install or distribute game assets. The plugin DLL alone cannot reliably remove this splash.

Requires .NET 10 and AssetsTools.NET 3.0.5 (restored by NuGet). Download and extract the non-Brotli LZ4 type-tree package from [AssetRipper Tpk](https://github.com/AssetRipper/Tpk#type-tree-tpk), placing `lz4.tpk` outside the repository. The tool refuses untested Unity versions.

Inspect without writing:

```powershell
dotnet run --project tools/SplashSettings -c Release -- "D:\SteamLibrary\steamapps\common\The Spirit of the Samurai\samurai_Data\data.unity3d" "C:\path\to\lz4.tpk"
```

To prepare the replacement, append `--prepare`. It stages `data.unity3d.splash-disabled.tmp`, and compares every decompressed byte with the original. Only these fields may differ:

- PlayerSettings: disable `m_ShowUnitySplashScreen` and `m_ShowUnitySplashLogo`; null logo references; zero logo durations and background RGB.
- BuildSettings: set `hasPROVersion` and `isNoWatermarkBuild` to true. These are local serialized build metadata; this tool does not grant a Unity license.

The build-level flags were the final change required to remove the Made with Unity logo in this installation. Earlier visibility/logo changes alone did not remove it. Remaining loading time can still display a black screen.

If no changes are needed, the tool exits without staging anything. Existing staging files are never overwritten. Any verification failure means the staged file must not be installed. Preparation needs several GB of free space for the uncompressed temporary bundle, replacement and backup.

After preparation completes successfully, close the game. Back up `data.unity3d`, then replace it with the verified `.splash-disabled.tmp` file. Retain the original backup outside Git. Restore that backup to undo this modification. Do not distribute either game bundle. Steam verification or a game update can restore the original settings.

The tool was checked against the current installed bundle (zero changes needed) and the original backup (full decompressed comparison). It makes no changes to plugin controls, save files, or gameplay logic.
