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

        private static VNode SourceNode;
        private static VNode TargetNode;

        public MainWindow()
        {
            InitializeComponent();
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
                    SaveAirfieldButton.IsEnabled = true;

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

        private void Save_Airfield(object sender, RoutedEventArgs e)
        {
            try
            {
                string json = JsonConvert.SerializeObject(airfield, Formatting.Indented);
                File.WriteAllText(FileName, json);
            }
            catch (Exception _)
            {
                MessageBox.Show("Error writing Airfield JSON", "Serialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DisplayGraph()
        {
            GraphPanel.Children.Clear();

            Graph graph = new Graph();

            foreach (TaxiPoint taxiPoint in airfield.TaxiNavigationGraph.Vertices)
            {
                var node = graph.AddNode(taxiPoint.Name);
                if (taxiPoint is Runway)
                {
                    node.Attr.Shape = Shape.DoubleCircle;
                    node.Attr.Color = Color.Green;
                }
                else if (taxiPoint is Junction)
                {
                    node.Attr.Shape = Shape.Hexagon;
                    node.Attr.Color = Color.Blue;
                }
                else if (taxiPoint is ParkingSpot)
                {
                    node.Attr.Shape = Shape.Octagon;
                    node.Attr.Color = Color.Orange;

                }
            }

            foreach (TaggedEdge<TaxiPoint, string> edge in airfield.TaxiNavigationGraph.Edges)
            {
                var displayEdge = graph.AddEdge(edge.Source.Name, edge.Tag, edge.Target.Name);

                if (airfield.TaxiwayCost[edge] >= 999)
                {
                    displayEdge.Attr.Color = Color.Red;
                }
                else if (airfield.TaxiwayCost[edge] >= 99)
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
                LayoutEditingEnabled = false,
            };

            graphViewer.BindToPanel(GraphPanel);


            graphViewer.MouseDown += (s, ev) =>
            {
                if (graphViewer.ObjectUnderMouseCursor is VNode && ev.RightButtonIsPressed && (bool)AddTaxiPathButton.IsChecked)
                {
                    SourceNode = (VNode)graphViewer.ObjectUnderMouseCursor;
                }
            };

            graphViewer.MouseUp += (s, ev) =>
            {
                if (graphViewer.ObjectUnderMouseCursor is VNode && (bool)AddTaxiPathButton.IsChecked)
                {
                    TargetNode = (VNode)graphViewer.ObjectUnderMouseCursor;

                    string taxiName = null;

                    taxiName = SourceNode.Node.Id.Split()
                         .Intersect(TargetNode.Node.Id.Split())
                         .FirstOrDefault();

                    // The main route
                    graph.AddEdge(SourceNode.Node.Id, taxiName, TargetNode.Node.Id);

                    airfield.Taxiways.Add(new TaxiPath()
                    {
                        Source = SourceNode.Node.Id,
                        Target = TargetNode.Node.Id,
                        Name = taxiName,
                        Cost = 1,
                    });

                    // And the reverse route
                    graph.AddEdge(TargetNode.Node.Id, taxiName, SourceNode.Node.Id);

                    airfield.Taxiways.Add(new TaxiPath()
                    {
                        Source = SourceNode.Node.Id,
                        Target = TargetNode.Node.Id,
                        Name = taxiName,
                        Cost = 1,
                    });

                    graphViewer.Graph = graph;
                }
            };

            graphViewer.Graph = graph;
        }
    }
}
