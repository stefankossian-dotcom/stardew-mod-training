using System;
using StardewModdingAPI;

namespace ZebrusQuestBoard
{
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string> tooltip = null, string fieldId = null);

        // FAKT: Dies ist der neue Befehl für GMCM-Überschriften
        void AddSectionTitle(IManifest mod, Func<string> text, Func<string> tooltip = null);
    }
}