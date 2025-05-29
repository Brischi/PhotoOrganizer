// Datei speichern in: Model/PhotoFileInfo.cs

using System;

namespace AppleTransfer.Model
{
    // Diese Klasse speichert alle Infos zu einer Foto- oder Videodatei,
    // damit die Logik damit flexibel arbeiten kann.
    public class PhotoFileInfo
    {
        public string OriginalPath { get; set; }      // Ursprünglicher Pfad der Datei
        public string FileName { get; set; }          // Neuer Dateiname (z.B. "2024-06-01_14-12-30.jpg")
        public DateTime? CaptureDate { get; set; }    // Aufnahmedatum (aus EXIF, QuickTime oder Dateisystem)
        public string TargetFolder { get; set; }      // Zielordner (nach Jahr/Monat)
        public string TargetPath { get; set; }        // Zielpfad inkl. neuem Dateinamen
        public bool IsDuplicate { get; set; }         // True, wenn schon Datei mit gleichem Datum im Zielordner ist
        public bool IsExactDuplicate { get; set; }    // True, wenn exakt gleiche Datei im Ziel ist
        public bool HasNoDate { get; set; }           // True, wenn kein Aufnahmedatum gefunden wurde
    }
}
