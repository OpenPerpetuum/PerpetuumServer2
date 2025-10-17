using Perpetuum.Accounting.Characters;
using Perpetuum.Data;
using Perpetuum.Services.Channels;
using System.Collections.Concurrent;
using System.Transactions;

namespace Perpetuum.Groups.Gangs
{
    public class GangManager(IGangRepository gangRepository, IChannelManager channelManager, Gang.Factory gangFactory) : IGangManager
    {
        private readonly IGangRepository _gangRepository = gangRepository;
        private readonly IChannelManager _channelManager = channelManager;
        private readonly Gang.Factory _gangFactory = gangFactory;
        private readonly ConcurrentDictionary<Guid, Gang> _gangs = [];

        public Gang? GetGang(Guid gangID)
        {
            Gang gang = _gangs.GetOrAdd(gangID, _ => _gangRepository.Get(gangID));
            if (gang != null)
            {
                return gang;
            }

            _gangs.Remove(gangID);
            return null;
        }

        public Gang? GetGangByMember(Character member)
        {
            Gang? gang = _gangs.Values.FirstOrDefault(g => g.IsMember(member));
            if (gang != null)
            {
                return gang;
            }

            Guid gangID = _gangRepository.GetGangIDByMember(member);
            if (gangID == Guid.Empty)
            {
                return null;
            }

            gang = GetGang(gangID);
            return gang;
        }

        public Gang CreateGang(string gangName, Character leader)
        {
            if (string.IsNullOrEmpty(gangName))
            {
                throw new PerpetuumException(ErrorCodes.GangNameTooShort);
            }

            Gang gang = _gangFactory();
            gang.Id = Guid.NewGuid();
            gang.Name = gangName;
            gang.Leader = leader;
            // In addition to leadership, an assistant role is also needed
            // so that when the leader changes, the creator does not lose
            // control over the gang
            gang.SetMember(leader, GangRole.Assistant);

            _gangRepository.Insert(gang);

            void Finish()
            {
                // In addition to the DB repository, need to add a new gang to the dictionary
                _gangs.Add(gang.Id, gang);
                _channelManager.CreateAndJoinChannel(ChannelType.Gang, gang.ChannelName, gang.Leader);
                // Perform actions upon completion of the gang creation
                GangCreate?.Invoke(gang, leader);
            }

            if (Transaction.Current != null)
                Transaction.Current.OnCommited(Finish);
            else
                Finish();

            return gang;
        }

        public void DisbandGang(Gang gang)
        {
            _gangRepository.Delete(gang);

            void Finish()
            {
                Message.Builder.SetCommand(Commands.GangDelete).WithData(gang.GetGangData()).ToCharacters(gang.GetMembers()).Send();
                _channelManager.DeleteChannel(gang.ChannelName);
                _gangs.Remove(gang.Id);
                GangDisbanded?.Invoke(gang);
            }

            if (Transaction.Current != null)
            {
                Transaction.Current.OnCommited(Finish);
            }
            else
            {
                Finish();
            }
        }

        public event Action<Gang> GangDisbanded;

        public void RemoveMember(Gang gang, Character member, bool isKick)
        {
            if (gang == null)
            {
                return;
            }

            if (!gang.IsMember(member))
            {
                throw new PerpetuumException(ErrorCodes.CharacterNotInTheCurrentGang);
            }

            _gangRepository.DeleteMember(gang, member);

            void Finish()
            {
                Dictionary<string, object> data = new()
                {
                    {k.data, gang.GetGangData()},
                    {k.memberID, member.Id}
                };

                Command cmd = isKick ? Commands.GangKickMember : Commands.GangRemoveMember;
                Message.Builder.SetCommand(cmd).WithData(data).ToCharacters(gang.GetMembers()).Send();

                gang.RemoveMember(member);

                _channelManager.LeaveChannel(gang.ChannelName, member);

                OnGangMemberRemoved(gang, member);
            }

            if (Transaction.Current != null)
            {
                Transaction.Current.OnCommited(Finish);
            }
            else
            {
                Finish();
            }
        }

        protected virtual void OnGangMemberRemoved(Gang gang, Character member)
        {
            try
            {
                Character[] members = gang.GetMembers().ToArray();

                if (members.Length <= 0)
                {
                    DisbandGang(gang);
                    return;
                }

                if (gang.Leader != member)
                {
                    return;
                }

                // nincs leader
                Character newLeader = members.FirstOrDefault(mm => gang.HasRole(mm, GangRole.Assistant)) ?? Character.None;
                if (newLeader == Character.None)
                {
                    Character firstMember = members.First();
                    newLeader = firstMember;
                }

                ChangeLeader(gang, newLeader);
            }
            finally
            {
                GangMemberRemoved?.Invoke(gang, member);
            }
        }


        public void ChangeLeader(Gang gang, Character newLeader)
        {
            if (!gang.IsMember(newLeader))
            {
                throw new PerpetuumException(ErrorCodes.CharacterNotInTheCurrentGang);
            }

            _gangRepository.UpdateLeader(gang, newLeader);

            void Finish()
            {
                gang.Leader = newLeader;
                Message.Builder.SetCommand(Commands.GangSetLeader).WithData(new Dictionary<string, object>
                {
                    { k.leaderId, newLeader.Id }
                }).ToCharacters(gang.GetMembers()).Send();
                _channelManager.SetMemberRole(gang.ChannelName, newLeader, ChannelMemberRole.Operator);
                GangLeaderChanged?.Invoke(gang);
            }

            if (Transaction.Current != null)
            {
                Transaction.Current.OnCommited(Finish);
            }
            else
            {
                Finish();
            }
        }

        public event Action<Gang> GangLeaderChanged;

        public void JoinMember(Gang gang, Character member, bool joinChannel)
        {
            _gangRepository.InsertMember(gang, member);

            void Finish()
            {
                gang.SetMember(member);

                Dictionary<string, object> data = new()
                {
                    {k.data,gang.GetGangData()},
                    {k.memberID, member.Id}
                };

                Message.Builder.SetCommand(Commands.GangAddMember).WithData(data).ToCharacters(gang.GetMembers()).Send();

                GangMemberJoined?.Invoke(gang, member);

                if (joinChannel)
                {
                    _channelManager.JoinChannel(gang.ChannelName, member);
                }
            }

            if (Transaction.Current != null)
            {
                Transaction.Current.OnCommited(Finish);
            }
            else
            {
                Finish();
            }
        }

        public event Action<Gang, Character> GangCreate;
        public event Action<Gang, Character> GangMemberJoined;
        public event Action<Gang, Character> GangMemberRemoved;

        public void SetRole(Gang gang, Character member, GangRole newRole)
        {
            if (gang.Leader == member)
            {
                return;
            }

            if (!gang.IsMember(member))
            {
                throw new PerpetuumException(ErrorCodes.CharacterNotInTheCurrentGang);
            }

            _gangRepository.UpdateMemberRole(gang, member, newRole);

            void Finish()
            {
                gang.SetMember(member, newRole);
                Message.Builder.SetCommand(Commands.GangSetRole).WithData(new Dictionary<string, object>
                {
                    { k.memberID, member.Id },
                    { k.role, (int)newRole }
                }).ToCharacters(gang.GetMembers()).Send();

                ChannelMemberRole channelMemberRole = gang.HasRole(member, GangRole.Assistant) ? ChannelMemberRole.Operator : ChannelMemberRole.Undefined;
                _channelManager.SetMemberRole(gang.ChannelName, member, channelMemberRole);
            }

            if (Transaction.Current == null)
            {
                Transaction.Current.OnCommited(Finish);
            }
            else
            {
                Finish();
            }
        }

    }
}