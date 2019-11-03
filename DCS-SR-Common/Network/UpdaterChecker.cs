using System;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using NLog;
using Octokit;

namespace Ciribob.DCS.SimpleRadio.Standalone.Common
{
    //Quick and dirty update checker based on GitHub Published Versions
    public class UpdaterChecker
    {
        public static readonly string GITHUB_USERNAME = "ciribob";
        public static readonly string GITHUB_REPOSITORY = "DCS-SimpleRadioStandalone";
        // Required for all requests against the GitHub API, as per https://developer.github.com/v3/#user-agent-required
        public static readonly string GITHUB_USER_AGENT = $"{GITHUB_USERNAME}_{GITHUB_REPOSITORY}";

        public static readonly string MINIMUM_PROTOCOL_VERSION = "1.7.0.0";

        public static readonly string VERSION = "1.7.0.2";

        private static readonly Logger _logger = LogManager.GetCurrentClassLogger();

        public static async void CheckForUpdate(bool checkForBetaUpdates)
        {
            Version currentVersion = Version.Parse(VERSION);

#if DEBUG
            _logger.Info("Skipping update check due to DEBUG mode");
#else
            try
            {
                var githubClient = new GitHubClient(new ProductHeaderValue(GITHUB_USER_AGENT, VERSION));

                var releases = await githubClient.Repository.Release.GetAll(GITHUB_USERNAME, GITHUB_REPOSITORY);

                Version latestStableVersion = new Version();
                Release latestStableRelease = null;
                Version latestBetaVersion = new Version();
                Release latestBetaRelease = null;

                // Retrieve last stable and beta branch release as tagged on GitHub
                foreach (Release release in releases)
                {
                    Version releaseVersion;

                    if (Version.TryParse(release.TagName.Replace("v", ""), out releaseVersion))
                    {
                        if (release.Prerelease && releaseVersion > latestBetaVersion)
                        {
                            latestBetaRelease = release;
                            latestBetaVersion = releaseVersion;
                        }
                        else if (!release.Prerelease && releaseVersion > latestStableVersion)
                        {
                            latestStableRelease = release;
                            latestStableVersion = releaseVersion;
                        }
                    }
                    else
                    {
                        _logger.Warn($"Failed to parse GitHub release version {release.TagName}");
                    }
                }

                // Compare latest versions with currently running version depending on user branch choice
                if (checkForBetaUpdates && latestBetaVersion > currentVersion)
                {
                    ShowUpdateAvailableDialog("beta", latestBetaVersion, latestBetaRelease.HtmlUrl);
                }
                else if (latestStableVersion > currentVersion)
                {
                    ShowUpdateAvailableDialog("stable", latestStableVersion, latestStableRelease.HtmlUrl);
                }
                else if (checkForBetaUpdates && latestBetaVersion == currentVersion)
                {
                    _logger.Warn($"Running latest beta version: {currentVersion}");
                }
                else if (latestStableVersion == currentVersion)
                {
                    _logger.Warn($"Running latest stable version: {currentVersion}");
                }
                else
                {
                    _logger.Warn($"Running development version: {currentVersion}");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to check for updated version");
            }
#endif
        }

        public static void ShowUpdateAvailableDialog(string branch, Version version, string url)
        {
            _logger.Warn($"New {branch} version available on GitHub: {version}");

            var result = MessageBox.Show($"New {branch} version {version} available!\n\nDo you want to update?",
                "Update available", MessageBoxButton.YesNo, MessageBoxImage.Information);

            if (result == MessageBoxResult.Yes)
            {
                Process.Start(url);
            }
        }
    }
}