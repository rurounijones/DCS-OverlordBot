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
    /// Interaction logic for NodeListWindow.xaml
    /// </summary>
    public partial class NodeListWindow : Window
    {
        MainWindow ParentWindow;
        Airfield Airfield;
        public NodeListWindow(MainWindow parentwindow, Airfield airfield)
        {
            InitializeComponent();

            ParentWindow = parentwindow;
            Airfield = airfield;

            RefreshButton_Click(this, new RoutedEventArgs());
        }

        private void RunwayList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;
            var rwy = ((Runway)(e.AddedItems[0]));
            RunwayNameBox.Text = rwy.Name;
            RunwayLatBox.Text = rwy.Latitude.ToString();
            RunwayLongBox.Text = rwy.Longitude.ToString();
            RunwayHeadingBox.Text = rwy.Heading.ToString();

            ParentWindow.highlightPoint = rwy;
            ParentWindow.DisplayGraph();
        }

        private void RunwaySave_Click(object sender, RoutedEventArgs e)
        {
            var rwy = ((Runway)(RunwayList.SelectedItem));
            var oldName = rwy.Name;
            rwy.Name = RunwayNameBox.Text;
            try
            {
                rwy.Latitude = Double.Parse(RunwayLatBox.Text);
                rwy.Longitude = Double.Parse(RunwayLongBox.Text);
                rwy.Heading = Int32.Parse(RunwayHeadingBox.Text);
            }
            catch
            {
                MessageBox.Show("Failed to interpret one of your inputs to a valid number. Data not saved.");
            }
            foreach (var f in Airfield.Taxiways)
            {
                if (f.Source == oldName) f.Source = rwy.Name;
                if (f.Target == oldName) f.Target = rwy.Name;
            }
            ParentWindow.DisplayGraph();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            RunwayList.Items.Clear();
            foreach (var x in Airfield.Runways) RunwayList.Items.Add(x);
            JunctionList.Items.Clear();
            foreach (var x in Airfield.Junctions) JunctionList.Items.Add(x);
            ParkingSpotBox.Items.Clear();
            foreach (var x in Airfield.ParkingSpots) ParkingSpotBox.Items.Add(x);
        }

        private void JunctionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;
            var jct = ((Junction)(e.AddedItems[0]));
            JunctionNameBox.Text = jct.Name;
            JunctionLatBox.Text = jct.Latitude.ToString();
            JunctionLongBox.Text = jct.Longitude.ToString();

            ParentWindow.highlightPoint = jct;
            ParentWindow.DisplayGraph();
        }

        private void RunwayDelete_Click(object sender, RoutedEventArgs e)
        {
            var rwy = ((Runway)(RunwayList.SelectedItem));
            Airfield.Runways.Remove(rwy);
            RunwayList.Items.Remove(rwy);

            ParentWindow.DisplayGraph();

        }

        private void JunctionSave_Click(object sender, RoutedEventArgs e)
        {
            var jct = ((Junction)(JunctionList.SelectedItem));
            var oldName = jct.Name;
            jct.Name = JunctionNameBox.Text;
            try
            {
                jct.Latitude = Double.Parse(JunctionLatBox.Text);
                jct.Longitude = Double.Parse(JunctionLongBox.Text);
            }
            catch
            {
                MessageBox.Show("Failed to interpret one of your inputs to a valid number. Data not saved.");
            }
            
            foreach(var f in Airfield.Taxiways)
            {
                if (f.Source == oldName) f.Source = jct.Name;
                if (f.Target == oldName) f.Target = jct.Name;
            }

            ParentWindow.DisplayGraph();
        }

        private void JunctionDelete_Click(object sender, RoutedEventArgs e)
        {
            var jct = ((Junction)(JunctionList.SelectedItem));
            Airfield.Junctions.Remove(jct);
            JunctionList.Items.Remove(jct);

            ParentWindow.DisplayGraph();
        }

        private void ParkingSpotBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;
            var jct = ((Junction)(e.AddedItems[0]));
            ParkingSpotName.Text = jct.Name;
            ParkingSpotLat.Text = jct.Latitude.ToString();
            ParkingSpotLong.Text = jct.Longitude.ToString();

            ParentWindow.highlightPoint = jct;
            ParentWindow.DisplayGraph();
        }

        private void ParkingSpotSave_Click(object sender, RoutedEventArgs e)
        {
            var ps = ((ParkingSpot)(ParkingSpotBox.SelectedItem));
            var oldName = ps.Name;
            ps.Name = ParkingSpotName.Text;
            try
            {
                ps.Latitude = Double.Parse(ParkingSpotLat.Text);
                ps.Longitude = Double.Parse(ParkingSpotLong.Text);
            }
            catch
            {
                MessageBox.Show("Failed to interpret one of your inputs to a valid number. Data not saved.");
            }
            foreach (var f in Airfield.Taxiways)
            {
                if (f.Source == oldName) f.Source = ps.Name;
                if (f.Target == oldName) f.Target = ps.Name;
            }
            ParentWindow.DisplayGraph();
        }
    }
}
