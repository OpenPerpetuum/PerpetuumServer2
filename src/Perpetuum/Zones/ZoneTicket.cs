using Perpetuum.Accounting.Characters;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Perpetuum.Zones
{
    public readonly struct ZoneTicket
    {
        private static readonly TimeSpan _ticketLifetime = TimeSpan.FromHours(1);

        private static readonly MD5 _md5 = MD5.Create();
        private static readonly int _md5HashSize = _md5.HashSize / 8;
        private static readonly Rijndael _rijndael = Rijndael.Create();
        private readonly DateTime _created;

        public int CharacterId { get; }

        static ZoneTicket()
        {
            _rijndael.GenerateKey();
            _rijndael.GenerateIV();
        }

        private ZoneTicket(int characterId) : this()
        {
            CharacterId = characterId;
            _created = DateTime.Now;
        }

        public bool IsExpired => DateTime.Now.Subtract(_created) > _ticketLifetime;

        private static byte[] Encrypt(ZoneTicket ticket)
        {
            using MemoryStream ms = new();
            ICryptoTransform encryptor = _rijndael.CreateEncryptor(_rijndael.Key, _rijndael.IV);

            using (CryptoStream cs = new(ms, encryptor, CryptoStreamMode.Write))
            {
                byte[] e = ticket.ToByteArray();
                byte[] h = _md5.ComputeHash(e);
                cs.Write(h, 0, h.Length);
                cs.Write(e, 0, e.Length);
            }

            return ms.ToArray();
        }

        public static bool TryDecrypt(byte[] data, out ZoneTicket ticket)
        {
            ticket = default;

            using MemoryStream ms = new();
            ICryptoTransform decryptor = _rijndael.CreateDecryptor(_rijndael.Key, _rijndael.IV);

            using (CryptoStream cs = new(ms, decryptor, CryptoStreamMode.Write))
            {
                cs.Write(data, 0, data.Length);
            }

            byte[] decryptedData = ms.ToArray();
            byte[] ticketData = decryptedData.Skip(_md5HashSize).ToArray();

            int size = Marshal.SizeOf(ticket);

            if (ticketData.Length != size)
            {
                return false;
            }

            if (!decryptedData.Take(_md5HashSize).SequenceEqual(_md5.ComputeHash(ticketData)))
            {
                return false;
            }

            ticket = ticketData.ToStruct<ZoneTicket>();
            return true;
        }

        public static byte[] CreateAndEncryptFor(Character character)
        {
            return Encrypt(new ZoneTicket(character.Id));
        }

        public static Character GetCharacterFromEncryptedTicket(byte[] encrypted)
        {
            TryDecrypt(encrypted, out ZoneTicket ticket).ThrowIfFalse(ErrorCodes.WTFErrorMedicalAttentionSuggested);
            ticket.IsExpired.ThrowIfTrue(ErrorCodes.WTFErrorMedicalAttentionSuggested);
            return Character.Get(ticket.CharacterId);
        }
    }
}