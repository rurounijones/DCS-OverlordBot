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
using System.Collections.Generic;

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

        internal NavigationPoint highlightPoint = null;

        private MarkerImportWindow markerImport = null;

        private NodeListWindow nodeList = null;

        private AirfieldInformationWindow airfieldinfo = null;

        private EdgeEditorWindow edgeList = null;

        //Phonetic alphabet, alternate spellings, and double-letter taxiways. Will add Letter+Number taxiways in the event that they show up in numbers.
        //Doubles first so they take priority.
        private const string LikelyTaxiwayString = "Alpha Alpha|Alfa Alfa|Beta Beta|Charlie Charlie|Delta Delta|Golf Golf|Echo Echo|Foxtrot Foxtrot|Hotel Hotel|Juliett Juliett|Juliet Juliet|Kilo Kilo|Lima Lima|Mike Mike|November November|Oscar Oscar|Papa Papa|Quebec Quebec|Sierra Sierra|Tango Tango|Uniform Uniform|Victor Victor|Whiskey Whiskey|Yankee Yankee|Zulu Zulu|Alpha|Alfa|Beta|Charlie|Delta|Golf|Echo|Foxtrot|Hotel|Juliett|Juliet|Kilo|Lima|Mike|November|Oscar|Papa|Quebec|Sierra|Tango|Uniform|Victor|Whiskey|Yankee|Zulu";
        private IEnumerable<string> LikelyTaxiwayNames 
        {
            get { return LikelyTaxiwayString.Split('|'); }
        }
           
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
                    ShowMarkerImport.IsEnabled = true;
                    NewButton.IsEnabled = false;
                    AirfieldInfoButton.IsEnabled = true;
                    ShowNodeList.IsEnabled = true;
                    ShowEdgeList.IsEnabled = true;

                    DisplayGraph();
                }
                catch (Exception _)
                {
                    MessageBox.Show("Error reading Airfield JSON", "Deserialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Reload_Airfield(object sender, RoutedEventArgs e)
        {
            try
            {
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

                //Also write to temp dir file.
                File.WriteAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "temp", "markers.json"), json);
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
                    case NavigationPoint _:
                        node.Attr.Shape = Shape.Circle;
                        if (navigationPoint == highlightPoint) node.Attr.Color = Color.Red;
                        else node.Attr.Color = Color.Black;
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

        internal void DisplayGraph()
        {
            GraphPanel.Children.Clear();

            BuildGraph();

            if (DisplayRealGraphButton.IsChecked != null && (bool)DisplayRealGraphButton.IsChecked)
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

        private ICurve CreateLabelAndBoundary(NavigationPoint navigationPoint, Microsoft.Msagl.Drawing.Node node)
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
                case NavigationPoint _:
                    if (navigationPoint == highlightPoint) node.Attr.Color = Color.Red;
                    else node.Attr.Color = Color.Black;
                    return CurveFactory.CreateCircle(100, positionalPoint);
            }

            return CurveFactory.CreateCircle(5, positionalPoint);
        }

        private void MouseDownHandler(object s, MsaglMouseEventArgs ev)
        {
            if (!ev.RightButtonIsPressed || !(bool)AddTaxiPathButton.IsChecked)
                return;

            if (_graphViewer.ObjectUnderMouseCursor is VNode node)
            {
                SourceNode = node;
            }
            else if (_graphViewer.ObjectUnderMouseCursor?.DrawingObject is Label label)
            {
                var taggedEdge = (TaggedEdge<NavigationPoint, string>)label.Owner.UserData;

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

                if(taxiName == null || Int32.TryParse(taxiName, out _))
                {
                    taxiName = SourceNode.Node.Id.Replace('-', ' ')
                    .Split()
                    .Intersect(LikelyTaxiwayNames)
                    .FirstOrDefault();
                }

                if (taxiName == null || Int32.TryParse(taxiName, out _))
                {
                    taxiName = TargetNode.Node.Id.Replace('-', ' ')
                    .Split()
                    .Intersect(LikelyTaxiwayNames)
                    .FirstOrDefault();
                }

                if (taxiName == null || Int32.TryParse(taxiName, out _))
                {
                    taxiName = $"{SourceNode.Node.Id} to {TargetNode.Node.Id}";
                }

                var mainCost = 1;
                var reverseCost = 1;

                if (SourceNode.Node.Id.Contains("Apron") || SourceNode.Node.Id.Contains("Ramp"))
                {
                    reverseCost = 100;
                }
                if (TargetNode.Node.Id.Contains("Apron") || TargetNode.Node.Id.Contains("Ramp"))
                {
                    mainCost = 100;
                }
                if (SourceNode.Node.Id.Contains("Spot") || SourceNode.Node.Id.Contains("Maintenance") || SourceNode.Node.Id.Contains("Bunker") || SourceNode.Node.Id.Contains("Shelter") || SourceNode.Node.Id.Contains("Parking") || SourceNode.Node.Id.Contains("Cargo") || SourceNode.Node.Id.Contains("Revetment"))
                {
                    reverseCost = 999;
                }
                if (TargetNode.Node.Id.Contains("Spot") || TargetNode.Node.Id.Contains("Maintenance") || TargetNode.Node.Id.Contains("Bunker") || TargetNode.Node.Id.Contains("Shelter") || TargetNode.Node.Id.Contains("Parking") || TargetNode.Node.Id.Contains("Cargo") || TargetNode.Node.Id.Contains("Revetment"))
                {
                    mainCost = 999;
                }
                if (SourceNode.Node.Id.Contains("Runway") && TargetNode.Node.Id.Contains("Runway"))
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
                var mainEdge = new TaggedEdge<NavigationPoint, string>((NavigationPoint)SourceNode.Node.UserData,
                    (NavigationPoint)TargetNode.Node.UserData, taxiName);
                airfield.NavigationGraph.AddEdge(mainEdge);
                if (airfield.NavigationCost == null) airfield.NavigationCost = new Dictionary<TaggedEdge<NavigationPoint, string>, double>();
                airfield.NavigationCost[mainEdge] = mainCost;

                var reverseEdge = new TaggedEdge<NavigationPoint, string>((NavigationPoint)TargetNode.Node.UserData,
                    (NavigationPoint)SourceNode.Node.UserData, taxiName);
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

        private void ShowMarkerImport_Checked(object sender, RoutedEventArgs e)
        {
            if (markerImport == null) markerImport = new MarkerImportWindow(airfield, this);
            markerImport.Closed += MarkerImport_Closed;
            markerImport.Show();
        }

        private void MarkerImport_Closed(object sender, EventArgs e)
        {
            markerImport = null;
        }

        private void ShowNodeList_Checked(object sender, RoutedEventArgs e)
        {
            if (nodeList == null) nodeList = new NodeListWindow(this, airfield);
            nodeList.Closed += NodeList_Closed;
            nodeList.Show();
        }

        private void NodeList_Closed(object sender, EventArgs e)
        {
            nodeList = null;
        }

        private void NewButton_Click(object sender, RoutedEventArgs e)
        {
            var saveFileDialog = new SaveFileDialog()
            {
                Filter = "JSON files (*.json)|*.json",
                InitialDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Data\Airfields")
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    FileName = saveFileDialog.FileName;
                    airfield = new Airfield();
                    Save_Airfield(sender, e);
                }
                catch
                {
                    MessageBox.Show("Failure creating new airfield");
                    return;
                }

                DisplayGraph();

                ReloadAirfieldButton.IsEnabled = true;
                SaveAirfieldButton.IsEnabled = true;
                AddTaxiPathButton.IsEnabled = true;
                DisplayRealGraphButton.IsEnabled = true;
                ShowMarkerImport.IsEnabled = true;
                NewButton.IsEnabled = false;
                AirfieldInfoButton.IsEnabled = true;
                ShowNodeList.IsEnabled = true;
                ShowEdgeList.IsEnabled = true;
            }
        }

        private void AirfieldInfoButton_Checked(object sender, RoutedEventArgs e)
        {
            if (airfieldinfo == null) airfieldinfo = new AirfieldInformationWindow(airfield, this);
            airfieldinfo.Closed += Airfieldinfo_Closed;
            airfieldinfo.Show();

        }

        private void Airfieldinfo_Closed(object sender, EventArgs e)
        {
            airfieldinfo = null;
        }

        private void ShowEdgeList_Click(object sender, RoutedEventArgs e)
        {
            if (edgeList == null) edgeList = new EdgeEditorWindow(this, airfield);
            edgeList.Closed += EdgeList_Closed;
            edgeList.Show();
        }

        private void EdgeList_Closed(object sender, EventArgs e)
        {
            edgeList = null;
        }
    }
}
