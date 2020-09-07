using System.Windows;
using System.Windows.Controls;

namespace Ciribob.DCS.SimpleRadio.Standalone.Client.UI
{
    public static class ToolTips
    {
        public static ToolTip ExternalAwacsMode;
        public static ToolTip ExternalAwacsModeName;
        public static ToolTip ExternalAwacsModePassword;
        public static ToolTip NoMicAvailable;

        public static void Init()
        {
            ExternalAwacsMode = new ToolTip();
            var externalAwacsModeContent = new StackPanel();

            externalAwacsModeContent.Children.Add(new TextBlock
            {
                Text = "External AWACS Mode",
                FontWeight = FontWeights.Bold
            });
            externalAwacsModeContent.Children.Add(new TextBlock
            {
                Text = "External AWACS Mode (EAM) allows you to use the AWACS functionality of SRS without having to run DCS."
            });
            externalAwacsModeContent.Children.Add(new TextBlock
            {
                Text = "Enter the side password provided to you by the SRS server admin to confirm a side selection."
            });

            ExternalAwacsMode.Content = externalAwacsModeContent;


            ExternalAwacsModeName = new ToolTip();
            var externalAwacsModeNameContent = new StackPanel();

            externalAwacsModeNameContent.Children.Add(new TextBlock
            {
                Text = "External AWACS Mode name",
                FontWeight = FontWeights.Bold
            });
            externalAwacsModeNameContent.Children.Add(new TextBlock
            {
                Text = "Choose a name to display in the client list and export of the SRS server."
            });

            ExternalAwacsModeName.Content = externalAwacsModeNameContent;


            ExternalAwacsModePassword = new ToolTip();
            var externalAwacsModePasswordContent = new StackPanel();

            externalAwacsModePasswordContent.Children.Add(new TextBlock
            {
                Text = "External AWACS Mode coalition password",
                FontWeight = FontWeights.Bold
            });
            externalAwacsModePasswordContent.Children.Add(new TextBlock
            {
                Text = "The coalition password is provided to you by the SRS server admin."
            });
            externalAwacsModePasswordContent.Children.Add(new TextBlock
            {
                Text = "Entering the correct password for a coalitions allows you to access that side's comms."
            });

            ExternalAwacsModePassword.Content = externalAwacsModePasswordContent;


            NoMicAvailable = new ToolTip();
            var noMicAvailableContent = new StackPanel();

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
