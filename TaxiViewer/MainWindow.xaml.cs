using Microsoft.Win32;
using Newtonsoft.Json;
using RurouniJones.DCS.Airfields.Structure;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace TaxiViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private string FileName;
        private static Airfield airfield; 

        public MainWindow()
        {
            InitializeComponent();
        }

        private void DisplayGraph()
        {
            var _ = airfield.TaxiNavigationGraph;
        }

        private void Load_Airfield(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                InitialDirectory = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, @"Data")
        };
            if (openFileDialog.ShowDialog() == true)
            {
                FileName = openFileDialog.FileName;
                airfield = JsonConvert.DeserializeObject<Airfield>(File.ReadAllText(FileName));
                ReloadAirfieldButton.IsEnabled = true;
                DisplayGraph();
            }
        }

        private void Reload_Airfield(object sender, RoutedEventArgs e)
        {
            airfield = JsonConvert.DeserializeObject<Airfield>(File.ReadAllText(FileName));
            DisplayGraph();
        }
    }
}
