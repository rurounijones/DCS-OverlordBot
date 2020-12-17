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
    /// Interaction logic for EdgeEditorWindow.xaml
    /// </summary>
    public partial class EdgeEditorWindow : Window
    {
        MainWindow ParentWindow;
        Airfield Airfield;

        public EdgeEditorWindow(MainWindow parent, Airfield airfield)
        {
            InitializeComponent();
            ParentWindow = parent;
            Airfield = airfield;

            //Populate NodeList
            foreach(var x in Airfield.NavigationGraph.Vertices)
            {
                NodeList.Items.Add(x);
            }
        }

        private void NodeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            //Populate EdgeList
            EdgeList.Items.Clear();
            var item = ((NavigationPoint)(e.AddedItems[0])).Name;
            foreach( var edge in Airfield.Taxiways.Where(x => x.Source == item || x.Target == item))
            {
                EdgeList.Items.Add(edge);
            }
        }

        private void EdgeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count == 0) return;
            //Populate Edge Properties
            var item = ((NavigationPath)(e.AddedItems[0]));
            EdgeName.Text = item.Name;
            EdgeCost.Text = item.Cost.ToString();
            ToLabel.Content = $"To: {item.Target}";
            FromLabel.Content = $"From: {item.Source}";

        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            //Save edge properties
            if(EdgeCost.Text != "1" && EdgeCost.Text != "100" && EdgeCost.Text != "999")
            {
                MessageBox.Show("Cost may only be 1, 100 or 999");
            }
            
            var edge = (NavigationPath)(EdgeList.SelectedItem);
            if (edge == null) return;
            edge.Cost = int.Parse(EdgeCost.Text);
            edge.Name = EdgeName.Text;

            //Refresh EdgeList
            EdgeList.Items.Clear();
            var item = ((NavigationPoint)(NodeList.SelectedItem)).Name;
            foreach (var listedge in Airfield.Taxiways.Where(x => x.Source == item || x.Target == item))
            {
                EdgeList.Items.Add(listedge);
            }

            //Refresh graph
            ParentWindow.DisplayGraph();
        }
    }
}
