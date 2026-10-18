using System.Buffers.Binary;
using System.Text.Json;
using FireRedAudio;
namespace FireRed.Tests.Audio;

[TestClass,TestCategory("Audio")]
public sealed class RoarCryPresetTests
{
    [TestMethod]
    public void AnimationPresetsMatchExtractedPlayCryInternalIncludingBothForwardRoarModes()
    {
        var resources=TestAssets.Cached(()=>Resources.FromPack(Path.Combine(TestAssets.Root,"content/generated/audio/synth/firered.pack")));
        var catalog=TestAssets.Cached(()=>NormalCryCatalog.FromFile(Path.Combine(TestAssets.Root,"content/generated/audio/synth/normal-cries.bin")));
        using var json=JsonDocument.Parse(File.ReadAllBytes(TestAssets.GeneratedFile("tests/Game.Audio.Tests/Fixtures/roar-cry-presets.json", "python tests/Game.Audio.Tests/Reference/generate_roar_cry_vectors.py --root . --output tests/Game.Audio.Tests/Fixtures/roar-cry-presets.json")));int cases=0,roar=0;
        foreach(var r in json.RootElement.EnumerateArray().Select(e=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray()))
        {
            if(!SourceAnimationCryPreset.IsSupported(r[0]))continue;
            var(song,sequence)=SourceAnimationCryPreset.Build(resources,catalog,4,r[0],r[1]);string why=$"mode {r[0]} pan {r[1]}";
            Assert.AreEqual(r[2],(int)sequence[0x1d],why);Assert.AreEqual(((r[3]+128)>>8)&127,(int)sequence[0x2a],why);Assert.AreEqual(((r[3]+128)>>1)&127,(int)sequence[0x12],why);Assert.AreEqual(r[4],(int)BinaryPrimitives.ReadUInt16LittleEndian(sequence.AsSpan(0x2e)),why);Assert.AreEqual((uint)r[5],BinaryPrimitives.ReadUInt32LittleEndian(sequence.AsSpan(0x20)),why);Assert.AreEqual(r[6],(int)sequence[0x26],why);Assert.AreEqual(r[7]==0?1:2,song.Tracks.Length,why);Assert.AreEqual(r[8],(int)song.Priority,why);Assert.AreEqual(r[9]!=0,SourceAnimationCryPreset.UsesReverse(r[0]),why);Assert.AreEqual(r[9]!=0?catalog.Get(4).ReverseToneAddress:catalog.Get(4).ToneAddress,song.Voices,why);Assert.AreEqual((r[1]+64)&127,(int)sequence[0x28],why);cases++;if(r[0] is 7 or 8)roar++;
        }
        Assert.AreEqual(35,cases);Assert.AreEqual(10,roar);
    }
}
