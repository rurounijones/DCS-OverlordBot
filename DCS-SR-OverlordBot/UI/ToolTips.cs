using System.Windows;
using System.Windows.Controls;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI
{
    public static class ToolTips
    {
        public static ToolTip ExternalAWACSMode;
        public static ToolTip ExternalAWACSModeName;
        public static ToolTip ExternalAWACSModePassword;
        public static ToolTip NoMicAvailable;

        public static void Init()
        {
            ExternalAWACSMode = new ToolTip();
            StackPanel externalAWACSModeContent = new StackPanel();

            externalAWACSModeContent.Children.Add(new TextBlock
            {
                Text = "External AWACS Mode",
                FontWeight = FontWeights.Bold
            });
            externalAWACSModeContent.Children.Add(new TextBlock
            {
                Text = "External AWACS Mode (EAM) allows you to use the AWACS functionality of SRS without having to run DCS."
            });
            externalAWACSModeContent.Children.Add(new TextBlock
            {
                Text = "Enter the side password provided to you by the SRS server admin to confirm a side selection."
            });

            ExternalAWACSMode.Content = externalAWACSModeContent;


            ExternalAWACSModeName = new ToolTip();
            StackPanel externalAWACSModeNameContent = new StackPanel();

            externalAWACSModeNameContent.Children.Add(new TextBlock
            {
                Text = "External AWACS Mode name",
                FontWeight = FontWeights.Bold
            });
            externalAWACSModeNameContent.Children.Add(new TextBlock
            {
                Text = "Choose a name to display in the client list and export of the SRS server."
            });

            ExternalAWACSModeName.Content = externalAWACSModeNameContent;


            ExternalAWACSModePassword = new ToolTip();
            StackPanel externalAWACSModePasswordContent = new StackPanel();

            externalAWACSModePasswordContent.Children.Add(new TextBlock
            {
                Text = "External AWACS Mode coalition password",
                FontWeight = FontWeights.Bold
            });
            externalAWACSModePasswordContent.Children.Add(new TextBlock
            {
                Text = "The coalition password is provided to you by the SRS server admin."
            });
            externalAWACSModePasswordContent.Children.Add(new TextBlock
            {
                Text = "Entering the correct password for a coalitions allows you to access that side's comms."
            });

            ExternalAWACSModePassword.Content = externalAWACSModePasswordContent;


            NoMicAvailable = new ToolTip();
            StackPanel noMicAvailableContent = new StackPanel();

            noMicAvailableContent.Children.Add(new TextBlock
            {
                Text = "No microphone available",
                FontWeight = FontWeights.Bold
            });
            noMicAvailableContent.Children.Add(new TextBlock
            {
                Text = "No valid microphone is available - others will not be able to hear you."
            });
            noMicAvailableContent.Children.Add(new TextBlock
            {
                Text = "You can still use SRS to listen to radio calls, but will not be able to transmit anything yourself."
            });

            NoMicAvailable.Content = noMicAvailableContent;
        }
    }
}
