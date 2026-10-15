using System.Buffers.Binary;
using FireRed.Tests;
using FireRedAudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Audio;
[TestClass, TestCategory("Audio")]
public sealed class PatternValidationTests : SequenceFixture
{

    [TestMethod]
    public void actual_pattern_stack_overflow_still_fails_explicitly()
    {
        void Reject(Action action, string name)
        {
            try
            {
                action();
            }
            catch (Exception error)when (error is InvalidDataException or NotSupportedException)
            {
                return;
            }

            throw new Exception("FAIL " + name);
        }

        var overflowCommands = new byte[]
        {
            0xb3,
            0,
            0,
            0,
            0
        };
        BinaryPrimitives.WriteUInt32LittleEndian(overflowCommands.AsSpan(1), Resources.Base + 256);
        var overflow = Fixture(overflowCommands);
        Reject(() => new Player(overflow, new Synth(overflow), Song.Read(overflow, Resources.Base), 0).Frame(), "actual pattern-stack overflow still fails explicitly");
    }

    [TestMethod]
    public void pattern_call_outside_resource_bounds_still_fails_explicitly()
    {
        void Reject(Action action, string name)
        {
            try
            {
                action();
            }
            catch (Exception error)when (error is InvalidDataException or NotSupportedException)
            {
                return;
            }

            throw new Exception("FAIL " + name);
        }

        var escapedPattern = new byte[]
        {
            0xb3,
            0,
            0,
            0,
            0
        };
        BinaryPrimitives.WriteUInt32LittleEndian(escapedPattern.AsSpan(1), Resources.End + 4);
        var patternBounds = Fixture(escapedPattern);
        Reject(() => new Player(patternBounds, new Synth(patternBounds), Song.Read(patternBounds, Resources.Base), 0).Frame(), "pattern call outside resource bounds still fails explicitly");
    }
}
