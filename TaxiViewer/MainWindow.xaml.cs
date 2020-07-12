using Microsoft.Msagl.Drawing;
using Microsoft.Msagl.WpfGraphControl;
using Microsoft.Win32;
using Newtonsoft.Json;
using QuikGraph;
using RurouniJones.DCS.Airfields.Structure;
using System;
using System.IO;
using System.Linq;
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
            GraphPanel.Children.Clear();

            Graph graph = new Graph();

            foreach(TaxiPoint taxiPoint in airfield.TaxiNavigationGraph.Vertices)
            {
                var node = graph.AddNode(taxiPoint.Name);
                if(taxiPoint is Runway)
                {
                    node.Attr.Shape = Shape.DoubleCircle;
                } else if (taxiPoint is Junction)
                {
                    node.Attr.Shape = Shape.Diamond;
                } else if (taxiPoint is ParkingSpot)
                {
                    node.Attr.Shape = Shape.Hexagon;
                }
            }

            foreach (TaggedEdge<TaxiPoint, string> edge in airfield.TaxiNavigationGraph.Edges)
            {
                var displayEdge = graph.AddEdge(edge.Source.Name, edge.Tag, edge.Target.Name);

                if(airfield.TaxiwayCost[edge] >= 999 )
                {
                    displayEdge.Attr.Color = Color.Red;
                }
                else if(airfield.TaxiwayCost[edge] >= 99)
                {
                    displayEdge.Attr.Color = Color.Orange;
                }
                else
                {
                    displayEdge.Attr.Color = Color.Green;
                }
            }

            GraphViewer graphViewer = new GraphViewer
            {
                LayoutEditingEnabled = false
            };

            graphViewer.BindToPanel(GraphPanel);

            graphViewer.Graph = graph;
        }

        private void Load_Airfield(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json",
                InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Data")
        };
            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    FileName = openFileDialog.FileName;
                    airfield = JsonConvert.DeserializeObject<Airfield>(File.ReadAllText(FileName));
                    ReloadAirfieldButton.IsEnabled = true;
                    DisplayGraph();
                } catch(Exception _)
                {
                    MessageBox.Show("Error reading Airfield JSON", "Deserialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Reload_Airfield(object sender, RoutedEventArgs e)
        {
            try { 
              airfield = JsonConvert.DeserializeObject<Airfield>(File.ReadAllText(FileName));
              DisplayGraph();
            }
            catch (Exception _)
            {
                MessageBox.Show("Error reading Airfield JSON", "Deserialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
