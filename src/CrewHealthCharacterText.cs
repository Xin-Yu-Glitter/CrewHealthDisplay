using Barotrauma;
using System;

namespace CrewHealthDisplayMod
{
    public static class CrewHealthCharacterText
    {
        public static string GetDisplayName(Character target)
        {
            if (target == null) { return "Unknown"; }

            string name = target.Info?.DisplayName;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = target.Name;
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                name = target.SpeciesName.Value;
            }
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Unknown";
            }
            return name;
        }
    }
}
