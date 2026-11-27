using Barotrauma;
using Barotrauma.Networking;
using System.Collections.Generic;
using System.Globalization;

namespace CrewHealthDisplayMod
{
    public static class CrewHealthCharacterRemovePatch
    {
        public static void Prefix(Character __instance)
        {
            CrewHealthRemovedCrewMemory.RememberRemovedCrew(__instance);
        }
    }

    public static class CrewHealthRemovedCrewMemory
    {
        private const int MaxSnapshots = 32;

        private static readonly Dictionary<string, RemovedCrewSnapshot> snapshots = new Dictionary<string, RemovedCrewSnapshot>();
        private static readonly HashSet<string> currentCrewKeys = new HashSet<string>();
        private static readonly Dictionary<Character, string> characterKeys = new Dictionary<Character, string>();
        private static readonly List<string> identityKeys = new List<string>();
        private static object lastSession;
        private static CharacterTeamType? lastKnownTeam;
        private static int sequence;

        public sealed class RemovedCrewSnapshot
        {
            public string Key;
            public string Name;
            public int Sequence;
            public bool IsAICrew;
        }

        public static void UpdateFromCharacterList(bool isEditorMode)
        {
            if (!ReferenceEquals(lastSession, GameMain.GameSession))
            {
                lastSession = GameMain.GameSession;
                ClearRoundSnapshots();
            }

            if (isEditorMode)
            {
                ClearRoundSnapshots();
                return;
            }

            if (Character.Controlled != null)
            {
                lastKnownTeam = Character.Controlled.TeamID;
            }

            currentCrewKeys.Clear();
            if (Character.CharacterList == null) { return; }

            for (int i = 0; i < Character.CharacterList.Count; i++)
            {
                Character character = Character.CharacterList[i];
                if (!IsCrewIdentity(character)) { continue; }

                string key = GetKey(character);
                AddCurrentIdentityKeys(character, key);
                if (character.IsDead && !character.Enabled)
                {
                    RememberRemovedCrew(character);
                }
                else if (!character.IsDead)
                {
                    RemoveSnapshotsForLivingCharacter(character, key);
                }
            }
        }

        public static void RememberRemovedCrew(Character character)
        {
            if (!IsCrewIdentity(character)) { return; }
            if (!character.IsDead) { return; }

            string key = GetKey(character);
            snapshots[key] = new RemovedCrewSnapshot
            {
                Key = key,
                Name = CrewHealthCharacterText.GetDisplayName(character),
                Sequence = sequence++,
                IsAICrew = CrewHealthCharacterFilter.IsAICrewMember(character)
            };

            if (snapshots.Count <= MaxSnapshots) { return; }

            string oldestKey = null;
            int oldestSequence = int.MaxValue;
            foreach (var snapshot in snapshots.Values)
            {
                if (snapshot.Sequence >= oldestSequence) { continue; }
                oldestSequence = snapshot.Sequence;
                oldestKey = snapshot.Key;
            }
            if (oldestKey != null)
            {
                snapshots.Remove(oldestKey);
            }
        }

        public static void GetDrawableSnapshots(List<RemovedCrewSnapshot> target, bool hideAICrew)
        {
            if (target == null) { return; }
            foreach (var snapshot in snapshots.Values)
            {
                if (currentCrewKeys.Contains(snapshot.Key)) { continue; }
                if (hideAICrew && snapshot.IsAICrew) { continue; }
                target.Add(snapshot);
            }
            target.Sort((a, b) => a.Sequence.CompareTo(b.Sequence));
        }

        public static void ClearRoundSnapshots()
        {
            snapshots.Clear();
            currentCrewKeys.Clear();
            characterKeys.Clear();
            identityKeys.Clear();
            lastKnownTeam = Character.Controlled?.TeamID;
            sequence = 0;
        }

        private static bool IsCrewIdentity(Character character)
        {
            if (character == null) { return false; }
            if (!character.IsHuman) { return false; }
            if (character.Info == null) { return false; }
            if (character.CharacterHealth == null) { return false; }
            if (character.MaxHealth <= 0.0f) { return false; }

            CharacterTeamType? team = Character.Controlled?.TeamID ?? lastKnownTeam;
            if (team.HasValue && character.TeamID != team.Value) { return false; }
            return true;
        }

        private static string GetKey(Character character)
        {
            if (character != null && characterKeys.TryGetValue(character, out string cachedKey))
            {
                return cachedKey;
            }

            string key = CreateKey(character);
            if (character != null)
            {
                characterKeys[character] = key;
            }
            return key;
        }

        private static string CreateKey(Character character)
        {
            string accountKey = GetAccountKey(character);
            if (accountKey != null)
            {
                return accountKey;
            }
            if (character?.Info != null && character.Info.ID > 0)
            {
                return "info:" + character.Info.ID.ToString(CultureInfo.InvariantCulture);
            }
            string persistentInfoKey = GetPersistentInfoKey(character);
            if (persistentInfoKey != null)
            {
                return persistentInfoKey;
            }
            return "name:" + CrewHealthCharacterText.GetDisplayName(character);
        }

        private static void AddCurrentIdentityKeys(Character character, string canonicalKey)
        {
            BuildIdentityKeys(character, canonicalKey);
            for (int i = 0; i < identityKeys.Count; i++)
            {
                currentCrewKeys.Add(identityKeys[i]);
            }
        }

        private static void RemoveSnapshotsForLivingCharacter(Character character, string canonicalKey)
        {
            BuildIdentityKeys(character, canonicalKey);
            for (int i = 0; i < identityKeys.Count; i++)
            {
                snapshots.Remove(identityKeys[i]);
            }
        }

        private static void BuildIdentityKeys(Character character, string canonicalKey)
        {
            identityKeys.Clear();
            AddIdentityKey(canonicalKey);
            AddIdentityKey(GetAccountKey(character));

            CharacterInfo info = character?.Info;
            if (info != null && info.ID > 0)
            {
                AddIdentityKey("info:" + info.ID.ToString(CultureInfo.InvariantCulture));
            }

            AddIdentityKey(GetPersistentInfoKey(character));
            if (identityKeys.Count == 0)
            {
                AddIdentityKey("name:" + CrewHealthCharacterText.GetDisplayName(character));
            }
        }

        private static void AddIdentityKey(string key)
        {
            if (string.IsNullOrEmpty(key) || identityKeys.Contains(key)) { return; }
            identityKeys.Add(key);
        }

        private static string GetPersistentInfoKey(Character character)
        {
            CharacterInfo info = character?.Info;
            if (info == null || info.Head?.Preset == null || string.IsNullOrWhiteSpace(info.OriginalName))
            {
                return null;
            }

            int identifier = info.GetIdentifierUsingOriginalName();
            return "identity:" + identifier.ToString(CultureInfo.InvariantCulture);
        }

        private static string GetAccountKey(Character character)
        {
            if (character == null || GameMain.Client == null) { return null; }

            IReadOnlyList<Client> connectedClients = GameMain.Client.ConnectedClients;
            for (int i = 0; i < connectedClients.Count; i++)
            {
                Client client = connectedClients[i];
                if (client.Character != character) { continue; }
                if (client.AccountId.TryUnwrap(out AccountId accountId))
                {
                    return "account:" + accountId.StringRepresentation;
                }
            }

            foreach (Client client in GameMain.Client.PreviouslyConnectedClients)
            {
                if (client.Character != character) { continue; }
                if (client.AccountId.TryUnwrap(out AccountId accountId))
                {
                    return "account:" + accountId.StringRepresentation;
                }
            }

            return null;
        }
    }
}
