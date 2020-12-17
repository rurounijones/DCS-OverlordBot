using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.IO;
using RurouniJones.DCS.Airfields.Structure;

namespace TaxiViewer
{
    /// <summary>
    /// Interaction logic for MarketImportWindow.xaml
    /// </summary>
    public partial class MarkerImportWindow : Window
    {
        public MarkerImport.Rootobject ImportData;
        public Airfield Airfield { get; set; }
        public MainWindow ParentWindow { get; set; }

        public MarkerImportWindow(Airfield airfield, MainWindow mainWindow)
        {
            InitializeComponent();
            ParentWindow = mainWindow;
            Airfield = airfield;
        }

        private void MarkerImportButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ImportData = JsonConvert.DeserializeObject<MarkerImport.Rootobject>(File.ReadAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "temp", "markers-edited.json")));

                foreach (var x in ImportData.savedPoints)
                {
                    //Skip if already in the airport...
                    if (Airfield.NavigationGraph.Vertices.FirstOrDefault(v => v.Latitude == x.lat && v.Longitude == x.lon) != null) continue;

                    MarkerListBox.Items.Add(x);
                    x.navpoint = new NavigationPoint() { Latitude = x.lat, Longitude = x.lon, Name = x.name };
                    Airfield.NavigationGraph.AddVertex(x.navpoint);
                }

                ParentWindow.DisplayGraph();
            }
            catch(Exception)
            {
                MessageBox.Show("Failed to load marker json");
                return;
            }

            
        }

        private void MarkerListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count != 1) return;
            ParentWindow.highlightPoint = ((MarkerImport.Savedpoint)(e.AddedItems[0])).navpoint;
            ParentWindow.DisplayGraph();
        }

        private void RunwayEndButton_Click(object sender, RoutedEventArgs e)
        {
            if (MarkerListBox.SelectedIndex == -1) return;
            var sel = CategorizationPrep();

            var rwy = new Runway() { Latitude = sel.lat, Longitude = sel.lon, Name = sel.name };
            Airfield.NavigationGraph.AddVertex(rwy);
            Airfield.Runways.Add(rwy);
            ParentWindow.DisplayGraph();
        }

        private void IntersectionButton_Click(object sender, RoutedEventArgs e)
        {
            if (MarkerListBox.SelectedIndex == -1) return;
            var sel = CategorizationPrep();


            var jct = new Junction() { Latitude = sel.lat, Longitude = sel.lon, Name = sel.name };
            Airfield.NavigationGraph.AddVertex(jct);
            Airfield.Junctions.Add(jct);
            ParentWindow.DisplayGraph();
        }

        private MarkerImport.Savedpoint CategorizationPrep()
        {
            ParentWindow.highlightPoint = null;
            var sel = ((MarkerImport.Savedpoint)(MarkerListBox.SelectedItem));
            Airfield.NavigationGraph.RemoveVertex(sel.navpoint);
            MarkerListBox.Items.Remove(sel);
            MarkerListBox.SelectedIndex = 0;

            return sel;
        }

        private void ParkingButton_Click(object sender, RoutedEventArgs e)
        {
            if (MarkerListBox.SelectedIndex == -1) return;
            var sel = CategorizationPrep();

            var pkg = new ParkingSpot() { Latitude = sel.lat, Longitude = sel.lon, Name = sel.name };
            Airfield.NavigationGraph.AddVertex(pkg);
            Airfield.ParkingSpots.Add(pkg);
            ParentWindow.DisplayGraph();
        }
    }
}
