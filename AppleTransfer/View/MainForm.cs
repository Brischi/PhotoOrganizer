using System;
using System.Drawing;
using System.Windows.Forms;
using AppleTransfer.Controller;
using AppleTransfer.Model;

namespace AppleTransfer.View
{
    public partial class MainForm : Form
    {
        // Der Controller steuert alles und "kennt" die Model-Daten
        private PhotoOrganizerController controller = new PhotoOrganizerController();
        public MainForm()
        {
            InitializeComponent();
            this.Icon = new Icon("../../Logo.ico"); // Pfad zur .ico-Datei anpassen!
            // Events für Statusmeldungen und Statistiken abonnieren
            controller.StatusChanged += (msg) =>
                Invoke((Action)(() => logBox.Items.Add(msg))); // logBox: ListBox für Statusmeldungen
            controller.ProgressChanged += Controller_ProgressChanged;
            controller.ProgressChanged += (stats) =>
            {
                Invoke((Action)(() =>
                {
                    lblStatus.Text = $"Verschoben: {stats.Verschoben}, Doppelt: {stats.Doppelt}, Exakt: {stats.ExaktGleich}, OhneDatum: {stats.OhneDatum}, Übersprungen: {stats.Übersprungen}";
                }));
            };
        }

        // Quellordner durchsuchen
        private void btnBrowseSource_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                    txtSource.Text = dlg.SelectedPath;
            }
        }

        // Zielordner durchsuchen
        private void btnBrowseTarget_Click(object sender, EventArgs e)
        {
            using (var dlg = new FolderBrowserDialog())
            {
                if (dlg.ShowDialog() == DialogResult.OK)
                    txtTarget.Text = dlg.SelectedPath;
            }
        }

        // Start-Button
        private void btnStart_Click(object sender, EventArgs e)
        {
            // Gesamtanzahl aus dem Controller holen (optional, aber für ProgressBar nötig)
            int gesamt = controller.ZuVerarbeitendeDateien;

            progressBar1.Minimum = 0;
            progressBar1.Maximum = gesamt; // oder controller.ZuVerarbeitendeDateien
            progressBar1.Value = 0;

            logBox.Items.Clear();
            lblStatus.Text = "Vorgang läuft...";

            // Kopiervorgang EINMAL starten und Rückgabewert merken!
            var stats = controller.SortAndOrganizePhotos(txtSource.Text, txtTarget.Text);

            lblStatus.Text = "Fertig!";

            // Statistik anzeigen
            logBox.Items.Add(
              $"Verschoben: {stats.Verschoben} | " +
              $"Doppelt: {stats.Doppelt} | " +
              $"Exakt gleich: {stats.ExaktGleich} | " +
              $"Ohne Datum: {stats.OhneDatum} | " +
              $"Übersprungen: {stats.Übersprungen} | " +
              $"Fertig!"
            );
        }

        private void Controller_ProgressChanged(OrganizerStats stats)
        {
            // Gesamtanzahl bitte aus deinem Controller holen!
            int gesamt = controller.ZuVerarbeitendeDateien;

            // Zähler berechnen: Alle abgeschlossenen Operationen
            int erledigt = stats.Verschoben + stats.Doppelt + stats.ExaktGleich + stats.OhneDatum + stats.Übersprungen;

            // ProgressBar updaten
            // Achtung: Wenn du im Hintergrundthread arbeitest, dann so:
            if (progressBar1.InvokeRequired)
            {
                progressBar1.Invoke(new Action(() => progressBar1.Value = Math.Min(erledigt, gesamt)));
            }
            else 
            {
                progressBar1.Value = Math.Min(erledigt, progressBar1.Maximum);
            }
        }



    }
}
