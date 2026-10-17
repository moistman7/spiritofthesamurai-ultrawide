using AssetsTools.NET;
using AssetsTools.NET.Extra;

if (args.Length < 2 || args.Length > 3 || (args.Length == 3 && args[2] != "--prepare"))
    throw new ArgumentException("Usage: SplashSettings <data.unity3d> <classdata.tpk> [--prepare]");
string input = Path.GetFullPath(args[0]);
string temp = input + ".splash-uncompressed.tmp";
string result = input + ".splash-disabled.tmp";
var manager = new AssetsManager();
try
{
    manager.LoadClassPackage(Path.GetFullPath(args[1]));
    var bundle = manager.LoadBundleFile(input, false);
    if (bundle.file.DataIsCompressed) throw new Exception("LZMA bundles are unsupported; expected this game's LZ4 bundle.");
    var inst = manager.LoadAssetsFileFromBundle(bundle, "globalgamemanagers", false);
    if (inst.file.Metadata.UnityVersion != "2021.3.45f2")
        throw new Exception("Untested Unity version; no files changed.");
    manager.LoadClassDatabaseFromPackage(inst.file.Metadata.UnityVersion);
    int index = bundle.file.GetFileIndex("globalgamemanagers");
    bundle.file.GetFileRange(index, out long assetOffset, out long assetLength);
    bundle.file.DataReader.Position = assetOffset;
    var assetBytes = bundle.file.DataReader.ReadBytes(checked((int)assetLength));
    var expected = new Dictionary<long, byte>();

    void PatchAsset(AssetClassID type, Action<AssetTypeValueField> edit)
    {
        var info = inst.file.GetAssetsOfType(type).Single();
        var field = manager.GetBaseField(inst, info);
        byte[] original = field.WriteToByteArray();
        edit(field);
        byte[] modified = field.WriteToByteArray();
        long relative = info.GetAbsoluteByteOffset(inst.file);
        if (original.Length != modified.Length || original.Length != info.ByteSize ||
            !assetBytes.AsSpan(checked((int)relative), original.Length).SequenceEqual(original))
            throw new Exception("Serialized layout mismatch; no files changed.");
        for (int i = 0; i < original.Length; i++)
        {
            if (original[i] == modified[i]) continue;
            expected.Add(assetOffset + relative + i, modified[i]);
            assetBytes[relative + i] = modified[i];
        }
    }

    PatchAsset(AssetClassID.PlayerSettings, field =>
    {
        field["m_ShowUnitySplashScreen"].AsBool = false;
        field["m_ShowUnitySplashLogo"].AsBool = false;
        foreach (var entry in field["m_SplashScreenLogos.Array"].Children)
        {
            entry["logo.m_FileID"].AsInt = 0;
            entry["logo.m_PathID"].AsLong = 0;
            entry["duration"].AsFloat = 0f;
        }
        foreach (string channel in new[] { "r", "g", "b" })
            field["m_SplashScreenBackgroundColor"][channel].AsFloat = 0f;
    });
    PatchAsset(AssetClassID.BuildSettings, field =>
    {
        field["hasPROVersion"].AsBool = true;
        field["isNoWatermarkBuild"].AsBool = true;
    });
    Console.WriteLine($"Unity {inst.file.Metadata.UnityVersion}: {expected.Count} splash-setting bytes need changing.");
    if (args.Length != 3 || expected.Count == 0) return;
    if (File.Exists(temp) || File.Exists(result)) throw new IOException("Staging files already exist; inspect them before retrying.");
    bundle.file.BlockAndDirInfo.DirectoryInfos[index].SetNewData(assetBytes);
    Console.WriteLine("Writing staging bundle...");
    using (var writer = new AssetsFileWriter(temp)) bundle.file.Write(writer);
    var unpacked = new AssetBundleFile();
    try
    {
        unpacked.Read(new AssetsFileReader(File.OpenRead(temp)));
        Console.WriteLine("Compressing staging bundle...");
        using var writer = new AssetsFileWriter(result);
        unpacked.Pack(writer, AssetBundleCompressionType.LZ4Fast);
    }
    finally { unpacked.Close(); }
    File.Delete(temp);
    var verify = manager.LoadBundleFile(result, true);
    var beforeDirs = bundle.file.BlockAndDirInfo.DirectoryInfos;
    var afterDirs = verify.file.BlockAndDirInfo.DirectoryInfos;
    if (beforeDirs.Count != afterDirs.Count || beforeDirs.Where((d, i) =>
        d.Name != afterDirs[i].Name || d.Offset != afterDirs[i].Offset ||
        d.DecompressedSize != afterDirs[i].DecompressedSize).Any())
        throw new Exception("Bundle directory changed; do not install staging file.");
    Console.WriteLine("Comparing every decompressed byte...");
    bundle.file.DataReader.Position = 0;
    verify.file.DataReader.Position = 0;
    long length = beforeDirs.Max(d => d.Offset + d.DecompressedSize);
    long position = 0;
    int diffs = 0;
    while (position < length)
    {
        int count = (int)Math.Min(1048576, length - position);
        byte[] left = bundle.file.DataReader.ReadBytes(count), right = verify.file.DataReader.ReadBytes(count);
        if (left.Length != count || right.Length != count) throw new Exception("Truncated bundle.");
        for (int i = 0; i < count; i++)
        {
            if (left[i] == right[i]) continue;
            if (!expected.TryGetValue(position + i, out byte value) || right[i] != value)
                throw new Exception("Unexpected data change; do not install staging file.");
            diffs++;
        }
        position += count;
    }
    if (diffs != expected.Count) throw new Exception("Difference count mismatch.");
    Console.WriteLine($"Verified {length} bytes; only {diffs} expected bytes changed. Ready: {result}");
}
finally { manager.UnloadAll(); }