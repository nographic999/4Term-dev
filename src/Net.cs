using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Windows.Forms;

namespace _4Term
{
    public class Net
    {
        /*----------------------------------------------------------
         * Net Class
         * ---------------------------------------------------------
         * DEVELOPMENT STATUS
         *
         * [✓] CheckGitHubConnection
         * [✓] GetRemoteVersion
         * [✓] DownloadFile
         * [✓] ExtractWith7zDecExe
         *---------------------------------------------------------*/

        /*------------------------------------------------------------
         * CheckGitHubConnection
         * 
         * Synchronously attempts to ping GitHub to verify network
         * connectivity. Returns true immediately upon first successful
         * ping response; otherwise retries up to MaxAttempts times before
         * returning false. Between retries, pauses execution for 100 ms.
         *------------------------------------------------------------*/
        public static bool CheckGitHubConnection()
        {
            string GitHubHost = "github.com";
            int MaxAttempts = 3;

            /* Attempt to reach GitHub */
            for (int Attempt = 1; Attempt <= MaxAttempts; Attempt++)
            {
                try
                {
                    /* Create Ping instance and send ICMP echo request */
                    using (Ping ping = new Ping())
                    {
                        var Reply = ping.Send(GitHubHost, 1000);

                        /* If ping reply indicates success, return true immediately */
                        if (Reply.Status == IPStatus.Success)
                            return true;
                    }
                }
                /* Suppress any exceptions that might occur during ping */
                catch { }

                /* Delay before next attempt, if not the last one */
                if (Attempt < MaxAttempts)
                    Thread.Sleep(100);
            }

            /* All attempts failed, return false indicating no connectivity */
            return false;
        }

        /*------------------------------------------------------------
         * GetRemoteVersion
         * 
         * Synchronously downloads a string from a given URL (version file)
         * using HttpClient. Blocks on the async call internally to avoid
         * exposing async to callers. Returns the trimmed response string
         * on success, or "none" if any exception occurs (e.g., network error).
         *------------------------------------------------------------*/
        public static string GetRemoteVersion(string versionUrl)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    /* Synchronously get the version string by blocking on async method */
                    return client.GetStringAsync(versionUrl).GetAwaiter().GetResult().Trim();
                }
                catch
                {
                    /* Return "none" if unable to retrieve version */
                    return "none";
                }
            }
        }

        /*------------------------------------------------------------
         * DownloadFile
         * 
         * Downloads a file from the specified URL and saves it to the 
         * destination path using a blocking HTTP request. Uses buffered 
         * file stream to write data to disk.
         *------------------------------------------------------------*/
        public static void DownloadFile(string url, string destinationPath)
        {
            using (HttpClient client = new HttpClient())
            {
                try
                {
                    /* Send GET request and wait for headers */
                    using (var response = client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
                    {
                        /* Ensure the HTTP request was successful */
                        response.EnsureSuccessStatusCode();

                        /* Open the content stream and write to file */
                        using (var contentStream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                        using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            contentStream.CopyTo(fileStream);
                        }
                    }
                }
                catch (Exception ex)
                {
                    /* Display error message if download fails */
                    MessageBox.Show($"Download failed: {ex.Message}");
                }
            }
        }

        /*------------------------------------------------------------
         * ExtractWith7zDecExe
         * 
         * Launches 7zDec.exe with the provided archive file path to extract
         * the archive contents. Displays a visible console window and waits
         * for extraction to finish.
         *------------------------------------------------------------*/
        public static bool ExtractWith7zDecExe(string archivePath)
        {
            /* Path to 7zDec.exe in the application directory */
            string sevenZipDecExe = Path.Combine(AppContext.BaseDirectory, "7zDec.exe");

            /* Ensure 7zDec.exe exists before running */
            if (!File.Exists(sevenZipDecExe))
                return false;

            /* Setup process start info */
            var psi = new ProcessStartInfo
            {
                FileName = sevenZipDecExe,
                Arguments = $"x \"{archivePath}\"", /* Extract archive */
                UseShellExecute = true,             /* Show console window */
                CreateNoWindow = false              /* Keep console visible */
            };

            try
            {
                /* Start 7zDec process */
                using (var process = Process.Start(psi))
                {
                    if (process == null)
                        return false;

                    /* Wait for process to complete */
                    process.WaitForExit();

                    /* Return success based on exit code */
                    return process.ExitCode == 0;
                }
            }
            catch
            {
                /* Return false on failure */
                return false;
            }
        }
    }
}
