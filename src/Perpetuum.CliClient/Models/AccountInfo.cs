using System;
using System.Collections.Generic;

namespace Perpetuum.CliClient.Models
{
    public class AccountInfo
    {
        public int AccountId { get; init; }
        public string Email { get; init; } = string.Empty;
        public AccessLevel AccessLevel { get; init; } = AccessLevel.normal;
        public int Credit { get; init; }
        public bool IsSubscriber { get; init; }
        public bool EmailConfirmed { get; init; }
        public DateTime? ValidUntil { get; init; }
        public IReadOnlyDictionary<string, object> RawData { get; init; } = new Dictionary<string, object>();

        public static AccountInfo FromDictionary(IDictionary<string, object> data)
        {
            var accountId = data.GetInt(k.accountID);
            if (accountId == 0)
            {
                accountId = data.GetInt("id");
            }

            var accessLevelInt = data.GetInt(k.accessLevel);
            if (accessLevelInt == 0)
            {
                accessLevelInt = data.GetInt(k.accLevel);
            }

            return new AccountInfo
            {
                AccountId = accountId,
                Email = data.GetString(k.email),
                AccessLevel = (AccessLevel)accessLevelInt,
                Credit = data.GetInt(k.credit),
                IsSubscriber = data.GetBool(k.isSubscriber),
                EmailConfirmed = data.GetBool(k.emailConfirmed),
                ValidUntil = data.GetDateTime(k.validUntil),
                RawData = (data as IReadOnlyDictionary<string, object>) ?? new Dictionary<string, object>(data)
            };
        }
    }
}
