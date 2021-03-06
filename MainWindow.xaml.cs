﻿using System.Collections.Generic;
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
        WithoutCurrentVersionFirst = 2,
        /// <summary>
        /// Games which latest version hasn't been played first.
        /// </summary>
        UnfinishedFirst = 3
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
                    case SortOrder.UnfinishedFirst:
                        return "Unfinished first";
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
            SortOrder.UnfinishedFirst,
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
                        Tools.ShowErrorMessage("Couldn\'t find requested thread. Make sure you entered it correctly.");
                        return;
                    }

                    if (!isInitializationSuccessfull)
                        Tools.ShowErrorMessage($"Couldn\'t find thread \"{gameInfoViewModel.GameInfo.Id}\". Make sure you entered it correctly.");
                    else
                    {
                        if (!GameInfoViewModelsCollection.Contains(gameInfoViewModel))
                        {
                            GameInfoViewModelsCollection.Add(gameInfoViewModel);
                            SortGameInfoViewModelsCollection();
                            gameInfoViewModelsListView.ScrollIntoView(gameInfoViewModel);

                            HaveChanges = true;
                        }
                        else
                            Tools.ShowInformationMessage("Game is already in collection.");
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
                        gameInfoViewModel.SyncVersions();
                        SortGameInfoViewModelsCollection();
                        //gameInfoViewModelsListView.ScrollIntoView(gameInfoViewModel);

                        HaveChanges = true;
                    }
                },
                (object sender1, CanExecuteRoutedEventArgs e1) =>
                {
                    e1.CanExecute = (e1.Parameter is F95GameInfoViewModel gameInfoViewModel) && !gameInfoViewModel.AreVersionsMatch && !AddGameInfoRunning && !LoginRunning && !SaveChangesRunning && !GetLatestVersionsRunning;
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
                        Tools.ShowErrorMessage("Unable to save games collection. Something went wrong.");
                        return;
                    }

                    Tools.ShowInformationMessage("Games collection succcessfuly saved.");

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
                        Tools.ShowErrorMessage("Unable to get latest game versions. Something went wrong.");
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
                        Tools.ShowErrorMessage("Unable to open thread in browser. Something went wrong.");
                    }
                },
                (object sender1, CanExecuteRoutedEventArgs e1) =>
                {
                    e1.CanExecute = true;
                }));

            CommandManager.InvalidateRequerySuggested();
        }

        private void SortGameInfoViewModelsCollection()
        {
            if (GameInfoViewModelsCollection == null)
                return;

            var collectionView = CollectionViewSource.GetDefaultView(GameInfoViewModelsCollection);
            using (collectionView.DeferRefresh())
            {
                collectionView.SortDescriptions.Clear();

                void AddAlphabeticalSortOrder(ListSortDirection sortDirection = ListSortDirection.Ascending)
                {
                    collectionView.SortDescriptions.Add(new SortDescription(nameof(F95GameInfoViewModel.Name), sortDirection));
                }

                void AddFavoriteSortOrder(ListSortDirection sortDirection = ListSortDirection.Descending)
                {
                    if (GivePriorityToFavoritesWhileSorting)
                        collectionView.SortDescriptions.Add(new SortDescription(nameof(F95GameInfoViewModel.IsFavorite), sortDirection));
                }

                void AddAreVersionsMatchSortOrder(ListSortDirection sortDirection = ListSortDirection.Ascending)
                {
                    collectionView.SortDescriptions.Add(new SortDescription(nameof(F95GameInfoViewModel.AreVersionsMatch), sortDirection));
                }

                void AddHasCurrentVersionSortOrder(ListSortDirection sortDirection = ListSortDirection.Ascending)
                {
                    collectionView.SortDescriptions.Add(new SortDescription(nameof(F95GameInfoViewModel.HasCurrentVersion), sortDirection));
                }

                void AddIsVersionFinishedSortOrder(ListSortDirection sortDirection = ListSortDirection.Ascending)
                {
                    collectionView.SortDescriptions.Add(new SortDescription(nameof(F95GameInfoViewModel.IsVersionFinished), sortDirection));
                }

                switch (SortOrder)
                {
                    case SortOrder.Alphabetical:
                        AddFavoriteSortOrder();
                        AddAlphabeticalSortOrder();
                        break;
                    case SortOrder.NotUpdatedFirst:
                        AddAreVersionsMatchSortOrder();
                        AddFavoriteSortOrder();
                        AddHasCurrentVersionSortOrder(ListSortDirection.Descending);
                        AddAlphabeticalSortOrder();
                        break;
                    case SortOrder.WithoutCurrentVersionFirst:
                        AddHasCurrentVersionSortOrder();
                        AddFavoriteSortOrder();
                        AddAlphabeticalSortOrder();
                        break;
                    case SortOrder.UnfinishedFirst:
                        AddIsVersionFinishedSortOrder();
                        AddFavoriteSortOrder();
                        AddHasCurrentVersionSortOrder(ListSortDirection.Descending);
                        AddAlphabeticalSortOrder();
                        break;
                    default:
                        break;
                }
            }
        }

        private void SetGameInfoViewModelsCollectionFilter()
        {
            CollectionViewSource.GetDefaultView(GameInfoViewModelsCollection).Filter = (object item) =>
            {
                var userInputStringLower = UserInputString?.ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(userInputStringLower) || userInputStringLower.Contains(F95Urls.SiteUrl.ToLowerInvariant()))
                    return true;

                return !(item is F95GameInfoViewModel gameInfoViewModel) || gameInfoViewModel.Name.ToLowerInvariant().Contains(userInputStringLower);
            };
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (HaveChanges && (Tools.ShowQuestionMessage("Games collection changed. Save changes?") == MessageBoxResult.Yes))
                F95GameInfoCollectionCommands.SaveGameInfoCollection.Execute(null, this);
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
                    Tools.ShowErrorMessage("Unable to open url.");
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

        private void IsVersionFinishedCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            HaveChanges = true;
            SortGameInfoViewModelsCollection();
        }

        private void IsVersionFinishedCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            HaveChanges = true;
            SortGameInfoViewModelsCollection();
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

    public static class Tools
    {
        #region Public methods

        public static void ShowErrorMessage(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static void ShowWarningMessage(string message)
        {
            MessageBox.Show(message, "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public static void ShowInformationMessage(string message)
        {
            MessageBox.Show(message, "Information", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public static MessageBoxResult ShowQuestionMessage(string message)
        {
            return MessageBox.Show(message, "Information", MessageBoxButton.YesNo, MessageBoxImage.Information);
        }

        #endregion
    }
}
