using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

using Flurl.Http;

namespace F95UpdatesChecker
{
    /// <summary>
    /// F95 site urls.
    /// </summary>
    public static class F95Urls
    {
        #region Public fields

        public const string SiteUrl = "https://f95zone.to";

        public static readonly string LoginUrl = string.Join("/", SiteUrl, LoginUrlPathSegment);
        public const string LoginUrlPathSegment = "login";

        public static readonly string GamesForumUrl = string.Join("/", SiteUrl, GamesForumUrlPathSegment);
        public const string GamesForumUrlPathSegment = "forums/games.2/";

        public static readonly string ThreadsUrl = string.Join("/", SiteUrl, ThreadsUrlPathSegment);
        public const string ThreadsUrlPathSegment = "threads";

        public const string TestGameUrl = "https://f95zone.to/threads/city-of-broken-dreamers-v1-02-phillygames.25739/";

        #endregion
    }

    /// <summary>
    /// Commands for game info collection manipulation.
    /// </summary>
    public static class F95GameInfoCollectionCommands
    {
        #region Public fields

        public static RoutedCommand AddGameInfoCommand = new RoutedCommand(nameof(AddGameInfoCommand), typeof(F95GameInfoCollectionCommands));
        public static RoutedCommand RemoveGameInfoCommand = new RoutedCommand(nameof(RemoveGameInfoCommand), typeof(F95GameInfoCollectionCommands));

        public static RoutedCommand SyncGameVersionsCommand = new RoutedCommand(nameof(SyncGameVersionsCommand), typeof(F95GameInfoCollectionCommands));

        public static RoutedCommand GetLatestGameVersionsCommand = new RoutedCommand(nameof(GetLatestGameVersionsCommand), typeof(F95GameInfoCollectionCommands));

        public static RoutedCommand SaveGameInfoCollection = new RoutedCommand(nameof(SaveGameInfoCollection), typeof(F95GameInfoCollectionCommands));

        public static RoutedCommand OpenInBrowserCommand = new RoutedCommand(nameof(OpenInBrowserCommand), typeof(F95GameInfoCollectionCommands));

        #endregion
    }

    /// <summary>
    /// Sort order types enumeration.
    /// </summary>
    public enum SortOrder
    {
        /// <summary>
        /// Alphabetical sort order based on game name.
        /// </summary>
        Alphabetical = 0,
        /// <summary>
        /// Not updated games first, then alphabetical.
        /// </summary>
        NotUpdatedFirst = 1,
        /// <summary>
        /// Games without current version first, then alphabetical.
        /// </summary>
        WithoutCurrentVersionFirst = 2
    }

    public class SortOrderToStringConverter : IValueConverter
    {
        #region Public methods

        public object Convert(object value, System.Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SortOrder sortOrder)
            {
                switch (sortOrder)
                {
                    case SortOrder.Alphabetical:
                        return "Alphabetical";
                    case SortOrder.NotUpdatedFirst:
                        return "Not updated first";
                    case SortOrder.WithoutCurrentVersionFirst:
                        return "Without current version first";
                    default:
                        return sortOrder.ToString();
                }
            }
            else
                return value;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, CultureInfo culture) => throw new System.NotImplementedException();

        #endregion
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        #region Events

        /// <summary>
        /// Event for property changed notification.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        #endregion

        #region Properties

        /// <summary>
        /// Collection of game info view models.
        /// </summary>
        public ObservableCollection<F95GameInfoViewModel> GameInfoViewModelsCollection
        {
            get => gameInfoViewModelsCollection;
            set
            {
                if (gameInfoViewModelsCollection != value)
                {
                    gameInfoViewModelsCollection = value;
                    RaisePropertyChanged(nameof(GameInfoViewModelsCollection));
                }
            }
        }

        /// <summary>
        /// Request string entered in main text box (thread url or filter string).
        /// </summary>
        public string UserInputString
        {
            get => userInputString;
            set
            {
                if (userInputString != value)
                {
                    userInputString = value;
                    RaisePropertyChanged(nameof(UserInputString));

                    CollectionViewSource.GetDefaultView(GameInfoViewModelsCollection)?.Refresh();
                }
            }
        }

        /// <summary>
        /// Sort order types collection.
        /// </summary>
        public IEnumerable<SortOrder> SortOrders => sortOrders;
        /// <summary>
        /// Selected sort order type.
        /// </summary>
        public SortOrder SortOrder
        {
            get => sortOrder;
            set
            {
                sortOrder = value;
                RaisePropertyChanged(nameof(SortOrder));

                SortGameInfoViewModelsCollection();
            }
        }
        /// <summary>
        /// Flag for identification of whether to give priority to favorites while sorting or not.
        /// </summary>
        public bool GivePriorityToFavoritesWhileSorting
        {
            get => givePriorityToFavoritesWhileSorting;
            set
            {
                if (givePriorityToFavoritesWhileSorting != value)
                {
                    givePriorityToFavoritesWhileSorting = value;
                    RaisePropertyChanged(nameof(GivePriorityToFavoritesWhileSorting));

                    SortGameInfoViewModelsCollection();
                }
            }
        }

        /// <summary>
        /// Flag for identification of whether game infos have changes.
        /// </summary>
        public bool HaveChanges
        {
            get => haveChanges;
            set
            {
                haveChanges = value;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// Flag for "Add game" command is running checks.
        /// </summary>
        public bool AddGameInfoRunning
        {
            get => addGameInfoRunning;
            set
            {
                addGameInfoRunning = value;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        /// <summary>
        /// Flag for "Login" command is running checks.
        /// </summary>
        public bool LoginRunning
        {
            get => loginRunning;
            set
            {
                loginRunning = value;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        /// <summary>
        /// Flag for "Save changes" command is running checks.
        /// </summary>
        public bool SaveChangesRunning
        {
            get => saveChangesRunning;
            set
            {
                saveChangesRunning = value;
                CommandManager.InvalidateRequerySuggested();
            }
        }
        /// <summary>
        /// Flag for "Get latest versions" command is running checks.
        /// </summary>
        public bool GetLatestVersionsRunning
        {
            get => getLatestVersionsRunning;
            set
            {
                getLatestVersionsRunning = value;
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// Currently updating game info index after "Get latest versions" command execution.
        /// </summary>
        public int CurrentlyUpdatingGameInfoIndex
        {
            get => currentlyUpdatingGameInfoIndex;
            private set
            {
                if (currentlyUpdatingGameInfoIndex != value)
                {
                    currentlyUpdatingGameInfoIndex = value;
                    RaisePropertyChanged(nameof(CurrentlyUpdatingGameInfoIndex));
                }
            }
        }

        #endregion

        #region Private fields

        /// <summary>
        /// HTTP client.
        /// </summary>
        private FlurlClient httpClient;
        /// <summary>
        /// View model of login window.
        /// </summary>
        private F95LoginViewModel loginViewModel;

        /// <summary>
        /// Collection of game info view models.
        /// </summary>
        private ObservableCollection<F95GameInfoViewModel> gameInfoViewModelsCollection = new ObservableCollection<F95GameInfoViewModel>();

        /// <summary>
        /// Request string entered in main text box (thread url or filter string).
        /// </summary>
        private string userInputString;

        /// <summary>
        /// Sort order types collection.
        /// </summary>
        private readonly IReadOnlyList<SortOrder> sortOrders = new List<SortOrder>
        {
            SortOrder.Alphabetical,
            SortOrder.NotUpdatedFirst,
            SortOrder.WithoutCurrentVersionFirst
        };
        /// <summary>
        /// Selected sort order type.
        /// </summary>
        private SortOrder sortOrder;
        /// <summary>
        /// Flag for identification of whether to give priority to favorites while sorting or not.
        /// </summary>
        private bool givePriorityToFavoritesWhileSorting = true;

        /// <summary>
        /// Flag for identification of whether game infos have changes.
        /// </summary>
        private bool haveChanges = false;

        /// <summary>
        /// Flag for "Add game" command is running checks.
        /// </summary>
        private bool addGameInfoRunning = false;
        /// <summary>
        /// Flag for "Login" command is running checks.
        /// </summary>
        private bool loginRunning = false;
        /// <summary>
        /// Flag for "Save changes" command is running checks.
        /// </summary>
        private bool saveChangesRunning = false;
        /// <summary>
        /// Flag for "Get latest versions" command is running checks.
        /// </summary>
        private bool getLatestVersionsRunning = false;

        /// <summary>
        /// Currently updating game info index after "Get latest versions" command execution.
        /// </summary>
        private int currentlyUpdatingGameInfoIndex = 0;

        #endregion

        #region Constructors

        /// <summary>
        /// Main window constructor.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();
        }

        #endregion

        #region Private methods

        /// <summary>
        /// Handler for <see cref="Window.Loaded"/> event of main window.
        /// </summary>
        /// <param name="sender">Sender of an event.</param>
        /// <param name="e">Event arguments.</param>
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // HTTP client initialization
            httpClient = new FlurlClient(F95Urls.SiteUrl).EnableCookies();

            // Checking if login credentials are loaded and try to login
            LoginRunning = true;
            loginViewModel = new F95LoginViewModel(httpClient);
            var isLoginCredentialsLoaded = await loginViewModel.LoadLoginCredentialsFromFileAsync();
            if (!isLoginCredentialsLoaded)
                ShowLoginWindow();
            else
                _ = await loginViewModel.LoginAsync();
            LoginRunning = false;

            // Loding saved game infos collection
            var gameInfoCollection = await F95GameInfoTools.LoadGameInfoCollectionFromFileAsync();
            if (gameInfoCollection.Any())
                GameInfoViewModelsCollection = new ObservableCollection<F95GameInfoViewModel>(gameInfoCollection.Select(gi => new F95GameInfoViewModel(httpClient, gi)));
            SetGameInfoViewModelsCollectionFilter();
            SortGameInfoViewModelsCollection();

            InitializeCommandBindings();
        }

        /// <summary>
        /// Initialize command bindings.
        /// </summary>
        private void InitializeCommandBindings()
        {
            CommandBindings.Add(new CommandBinding(F95GameInfoCollectionCommands.AddGameInfoCommand,
                async (object sender1, ExecutedRoutedEventArgs e1) =>
                {
                    AddGameInfoRunning = true;

                    var gameInfoViewModel = new F95GameInfoViewModel(httpClient, UserInputString);
                    var isInitializationSuccessfull = false;
                    try
                    {
                        isInitializationSuccessfull = await gameInfoViewModel.InitializeGameInfoAsync();
                    }
                    catch
                    {
                        MessageBox.Show("Couldn\'t find requested thread. Make sure you entered it correctly.", "Error!");
                        return;
                    }

                    if (!isInitializationSuccessfull)
                        MessageBox.Show($"Couldn\'t find thread \"{gameInfoViewModel.GameInfo.Id}\". Make sure you entered it correctly.", "Error!");
                    else
                    {
                        GameInfoViewModelsCollection.Add(gameInfoViewModel);
                        SortGameInfoViewModelsCollection();
                        gameInfoViewModelsListView.ScrollIntoView(gameInfoViewModel);

                        HaveChanges = true;
                    }

                    AddGameInfoRunning = false;
                },
                (object sender1, CanExecuteRoutedEventArgs e1) =>
                {
                    e1.CanExecute = !string.IsNullOrWhiteSpace(UserInputString) && UserInputString.Contains(F95Urls.ThreadsUrl) && !AddGameInfoRunning && !LoginRunning && !SaveChangesRunning && !GetLatestVersionsRunning;
                }));
            CommandBindings.Add(new CommandBinding(F95GameInfoCollectionCommands.RemoveGameInfoCommand,
                (object sender1, ExecutedRoutedEventArgs e1) =>
                {
                    if (e1.Parameter is F95GameInfoViewModel gameInfoViewModel)
                    {
                        GameInfoViewModelsCollection.Remove(gameInfoViewModel);
                        SortGameInfoViewModelsCollection();

                        HaveChanges = true;
                    }
                },
                (object sender1, CanExecuteRoutedEventArgs e1) =>
                {
                    e1.CanExecute = !AddGameInfoRunning && !LoginRunning && !SaveChangesRunning && !GetLatestVersionsRunning;
                }));
            CommandBindings.Add(new CommandBinding(F95GameInfoCollectionCommands.SyncGameVersionsCommand,
                (object sender1, ExecutedRoutedEventArgs e1) =>
                {
                    if (e1.Parameter is F95GameInfoViewModel gameInfoViewModel)
                    {
                        gameInfoViewModel.CurrentVersion = gameInfoViewModel.LatestVersion;
                        SortGameInfoViewModelsCollection();
                        //gameInfoViewModelsListView.ScrollIntoView(gameInfoViewModel);

                        HaveChanges = true;
                    }
                },
                (object sender1, CanExecuteRoutedEventArgs e1) =>
                {
                    e1.CanExecute = (e1.Parameter is F95GameInfoViewModel gameInfoViewModel) && !gameInfoViewModel.AreVersionsMatch && !AddGameInfoRunning && !LoginRunning && !SaveChangesRunning && !GetLatestVersionsRunning;
                }));
            CommandBindings.Add(new CommandBinding(F95LoginCommands.ReloginCommand,
                (object sender1, ExecutedRoutedEventArgs e1) =>
                {
                    LoginRunning = true;

                    ShowLoginWindow();

                    LoginRunning = false;
                },
                (object sender1, CanExecuteRoutedEventArgs e1) =>
                {
                    e1.CanExecute = !AddGameInfoRunning && !LoginRunning && !SaveChangesRunning && !GetLatestVersionsRunning;
                }));
            CommandBindings.Add(new CommandBinding(F95GameInfoCollectionCommands.SaveGameInfoCollection,
                async (object sender1, ExecutedRoutedEventArgs e1) =>
                {
                    SaveChangesRunning = true;

                    try
                    {
                        _ = await F95GameInfoTools.SaveGameInfoCollectionToFileAsync(GameInfoViewModelsCollection.Any() ? GameInfoViewModelsCollection.Select(vm => vm.GameInfo).ToList() : new List<F95GameInfo>());
                    }
                    catch
                    {
                        MessageBox.Show("Unable to save games collection. Something went wrong.", "Error!");
                        return;
                    }

                    MessageBox.Show("Games collection succcessfuly saved.", "Success!");

                    HaveChanges = false;

                    SaveChangesRunning = false;
                },
                (object sender1, CanExecuteRoutedEventArgs e1) =>
                {
                    e1.CanExecute = HaveChanges && !AddGameInfoRunning && !LoginRunning && !SaveChangesRunning && !GetLatestVersionsRunning;
                }));
            CommandBindings.Add(new CommandBinding(F95GameInfoCollectionCommands.GetLatestGameVersionsCommand,
                async (object sender1, ExecutedRoutedEventArgs e1) =>
                {
                    GetLatestVersionsRunning = true;

                    try
                    {
                        CurrentlyUpdatingGameInfoIndex = 0;
                        foreach (var gameInfoViewModel in GameInfoViewModelsCollection)
                        {
                            _ = await gameInfoViewModel.RefreshLatestVersionAsync();
                            CurrentlyUpdatingGameInfoIndex++;
                        }
                        CurrentlyUpdatingGameInfoIndex = 0;

                        SortGameInfoViewModelsCollection();
                    }
                    catch
                    {
                        CurrentlyUpdatingGameInfoIndex = 0;
                        MessageBox.Show("Unable to get latest game versions. Something went wrong.", "Error!");
                        return;
                    }

                    HaveChanges = true;

                    GetLatestVersionsRunning = false;
                },
                (object sender1, CanExecuteRoutedEventArgs e1) =>
                {
                    e1.CanExecute = !AddGameInfoRunning && !LoginRunning && !SaveChangesRunning && !GetLatestVersionsRunning;
                }));

            CommandBindings.Add(new CommandBinding(F95GameInfoCollectionCommands.OpenInBrowserCommand,
                (object sender1, ExecutedRoutedEventArgs e1) =>
                {
                    try
                    {
                        if (e1.Parameter is F95GameInfoViewModel gameInfoViewModel)
                            OpenUrlInDefaultBrowser(gameInfoViewModel.GameInfo.Url);
                    }
                    catch
                    {
                        MessageBox.Show("Unable to open thread in browser. Something went wrong.", "Error!");
                    }
                },
                (object sender1, CanExecuteRoutedEventArgs e1) =>
                {
                    e1.CanExecute = true;
                }));

            CommandManager.InvalidateRequerySuggested();
        }

        private void ShowLoginWindow()
        {
            var loginWindow = new F95LoginWIndow(loginViewModel)
            {
                Owner = this,
                ShowActivated = true,
                ShowInTaskbar = true,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            loginWindow.ShowDialog();
        }

        private void SortGameInfoViewModelsCollection()
        {
            if (GameInfoViewModelsCollection == null)
                return;

            var collectionView = CollectionViewSource.GetDefaultView(GameInfoViewModelsCollection);
            using (collectionView.DeferRefresh())
            {
                collectionView.SortDescriptions.Clear();

                switch (SortOrder)
                {
                    case SortOrder.Alphabetical:
                        break;
                    case SortOrder.NotUpdatedFirst:
                        collectionView.SortDescriptions.Add(new SortDescription(nameof(F95GameInfoViewModel.AreVersionsMatch), ListSortDirection.Ascending));
                        break;
                    case SortOrder.WithoutCurrentVersionFirst:
                        collectionView.SortDescriptions.Add(new SortDescription(nameof(F95GameInfoViewModel.HasCurrentVersion), ListSortDirection.Ascending));
                        break;
                    default:
                        break;
                }

                if (GivePriorityToFavoritesWhileSorting)
                    collectionView.SortDescriptions.Add(new SortDescription(nameof(F95GameInfoViewModel.IsFavorite), ListSortDirection.Descending));
                collectionView.SortDescriptions.Add(new SortDescription(nameof(F95GameInfoViewModel.Name), ListSortDirection.Ascending));
            }
        }

        private void SetGameInfoViewModelsCollectionFilter()
        {
            CollectionViewSource.GetDefaultView(GameInfoViewModelsCollection).Filter = (object item) =>
            {
                if (string.IsNullOrWhiteSpace(UserInputString) || UserInputString.Contains(F95Urls.SiteUrl))
                    return true;

                return !(item is F95GameInfoViewModel gameInfoViewModel) || gameInfoViewModel.Name.Contains(UserInputString);
            };
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (HaveChanges)
            {
                var result = MessageBox.Show("Games collection changed. Save changes?", "Warning!", MessageBoxButton.YesNo);
                if (result == MessageBoxResult.Yes)
                    F95GameInfoCollectionCommands.SaveGameInfoCollection.Execute(null, this);
            }
        }

        private void OpenUrlInDefaultBrowser(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch
            {
                // hack because of this: https://github.com/dotnet/corefx/issues/10361
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    url = url.Replace("&", "^&");
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}")
                    {
                        CreateNoWindow = true
                    });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else
                {
                    MessageBox.Show("Unable to open url.", "Error!");
                }
            }
        }

        private void CurrentVersionTextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
                textBox.IsReadOnly = false;
        }

        private void CurrentVersionTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if ((sender is TextBox textBox) && (e.Key == Key.Enter))
            {
                textBox.IsReadOnly = true;
                textBox.SelectionStart = 0;
            }
        }

        private void CurrentVersionTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.IsReadOnly = true;
                textBox.SelectionStart = 0;
                gameInfoViewModelsListView.Focus();
            }
        }

        private void GameInfoViewModelsListView_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if ((sender is ListView listView) && (e.LeftButton == MouseButtonState.Pressed))
                listView.Focus();
        }

        private void IsFavoriteCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            HaveChanges = true;
            SortGameInfoViewModelsCollection();
        }

        private void IsFavoriteCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            HaveChanges = true;
            SortGameInfoViewModelsCollection();
        }

        private void UserInputTextBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
                textBox.SelectAll();
        }

        private void RaisePropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
