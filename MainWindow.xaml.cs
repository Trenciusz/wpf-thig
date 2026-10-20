using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Microsoft.Win32;

namespace AtletikaRajtszamok
{
    public partial class MainWindow : Window
    {
        private const string CsvFejlec = "Rajtszam;Nev;Versenyszam;Nem;Eletkor;OR;VR;ER";
        private const int MinRajtszam = 1;
        private const int MaxRajtszam = 9999;

        public ObservableCollection<Versenyzo> Versenyzok { get; } = new ObservableCollection<Versenyzo>();

        public MainWindow()
        {
            DataContext = this;
            InitializeComponent();
            AlkalmazSzuro();
        }

        private void Eletkor_Valtozott(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (txtEletkor == null)
            {
                return;
            }
            txtEletkor.Text = ((int)Math.Round(e.NewValue)).ToString(CultureInfo.InvariantCulture) + " év";
        }

        private void Szuro_Valtozott(object sender, SelectionChangedEventArgs e)
        {
            if (txtLetszam == null || cmbRekordSzuro == null)
            {
                return;
            }
            AlkalmazSzuro();
        }

        private bool SzuroFeltetel(object elem)
        {
            Versenyzo v = elem as Versenyzo;
            if (v == null)
            {
                return false;
            }
            switch (cmbRekordSzuro.SelectedIndex)
            {
                case 1: return v.OR;
                case 2: return v.VR;
                case 3: return v.ER;
                default: return true; 
            }
        }

        private void AlkalmazSzuro()
        {
            ICollectionView nezet = CollectionViewSource.GetDefaultView(Versenyzok);
            nezet.Filter = SzuroFeltetel;
            nezet.Refresh();
            FrissitSzamlalo();
        }

        private void FrissitSzamlalo()
        {
            ICollectionView nezet = CollectionViewSource.GetDefaultView(Versenyzok);
            int lathato = nezet.Cast<object>().Count();
            txtLetszam.Text = "Látható: " + lathato.ToString(CultureInfo.InvariantCulture)
                            + " / Összesen: " + Versenyzok.Count.ToString(CultureInfo.InvariantCulture);
        }

        private static void Figyelmeztet(string uzenet)
        {
            MessageBox.Show(uzenet, "Figyelmeztetés", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private static void Hiba(string uzenet)
        {
            MessageBox.Show(uzenet, "Hiba", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void Regisztral_Click(object sender, RoutedEventArgs e)
        {
            string nev = txtNev.Text.Trim();
            if (nev.Length == 0)
            {
                Figyelmeztet("A versenyző nevének megadása kötelező.");
                return;
            }
            if (nev.Contains(";") || nev.Contains("|"))
            {
                Figyelmeztet("A névben pontosvessző és függőleges vonal nem használható.");
                return;
            }

            if (cmbVersenyszam.SelectedItem == null)
            {
                Figyelmeztet("Válasszon versenyszámot.");
                return;
            }
            string versenyszam;
            ComboBoxItem kijelolt = cmbVersenyszam.SelectedItem as ComboBoxItem;
            if (kijelolt != null)
            {
                versenyszam = Convert.ToString(kijelolt.Content, CultureInfo.InvariantCulture);
            }
            else
            {
                versenyszam = Convert.ToString(cmbVersenyszam.SelectedItem, CultureInfo.InvariantCulture);
            }

            string nem;
            if (rbFerfi.IsChecked == true)
            {
                nem = "F";
            }
            else if (rbNo.IsChecked == true)
            {
                nem = "N";
            }
            else
            {
                Figyelmeztet("Adja meg a versenyző nemét.");
                return;
            }

            HashSet<int> foglalt = new HashSet<int>(Versenyzok.Select(v => v.Rajtszam));
            string kertSzoveg = txtRajtszam.Text.Trim();
            int rajtszam;

            if (kertSzoveg.Length > 0)
            {
                int ertek;
                if (!int.TryParse(kertSzoveg, NumberStyles.None, CultureInfo.InvariantCulture, out ertek)
                    || ertek < MinRajtszam || ertek > MaxRajtszam)
                {
                    Figyelmeztet("A rajtszám 1 és 9999 közötti egész szám lehet.");
                    return;
                }

                if (foglalt.Contains(ertek))
                {
                    Figyelmeztet("A kért rajtszám már foglalt. Adjon meg másikat, vagy hagyja üresen a mezőt.");
                    return;
                }
                rajtszam = ertek;
            }
            else
            {
                int jelolt = MinRajtszam;
                while (jelolt <= MaxRajtszam && foglalt.Contains(jelolt))
                {
                    jelolt++;
                }
                if (jelolt > MaxRajtszam)
                {
                    Figyelmeztet("Nincs több szabad rajtszám.");
                    return;
                }
                rajtszam = jelolt;
            }

            Versenyzo uj = new Versenyzo
            {
                Rajtszam = rajtszam,
                Nev = nev,
                Versenyszam = versenyszam,
                Nem = nem,
                Eletkor = (int)Math.Round(sldEletkor.Value),
                OR = chkOlimpiai.IsChecked == true,
                VR = chkVB.IsChecked == true,
                ER = chkEB.IsChecked == true
            };

            Versenyzok.Add(uj);
            FrissitSzamlalo(); 

            if (SzuroFeltetel(uj))
            {
                dgVersenyzok.ScrollIntoView(uj);
            }

            MessageBox.Show("A regisztráció sikeres. Kiosztott rajtszám: " + rajtszam.ToString(CultureInfo.InvariantCulture),
                            "Regisztráció", MessageBoxButton.OK, MessageBoxImage.Information);

            txtNev.Clear();
            txtRajtszam.Clear();
            txtNev.Focus();
        }

        private void Mentes_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "CSV-fájl (*.csv)|*.csv|Szövegfájl (*.txt)|*.txt";
            dialog.FilterIndex = 1;
            dialog.FileName = "atletika.csv";
            dialog.AddExtension = false;

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            bool csv = dialog.FilterIndex == 1;
            string vartKiterjesztes = csv ? ".csv" : ".txt";
            string utvonal = dialog.FileName;
            string kiterjesztes = Path.GetExtension(utvonal);

            if (string.IsNullOrEmpty(kiterjesztes))
            {
                utvonal += vartKiterjesztes;
                if (File.Exists(utvonal))
                {
                    MessageBoxResult valasz = MessageBox.Show(
                        Path.GetFileName(utvonal) + " már létezik. Felülírja?",
                        "Mentés", MessageBoxButton.YesNo, MessageBoxImage.Question);
                    if (valasz != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
            }
            else if (!string.Equals(kiterjesztes, vartKiterjesztes, StringComparison.OrdinalIgnoreCase))
            {
                Figyelmeztet("A kiválasztott fájltípus (" + (csv ? "CSV" : "TXT")
                             + ") és a fájlnév kiterjesztése (" + kiterjesztes + ") nem egyezik. A mentés nem történt meg.");
                return;
            }

            try
            {
                List<string> sorok = csv ? CsvSorok() : TxtSorok();
                File.WriteAllLines(utvonal, sorok, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Hiba("A mentés nem sikerült: " + ex.Message);
            }
        }

        private List<string> CsvSorok()
        {
            List<string> sorok = new List<string>();
            sorok.Add(CsvFejlec);
            foreach (Versenyzo v in Versenyzok)
            {
                sorok.Add(string.Join(";", new string[]
                {
                    v.Rajtszam.ToString(CultureInfo.InvariantCulture),
                    v.Nev,
                    v.Versenyszam,
                    v.Nem,
                    v.Eletkor.ToString(CultureInfo.InvariantCulture),
                    v.OR ? "1" : "0",
                    v.VR ? "1" : "0",
                    v.ER ? "1" : "0"
                }));
            }
            return sorok;
        }

        private List<string> TxtSorok()
        {
            List<string> sorok = new List<string>();
            bool elso = true;
            foreach (Versenyzo v in Versenyzok)
            {
                if (!elso)
                {
                    sorok.Add("");
                }
                elso = false;

                string nem = v.Nem == "F" ? "férfi" : "női";
                string bekezdes =
                    "A " + v.Eletkor.ToString(CultureInfo.InvariantCulture) + " éves " + nem
                    + " versenyző a(z) " + v.Versenyszam + " versenyszámban indul, rajtszáma "
                    + v.Rajtszam.ToString(CultureInfo.InvariantCulture) + ". "
                    + "Olimpiai csúccsal " + (v.OR ? "rendelkezik" : "nem rendelkezik") + ", "
                    + "világcsúccsal " + (v.VR ? "rendelkezik" : "nem rendelkezik") + ", "
                    + "Európa-csúccsal " + (v.ER ? "rendelkezik" : "nem rendelkezik") + ".";

                sorok.Add(v.Nev);
                sorok.Add(bekezdes);
            }
            return sorok;
        }

        private void Betoltes_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "CSV-fájl (*.csv)|*.csv";
            dialog.CheckFileExists = true;

            if (dialog.ShowDialog() != true)
            {
                return;
            }

            try
            {
                string[] fajlSorok = File.ReadAllLines(dialog.FileName, new UTF8Encoding(false));
                List<Versenyzo> ideiglenes = new List<Versenyzo>();
                HashSet<int> latott = new HashSet<int>();

                for (int i = 1; i < fajlSorok.Length; i++) 
                {
                    string sor = fajlSorok[i];
                    if (sor.Trim().Length == 0)
                    {
                        continue;
                    }

                    string[] m = sor.Split(';');
                    for (int j = 0; j < m.Length; j++)
                    {
                        m[j] = m[j].Trim();
                    }

                    Versenyzo v = new Versenyzo
                    {
                        Rajtszam = int.Parse(m[0], NumberStyles.None, CultureInfo.InvariantCulture),
                        Nev = m[1],
                        Versenyszam = m[2],
                        Nem = m[3],
                        Eletkor = int.Parse(m[4], NumberStyles.None, CultureInfo.InvariantCulture),
                        OR = m[5] == "1",
                        VR = m[6] == "1",
                        ER = m[7] == "1"
                    };

                    if (!latott.Add(v.Rajtszam))
                    {
                        Hiba("A fájlban ismétlődő rajtszám szerepel: " + v.Rajtszam.ToString(CultureInfo.InvariantCulture)
                             + ". A betöltés megszakadt, a korábbi adatok megmaradtak.");
                        return;
                    }
                    ideiglenes.Add(v);
                }

                Versenyzok.Clear();
                foreach (Versenyzo v in ideiglenes)
                {
                    Versenyzok.Add(v);
                }
            }
            catch (Exception ex)
            {
                Hiba("A betöltés nem sikerült: " + ex.Message);
            }
        }
    }
}
