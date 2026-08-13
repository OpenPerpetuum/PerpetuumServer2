using Xunit;

namespace Perpetuum.Tests.Unit
{
    public class ValueTypeExtensionsTests
    {
        [Theory]
        [InlineData(5, 10, 40, 10)]
        [InlineData(50, 10, 40, 40)]
        [InlineData(25, 10, 40, 25)]
        [InlineData(10, 10, 40, 10)]
        [InlineData(40, 10, 40, 40)]
        public void Clamp_int_bounds_the_value(int value, int lower, int upper, int expected)
        {
            Assert.Equal(expected, value.Clamp(lower, upper));
        }

        [Theory]
        [InlineData(0, false)]
        [InlineData(1, true)]
        [InlineData(-1, false)]
        [InlineData(int.MaxValue, true)]
        public void ToBool_is_true_only_above_zero(int value, bool expected)
        {
            Assert.Equal(expected, value.ToBool());
        }

        [Fact]
        public void Min_returns_the_lower_of_the_two()
        {
            Assert.Equal(3, 7.Min(3));
            Assert.Equal(3, 3.Min(7));
        }

        [Fact]
        public void Max_returns_the_higher_of_the_two()
        {
            Assert.Equal(7, 7.Max(3));
            Assert.Equal(7, 3.Max(7));
        }
    }
}
