using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Inedo.BuildMaster;
using Inedo.BuildMaster.Configuration;
using Inedo.BuildMaster.Extensibility.Providers;
using Inedo.BuildMaster.Extensibility.Providers.SourceControl;
using Inedo.BuildMaster.Files;
using Inedo.BuildMaster.Web;
using Inedo.Linq;

namespace Inedo.BuildMasterExtensions.Plastic
{
    /// <summary>
    /// Provides functionality for getting files, browsing folders, and applying labels in Plastic SCM.
    /// </summary>
    [ProviderProperties(
        "Plastic SCM",
        "Provides functionality for getting files, browsing folders, and applying labels in Plastic SCM.")]
    [CustomEditor(typeof(PlasticProviderEditor))]
    public sealed class PlasticProvider : SourceControlProviderBase, IVersioningProvider, IRevisionProvider
    {
        //lookup version of Plastic by path to cm.exe
        private static readonly Dictionary<string, string> _versionLookup = new Dictionary<string, string>();
        private static readonly ReaderWriterLock _rwlVersionLookup = new ReaderWriterLock();

        /// <summary>
        /// Gets or sets the path to the CM.EXE file.
        /// </summary>
        [Persistent]
        public string ExePath { get; set; }
        /// <summary>
        /// Gets or sets the Repository Name
        /// </summary>
        [Persistent]
        public string RepositoryName { get; set; }
        /// <summary>
        /// Gets or sets the time created in ticks
        /// </summary>
        [Persistent]
        public long? CreatedTicks { get; set; }
        /// <summary>
        /// Gets the name of the branch.
        /// </summary>
        public string BranchName
        {
            get
            {
                return "br:/main";
            }
        }

        /// <summary>
        /// Gets the <see cref="T:System.Char"/> used by the
        /// provider to separate directories/files in a path string.
        /// </summary>
        public override char DirectorySeparator
        {
            get { return '/'; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PlasticProvider"/> class.
        /// </summary>
        public PlasticProvider()
        {
        }

        /// <summary>
        /// Gets the repositories.
        /// </summary>
        /// <param name="exePath">The exe path.</param>
        /// <returns></returns>
        public static List<string> GetRepositories(string exePath )
        {
            var prov = new PlasticProvider() { ExePath = exePath };
            var reps = prov.CM("lrep", "--format={1}");
            if (reps != null && reps.Count > 0)
            {
                return reps.OrderBy(r => r).ToList();
            }
            return null;
        }

        /// <summary>
        /// When implemented in a derived class, retrieves the latest version of
        /// the source code from the provider's source path into the target path.
        /// </summary>
        /// <param name="sourcePath">Provider source path.</param>
        /// <param name="targetPath">Target file path.</param>
        public override void GetLatest(string sourcePath, string targetPath)
        {   
            var workspace = GetWorkspace();
            SwitchToBranch(workspace);
            CMPath(workspace.Location, "upd", ".");

            sourcePath = (sourcePath ?? string.Empty).Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar);
            var sourceCopyPath = Path.Combine(workspace.Location, sourcePath);
            Util.Files.CopyFiles(sourceCopyPath, targetPath);
        }

        /// <summary>
        /// When implemented in a derived class, returns a <see cref="T:Inedo.BuildMaster.Files.DirectoryEntryInfo"/>
        /// object from the specified source path.
        /// </summary>
        /// <param name="sourcePath">Provider source path.</param>
        /// <returns>
        /// 	<see cref="T:Inedo.BuildMaster.Files.DirectoryEntryInfo"/> object populated with the files in source control.
        /// </returns>
        /// <remarks>
        /// This method is not designed to be recursive
        /// </remarks>
        public override DirectoryEntryInfo GetDirectoryEntryInfo(string sourcePath)
        {
            var workspace = GetWorkspace();
            SwitchToBranch(workspace);
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

            return new DirectoryEntryInfo(dirName ?? string.Empty, sourcePath ?? string.Empty, dirList.ToArray(), fileList.ToArray());
        }

        /// <summary>
        /// When implemented in a derived class, returns the contents of the specified file.
        /// </summary>
        /// <param name="filePath">Provider file path.</param>
        /// <returns>
        /// Contents of the file as an array of bytes.
        /// </returns>
        public override byte[] GetFileContents(string filePath)
        {
            var workspace = GetWorkspace();
            SwitchToBranch(workspace);
            CMPath(workspace.Location, "upd", ".");

            filePath = Path.Combine(workspace.Location, filePath.Replace('/', Path.DirectorySeparatorChar));
            return File.ReadAllBytes(filePath);
        }

        /// <summary>
        /// When implemented in a derived class, indicates whether the provider
        /// is installed and available for use in the current execution context.
        /// </summary>
        /// <returns>
        /// Value indicating whether the provider is available in the current context.
        /// </returns>
        public override bool IsAvailable()
        {
            return true;
        }

        /// <summary>
        /// When implemented in a derived class, attempts to connect with the
        /// current configuration and throws an exception if unsuccessful.
        /// </summary>
        public override void ValidateConnection()
        {
            CM("cc");
        }

        /// <summary>
        /// When implemented in a derived class, applies the specified label to the specified
        /// source path.
        /// </summary>
        /// <param name="label">Label to apply.</param>
        /// <param name="sourcePath">Path to apply label to.</param>
        public void ApplyLabel(string label, string sourcePath)
        {
            var workspace = GetWorkspace();
            SwitchToBranch(workspace);
            CMPath(workspace.Location, "upd", ".");
            CMPath(workspace.Location, "mklb", label);

            sourcePath = (sourcePath ?? string.Empty).Trim('/');
            if (sourcePath == string.Empty)
                sourcePath = ".";

            CMPath(workspace.Location, "label", "lb:" + label, "-R", sourcePath);
        }

        /// <summary>
        /// When implemented in a derived class, retrieves labeled
        /// source code from the provider's source path into the target path.
        /// </summary>
        /// <param name="label">Label of source files to get.</param>
        /// <param name="sourcePath">Provider source path.</param>
        /// <param name="targetPath">Target file path.</param>
        public void GetLabeled(string label, string sourcePath, string targetPath)
        {
            var workspace = GetWorkspace();
            CMPath(workspace.Location, "stb", "--label=" + label, "--repository=" + RepositoryName);
            CMPath(workspace.Location, "upd", ".");

            sourcePath = (sourcePath ?? string.Empty).Replace('\\', '/').Replace('/', Path.DirectorySeparatorChar);
            var sourceCopyPath = Path.Combine(workspace.Location, sourcePath);
            Util.Files.CopyFiles(sourceCopyPath, targetPath);
        }

        /// <summary>
        /// Returns a "fingerprint" that represents the current revision on the source control repository.
        /// </summary>
        /// <param name="path">The source control path to monitor.</param>
        /// <returns>
        /// A representation of the current revision in source control.
        /// </returns>
        public byte[] GetCurrentRevision(string path)
        {
            var workspace = GetWorkspace();
            var ver = GetPlasticVersion();

            if (string.IsNullOrEmpty(path))
            {
                //If we are not looking for a subpath, the newest changeset is all you need to get to know if anything has changed anywhere in the repository
                var results = CMPath(workspace.Location, "query", "select top 1 changeset.iobjid from changeset,branch where changeset.fidbranch=branch.iobjid and branch.sname='main'  order by changeset.iobjid desc");
                if (results != null && results.Count > 1)
                {
                    long id;
                    if (long.TryParse(results[results.Count - 1], out id))
                    {
                        return BitConverter.GetBytes(id);
                    }
                }
            }
            else
            {
                var fullPath = Path.Combine(workspace.Location, path.Replace('/', '\\')).ToLower();
                //this query gets all of the files with their highest revision id (not revisionnumber)
                string query;
                if (ver == null || ver[0] == '3')
                {
                    //revisions with revisionnumber = -1 are excluded because these are checkouts, not check-ins
                    query = "select max(revisions.objectid) as maxrevision, revisions.itemid from revisions,branch where revisions.branchid = branch.iobjid and branch.sname='main' and revisions.revisionnumber >= 0 group by revisions.itemid order by max(revisions.objectid) desc";
                }
                else
                {
                    query = "select max(revisions.objectid) as maxrevision, revisions.itemid from revisions,branch where revisions.branchid = branch.iobjid and branch.sname='main' group by revisions.itemid order by max(revisions.objectid) desc";
                }
                var results = CMPath(workspace.Location, "query", query, "--solvepath=itemid");
                if (results != null && results.Count > 1)
                {
                    //the first matching item is the max revision id because we are ordering the query by revision id descending
                    for (int i = 0; i < results.Count; i++)
                    {
                        if (results[i].ToLower().Contains(fullPath))
                        {
                            var spl = results[i].Split(new char[] { ' ' }, 2);
                            if (spl != null && spl.Length > 0)
                            {
                                long revID;
                                if (long.TryParse(spl[0], out revID))
                                {
                                    return BitConverter.GetBytes(revID);
                                }
                            }
                        }
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Returns a <see cref="System.String"/> that represents this instance.
        /// </summary>
        /// <returns>
        /// A <see cref="System.String"/> that represents this instance.
        /// </returns>
        public override string ToString()
        {
            return "Provides functionality for getting files, browsing folders, and applying labels in Plastic SCM.";
        }

        internal void SwitchToBranch(WorkspaceInfo workspace)
        {
            CMPath(workspace.Location, "stb", this.BranchName, string.Format("--repository={0}", this.RepositoryName));
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
            var usersPath = Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"..\..\"));
            return Path.Combine(usersPath, Environment.UserName);
        }

        private string GetWorkspacePath()
        {
            var workspacesDir = Path.Combine(CoreConfig.BaseWorkingDirectory, "PlasticWorkspaces");
            if (!Directory.Exists(workspacesDir))
                Directory.CreateDirectory(workspacesDir);

            return Path.Combine(workspacesDir, this.RepositoryName + '_' + this.CreatedTicks);
        }

        private List<string> CM(string command, params string[] args)
        {
            return CMPath(null, command, args);
        }

        private List<string> CMPath(string workingDirectory, string command, params string[] args)
        {
            var argBuffer = new StringBuilder(command);
            argBuffer.Append(' ');

            foreach (var arg in args) {
                if (arg != null) {
                    argBuffer.AppendFormat("\"{0}\" ", arg);
                }
            }

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
            var workspacePath = GetWorkspacePath();

            List<string> results = null;
            try
            {
                results = CMPath(workspacePath, "wi");
            }
            catch (InvalidOperationException ioex)
            {
                //return value of > 0, this is normal for a folder not in a workspace
            }
            
            if (results == null || results.Count == 0 || results[0].Contains("not in a workspace"))
            {
                var wkspName = "BuildMaster_" + this.RepositoryName + "_" +  this.CreatedTicks;
                if (workspacePath.Contains("_WEBTMP"))
                    wkspName = wkspName + "_WEBTMP";
                CM("mkwk", wkspName, workspacePath);
                CMPath(workspacePath, "stb", this.BranchName, string.Format("--repository={0}", this.RepositoryName));
            }
            
            return new WorkspaceInfo(workspacePath);
        }

        private string GetPlasticVersion()
        {
            try
            {
                _rwlVersionLookup.AcquireReaderLock(1000);
                string ver = null;
                if (_versionLookup.TryGetValue(this.ExePath, out ver)) {
                    _rwlVersionLookup.ReleaseLock();
                    return ver;
                } else {
                    var verCheck = CM("version");
                    if (verCheck != null && verCheck.Count > 0) {
                        ver = verCheck[0].Trim();
                        if (!_versionLookup.ContainsKey(this.ExePath)) {
                            _rwlVersionLookup.UpgradeToWriterLock(1000);
                            if (!_versionLookup.ContainsKey(this.ExePath)) {
                                _versionLookup.Add(this.ExePath, ver);
                            }
                        }
                        _rwlVersionLookup.ReleaseLock();
                        return ver;
                    }
                }
            }
            catch (Exception ex)
            {
                //failed to get version, return default version (below)
            }
            finally
            {
                if (_rwlVersionLookup.IsReaderLockHeld || _rwlVersionLookup.IsWriterLockHeld)
                {
                    _rwlVersionLookup.ReleaseLock();
                }
            }
            return "4.0.0.0";
        }
    }
}
