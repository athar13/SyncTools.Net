using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SyncTools
{
    public class SyncTools
    {
        private const string DEF_BANNER = ".NET Core 3.1 port of SyncTool for Sysinternal. Copyright (c) 2021 Athar Syed";
        private const string DEF_USERAGENT = "SyncTool.NET/1.0.1";
        private const string DEF_IGNOREFILE = ".syncignore";
        private const string DEF_STATUSFILE = ".syncstatus";
        private const string DEF_DOWNLOADURL = "https://live.sysinternals.com/";

        private static readonly HttpClient httpClient = new HttpClient();

        private readonly string cachePath;

        private bool bIsDebug;
        private bool bVerbose;
        private string DirectoryPath;
        private string DownloadUrl;

        internal class Download
        {
            public string File { get; }
            public string Signature { get; }
            public bool IsNew { get; }

            internal Download(string file, string signature, bool is_new)
            {
                File = file;
                Signature = signature;
                IsNew = is_new;
            }
        }

        public SyncTools(string[] args)
        {
            cachePath = Environment.GetFolderPath(Environment.SpecialFolder.InternetCache);
            LoadDefaultValues();
            ParseArguments(args);
        }

        private void LoadDefaultValues()
        {
            bIsDebug = false;
            bVerbose = false;
            DirectoryPath = Environment.CurrentDirectory;
            DownloadUrl = DEF_DOWNLOADURL;
        }

        private void ParseArguments(string[] args)
        {
            PrintDebug($"{nameof(ParseArguments)} - START");
            if (args.Length > 0)
            {
                for (var pos = 0; pos < args.Length; pos++)
                {
                    var arg = args[pos];
                    if (arg.StartsWith("-") || arg.StartsWith("--"))
                    {
                        if (arg.Equals("-d", StringComparison.OrdinalIgnoreCase) || arg.Equals("--directory", StringComparison.OrdinalIgnoreCase))
                        {
                            DirectoryPath = args[++pos];
                            //PrintDebug($"{nameof(ParseArguments)} - Directory set to {DirectoryPath}.");
                        }

                        if (arg.Equals("-u", StringComparison.OrdinalIgnoreCase) || arg.Equals("--url", StringComparison.OrdinalIgnoreCase))
                        {
                            DownloadUrl = args[++pos];
                            //PrintDebug($"{nameof(ParseArguments)} - Url set to {DownloadUrl}.");
                        }

                        if (arg.Equals("-t", StringComparison.OrdinalIgnoreCase) || arg.Equals("--testmode", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"{nameof(ParseArguments)} - Debug mode enabled.");
                            bIsDebug = true;
                        }

                        if (arg.Equals("-v", StringComparison.OrdinalIgnoreCase) || arg.Equals("--verbose", StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"{nameof(ParseArguments)} - Verbose mode enabled.");
                            bVerbose = true;
                        }

                        if (arg.Equals("-h", StringComparison.OrdinalIgnoreCase) || arg.Equals("--help", StringComparison.OrdinalIgnoreCase))
                        {
                            PrintHelp();
                            Environment.Exit(0);
                        }
                    }
                }
            }
            PrintDebug($"{nameof(ParseArguments)} - END");
        }

        private bool PrepareDirectory(string outdir)
        {
            PrintDebug($"{nameof(PrepareDirectory)} - START");
            if (string.IsNullOrEmpty(outdir))
            {
                PrintDebug($"{nameof(PrepareDirectory)} - Using curreny directory.");
                DirectoryPath = Environment.CurrentDirectory;
                return true;
            }

            var di = new DirectoryInfo(outdir);
            DirectoryPath = di.FullName;

            if (!di.Exists)
            {
                PrintDebug($"{nameof(PrepareDirectory)} - Output directory does not exist.");
                return false;
            }

            PrintDebug($"{nameof(PrepareDirectory)} - END");
            return true;
        }

        private bool PrepareUrl(string url)
        {
            PrintDebug($"{nameof(PrepareUrl)} - START");
            if (string.IsNullOrEmpty(url))
            {
                PrintDebug($"{nameof(PrepareUrl)} - Using default url.");
                DownloadUrl = DEF_DOWNLOADURL;
                return true;
            }
            else
            {
                try
                {
                    var uri = new Uri(url);
                    DownloadUrl = uri.ToString();
                }
                catch (Exception ex)
                {
                    PrintDebug($"{nameof(PrepareUrl)} - ERROR");
                    Console.WriteLine($"{url} is not a valid Url.");
                    PrintDebug($"{ex.Message}");
                    return false;
                }
            }
            PrintDebug($"{nameof(PrepareUrl)} - END");
            return true;
        }

        public async Task Run()
        {
            PrintHeaders();
            PrintDebug($"{nameof(SyncTools)}:{nameof(Run)} - START");

            try
            {
                if (!PrepareDirectory(DirectoryPath))
                {
                    return;
                }

                if (!PrepareUrl(DownloadUrl))
                {
                    return;
                }

                // get list of tools to download
                var toolList = await GetOnlineToolList(DownloadUrl);

                // prepare list of downloadables
                var downloadList = await PrepareDownloadList(toolList);

                // run download
                await DownloadUpdates(downloadList);

                PrintDebug($"{nameof(SyncTools)}:{nameof(Run)} - END");
            }
            catch (Exception ex)
            {
                PrintDebug($"{nameof(SyncTools)}:{nameof(Run)} - ERROR");
                PrintDebug($"Exception: {ex.Message}");
            }
        }

        private async Task<Dictionary<string, string>> DownloadUpdates(List<Download> list)
        {
            var status = new Dictionary<string, string>();

            foreach (var item in list)
            {
                //if (item.IsNew)
                //{
                //    Console.Write($"* {item.File} ");
                //}
                //else
                //{
                //    Console.Write($"u {item.File} ");
                //}
                var result = DownloadFile(DownloadUrl, item.File, DirectoryPath);
                if (result)
                {
                    Console.SetCursorPosition(0, Console.CursorTop);
                    Console.Write(new string(' ', Console.WindowWidth));
                    Console.SetCursorPosition(0, Console.CursorTop);
                    if (item.IsNew)
                    {
                        Console.WriteLine($"* {item.File}");
                    }
                    else
                    {
                        Console.WriteLine($"u {item.File}");
                    }
                    status.Add(item.File, item.Signature);
                }
                else
                {
                    Console.WriteLine($"! {item.File}");
                }
            }

            await SaveStatus(status);

            return status;
        }

        internal class FileUserState
        {
            public string Filename { get; }
            public string CachePath { get; }
            ////public bool IsNew { get; }
            internal FileUserState(string file_name, string cache_path) ////, bool is_new)
            {
                Filename = file_name;
                CachePath = cache_path;
                ////IsNew = is_new;
            }

        }

        private bool DownloadFile(string url, string filename, string filepath)
        {
            PrintDebug($"{nameof(DownloadFile)}:{filename} - START");
            try
            {
                //
                var cacheFile = Path.Combine(cachePath, filename);
                var toolFile = Path.Combine(filepath, filename);
                var userState = new FileUserState(toolFile, cacheFile);

                // prepare
                if (File.Exists(cacheFile))
                {
                    try
                    {
                        File.Delete(cacheFile);
                    }
                    catch (Exception)
                    {
                        // cannot delete for some reason so create a random but unique filename for cache
                        cacheFile = Path.Combine(cachePath, $"{Guid.NewGuid()}-{DateTime.Now:yyyyMMddHHmmssfffff}-{filename}");
                    }
                }

                var downloadStatus = false;
                // print start
                using (var client = new WebClient())
                using (var completedSignal = new AutoResetEvent(false))
                {
                    // download complete event handler
                    client.DownloadFileCompleted += (s, e) =>
                    {
                        if (e.Error != null)
                        {
                            PrintDebug($"{nameof(DownloadFile)}:{filename} - DOWNLOAD FAIL");
                            downloadStatus = false;
                        }
                        else
                            downloadStatus = true;
                        completedSignal.Set();
                    };

                    // download progress event handler
                    client.DownloadProgressChanged += (s, e) =>
                    {
                        Console.SetCursorPosition(0, Console.CursorTop);
                        Console.Write($"{filename} : {e.ProgressPercentage}");
                    };

                    // download
                    client.DownloadFileAsync(new Uri(url), cacheFile, userState);
                    completedSignal.WaitOne();
                }

                // move file from temp to folder
                if (downloadStatus)
                {
                    try
                    {
                        File.Move(cacheFile, toolFile, true);
                    }
                    catch (Exception)
                    {
                        PrintDebug($"{nameof(DownloadFile)}:{filename} - MOVE FAIL");
                        return false;
                    }
                }
                PrintDebug($"{nameof(DownloadFile)}:{filename} - END");
                return true;
            }
            catch (Exception ex)
            {
                PrintDebug($"{nameof(DownloadFile)}:{filename} - ERROR");
                Console.WriteLine(ex);
                return false;
            }
        }

        private async Task<List<Download>> PrepareDownloadList(Dictionary<string, string> tools)
        {
            PrintDebug($"{nameof(PrepareDownloadList)} - START");

            // load statuses
            var status = await LoadStatus();

            // load ignores
            var ignorable = await LoadIgnores();

            var downloadList = new List<Download>();
            int countNew = 0;
            int countUpdate = 0;
            int countUpToDate = 0;
            foreach (var key in tools.Keys)
            {
                PrintDebug($"{key}: {tools[key]}");
                if (ShouldIgnore(key, ignorable))
                {
                    PrintDebug($"Ignoring {key}");
                    continue;
                }

                var newFile = !status.ContainsKey(key);
                var fileStatusSignature = newFile ? string.Empty : status[key];

                if (!fileStatusSignature.Equals(tools[key], StringComparison.OrdinalIgnoreCase))
                {
                    downloadList.Add(new Download
                    (
                        key,
                        tools[key],
                        newFile
                    ));

                    if (newFile)
                    {
                        countNew++;
                    }
                    else
                    {
                        countUpdate++;
                    }
                }
                else
                {
                    countUpToDate++;
                }
            }

            DisplayReport(countNew, countUpdate, countUpToDate);

            PrintDebug($"{nameof(PrepareDownloadList)} - END");
            return downloadList;
        }

        private void DisplayReport(int countNew, int countUpdate, int countUpToDate)
        {
            if (countUpToDate > 0)
            {
                Console.WriteLine($"{countUpToDate} {(countUpToDate > 1 ? "files" : "file")} are up to date");
            }

            if (countUpdate == 0 && countNew == 0)
            {
                Console.WriteLine("No updates are available");
                Environment.Exit(0);
            }

            if (countUpdate > 0)
            {
                Console.WriteLine($"{countUpdate} {(countUpdate > 1 ? "Updates" : "Update")} to download.");
            }

            if (countNew > 0)
            {
                Console.WriteLine($"{countNew} new {(countNew > 1 ? "files" : "file")} to download.");
            }
        }

        private bool ShouldIgnore(string file, string ignore, bool strict = true)
        {
            var pos = file.LastIndexOf(".");
            if (pos < 0 && strict)
                return true;

            var ext = $"*{file.Substring(pos)}";
            return ignore.IndexOf(ext, 0, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private void PrintVerbose(string message)
        {
            if (bVerbose)
            {
                Console.WriteLine(message);
            }
        }

        private void PrintDebug(string message)
        {
            if (bIsDebug)
            {
                Console.WriteLine($"{DateTime.Now:s} - {message}");
            }
        }

        private void PrintHeaders()
        {
            Console.WriteLine(DEF_BANNER);
        }

        private void PrintHelp()
        {
            var format = "    {0,-11} ({1,-2}) - {2}.";
            Console.WriteLine("Usage:");
            Console.WriteLine("    SyncTools -u <url> -d <path>");
            Console.WriteLine();
            Console.WriteLine(string.Format(format, "--url", "-u", "Url"));
            Console.WriteLine(string.Format(format, "--directory", "-d", "Output directory"));
            ////Console.WriteLine(string.Format(format, "--verbose", "-v", "Display detailed information"));
            Console.WriteLine(string.Format(format, "--help", "-h", "This help information"));
        }

        private async Task SaveStatus(Dictionary<string, string> status)
        {
            PrintDebug($"{nameof(SaveStatus)} - START");
            try
            {
                var lines = new List<string>();
                foreach (var key in status.Keys)
                {
                    lines.Add($"{key};{status[key]}");
                }
                string file = Path.Combine(DirectoryPath, DEF_STATUSFILE);
                await File.WriteAllLinesAsync(file, lines, Encoding.UTF8);
            }
            catch (Exception ex)
            {
                PrintDebug($"{nameof(SaveStatus)} - ERROR");
                PrintDebug(ex.Message);
                return;
            }
            PrintDebug($"{nameof(SaveStatus)} - END");
        }

        private async Task<Dictionary<string, string>> LoadStatus()
        {
            PrintDebug($"{nameof(LoadStatus)} - START");
            var statuses = new Dictionary<string, string>();
            try
            {
                string file = Path.Combine(DirectoryPath, DEF_STATUSFILE);
                if (File.Exists(file))
                {
                    var lines = await File.ReadAllLinesAsync(file, Encoding.UTF8);
                    foreach (var line in lines)
                    {
                        var items = line.Split(";");
                        statuses.Add(items[0], items[1]);
                    }
                }
            }
            catch (Exception ex)
            {
                PrintDebug($"Error in LoadStatus. {ex.Message}");
            }
            PrintDebug($"{nameof(LoadStatus)} - END");
            return statuses;
        }

        private async Task<string> LoadIgnores()
        {
            PrintDebug($"{nameof(LoadIgnores)} - START");
            var _ignore = string.Empty;
            try
            {
                string file = Path.Combine(DirectoryPath, DEF_IGNOREFILE);
                if (File.Exists(file))
                {
                    var lines = await File.ReadAllLinesAsync(file, Encoding.UTF8);
                    _ignore = string.Join(";", lines);
                }
            }
            catch (Exception ex)
            {
                PrintDebug($"{nameof(LoadIgnores)} - ERROR");
                PrintDebug($"{nameof(LoadIgnores)} - {ex.Message}");
            }
            PrintDebug($"{nameof(LoadIgnores)} - END");
            return _ignore;
        }

        private async Task<Dictionary<string, string>> GetOnlineToolList(string url)
        {
            // get data from online
            var rawData = await GetLiveData(url);
            if (string.IsNullOrEmpty(rawData))
                return null;

            // clean the data
            var data = GetDataFromPre(rawData);
            if (string.IsNullOrEmpty(data))
                return null;

            // lines
            var lines = GetLinesFromPreData(data);
            if (lines.Count <= 0)
                return null;

            // tool mapping
            var tools = GetToolsFromList(lines);

            return tools;
        }

        private async Task<string> GetLiveData(string url)
        {
            PrintDebug($"{nameof(GetLiveData)} - START");
            try
            {
                httpClient.DefaultRequestHeaders.Accept.Clear();
                httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("text/html"));
                httpClient.DefaultRequestHeaders.Add("User-Agent", DEF_USERAGENT);

                var response = await httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();
                string responseBody = await response.Content.ReadAsStringAsync();

                return responseBody;
            }
            catch (Exception ex)
            {
                PrintDebug($"{nameof(GetLiveData)} - ERROR");
                PrintDebug($"{ex.Message}");
                return string.Empty;
            }
        }

        private string GetDataFromPre(string data)
        {
            try
            {
                PrintDebug($"{nameof(GetDataFromPre)} - START");
                string pattern = @"<pre>(?<data>.*)</pre>";
                string value = string.Empty;

                Match match = Regex.Match(data, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);

                if (match.Success)
                {
                    value = match.Groups["data"].Value;
                }

                PrintDebug($"{nameof(GetDataFromPre)} - END");
                return value;
            }
            catch (Exception ex)
            {
                PrintDebug($"{nameof(GetDataFromPre)} - ERROR");
                PrintDebug(ex.Message);
                return string.Empty;
            }
        }

        private List<string> GetLinesFromPreData(string data)
        {
            try
            {
                PrintDebug($"{nameof(GetLinesFromPreData)} - START");

                string pattern = @"<br>";

                var linedArray = Regex.Split(data, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);
                var listedLine = linedArray.Select(item => item.Trim()).ToList();

                PrintDebug($"{nameof(GetLinesFromPreData)} - END");
                return listedLine;
            }
            catch (Exception ex)
            {
                PrintDebug($"{nameof(GetLinesFromPreData)} - ERROR");
                Console.WriteLine(ex);
                return new List<string>();
            }
        }

        private Dictionary<string, string> GetToolsFromList(List<string> lines)
        {
            var tools = new Dictionary<string, string>();
            try
            {
                PrintDebug($"{nameof(GetToolsFromList)} - START");

                foreach (var line in lines)
                {
                    var pattern = @"^(?<signature>.+?\d+)\s+<a\s+href=""(?<href>.*)"">(?<filename>.*)</a>$";
                    Match match = Regex.Match(line, pattern, RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.IgnorePatternWhitespace | RegexOptions.ExplicitCapture);
                    if (match.Success)
                    {
                        var _signature = match.Groups["signature"].Value;
                        var _filename = match.Groups["filename"].Value;
                        tools.Add(_filename, _signature);
                    }
                    else
                    {
                        PrintDebug($"Skipped {line}.");
                    }
                    PrintDebug($"{tools.Count} tools found.");
                }
                PrintDebug($"{nameof(GetToolsFromList)} - END.");
            }
            catch (Exception ex)
            {
                PrintDebug($"{nameof(GetToolsFromList)} - ERROR");
                Console.WriteLine(ex);
            }
            return tools;
        }
    }
}
