using RurouniJones.DCS.Airfields.Structure;
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
using System.Windows.Shapes;

namespace TaxiViewer
{
    /// <summary>
    /// Interaction logic for AirfieldInfoWindow.xaml
    /// </summary>
    public partial class AirfieldInformationWindow : Window
    {
        private Airfield airfield;
        private MainWindow parent;


        public AirfieldInformationWindow(Airfield airfield, MainWindow parent)
        {
            InitializeComponent();
            this.airfield = airfield;
            this.parent = parent;
            NameBox.Text = airfield.Name ?? "";
            LatBox.Text = airfield.Latitude.ToString();
            LongBox.Text = airfield.Latitude.ToString();
            AltBox.Text = airfield.Altitude.ToString();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            airfield.Name = NameBox.Text;
            try
            {
                airfield.Latitude = Double.Parse(LatBox.Text);
                airfield.Longitude = Double.Parse(LongBox.Text);
                airfield.Altitude = Double.Parse(AltBox.Text);
            }
            catch
            {
                MessageBox.Show("Failed to interpret one of your inputs to a valid number. Data not saved.");
            }
        }
    }
}
