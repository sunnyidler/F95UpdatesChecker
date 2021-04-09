using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

using AngleSharp.Html.Parser;

using Flurl.Http;

namespace F95UpdatesChecker
{
    public class F95GameInfoViewModel : BaseViewModel, IEquatable<F95GameInfoViewModel>
    {
        #region Properties

        public string Name
        {
            get => gameInfo.Name;
            set
            {
                if (gameInfo.Name != value)
                {
                    gameInfo.Name = value;
                    RaisePropertyChanged(nameof(Name));
                }
            }
        }
        public string CurrentVersion
        {
            get => gameInfo.CurrentVersion;
            set
            {
                if (gameInfo.CurrentVersion != value)
                {
                    gameInfo.CurrentVersion = value;
                    RaisePropertyChanged(nameof(CurrentVersion));
                    RaisePropertyChanged(nameof(AreVersionsMatch));
                    RaisePropertyChanged(nameof(HasCurrentVersion));
                }
            }
        }
        public string LatestVersion
        {
            get => gameInfo.LatestVersion;
            set
            {
                if (gameInfo.LatestVersion != value)
                {
                    gameInfo.LatestVersion = value;
                    RaisePropertyChanged(nameof(LatestVersion));
                    RaisePropertyChanged(nameof(AreVersionsMatch));
                }
            }
        }

        public bool IsFavorite
        {
            get => gameInfo.IsFavorite;
            set
            {
                if (gameInfo.IsFavorite != value)
                {
                    gameInfo.IsFavorite = value;
                    RaisePropertyChanged(nameof(IsFavorite));
                }
            }
        }

        public bool IsVersionFinished
        {
            get => gameInfo.IsVersionFinished;
            set
            {
                if (gameInfo.IsVersionFinished != value)
                {
                    gameInfo.IsVersionFinished = value;
                    RaisePropertyChanged(nameof(IsVersionFinished));
                }
            }
        }

        public bool AreVersionsMatch => CurrentVersion == LatestVersion;

        public bool HasCurrentVersion => CurrentVersion != F95GameInfo.NoVersionString;

        public F95GameInfo GameInfo
        {
            get => gameInfo;
            set
            {
                if (gameInfo != value)
                {
                    gameInfo = value;
                    RaisePropertyChanged(nameof(GameInfo));
                    RaisePropertyChanged(nameof(Name));
                    RaisePropertyChanged(nameof(CurrentVersion));
                    RaisePropertyChanged(nameof(LatestVersion));
                    RaisePropertyChanged(nameof(AreVersionsMatch));
                    RaisePropertyChanged(nameof(HasCurrentVersion));
                }
            }
        }

        #endregion

        #region Private fields

        private F95GameInfo gameInfo;

        private readonly FlurlClient httpClient;

        #endregion

        #region Constructors

        public F95GameInfoViewModel(FlurlClient httpClient, string url)
        {
            this.httpClient = httpClient;
            GameInfo = new F95GameInfo(url);
        }

        public F95GameInfoViewModel(FlurlClient httpClient, F95GameInfo gameInfo)
        {
            this.httpClient = httpClient;
            GameInfo = gameInfo;
        }

        #endregion

        #region Public methods

        public async Task<bool> InitializeGameInfoAsync()
        {
            var threadName = await GetGameThreadNameAsync();
            if (threadName == null)
                return false;
            else
            {
                Name = GetGameName(threadName);
                LatestVersion = GetGameVersion(threadName);

                return true;
            }
        }

        public async Task<bool> RefreshLatestVersionAsync()
        {
            var threadName = await GetGameThreadNameAsync();
            if (threadName == null)
            {
                Tools.ShowErrorMessage($"Couldn\'t find thread \"{gameInfo.Id}\". Make sure it exists and you entered it correctly!");
                return false;
            }
            else
            {
                var newLatestVersion = GetGameVersion(threadName);
                if (newLatestVersion != null)
                    LatestVersion = newLatestVersion;
                else
                {
                    Tools.ShowErrorMessage($"Couldn\'t get game version from thread \"{gameInfo.Id}\"!");
                    return false;
                }

                return true;
            }
        }

        public void SyncVersions()
        {
            CurrentVersion = LatestVersion;
            IsVersionFinished = false;
        }

        #endregion

        #region Private methods

        private async Task<string> GetGameThreadNameAsync()
        {
            try
            {
                var response = await httpClient.Request(F95Urls.ThreadsUrlPathSegment, gameInfo.Id).GetAsync();
                var responseContent = await response.Content.ReadAsStringAsync();

                var htmlParser = new HtmlParser();
                var parsedResponseContent = await htmlParser.ParseDocumentAsync(responseContent);

                var threadName = parsedResponseContent.QuerySelector("h1[class=\"p-title-value\"]")?.TextContent;
                return threadName;
            }
            catch
            {
                return null;
            }
        }

        private static string GetGameName(string threadName)
        {
            var nameBeginIndex = -1;
            var nameEndIndex = -1;

            for (int i = 0; i < threadName.Length; i++)
            {
                if ((nameBeginIndex == -1) && (threadName[i] == ']') && ((i + 2) < threadName.Length) && (threadName[i + 2] != '['))
                    nameBeginIndex = i + 2;

                if ((nameBeginIndex != -1) && (threadName[i] == '['))
                    nameEndIndex = i - 1;

                if ((nameBeginIndex != -1) && (nameEndIndex != -1))
                    return threadName.Substring(nameBeginIndex, nameEndIndex - nameBeginIndex);
            }

            return null;
        }

        private static string GetGameVersion(string threadName)
        {
            var versionBeginIndex = -1;
            var versionEndIndex = -1;

            for (int i = 0; i < threadName.Length; i++)
                if ((threadName[i] == ']') && ((i + 2) < threadName.Length) && (threadName[i + 2] != '['))
                    for (int j = i + 2; j < threadName.Length; j++)
                    {
                        if ((versionBeginIndex == -1) && (threadName[j] == '[') && ((j + 1) < threadName.Length))
                            versionBeginIndex = j + 1;

                        if ((versionBeginIndex != -1) && (threadName[j] == ']'))
                            versionEndIndex = j;

                        if ((versionBeginIndex != -1) && (versionEndIndex != -1))
                            return threadName.Substring(versionBeginIndex, versionEndIndex - versionBeginIndex);
                    }

            return null;
        }

        public bool Equals(F95GameInfoViewModel other)
        {
            if (other == null)
                return false;

            return Name == other.Name;
        }

        #endregion
    }

    public class F95ThreadInfo
    {
        #region Properties

        public string Url { get; set; }

        public string Id { get; set; }

        #endregion

        #region Constructors

        public F95ThreadInfo()
        {
        }

        public F95ThreadInfo(string url)
        {
            Url = url;
            Id = GetThreadId(url);
        }

        #endregion

        #region Private methods

        private static string GetThreadId(string url)
        {
            var idBeginIndex = url.LastIndexOf('.') + 1;
            var idEndIndex = url.LastIndexOf('/');

            return url.Substring(idBeginIndex, idEndIndex - idBeginIndex);
        }

        #endregion
    }

    public class F95GameInfo : F95ThreadInfo
    {
        #region Properties

        public string Name { get; set; }

        public string CurrentVersion { get; set; } = NoVersionString;

        public string LatestVersion { get; set; } = NoVersionString;

        public bool IsFavorite { get; set; } = false;

        public bool IsVersionFinished { get; set; } = false;

        #endregion

        #region Public fields

        public const string NoVersionString = "-";

        #endregion

        #region Constructors

        public F95GameInfo()
        {

        }

        public F95GameInfo(string url) : base(url)
        {
        }

        #endregion
    }

    public static class F95GameInfoTools
    {
        #region Private fields

        private const string gameInfoCollectionFileName = "gameInfoCollection.json";

        #endregion

        #region Public methods

        public static async Task<bool> SaveGameInfoCollectionToFileAsync(List<F95GameInfo> gameInfoCollection)
        {
            var fs = default(FileStream);
            try
            {
                fs = File.Create(gameInfoCollectionFileName);
                await JsonSerializer.SerializeAsync(fs, gameInfoCollection);

                return true;
            }
            catch (System.Exception ex)
            {
                return false;
            }
            finally
            {
                fs?.Dispose();
            }
        }

        public static async Task<List<F95GameInfo>> LoadGameInfoCollectionFromFileAsync()
        {
            var fs = default(FileStream);
            try
            {
                fs = File.OpenRead(gameInfoCollectionFileName);
                var gameInfoCollection = await JsonSerializer.DeserializeAsync<List<F95GameInfo>>(fs);

                return gameInfoCollection;
            }
            catch (System.Exception ex)
            {
                return new List<F95GameInfo>();
            }
            finally
            {
                fs?.Dispose();
            }
        }

        #endregion
    }
}
