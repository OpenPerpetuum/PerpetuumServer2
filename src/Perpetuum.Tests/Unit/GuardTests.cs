using Xunit;

namespace Perpetuum.Tests.Unit
{
    // ErrorCodes lives in the Perpetuum namespace (src/Perpetuum/ErrorCodes.cs), which this
    // file's namespace resolves to without a using directive.
    public class GuardTests
    {
        [Fact]
        public void ThrowIfZero_int_throws_on_zero()
        {
            PerpetuumException ex = Assert.Throws<PerpetuumException>(
                () => 0.ThrowIfZero(ErrorCodes.WTFErrorMedicalAttentionSuggested));

            Assert.Equal(ErrorCodes.WTFErrorMedicalAttentionSuggested, ex.error);
        }

        [Fact]
        public void ThrowIfZero_int_passes_on_non_zero()
        {
            1.ThrowIfZero(ErrorCodes.WTFErrorMedicalAttentionSuggested);
        }

        [Fact]
        public void ThrowIfNull_throws_on_null_and_returns_the_value_otherwise()
        {
            object? nothing = null;
            _ = Assert.Throws<PerpetuumException>(
                () => nothing.ThrowIfNull(ErrorCodes.WTFErrorMedicalAttentionSuggested));

            object something = new();
            Assert.Same(something, something.ThrowIfNull(ErrorCodes.WTFErrorMedicalAttentionSuggested));
        }

        [Fact]
        public void ThrowIfTrue_and_ThrowIfFalse_are_mirror_images()
        {
            _ = Assert.Throws<PerpetuumException>(
                () => true.ThrowIfTrue(ErrorCodes.WTFErrorMedicalAttentionSuggested));
            false.ThrowIfTrue(ErrorCodes.WTFErrorMedicalAttentionSuggested);

            _ = Assert.Throws<PerpetuumException>(
                () => false.ThrowIfFalse(ErrorCodes.WTFErrorMedicalAttentionSuggested));
            true.ThrowIfFalse(ErrorCodes.WTFErrorMedicalAttentionSuggested);
        }

        [Theory]
        [InlineData(5, 3, true)]
        [InlineData(3, 3, false)]
        [InlineData(1, 3, false)]
        public void ThrowIfGreater_throws_only_when_strictly_greater(int source, int comparer, bool shouldThrow)
        {
            if (shouldThrow)
            {
                _ = Assert.Throws<PerpetuumException>(
                    () => source.ThrowIfGreater(comparer, ErrorCodes.WTFErrorMedicalAttentionSuggested));
            }
            else
            {
                Assert.Equal(source, source.ThrowIfGreater(comparer, ErrorCodes.WTFErrorMedicalAttentionSuggested));
            }
        }

        [Theory]
        [InlineData(5, 3, false)]
        [InlineData(3, 3, true)]
        [InlineData(1, 3, true)]
        public void ThrowIfLessOrEqual_throws_at_and_below_the_comparer(int source, int comparer, bool shouldThrow)
        {
            if (shouldThrow)
            {
                _ = Assert.Throws<PerpetuumException>(
                    () => source.ThrowIfLessOrEqual(comparer, ErrorCodes.WTFErrorMedicalAttentionSuggested));
            }
            else
            {
                Assert.Equal(source, source.ThrowIfLessOrEqual(comparer, ErrorCodes.WTFErrorMedicalAttentionSuggested));
            }
        }

        [Fact]
        public void ThrowIfError_passes_NoError_through_and_throws_on_anything_else()
        {
            Assert.Equal(ErrorCodes.NoError, ErrorCodes.NoError.ThrowIfError());

            PerpetuumException ex = Assert.Throws<PerpetuumException>(
                () => ErrorCodes.WTFErrorMedicalAttentionSuggested.ThrowIfError());

            Assert.Equal(ErrorCodes.WTFErrorMedicalAttentionSuggested, ex.error);
        }

        [Fact]
        public void ThrowIfError_invokes_the_exception_action_before_throwing()
        {
            bool invoked = false;

            _ = Assert.Throws<PerpetuumException>(
                () => ErrorCodes.WTFErrorMedicalAttentionSuggested.ThrowIfError(_ => invoked = true));

            Assert.True(invoked);
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("x", false)]
        public void ThrowIfNullOrEmpty_rejects_null_and_empty(string? text, bool shouldThrow)
        {
            if (shouldThrow)
            {
                _ = Assert.Throws<PerpetuumException>(
                    () => text!.ThrowIfNullOrEmpty(ErrorCodes.WTFErrorMedicalAttentionSuggested));
            }
            else
            {
                Assert.Equal(text, text!.ThrowIfNullOrEmpty(ErrorCodes.WTFErrorMedicalAttentionSuggested));
            }
        }

        [Fact]
        public void ThrowIfNotType_returns_the_cast_value_and_rejects_the_wrong_type()
        {
            object value = "text";
            Assert.Equal("text", value.ThrowIfNotType<string>(ErrorCodes.WTFErrorMedicalAttentionSuggested));

            _ = Assert.Throws<PerpetuumException>(
                () => value.ThrowIfNotType<int>(ErrorCodes.WTFErrorMedicalAttentionSuggested));
        }

        [Fact]
        public void ThrowIfType_rejects_the_named_type_and_passes_anything_else()
        {
            object value = "text";

            _ = Assert.Throws<PerpetuumException>(
                () => value.ThrowIfType<string>(ErrorCodes.WTFErrorMedicalAttentionSuggested));

            value.ThrowIfType<int>(ErrorCodes.WTFErrorMedicalAttentionSuggested);
        }

        [Fact]
        public void ThrowIfZero_long_throws_on_zero()
        {
            // Not an extension method: Guard.ThrowIfZero(long, Func<Exception>) takes its source
            // as a plain parameter, unlike the int overload above it.
            _ = Assert.Throws<PerpetuumException>(
                () => Guard.ThrowIfZero(0L, () => PerpetuumException.Create(ErrorCodes.WTFErrorMedicalAttentionSuggested)));

            Guard.ThrowIfZero(1L, () => PerpetuumException.Create(ErrorCodes.WTFErrorMedicalAttentionSuggested));
        }

        [Fact]
        public void ThrowIfZero_double_rejects_values_within_epsilon_of_zero()
        {
            _ = Assert.Throws<PerpetuumException>(
                () => 0.0d.ThrowIfZero(() => PerpetuumException.Create(ErrorCodes.WTFErrorMedicalAttentionSuggested)));

            Assert.Equal(
                2.5d,
                2.5d.ThrowIfZero(() => PerpetuumException.Create(ErrorCodes.WTFErrorMedicalAttentionSuggested)));
        }

        [Theory]
        [InlineData(5, 3, true)]
        [InlineData(3, 3, true)]
        [InlineData(1, 3, false)]
        public void ThrowIfGreaterOrEqual_throws_at_and_above_the_comparer(int source, int comparer, bool shouldThrow)
        {
            if (shouldThrow)
            {
                _ = Assert.Throws<PerpetuumException>(
                    () => source.ThrowIfGreaterOrEqual(comparer, ErrorCodes.WTFErrorMedicalAttentionSuggested));
            }
            else
            {
                Assert.Equal(source, source.ThrowIfGreaterOrEqual(comparer, ErrorCodes.WTFErrorMedicalAttentionSuggested));
            }
        }

        [Theory]
        [InlineData(1, 3, true)]
        [InlineData(3, 3, false)]
        [InlineData(5, 3, false)]
        public void ThrowIfLess_throws_only_when_strictly_less(int source, int comparer, bool shouldThrow)
        {
            if (shouldThrow)
            {
                _ = Assert.Throws<PerpetuumException>(
                    () => source.ThrowIfLess(comparer, ErrorCodes.WTFErrorMedicalAttentionSuggested));
            }
            else
            {
                Assert.Equal(source, source.ThrowIfLess(comparer, ErrorCodes.WTFErrorMedicalAttentionSuggested));
            }
        }

        [Fact]
        public void ThrowIfNotNull_throws_when_the_value_is_present()
        {
            object? nothing = null;
            nothing.ThrowIfNotNull(ErrorCodes.WTFErrorMedicalAttentionSuggested);

            _ = Assert.Throws<PerpetuumException>(
                () => new object().ThrowIfNotNull(ErrorCodes.WTFErrorMedicalAttentionSuggested));
        }

        [Fact]
        public void ThrowIfNotNull_invokes_the_exception_action_before_throwing()
        {
            bool invoked = false;

            _ = Assert.Throws<PerpetuumException>(
                () => new object().ThrowIfNotNull(ErrorCodes.WTFErrorMedicalAttentionSuggested, _ => invoked = true));

            Assert.True(invoked);
        }
    }
}
