using MediaLiveTile.Models;
using MediaLiveTile.Services;
using System.Collections.ObjectModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace MediaLiveTile
{
    public sealed partial class AllMediaPage : Page
    {
        public ObservableCollection<MediaSessionInfo> AllSessions { get; } =
            new ObservableCollection<MediaSessionInfo>();

        public AllMediaPage()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            AllSessions.Clear();

            foreach (var item in MediaRuntimeStore.AllSessions)
            {
                AllSessions.Add(item);
            }

            PageStatusTextBlock.Text = $"当前共 {AllSessions.Count} 个媒体会话";
        }

        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}