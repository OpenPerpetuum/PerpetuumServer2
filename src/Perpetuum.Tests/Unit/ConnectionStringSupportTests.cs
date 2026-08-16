using Microsoft.Data.SqlClient;
using Perpetuum.Data;
using Xunit;

namespace Perpetuum.Tests.Unit
{
    public class ConnectionStringSupportTests
    {
        private const string Installer =
            @"Server=localhost\PERPSQL;Database=perpetuumsa;Trusted_Connection=True;Connection Reset=True;TrustServerCertificate=True;Pooling=True;";

        private const string Supported =
            @"Server=localhost\PERPSQL;Database=perpetuumsa;Trusted_Connection=True;TrustServerCertificate=True;Pooling=True;Connection Timeout=30;";

        /// <summary>
        /// The premise of the whole class. If Microsoft.Data.SqlClient ever starts accepting the
        /// keyword again, this fails and the rest of the file becomes dead weight.
        /// </summary>
        [Fact]
        public void The_driver_really_does_reject_the_installer_string()
        {
            NotSupportedException ex = Assert.Throws<NotSupportedException>(() => new SqlConnection(Installer));

            Assert.Contains("Connection Reset", ex.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void The_keyword_the_installer_writes_is_reported()
        {
            IReadOnlyList<string> unsupported = ConnectionStringSupport.FindUnsupportedKeywords(Installer);

            Assert.Single(unsupported);
            Assert.Contains("Connection Reset", unsupported[0], StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void A_string_the_driver_accepts_reports_nothing()
        {
            Assert.Empty(ConnectionStringSupport.FindUnsupportedKeywords(Supported));
        }

        /// <summary>
        /// The installer's file carries more than one problem at a time. Reporting them one at a
        /// time would cost the operator a restart per keyword.
        /// </summary>
        [Fact]
        public void Every_rejected_keyword_is_reported_in_one_pass()
        {
            IReadOnlyList<string> unsupported = ConnectionStringSupport.FindUnsupportedKeywords(
                Supported + "Connection Reset=True;Asynchronous Processing=True;");

            Assert.Equal(2, unsupported.Count);
            Assert.Contains(unsupported, k => k.Contains("Connection Reset", StringComparison.OrdinalIgnoreCase));
            Assert.Contains(unsupported, k => k.Contains("Asynchronous Processing", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// These two were deliberately left alone while the code removed keywords, because removing
        /// them would have changed how the server connects. Reporting carries no such risk, so they
        /// are in scope now.
        /// </summary>
        [Theory]
        [InlineData("Network Library=dbmssocn;")]
        [InlineData("Context Connection=True;")]
        public void Keywords_that_still_carry_meaning_are_reported_too(string setting)
        {
            IReadOnlyList<string> unsupported = ConnectionStringSupport.FindUnsupportedKeywords(Supported + setting);

            Assert.Single(unsupported);
        }

        /// <summary>
        /// No hardcoded list to fall out of date: anything the driver refuses is reported, including
        /// a keyword nobody anticipated.
        /// </summary>
        [Fact]
        public void A_keyword_no_list_could_have_predicted_is_reported()
        {
            IReadOnlyList<string> unsupported = ConnectionStringSupport.FindUnsupportedKeywords(
                Supported + "Totally Made Up Keyword=1;");

            Assert.Single(unsupported);
            Assert.Contains("Totally Made Up Keyword", unsupported[0], StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("connection reset=True;")]
        [InlineData("CONNECTION RESET=True;")]
        public void Keyword_matching_ignores_case(string spelling)
        {
            Assert.Single(ConnectionStringSupport.FindUnsupportedKeywords(Supported + spelling));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Absent_or_blank_input_reports_nothing(string input)
        {
            Assert.Empty(ConnectionStringSupport.FindUnsupportedKeywords(input));
        }

        /// <summary>
        /// Malformed beyond anything this can explain. Report nothing and let the driver's own
        /// error stand, rather than inventing a second explanation for the same failure.
        /// </summary>
        [Fact]
        public void An_unparseable_string_reports_nothing()
        {
            Assert.Empty(ConnectionStringSupport.FindUnsupportedKeywords("=;=;"));
        }

        /// <summary>
        /// A value may legally contain the separator when quoted. Splitting on ';' by hand would
        /// invent keywords that are not there.
        /// </summary>
        [Fact]
        public void A_quoted_value_containing_the_separator_does_not_invent_a_keyword()
        {
            IReadOnlyList<string> unsupported = ConnectionStringSupport.FindUnsupportedKeywords(
                @"Server=localhost;Database=perpetuumsa;Password=""a;b"";Trusted_Connection=True;");

            Assert.Empty(unsupported);
        }

        /// <summary>
        /// The operator has to find the keyword in perpetuum.ini after reading it in the log, so
        /// what is reported has to match what is in the file.
        /// </summary>
        [Fact]
        public void A_reported_keyword_can_be_found_in_the_original_string()
        {
            IReadOnlyList<string> unsupported = ConnectionStringSupport.FindUnsupportedKeywords(Installer);

            Assert.Contains(unsupported[0], Installer, StringComparison.OrdinalIgnoreCase);
        }
    }
}
