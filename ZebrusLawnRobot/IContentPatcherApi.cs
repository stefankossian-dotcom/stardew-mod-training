using System;
using System.Collections.Generic;
using StardewModdingAPI;

namespace ZebrusLawnRobot
{
    public interface IContentPatcherApi
    {
        /// <summary>Registriert ein neues Token.</summary>
        /// <param name="mod">Das Manifest der Mod.</param>
        /// <param name="name">Der Name des Tokens.</param>
        /// <param name="getValue">Die Funktion, die den Wert liefert.</param>
        void RegisterToken(IManifest mod, string name, Func<IEnumerable<string>> getValue);
    }
}