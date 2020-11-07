using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

using AngleSharp.Html.Parser;

using Flurl.Http;

namespace F95UpdatesChecker
{
    public class F95LoginViewModel : BaseViewModel
    {
        #region Properties

        public string Username
        {
            get => username;
            set
            {
                if (username != value)
                {
                    username = value;
                    RaisePropertyChanged(nameof(Username));
                }
            }
        }
        public string Password
        {
            get => password;
            set
            {
                if (password != value)
                {
                    password = value;
                    RaisePropertyChanged(nameof(Password));
                }
            }
        }

        public string SessionToken
        {
            get => sessionToken;
            private set
            {
                if (sessionToken != value)
                {
                    sessionToken = value;
                    RaisePropertyChanged(nameof(SessionToken));
                }
            }
        }

        #endregion

        #region Private fields

        private string username;
        private string password;

        private string sessionToken;

        private readonly FlurlClient httpClient;

        private const string credentialsFileName = "credentials.json";

        #endregion

        #region Constructors

        public F95LoginViewModel(FlurlClient httpClient)
        {
            this.httpClient = httpClient;
        }

        #endregion

        #region Public methods

        public async Task<bool> LoginAsync()
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
                sessionToken = await GetSessionTokenAsync();

            var loginPayload = new
            {
                login = Username,
                url = "",
                password = Password,
                password_confirm = "",
                additional_security = "",
                remember = "1",
                _xfRedirect = F95Urls.SiteUrl,
                website_code = "",
                _xfToken = sessionToken
            };
            var response = await httpClient.Request(F95Urls.LoginUrlPathSegment).PostUrlEncodedAsync(loginPayload);

            if (!response.IsSuccessStatusCode)
                MessageBox.Show($"Something went wrong. {response.ReasonPhrase}", "Error!");
            else
                SaveLoginCredentialsToFileAsync();

            return response.IsSuccessStatusCode;
        }

        public async void SaveLoginCredentialsToFileAsync()
        {
            using (FileStream fs = File.Create(credentialsFileName))
            {
                var loginCredentials = new F95LoginCredentials() 
                { 
                    Username = Username, 
                    Password = Password 
                };
                await JsonSerializer.SerializeAsync(fs, loginCredentials);
            }
        }

        public async Task<bool> LoadLoginCredentialsFromFileAsync()
        {
            var fs = default(FileStream);
            try
            {
                fs = File.OpenRead(credentialsFileName);
                var loginCredentials = await JsonSerializer.DeserializeAsync<F95LoginCredentials>(fs);
                Username = loginCredentials.Username;
                Password = loginCredentials.Password;

                return true;
            }
            catch (System.Exception ex)
            {
                Username = "";
                Password = "";

                return false;
            }
            finally
            {
                fs?.Dispose();
            }
        }

        public void ResetLoginCredentials()
        {
            Username = "";
            Password = "";

            File.Delete(credentialsFileName);
        }

        #endregion

        #region Private methods

        private async Task<string> GetSessionTokenAsync()
        {
            var response = await httpClient.Request(F95Urls.LoginUrlPathSegment).GetAsync();
            var responseContent = await response.Content.ReadAsStringAsync();

            var htmlParser = new HtmlParser();
            var parsedResponseContent = await htmlParser.ParseDocumentAsync(responseContent);

            return parsedResponseContent.QuerySelector("input[name=\"_xfToken\"]").GetAttribute("value");
        }

        #endregion
    }

    public class F95LoginCredentials
    {
        #region Properties

        public string Username { get; set; }
        public string Password { get; set; }

        #endregion

        #region Constructors

        public F95LoginCredentials()
        {
        }

        #endregion
    }
}
