using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.IO;

namespace SceneNavi
{
    public partial class UpdateCheckDialog : Form
    {
        enum UpdateTxtLines : int { NewVersionNumber = 0, UpdatePageUrl = 1, ReleaseNotesUrl = 2 };

        readonly Version _localVersion;
        Version _remoteVersion;
        string _updatePageUrl, _releaseNotesUrl;

        private bool IsRemoteVersionNewer => (_remoteVersion > _localVersion);

        public UpdateCheckDialog()
        {
            InitializeComponent();

            _localVersion = new Version(Application.ProductVersion);
            //localVersion = new Version(1, 0, 1, 6); //fake beta6

            var tmr = new System.Timers.Timer();
            tmr.Elapsed += new System.Timers.ElapsedEventHandler(tmr_Elapsed);
            tmr.Interval = 2.0;
            tmr.Start();
        }

        private void tmr_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            (sender as System.Timers.Timer)?.Stop();

            Cursor.Current = Cursors.WaitCursor;
            this.UiThread(() => lblStatus.Text = "Checking for version information...");

            var finalStatusMsg = string.Empty;
            try
            {
                if (VersionManagement.RemoteFileExists(Configuration.UpdateServer))
                {
                    this.UiThread(() => lblStatus.Text = "Version information found; downloading...");

                    var updateInformation = VersionManagement.DownloadTextFile(Configuration.UpdateServer);

                    _remoteVersion = new Version(updateInformation[(int)UpdateTxtLines.NewVersionNumber]);
                    _updatePageUrl = updateInformation[(int)UpdateTxtLines.UpdatePageUrl];
                    if (updateInformation.Length >= 2) _releaseNotesUrl = updateInformation[(int)UpdateTxtLines.ReleaseNotesUrl];

                    this.UiThread(() => VersionManagement.DownloadRtfFile(_releaseNotesUrl, rlblChangelog));

                    if (IsRemoteVersionNewer)
                    {
                        this.UiThread(() => btnDownload.Enabled = true);
                        finalStatusMsg =
                            $"New version {VersionManagement.CreateVersionString(_remoteVersion)} is available!";
                    }
                    else
                    {
                        finalStatusMsg =
                            $"You are already using the most recent version {VersionManagement.CreateVersionString(_localVersion)}.\n";
                    }
                }
                else
                    finalStatusMsg = "Version information file not found found; please contact a developer.";
            }
            catch (WebException wex)
            {
                /* Web access failed */
                MessageBox.Show(wex.ToString(), "Web Exception", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            catch (Win32Exception w32Ex)
            {
                /* Win32 exception, ex. no browser found */
                if (w32Ex.ErrorCode == -2147467259) MessageBox.Show(w32Ex.Message, "Process Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
            catch (Exception ex)
            {
                /* General failure */
                MessageBox.Show(ex.ToString(), "General Exception", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }

            this.UiThread(() => lblStatus.Text = finalStatusMsg);
            Cursor.Current = DefaultCursor;
            this.UiThread(() => btnClose.Enabled = true);
        }

        private void btnDownload_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start(_updatePageUrl);
            DialogResult = DialogResult.Cancel;
        }
    }
}
