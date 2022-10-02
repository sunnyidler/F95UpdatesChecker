using AngleSharp.Html.Parser;

using Flurl.Http;

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace F95UpdatesChecker
{
    public class Settings
    {
        #region Properties

        /// <summary>
        /// Date and time of when was the last time games collection checked for updates.
        /// </summary>
        public DateTime LastChecked { get; set; } = DateTime.Now;
        /// <summary>
        /// Flag for identification of whether lists of games collapsed or not.
        /// </summary>
        public bool IsListsCollapsed { get; set; } = false;
        /// <summary>
        /// Sort order type.
        /// </summary>
        public SortOrder SortOrder { get; set; } = SortOrder.Alphabetical;
        /// <summary>
        /// Flag for identification of whether to give priority to favorites while sorting or not.
        /// </summary>
        public bool GivePriorityToFavoritesWhileSorting { get; set; } = true;
        /// <summary>
        /// Flag for identification of whether dark theme enabled.
        /// </summary>
        public bool IsDarkThemeEnabled { get; set; } = true;

        #endregion

        #region Private fields

        private const string settingsFileName = "settings.json";

        #endregion

        #region Public methods

        public static async Task<bool> SaveSettingsToFileAsync(Settings settings)
        {
            var fs = default(FileStream);
            try
            {
                fs = File.Create(settingsFileName);
                await JsonSerializer.SerializeAsync(fs, settings);

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

        public static async Task<Settings> LoadSettingsFromFileAsync()
        {
            var fs = default(FileStream);
            try
            {
                fs = File.OpenRead(settingsFileName);
                var settings = await JsonSerializer.DeserializeAsync<Settings>(fs);

                return settings;
            }
            catch (System.Exception ex)
            {
                return new Settings();
            }
            finally
            {
                fs?.Dispose();
            }
        }

        #endregion

    }
}
