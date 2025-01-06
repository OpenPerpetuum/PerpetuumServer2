using Perpetuum.Accounting.Characters;
using Perpetuum.Data;
using Perpetuum.Host.Requests;
using System.Transactions;

namespace Perpetuum.RequestHandlers.Characters
{
    public class CharacterSetMoodMessage : IRequestHandler
    {
        public void HandleRequest(IRequest request)
        {
            using TransactionScope scope = Db.CreateTransaction();
            string moodMessage = request.Data.GetOrDefault<string>(k.moodMessage);
            Character character = request.Session.Character;
            character.MoodMessage = moodMessage;

            Transaction.Current.OnCommited(() =>
            {
                List<Character> targets = new()
                { character };
                targets.AddRange(character.GetSocial().GetFriends());
                Dictionary<string, object> data = new()
                { { k.characterID, character.Id }, { k.moodMessage, moodMessage } };
                Message.Builder.SetCommand(Commands.UpdateMoodMessage).WithData(data).ToCharacters(targets).Send();
            });

            scope.Complete();
        }
    }
}