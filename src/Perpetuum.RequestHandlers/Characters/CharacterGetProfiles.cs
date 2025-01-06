using Perpetuum.Accounting.Characters;
using Perpetuum.Host.Requests;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterGetProfiles : IRequestHandler
    {
        private readonly IReadOnlyRepository<int, CharacterProfile> _characterProfileRepository;

        public CharacterGetProfiles(IReadOnlyRepository<int, CharacterProfile> characterProfileRepository)
        {
            _characterProfileRepository = characterProfileRepository;
        }

        public void HandleRequest(IRequest request)
        {
            List<Character> characters = new();

            int[] characterIds = request.Data.GetOrDefault(k.characterID, new int[0]);
            foreach (int characterId in characterIds)
            {
                Character character = Character.Get(characterId);
                if (character != Character.None)
                {
                    characters.Add(character);
                }
            }

            long[] characterEids = request.Data.GetOrDefault(k.characterEID, new long[0]);
            foreach (long characterEid in characterEids)
            {
                Character character = Character.GetByEid(characterEid);
                if (character != Character.None)
                {
                    characters.Add(character);
                }
            }

            Dictionary<string, object> result = characters.Select(c => _characterProfileRepository.Get(c.Id)).Where(p => p != null).ToDictionary("c", p => p.ToDictionary());
            Message.Builder.FromRequest(request).WithData(result).WrapToResult().WithEmpty().Send();
        }
    }
}