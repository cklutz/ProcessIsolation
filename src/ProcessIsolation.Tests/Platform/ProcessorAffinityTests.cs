using System;
using ProcessIsolation.Shared.Platform;
using Xunit;

namespace ProcessIsolation.Tests.Platform
{
    public class ProcessAffinityTests
    {
        [Theory]
        [InlineData(0, 4, "cpu0 cpu1 cpu2 cpu3")]
        [InlineData(-1, 4, "cpu0 cpu1 cpu2")]
        [InlineData(-2, 4, "cpu0 cpu1")]
        [InlineData(-3, 4, "cpu0")]
        [InlineData(-4, 4, "cpu0")]
        [InlineData(-5, 4, "cpu0")]
        [InlineData(int.MinValue, 4, "cpu0")]
        [InlineData(1, 4, "cpu0")]
        [InlineData(2, 4, "cpu0 cpu1")]
        [InlineData(3, 4, "cpu0 cpu1 cpu2")]
        [InlineData(4, 4, "cpu0 cpu1 cpu2 cpu3")]
        [InlineData(5, 4, "cpu0 cpu1 cpu2 cpu3")]
        [InlineData(int.MaxValue, 4, "cpu0 cpu1 cpu2 cpu3")]
        public void Calculate(int processorCount, int maximumProcessorCount, string expectedResult)
        {
            Assert.Equal(expectedResult, ProcessAffinity.ToString(ProcessAffinity.Calculate(processorCount, maximumProcessorCount)));
        }

        [Theory]
        [InlineData("cpu0", 4, false)]
        [InlineData("cpu0 cpu1 cpu2 cpu3", 4, false)]
        [InlineData("cpu0 cpu2 cpu3", 4, false)]
        [InlineData("cpu0 cpu1 cpu3", 4, false)]
        [InlineData("cpu1 cpu2 cpu3", 4, false)]
        [InlineData("0", 4, true)]
        [InlineData("0:1 2 3", 4, true)]
        [InlineData("0;2,3", 4, true)]
        [InlineData("0|1 3", 4, true)]
        [InlineData("1 2 3", 4, true)]
        public void Parse(string str, int maximumProcessorCount, bool concise)
        {
            Assert.Equal(Normalize(str), ProcessAffinity.ToString(ProcessAffinity.Parse(str), maximumProcessorCount, false, concise)?.Trim());
        }

        [Theory]
        [InlineData("cpu0", 4, false, true)]
        [InlineData("cpu0 cpu1 cpu2 cpu3", 4, false, true)]
        [InlineData("cpu0 cpu2 cpu3", 4, false, true)]
        [InlineData("cpu0 cpu1 cpu3", 4, false, true)]
        [InlineData("cpu1 cpu2 cpu3", 4, false, true)]
        [InlineData("0", 4, true, true)]
        [InlineData("0:1 2 3", 4, true, true)]
        [InlineData("0;2,3", 4, true, true)]
        [InlineData("0|1 3", 4, true, true)]
        [InlineData("1 2 3", 4, true, true)]
        [InlineData("", 4, true, false)]
        [InlineData("abc", 4, true, false)]
        [InlineData("cpu cpu cpu", 4, true, false)]
        [InlineData("cpuX cpuY cpuZ", 4, true, false)]
        public void TryParse(string str, int maximumProcessorCount, bool concise, bool result)
        {
            bool actual = ProcessAffinity.TryParse(str, out var affinity);
            Assert.Equal(result, actual);

            if (result)
            {
                Assert.Equal(Normalize(str), ProcessAffinity.ToString(affinity, maximumProcessorCount, false, concise)?.Trim());
            }
            else
            {
                Assert.Equal(IntPtr.Zero, affinity);
            }
        }

        [Theory]
        [InlineData("cpu0", 4, 1)]
        [InlineData("cpu0 cpu1 cpu2 cpu3", 4, 4)]
        [InlineData("cpu0 cpu2 cpu3", 4, 3)]
        [InlineData("cpu0 cpu1 cpu3", 4, 3)]
        [InlineData("cpu1 cpu2 cpu3", 4, 3)]
        [InlineData("cpu1 cpu2", 4, 2)]
        public void CountActiveProcessors(string str, int maximumProcessorCount, int expectedResult)
        {
            Assert.Equal(expectedResult, ProcessAffinity.CountActiveProcessors(ProcessAffinity.Parse(str), maximumProcessorCount));
        }

        [Theory]
        [InlineData(0, "cpu0", true)]
        [InlineData(1, "cpu0", false)]
        [InlineData(1, "cpu0 cpu1", true)]
        [InlineData(2, "cpu0 cpu1", false)]
        [InlineData(2, "cpu0 cpu2", true)]
        [InlineData(8, "cpu8", true)]
        public void IsProcessorActive(int index, string str, bool expectedResult)
        {
            Assert.Equal(expectedResult, ProcessAffinity.IsProcessorActive(ProcessAffinity.Parse(str), index));
        }

        private static string Normalize(string str)
        {
            string expected = str;

            foreach (char c in ProcessAffinity.GetAffinityDelimiters())
            {
                expected = expected.Replace(c, ' ');
            }

            return expected;
        }
    }
}
