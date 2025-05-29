// Datei speichern in: Model/OrganizerStats.cs

namespace AppleTransfer.Model
{
    // Diese Klasse zählt, wie viele Bilder in welcher Kategorie landen.
    public class OrganizerStats
    {
        public int Verschoben { get; set; }      // Anzahl erfolgreich verschobener Bilder
        public int Doppelt { get; set; }         // Anzahl Bilder mit identischem Datum, aber anderem Namen
        public int ExaktGleich { get; set; }     // Anzahl exakt gleicher Bilder (gleicher Name)
        public int OhneDatum { get; set; }       // Anzahl Bilder ohne (auslesbares) Aufnahmedatum
        public int Übersprungen { get; set; }    // Anzahl übersprungener Bilder (z.B. Fehler)
    }
}
