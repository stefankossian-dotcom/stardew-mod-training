using System;
using System.Collections.Generic;
using StardewModdingAPI;

namespace BirthdayBoard
{
    public interface IContentPatcherAPI
    {
        // FAKT: Dies ist der standardisierte Befehl, um eigene Platzhalter für die content.json zu registrieren
        void RegisterToken(IManifest mod, string name, Func<IEnumerable<string>> getValue);
    }
}