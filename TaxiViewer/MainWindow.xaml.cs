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
                node.UserData = navigationPoint;
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

                if (edge.Source is Runway && edge.Target is WayPoint)
                {
                    displayEdge.Attr.Color = Color.Purple;
                }
                else if (edge.Source is WayPoint)
                {
                    displayEdge.Attr.Color = Color.Purple;
                }
                else if (airfield.NavigationCost[edge] >= 999)
                {
                    displayEdge.Attr.Color = Color.Red;
                }
                else if (airfield.NavigationCost[edge] >= 100)
                {
                    displayEdge.Attr.Color = Color.Orange;
                }
                else
                {
                    displayEdge.Attr.Color = Color.Green;
                }

                if (edge.Source is WayPoint || edge.Target is WayPoint)
                {
                    displayEdge.Attr.AddStyle(Microsoft.Msagl.Drawing.Style.Dashed);
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
                else if (graphViewer.ObjectUnderMouseCursor?.DrawingObject is Label label)
                {
                    var cost = airfield.NavigationCost[(TaggedEdge<NavigationPoint, string>)label.Owner.UserData];

                    switch (cost)
                    {
                        // 0 shouldn't happen but has happened in the past due to bugs so cater to it.
                        case 0:
                            airfield.NavigationCost[(TaggedEdge<NavigationPoint, string>)label.Owner.UserData] = 100;
                            break;
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

                    if (SourceNode.Node.UserData is WayPoint && !(TargetNode.Node.UserData is WayPoint) && !(TargetNode.Node.UserData is Runway))
                        return;

                    if (!(SourceNode.Node.UserData is Runway) && !(SourceNode.Node.UserData is WayPoint) && TargetNode.Node.UserData is WayPoint)
                        return;

                    graphViewer.Graph = graph;

                    string taxiName = null;

                    taxiName = SourceNode.Node.Id.Replace('-', ' ')
                        .Split()
                        .Intersect(TargetNode.Node.Id.Replace('-', ' ').Split())
                        .FirstOrDefault();

                    // Default everything to green and cost 1
                    var mainEdge = graph.AddEdge(SourceNode.Node.Id, taxiName, TargetNode.Node.Id);
                    mainEdge.Attr.Color = Color.Green;
                    var mainCost = 1;

                    var reverseEdge = graph.AddEdge(TargetNode.Node.Id, taxiName, SourceNode.Node.Id);
                    reverseEdge.Attr.Color = Color.Green;
                    var reverseCost = 1;

                    if (SourceNode.Node.UserData is WayPoint || TargetNode.Node.UserData is WayPoint)
                    {
                        mainEdge.Attr.Color = Color.Purple;
                        mainEdge.Attr.AddStyle(Microsoft.Msagl.Drawing.Style.Dashed);
                        reverseEdge.Attr.Color = Color.Purple;
                        reverseEdge.Attr.AddStyle(Microsoft.Msagl.Drawing.Style.Dashed);
                    } 
                    if(SourceNode.Node.Id.Contains("Apron") || SourceNode.Node.Id.Contains("Ramp"))
                    {
                        reverseEdge.Attr.Color = Color.Orange;
                        reverseCost = 100;
                    }
                    if(TargetNode.Node.Id.Contains("Apron") || TargetNode.Node.Id.Contains("Ramp"))
                    {
                        mainEdge.Attr.Color = Color.Orange;
                        mainCost = 100;
                    }
                    if(SourceNode.Node.Id.Contains("Spot") || SourceNode.Node.Id.Contains("Maintenance"))
                    {
                        reverseEdge.Attr.Color = Color.Red;
                        reverseCost = 999;
                    }
                    if(TargetNode.Node.Id.Contains("Spot") || TargetNode.Node.Id.Contains("Maintenance"))
                    {
                        mainEdge.Attr.Color = Color.Red;
                        mainCost = 999;
                    }
                    if(SourceNode.Node.Id.Contains("Runway") && TargetNode.Node.Id.Contains("Runway"))
                    {
                        mainEdge.Attr.Color = Color.Red;
                        mainCost = 999;

                        reverseEdge.Attr.Color = Color.Red;
                        reverseCost = 999;
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
