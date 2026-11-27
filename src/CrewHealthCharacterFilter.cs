using Barotrauma;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace CrewHealthDisplayMod
{
    public static class CrewHealthCharacterFilter
    {
        private static readonly HashSet<Character> charactersSeenEnabled = new HashSet<Character>();

        public static int CountDrawableCharacters(bool isEditorMode, bool hideAICrew, bool hideDeadCrew, bool hideHealthyCrew, bool alwaysShowSelf, int hideHealthyThresholdPercent)
        {
            int count = 0;
            for (int i = 0; i < Character.CharacterList.Count; i++)
            {
                if (ShouldDrawCharacter(Character.CharacterList[i], isEditorMode, hideAICrew, hideDeadCrew, hideHealthyCrew, alwaysShowSelf, hideHealthyThresholdPercent))
                {
                    count++;
                }
            }
            return count;
        }

        public static int CountCandidates(bool isEditorMode)
        {
            int count = 0;
            for (int i = 0; i < Character.CharacterList.Count; i++)
            {
                if (isEditorMode ? IsEditorCandidate(Character.CharacterList[i]) : IsCrewCandidate(Character.CharacterList[i]))
                {
                    count++;
                }
            }
            return count;
        }

        public static bool ShouldDrawCharacter(Character character, bool isEditorMode, bool hideAICrew, bool hideDeadCrew, bool hideHealthyCrew, bool alwaysShowSelf, int hideHealthyThresholdPercent)
        {
            if (isEditorMode) { return IsEditorCandidate(character); }
            if (!IsCrewCandidate(character)) { return false; }
            if (hideAICrew && IsAICrewMember(character)) { return false; }
            if (hideDeadCrew && character.IsDead) { return false; }
            UpdateKnownSyncState(character);
            if (IsDistantCrewMember(character)) { return true; }
            if (IsAwaitingInitialSync(character)) { return true; }
            if (hideHealthyCrew && !(alwaysShowSelf && character == Character.Controlled) && IsHealthyCrewMember(character, hideHealthyThresholdPercent)) { return false; }
            return true;
        }

        public static bool IsAICrewMember(Character character)
        {
            return character != null && !character.IsPlayer && character.AIController is HumanAIController;
        }

        public static void ClearRoundState()
        {
            charactersSeenEnabled.Clear();
        }

        private static bool IsEditorCandidate(Character character)
        {
            if (character == null) { return false; }
            if (character.Removed) { return false; }
            if (character.CharacterHealth == null) { return false; }
            if (character.MaxHealth <= 0.0f) { return false; }
            return true;
        }

        private static bool IsCrewCandidate(Character character)
        {
            if (character == null) { return false; }
            if (character.Removed) { return false; }
            if (Character.Controlled != null && character.TeamID != Character.Controlled.TeamID) { return false; }
            if (!character.IsHuman) { return false; }
            if (character.Info == null) { return false; }
            if (character.CharacterHealth == null) { return false; }
            if (character.MaxHealth <= 0.0f) { return false; }
            return true;
        }

        public static bool IsDistantCrewMember(Character character)
        {
            if (character == null) { return false; }
            if (character.Removed) { return false; }
            UpdateKnownSyncState(character);
            if (character.Enabled) { return false; }
            if (character.IsDead) { return false; }
            return charactersSeenEnabled.Contains(character);
        }

        public static bool IsAwaitingInitialSync(Character character)
        {
            if (character == null) { return false; }
            if (character.Removed) { return false; }
            if (character.Enabled) { return false; }
            if (character.IsDead) { return false; }
            return !charactersSeenEnabled.Contains(character);
        }

        private static void UpdateKnownSyncState(Character character)
        {
            if (character == null) { return; }
            if (character.Removed)
            {
                charactersSeenEnabled.Remove(character);
                return;
            }
            if (character.Enabled || character == Character.Controlled)
            {
                charactersSeenEnabled.Add(character);
            }
        }

        private static bool IsHealthyCrewMember(Character character, int hideHealthyThresholdPercent)
        {
            if (character == null) { return false; }
            if (character.IsDead) { return false; }
            if (character.Health <= 0.0f) { return false; }
            float maxHealth = Math.Max(character.MaxHealth, 1.0f);
            float healthRatio = MathHelper.Clamp(character.Health / maxHealth, 0.0f, 1.0f);
            float thresholdRatio = hideHealthyThresholdPercent / 100.0f;
            return healthRatio > thresholdRatio;
        }
    }
}
