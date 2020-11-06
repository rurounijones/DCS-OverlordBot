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
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Core.Layout;
using Microsoft.Msagl.Layout.MDS;
using Microsoft.Msagl.Miscellaneous;
using Label = Microsoft.Msagl.Drawing.Label;

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

        private static GraphViewer _graphViewer;
        private static Graph _graph;

        LayoutAlgorithmSettings settings = new MdsLayoutSettings();

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
                    AddTaxiPathButton.IsEnabled = true;
                    DisplayRealGraphButton.IsEnabled = true;

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
                var json = JsonConvert.SerializeObject(airfield, Formatting.Indented);
                File.WriteAllText(FileName, json);
            }
            catch (Exception _)
            {
                MessageBox.Show("Error writing Airfield JSON", "Serialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuildGraph()
        {
            _graph = new Graph();

            foreach (var navigationPoint in airfield.NavigationGraph.Vertices)
            {

                var node = _graph.AddNode(navigationPoint.Name);
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

            foreach (var edge in airfield.NavigationGraph.Edges)
            {
                var displayEdge = _graph.AddEdge(edge.Source.Name, edge.Tag, edge.Target.Name);
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
        }

        private void DisplayGraph()
        {
            GraphPanel.Children.Clear();

            BuildGraph();

            if(DisplayRealGraphButton.IsChecked != null && (bool) DisplayRealGraphButton.IsChecked)
            {
                DisplayRealGraph();
            }
            else
            {
                DisplayAbstractGraph();
            }
        }

        private void DisplayAbstractGraph()
        {
            _graphViewer = new GraphViewer
            {
                LayoutEditingEnabled = false,
            };

            _graphViewer.BindToPanel(GraphPanel);
            _graphViewer.MouseDown += MouseDownHandler;
            _graphViewer.MouseUp += MouseUpHandler;
            _graphViewer.Graph = _graph;
        }

        private void DisplayRealGraph()
        {
            _graph.CreateGeometryGraph();

            foreach (var navigationPoint in airfield.NavigationGraph.Vertices)
            {

                var dnode = _graph.Nodes.FirstOrDefault(node => node.Id.Equals(navigationPoint.Name));
                if (dnode != null)
                {
                    dnode.GeometryNode.BoundaryCurve = CreateLabelAndBoundary(navigationPoint, dnode);
                }
                else
                {
                    MessageBox.Show($"Error Displaying {navigationPoint.Name}", "Display Error", MessageBoxButton.OK, MessageBoxImage.Error);

                }
            }

            LayoutHelpers.RouteAndLabelEdges(_graph.GeometryGraph, settings, _graph.GeometryGraph.Edges);

            _graphViewer = new GraphViewer
            {
                LayoutEditingEnabled = false,
                NeedToCalculateLayout = false
            };

            _graphViewer.BindToPanel(GraphPanel);
            _graphViewer.MouseDown += MouseDownHandler;
            _graphViewer.MouseUp += MouseUpHandler;
            _graphViewer.Graph = _graph;
        }

        private static ICurve CreateLabelAndBoundary(NavigationPoint navigationPoint, Microsoft.Msagl.Drawing.Node node)
        {
            node.Attr.LabelMargin *= 2;
            node.Label.IsVisible = false;

            var y = (navigationPoint.Latitude - airfield.Latitude) * 200000;
            var x = (navigationPoint.Longitude - airfield.Longitude) * 200000;
            var positionalPoint = new Microsoft.Msagl.Core.Geometry.Point(x, y);

            switch (navigationPoint)
            {
                case Runway _:
                    node.Attr.Color = Color.Green;
                    return CurveFactory.CreateCircle(50, positionalPoint);
                case Junction _:
                    node.Attr.Shape = Shape.Hexagon;
                    node.Attr.Color = Color.Blue;
                    return CurveFactory.CreateHexagon(100, 30, positionalPoint);
                case ParkingSpot _:
                    node.Attr.Color = Color.Orange;
                    return CurveFactory.CreateOctagon(100, 30, positionalPoint);
                case WayPoint _:
                    node.Attr.Color = Color.Purple;
                    return CurveFactory.CreateRectangle(100, 30, positionalPoint);
            }

            return CurveFactory.CreateCircle(5, positionalPoint);
        }

        private void MouseDownHandler(object s, MsaglMouseEventArgs ev)
        {
            if (!ev.RightButtonIsPressed || !(bool) AddTaxiPathButton.IsChecked)
                return;

            if (_graphViewer.ObjectUnderMouseCursor is VNode node)
            {
                SourceNode = node;
            }
            else if (_graphViewer.ObjectUnderMouseCursor?.DrawingObject is Label label)
            {
                var taggedEdge = (TaggedEdge<NavigationPoint, string>) label.Owner.UserData;

                if (taggedEdge == null)
                    return;

                var taxiPath = airfield.Taxiways.Find(x =>
                    x.Source == taggedEdge.Source.Name && x.Target == taggedEdge.Target.Name);

                var cost = airfield.NavigationCost[taggedEdge];

                switch (cost)
                {

                    // 0 shouldn't happen but has happened in the past due to bugs so cater to it.
                    case 0:
                        airfield.NavigationCost[taggedEdge] = 100;
                        taxiPath.Cost = 100;
                        break;
                    case 1:
                        airfield.NavigationCost[taggedEdge] = 100;
                        taxiPath.Cost = 100;
                        break;
                    case 100:
                        airfield.NavigationCost[taggedEdge] = 999;
                        taxiPath.Cost = 999;
                        break;
                    case 999:
                        airfield.NavigationCost[taggedEdge] = 1;
                        taxiPath.Cost = 1;
                        break;
                }

                DisplayGraph();
            }
        }

        private void MouseUpHandler(object s, MsaglMouseEventArgs ev)
        {
            if (_graphViewer.ObjectUnderMouseCursor is VNode && (bool)AddTaxiPathButton.IsChecked && SourceNode != null && (VNode)_graphViewer.ObjectUnderMouseCursor != SourceNode)
            {
                TargetNode = (VNode)_graphViewer.ObjectUnderMouseCursor;

                if (SourceNode.Node.UserData is WayPoint && !(TargetNode.Node.UserData is WayPoint) && !(TargetNode.Node.UserData is Runway))
                    return;

                if (!(SourceNode.Node.UserData is Runway) && !(SourceNode.Node.UserData is WayPoint) && TargetNode.Node.UserData is WayPoint)
                    return;

                var taxiName = SourceNode.Node.Id.Replace('-', ' ')
                    .Split()
                    .Intersect(TargetNode.Node.Id.Replace('-', ' ').Split())
                    .FirstOrDefault();

                var mainCost = 1;
                var reverseCost = 1;

                if(SourceNode.Node.Id.Contains("Apron") || SourceNode.Node.Id.Contains("Ramp"))
                {
                    reverseCost = 100;
                }
                if(TargetNode.Node.Id.Contains("Apron") || TargetNode.Node.Id.Contains("Ramp"))
                {
                    mainCost = 100;
                }
                if(SourceNode.Node.Id.Contains("Spot") || SourceNode.Node.Id.Contains("Maintenance"))
                {
                    reverseCost = 999;
                }
                if(TargetNode.Node.Id.Contains("Spot") || TargetNode.Node.Id.Contains("Maintenance"))
                {
                    mainCost = 999;
                }
                if(SourceNode.Node.Id.Contains("Runway") && TargetNode.Node.Id.Contains("Runway"))
                {
                    mainCost = 999;
                    reverseCost = 999;
                }

                // Add to the Taxiways for searching
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

                // Add to the underlying graph for data for refreshing UI
                var mainEdge = new TaggedEdge<NavigationPoint, string>((NavigationPoint) SourceNode.Node.UserData,
                    (NavigationPoint) TargetNode.Node.UserData, taxiName);
                airfield.NavigationGraph.AddEdge(mainEdge);
                airfield.NavigationCost[mainEdge] = mainCost;

                var reverseEdge = new TaggedEdge<NavigationPoint, string>((NavigationPoint) TargetNode.Node.UserData,
                    (NavigationPoint) SourceNode.Node.UserData, taxiName);
                airfield.NavigationGraph.AddEdge(reverseEdge);
                airfield.NavigationCost[reverseEdge] = reverseCost;

                SourceNode = null;
                DisplayGraph();
            }
        }

        private void DisplayRealGraphButton_Clicked(object sender, RoutedEventArgs e)
        {
            DisplayGraph();
        }
    }
}
