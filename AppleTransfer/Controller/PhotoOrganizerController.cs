using System;
using System.IO;
using System.Linq;
using AppleTransfer.Model;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;

namespace AppleTransfer.Controller
{
    // Der Controller vermittelt zwischen Model (Datenstruktur) und View (Benutzeroberfläche)
    public class PhotoOrganizerController
    {
        // EVENT-UPDATES FÜR DIE VIEW   
        public event Action<string> StatusChanged;// Event: Teilt der View jede Statusänderung als Textmeldung mit (zB für Logfenster)
        public event Action<OrganizerStats> ProgressChanged; // Event: Übergibt der View jeweils die aktuellen Statistikdaten
        public int ZuVerarbeitendeDateien { get; private set; } 

        private OrganizerStats stats = new OrganizerStats(); // Instanz für Zähler (Verschoben, ExaktGleich, OhneDatum, Übersprungen)

        // ///////////////////////////////////////////////////////////////////////////////////////////
        // Hauptfunktion: Findet, benennt um und verschiebt alle Bilder/Videos entsprechend den Regeln
        // ///////////////////////////////////////////////////////////////////////////////////////////
        public OrganizerStats SortAndOrganizePhotos(string sourceFolder, string targetFolder)
        {
            // Statistikzähler zu Beginn zurücksetzen
            stats = new OrganizerStats(); 

            // (1) Ordner für Spezialfälle erstellen
            // Doppelte Datumsfotos, exakt gleiche Namen, Bilder ohne Datum
            string doppeltOrdner = Path.Combine(targetFolder, "Doppelt");
            string exaktGleichOrdner = Path.Combine(targetFolder, "ExaktGleich");
            string ohneDatumOrdner = Path.Combine(targetFolder, "OhneDatum");

            // Zielordner und Spezialordner erstellen, falls nicht vorhanden (Existiert der Ordner schon, passiert einfach nichts )
            System.IO.Directory.CreateDirectory(targetFolder);
            System.IO.Directory.CreateDirectory(doppeltOrdner);
            System.IO.Directory.CreateDirectory(exaktGleichOrdner);
            System.IO.Directory.CreateDirectory(ohneDatumOrdner);

            // (2) Unterstützte Dateitypen definieren
            string[] erlaubteEndungen = { ".jpg", ".jpeg", ".png", ".heic", ".mov", ".mp4", ".gif" };

            // (3) Quell- und Zieldateien bestimmen (auch Unterordner)
            // String-Array mit allen Quelldateien mit den erlaubten Endungen
            string[] quellDateien = System.IO.Directory.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories) // "*" = Nimm alle Dateien, egal welchen Namen oder Endung sie haben. Ohne Unterordner wärs: TopDirectoryOnly
                .Where(f => erlaubteEndungen.Contains(Path.GetExtension(f).ToLower()))  
                .ToArray();
            ZuVerarbeitendeDateien = quellDateien.Length;
            /*Info für mich zu LINQ: wenn ich jede Datei haben wollen würde, die als zweiten Buchstaben im Namen ein "a" hat, würd ich schreiben
                string[] gefundeneDateien = Directory.GetFiles(sourceFolder, "*.*", SearchOption.AllDirectories)
                .Where(f =>
                 {
                     string dateiname = Path.GetFileNameWithoutExtension(f);
                     // Prüfen, ob Name mindestens 2 Zeichen lang ist, damit kein Fehler kommt
                     return dateiname.Length > 1 && dateiname[1] == 'a';
                 }).ToArray();*/
            // string-Array mit allen Zieldateien im Zielordner (für Dublettenprüfung)
            string[] zielDateien = System.IO.Directory.GetFiles(targetFolder, "*.*", SearchOption.AllDirectories);

            // (4) HashSet aller ersten 19 Zeichen von Ziel-Dateinamen (zur schnellen Dublettenprüfung)
            var zielNamensAnfänge = zielDateien
                .Select(f => Path.GetFileName(f).Length >= 19 ? Path.GetFileName(f).Substring(0, 19) : null)
                .Where(f => f != null)
                .ToHashSet(StringComparer.OrdinalIgnoreCase); //HashSet (cooli für große Datenmengen) jedes Element wird vor dem Speichern in Hashwert verwandelt. also Nach dem Aufbau ist jeder einzelne Vergleich(zielNamensAnfänge.Contains(...)) viel schneller & performant! - (StringComparer.OrdinalIgnoreCase) = strings bitweise vergleichen und Groß-/Kleinschreibung ignorieren

            int processed = 0;            // Fortschrittszähler (wie viele bearbeitet?)
            int ohneDatumZaehler = 1;     // Durchnummerierung für "OhneDatum"-Dateien

            // (5) Hauptschleife: Jede Quelldatei einzeln behandeln
            foreach (string quellPfad in quellDateien)
            {
    
                processed++;
                string ext = Path.GetExtension(quellPfad).ToLower();
                string aufnahmeDateiname;
                string aktuellerPfad = quellPfad; // Arbeitskopie vom aktuellen Dateipfad

                // --- Aufnahmedatum auslesen (EXIF/QuickTime/Erstellungsdatum) ---
                DateTime? aufnahmeDatum = null; // kann ja auch null sein 
                try
                {
                    // Erst EXIF versuchen (Fotos)
                    var metadata = ImageMetadataReader.ReadMetadata(quellPfad); // Metadaten auslesen
                    // Exif-Directory aus Fotodatei rausziehen und in nen string
                    var exif = metadata.OfType<ExifSubIfdDirectory>().FirstOrDefault();  // LINQ-Methode: .OfType<ExifSubIfdDirectory>() = nur die Elemente in der Liste, die genau vom Typ ExifSubIfdDirectory sind; Linq: .FirstOrDefault() = das erste Element in dieser Liste – oder null, falls es keins gibt. Es könnte mehrere Exif Directories geben, ich brauch nur das erste
                    // DateTime aus Exif-variable rausziehen
                    if (exif != null && exif.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out DateTime exifDatum))
                        aufnahmeDatum = exifDatum;

                    // Wenn noch kein Datum: QuickTime versuchen (Videos)
                    if (aufnahmeDatum == null)
                    {
                        var quickTime = metadata.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
                        if (quickTime != null && quickTime.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out DateTime videoDatum))
                            aufnahmeDatum = videoDatum;
                    }

                    // Fallback: Erstellungsdatum als PlanB
                    if (aufnahmeDatum == null)
                        aufnahmeDatum = File.GetCreationTime(quellPfad);
                }
                catch
                {
                    // Falls Metadaten nicht gelesen werden können: Erstellungsdatum verwenden
                    aufnahmeDatum = File.GetCreationTime(quellPfad);
                }
                // finally { } nur wenn ich daten aufräumen muss, Streams schließen oder so

                // ---  Neuen Dateinamen vergeben ---
                if (aufnahmeDatum != null)
                    aufnahmeDateiname = aufnahmeDatum.Value.ToString("yyyy-MM-dd_HH-mm-ss") + ext;
                else
                    aufnahmeDateiname = Path.GetFileName(quellPfad); // Der Name bleibt wie er war, wenn kein Aufnahmedatum ermittelt wurde

                // --- Im Quellordner umbenennen, falls nötig ---
                string quellVerzeichnis = Path.GetDirectoryName(quellPfad);
                string neuerQuellPfad = Path.Combine(quellVerzeichnis, aufnahmeDateiname);

                // Prüfen, ob der Name im Quellpfad geändert werdne muss, oder obs eh der alte ist
                if (!quellPfad.Equals(neuerQuellPfad, StringComparison.OrdinalIgnoreCase))
                {
                    // Bei Namenskonflikt neuen, eindeutigen Namen erzeugen mit der Methode unten
                    if (File.Exists(neuerQuellPfad))
                        neuerQuellPfad = MacheEinzigartigenPfad(neuerQuellPfad);
                    // Datei umbenennen
                    File.Move(quellPfad, neuerQuellPfad);
                    StatusChanged?.Invoke($"Umbenannt: {Path.GetFileName(quellPfad)} → {aufnahmeDateiname}"); //StatusChanged? = Event für meine Abonenten (View); .Invoke = Newsletter über Änderung
                    aktuellerPfad = neuerQuellPfad; // um mit dem aktuellen Pfad weiterzuarbeiten
                }

                string dateiname = aufnahmeDateiname;

                // --- Wenn kein (valides) Aufnahmedatum gefunden wurde ---
                // (Oder der Name zu kurz ist)
                if (dateiname.Length < 19 || !DateTime.TryParseExact(dateiname.Substring(0, 10), "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out DateTime parsedDatum))
                {
                    // Neuer durchnummerierter Name: img_1.jpg, img_2.png, ...
                    string extOhneDatum = Path.GetExtension(dateiname);
                    string neuerNameOhneDatum = $"img_{ohneDatumZaehler}{extOhneDatum}";
                    string zielOhneDatum = Path.Combine(ohneDatumOrdner, neuerNameOhneDatum);
                    zielOhneDatum = MacheEinzigartigenPfad(zielOhneDatum); // Bei Kollision ergänzen (_1, _2, ...)
                    File.Move(aktuellerPfad, zielOhneDatum);
                    StatusChanged?.Invoke($"{dateiname} → OhneDatum (umbenannt zu {neuerNameOhneDatum})");
                    stats.OhneDatum++;
                    ohneDatumZaehler++; // Für das nächste Bild ohne Datum hochzählen
                    continue; // Nächste Datei
                }

                // --- Prüfung auf Dubletten anhand der ersten 19 Zeichen des Namens (JJJJ-MM-TT_SS-MM-SS)---
                string name19 = dateiname.Substring(0, 19);

                if (zielNamensAnfänge.Contains(name19))
                {
                    // Gibt es eine exakt gleichnamige Datei im Ziel?
                    bool exakterTreffer = zielDateien.Any(p => Path.GetFileName(p).Equals(dateiname, StringComparison.OrdinalIgnoreCase)); // LINQ: prüft, ob mindestens ein Element in einer Sammlung eine bestimmte Bedingung erfüllt)
                    if (exakterTreffer)
                    {
                        // Exakt gleicher Name: in ExaktGleich-Ordner verschieben & statusmeldung
                        string zielPfad = Path.Combine(exaktGleichOrdner, dateiname);
                        zielPfad = MacheEinzigartigenPfad(zielPfad);
                        File.Move(aktuellerPfad, zielPfad);
                        StatusChanged?.Invoke($"{dateiname} → in ExaktGleich verschoben");
                        stats.ExaktGleich++;
                    }
                    else
                    {
                        // Nur gleiches Datum, aber anderer Name: in Doppelt-Ordner verschieben
                        string zielPfad = Path.Combine(doppeltOrdner, dateiname);
                        zielPfad = MacheEinzigartigenPfad(zielPfad);
                        File.Move(aktuellerPfad, zielPfad);
                        StatusChanged?.Invoke($"{dateiname} → in Doppelt verschoben");
                        stats.Doppelt++;
                    }
                }
                else
                {
                    try
                    {
                        // --- Datei in Ziel-Jahr/Monat-Ordner verschieben ---
                        string jahr = aufnahmeDatum.Value.Year.ToString();
                        string monat = aufnahmeDatum.Value.Month.ToString("D2");
                        string zielOrdner = Path.Combine(targetFolder, jahr, monat);
                        System.IO.Directory.CreateDirectory(zielOrdner);

                        string zielPfad = Path.Combine(zielOrdner, dateiname);
                        File.Move(aktuellerPfad, zielPfad);
                        zielNamensAnfänge.Add(name19); // Präfix zum Dubletten-HashSet hinzufügen
                        StatusChanged?.Invoke($"Verschoben: {dateiname} → {zielOrdner}");
                        stats.Verschoben++;
                    }
                    catch (Exception ex) // Exeptions abfangen und in ex variable speichern und an die View schicken
                    {
                        // Fehler beim Verschieben (z.B. Datei gesperrt)
                        StatusChanged?.Invoke($"Fehler bei {dateiname}: {ex.Message}");
                        stats.Übersprungen++;
                    }
                }
            }
            ProgressChanged?.Invoke(stats);
            // Am Ende die Statistiken zurückgeben  // --- Nach jedem Schritt Statusmeldung an die View melden ---
            return stats;
        }

        // --- Hilfsmethode: Erzeugt eindeutigen Namen bei Namenskonflikt ---
        // z.B. aus "bild.jpg" → "bild_1.jpg", "bild_2.jpg", ...
        private string MacheEinzigartigenPfad(string pfad)
        {
            string ordner = Path.GetDirectoryName(pfad);
            string name = Path.GetFileNameWithoutExtension(pfad);
            string ext = Path.GetExtension(pfad);
            int zähler = 1;

            while (File.Exists(pfad))
            {
                pfad = Path.Combine(ordner, $"{name}_{zähler}{ext}");
                zähler++;
            }
            return pfad;
        }


    }

}

