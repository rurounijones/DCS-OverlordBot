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
                InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Data\Airfields")
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
            
            foreach (var taxiPath in airfield.Taxiways)
            {
                taxiPath.Cost = (int) airfield.NavigationCost.FirstOrDefault(x => x.Key.Source.Name == taxiPath.Source && x.Key.Target.Name == taxiPath.Target).Value;
            }

            try
            {
                var json = JsonConvert.SerializeObject(airfield, Formatting.Indented);
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

            foreach (NavigationPoint navigationPoint in airfield.NavigationGraph.Vertices)
            {
                var node = graph.AddNode(navigationPoint.Name);
                switch (navigationPoint)
                {
                    case Runway _:
                        node.Attr.Shape = Shape.DoubleCircle;
                        node.Attr.Color = Color.Green;
                        break;
                    case Junction _:
                        node.Attr.Shape = Shape.Hexagon;
                        node.Attr.Color = Color.Blue;
                        break;
                    case ParkingSpot _:
                        node.Attr.Shape = Shape.Octagon;
                        node.Attr.Color = Color.Orange;
                        break;
                    case WayPoint _:
                        node.Attr.Shape = Shape.Box;
                        node.Attr.Color = Color.Purple;
                        break;
                }
            }

            foreach (TaggedEdge<NavigationPoint, string> edge in airfield.NavigationGraph.Edges)
            {
                var displayEdge = graph.AddEdge(edge.Source.Name, edge.Tag, edge.Target.Name);
                displayEdge.UserData = edge;

                if (airfield.NavigationCost[edge] >= 999)
                {
                    displayEdge.Attr.Color = Color.Red;
                }
                else if (airfield.NavigationCost[edge] >= 99)
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
                if (!ev.RightButtonIsPressed || !(bool)AddTaxiPathButton.IsChecked)
                    return;

                if (graphViewer.ObjectUnderMouseCursor is VNode node)
                {
                    SourceNode = node;
                } 
                else if (graphViewer.ObjectUnderMouseCursor.DrawingObject is Label label)
                {
                    var cost = airfield.NavigationCost[(TaggedEdge<NavigationPoint, string>)label.Owner.UserData];

                    switch (cost)
                    {
                        case 1:
                            airfield.NavigationCost[(TaggedEdge<NavigationPoint, string>)label.Owner.UserData] = 100;
                            break;
                        case 100:
                            airfield.NavigationCost[(TaggedEdge<NavigationPoint, string>)label.Owner.UserData] = 999;
                            break;
                        case 999:
                            airfield.NavigationCost[(TaggedEdge<NavigationPoint, string>)label.Owner.UserData] = 1;
                            break;
                    }
                    DisplayGraph();
                }
            };

            graphViewer.MouseUp += (s, ev) =>
            {
                if (graphViewer.ObjectUnderMouseCursor is VNode && (bool)AddTaxiPathButton.IsChecked && SourceNode != null && (VNode)graphViewer.ObjectUnderMouseCursor != SourceNode)
                {
                    TargetNode = (VNode)graphViewer.ObjectUnderMouseCursor;

                    graphViewer.Graph = graph;

                    string taxiName = null;

                    taxiName = SourceNode.Node.Id.Split()
                         .Intersect(TargetNode.Node.Id.Split())
                         .FirstOrDefault();

                    // The main route
                    var mainEdge = graph.AddEdge(SourceNode.Node.Id, taxiName, TargetNode.Node.Id);
                    mainEdge.Attr.Color = Color.Green;

                    var reverseEdge = graph.AddEdge(TargetNode.Node.Id, taxiName, SourceNode.Node.Id);

                    int mainCost = 1;
                    int reverseCost;

                    if(SourceNode.Node.Id.Contains("Apron") || SourceNode.Node.Id.Contains("Ramp"))
                    {
                        reverseCost = 100;
                        reverseEdge.Attr.Color = Color.Orange;
                    }
                    else if(SourceNode.Node.Id.Contains("Runway") && TargetNode.Node.Id.Contains("Runway"))
                    {
                        mainCost = 999;
                        mainEdge.Attr.Color = Color.Red;
                        reverseCost = 999;
                        reverseEdge.Attr.Color = Color.Red;
                    }
                    else
                    {
                        reverseCost = 1;
                        reverseEdge.Attr.Color = Color.Green;
                    }

                    airfield.Taxiways.Add(new NavigationPath
                    {
                        Source = SourceNode.Node.Id,
                        Target = TargetNode.Node.Id,
                        Name = taxiName,
                        Cost = mainCost,
                    });

                    airfield.Taxiways.Add(new NavigationPath
                    {
                        Source = TargetNode.Node.Id,
                        Target = SourceNode.Node.Id,
                        Name = taxiName,
                        Cost = reverseCost,
                    });

                    graphViewer.Graph = graph;
                }
                SourceNode = null;
            }; 
            
            graphViewer.Graph = graph;
        }
    }
}
