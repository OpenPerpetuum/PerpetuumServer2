using Perpetuum.AdminTool.Data;

namespace Perpetuum.AdminTool.Core.Tests.Data
{
    public class PasswordHashTests
    {
        [Fact]
        public void Compute_PreservesTheExistingAccountPasswordFormat()
        {
            Assert.Equal(
                "A94A8FE5CCB19BA61C4C0873D391E987982FBBD3",
                PasswordHash.Compute("test"));
        }

        [Fact]
        public void Compute_ReturnsEmptyForEmptyInput()
        {
            Assert.Equal(string.Empty, PasswordHash.Compute(string.Empty));
        }
    }
}
