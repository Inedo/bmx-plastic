using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Extensibility.Providers.SourceControl;
using Inedo.BuildMaster.Files;
using Inedo.BuildMaster.Web;

namespace Inedo.BuildMasterExtensions.Plastic
{
    /// <summary>
    /// Provides functionality for getting files, browsing folders, and applying labels in Plastic SCM.
    /// </summary>
    [ProviderProperties(
        "Plastic SCM",
        "Provides functionality for getting files, browsing folders, and applying labels in Plastic SCM.")]
    [CustomEditor(typeof(PlasticProviderEditor))]
    public sealed class PlasticProvider : SourceControlProviderBase, IVersioningProvider
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PlasticProvider"/> class.
        /// </summary>
        public PlasticProvider()
        {
        }

        /// <summary>
        /// Gets or sets the Plastic workspace associated with this provider.
        /// </summary>
        [Persistent]
        public string Workspace { get; set; }
        /// <summary>
        /// Gets or sets the path to the CM.EXE file.
        /// </summary>
        [Persistent]
        public string ExePath { get; set; }

        public override char DirectorySeparator
        {
            get { return '/'; }
        }

        public override void GetLatest(string sourcePath, string targetPath)
        {
            var workspace = GetWorkspace();
            CMPath(workspace.Location, "stb", "br:/main");
            CMPath(workspace.Location, "upd", ".");

            sourcePath = (sourcePath ?? string.Empty).Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar);
            var sourceCopyPath = Path.Combine(workspace.Location, sourcePath);
            Util.Files.CopyFiles(sourceCopyPath, targetPath);
        }
        public override DirectoryEntryInfo GetDirectoryEntryInfo(string sourcePath)
        {
            var workspace = GetWorkspace();
            CMPath(workspace.Location, "stb", "br:/main");
            var results = CMPath(workspace.Location, "dir", sourcePath, "--format={2}|{5}");

            var fileList = new List<FileEntryInfo>();
            var dirList = new List<DirectoryEntryInfo>();

            var joinSrcPath = string.Empty;
            if (!string.IsNullOrEmpty(sourcePath))
                joinSrcPath = sourcePath.TrimEnd('/') + "/";

            foreach (var line in results)
            {
                var components = line.Split('|');
                if (components[1] == ".")
                    continue;

                if (components[0] == "dir")
                    dirList.Add(new DirectoryEntryInfo(components[1], joinSrcPath + components[1], new DirectoryEntryInfo[0], new FileEntryInfo[0]));
                else
                    fileList.Add(new FileEntryInfo(components[1], joinSrcPath + components[1]));
            }

            var dirName = sourcePath;
            if (!string.IsNullOrEmpty(sourcePath) && sourcePath.Contains("/"))
                dirName = sourcePath.Substring(sourcePath.LastIndexOf('/') + 1);

            return new DirectoryEntryInfo(dirName, sourcePath, dirList.ToArray(), fileList.ToArray());
        }
        public override byte[] GetFileContents(string filePath)
        {
            var workspace = GetWorkspace();
            CMPath(workspace.Location, "stb", "br:/main");
            CMPath(workspace.Location, "upd", ".");

            filePath = Path.Combine(workspace.Location, filePath.Replace('/', Path.DirectorySeparatorChar));
            return File.ReadAllBytes(filePath);
        }
        public override bool IsAvailable()
        {
            return true;
        }
        public override void ValidateConnection()
        {
            CM("cc");
        }

        public override string ToString()
        {
            return "Provides functionality for getting files, browsing folders, and applying labels in Plastic SCM.";
        }
        public void ApplyLabel(string label, string sourcePath)
        {
            var workspace = GetWorkspace();
            CMPath(workspace.Location, "stb", "br:/main");
            CMPath(workspace.Location, "upd", ".");
            CMPath(workspace.Location, "mklb", label);

            sourcePath = (sourcePath ?? string.Empty).Trim('/');
            if (sourcePath == string.Empty)
                sourcePath = ".";

            CMPath(workspace.Location, "label", "lb:" + label, "-R", sourcePath);
        }
        public void GetLabeled(string label, string sourcePath, string targetPath)
        {
            var workspace = GetWorkspace();
            CMPath(workspace.Location, "stb", "--label=" + label);
            CMPath(workspace.Location, "upd", ".");

            sourcePath = (sourcePath ?? string.Empty).Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar);
            var sourceCopyPath = Path.Combine(workspace.Location, sourcePath);
            Util.Files.CopyFiles(sourceCopyPath, targetPath);
        }

        private List<string> CM(string command, params string[] args)
        {
            return CMPath(null, command, args);
        }
        private List<string> CMPath(string workingDirectory, string command, params string[] args)
        {
            var argBuffer = new StringBuilder(command);
            argBuffer.Append(' ');

            foreach (var arg in args)
                argBuffer.AppendFormat("\"{0}\" ", arg);

            var startInfo = new ProcessStartInfo(this.ExePath, argBuffer.ToString())
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            startInfo.EnvironmentVariables["USERPROFILE"] = GetProfilePath();

            if (!string.IsNullOrEmpty(workingDirectory))
                startInfo.WorkingDirectory = workingDirectory;

            var process = new Process()
            {
                StartInfo = startInfo
            };

            this.LogProcessExecution(startInfo);

            process.Start();

            var lines = new List<string>();
            string line;

            while (!process.HasExited)
            {
                line = process.StandardOutput.ReadLine();
                if (line != null)
                {
                    if (!string.IsNullOrEmpty(line))
                        lines.Add(line);
                }
                else
                    Thread.Sleep(5);
            }

            if (process.ExitCode != 0)
            {
                var errorMessage = string.Join("", lines.ToArray()) + process.StandardOutput.ReadToEnd().Replace("\r", "").Replace("\n", "");
                throw new InvalidOperationException(errorMessage);
            }

            while ((line = process.StandardOutput.ReadLine()) != null)
            {
                if (!string.IsNullOrEmpty(line))
                    lines.Add(line);
            }

            return lines;
        }

        private WorkspaceInfo GetWorkspace()
        {
            foreach (var workspace in GetAvailableWorkspaces())
            {
                if (string.Equals(workspace.Name, this.Workspace, StringComparison.CurrentCultureIgnoreCase))
                    return workspace;
            }

            throw new InvalidOperationException("The specified Plastic SCM workspace is not defined on this client.");
        }
        private IEnumerable<WorkspaceInfo> GetAvailableWorkspaces()
        {
            var results = CM("lwk");
            foreach (var line in results)
                yield return new WorkspaceInfo(line);
        }

        /// <summary>
        /// Returns the actual profile path of the current user.
        /// </summary>
        /// <returns>Profile path of the current user.</returns>
        /// <remarks>
        /// The USERPROFILE environment variable and the SpecialFolder.ApplicationData values
        /// generate a profile path using Default User when run under a .NET AppPool in IIS 6.
        /// </remarks>
        private static string GetProfilePath()
        {
            // Full AH
            var usersPath = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"..\..\"));
            return Path.Combine(usersPath, Environment.UserName);
        }
    }
}
