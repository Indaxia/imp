using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using imp.Repository;

namespace imp
{
    public class PackageManager: DelayedBusyState
    {
        public Package ProjectPackage = null;
        public string ProjectPackageName = "imp-package.json";
        public string ProjectDirectory = "";
        public Dictionary<string, PackageDependency> Dependencies;
        public List<string> DependenciesOrderIndex;

        private RepositoryManager rm;
        private string ProjectStateLockName = "state.lock.json";
        private string ProjectPackageDir = ".imp";
        private string DefaultProjectDependenciesDir = ".imp/packages";
        private bool VerboseLog = false;

        public PackageManager(string projectDir, bool verboseLog)
        {
            ProjectDirectory = projectDir;
            Dependencies = new Dictionary<string, PackageDependency>();
            DependenciesOrderIndex = new List<string>();
            VerboseLog = verboseLog;
            rm = new RepositoryManager();
        }

        public void Clear()
        {
            Dependencies.Clear();
            DependenciesOrderIndex.Clear();
        }

        protected string GetDependenciesDir()
        {
            if(ProjectPackage != null && ProjectPackage.Source.RemoteSources.Length > 0) {
                return ProjectPackage.Source.RemoteSources;
            }
            return DefaultProjectDependenciesDir;
        }

        public string GetDependencyDir(PackageDependency dep, bool absolute = true)
        {
            if(absolute) {
                return Path.Combine(ProjectDirectory, GetDependenciesDir(), dep.id);
            }
            return Path.Combine(GetDependenciesDir(), dep.id);
        }

        public string GetDependencyFile(PackageDependency dep, bool absolute = true)
        {
            if(absolute) {
                return Path.Combine(ProjectDirectory, GetDependenciesDir(), dep.id, "src", "file.src");
            }
            return Path.Combine(GetDependenciesDir(), dep.id, "src", "file.src");
        }

        public void InstallDependency(string url, string version, bool isFile = false)
        {
            ProjectPackage = LocateProjectPackage();

            foreach(PackageDependency dep in ProjectPackage.Dependencies) {
                if(dep.Resource == url && dep.Version == version) {
                    ConsoleColorChanger.UseWarning();
                    Console.WriteLine("This dependency already exists");
                    ConsoleColorChanger.UsePrimary();
                    return;
                }
            }

            ProjectPackage.Dependencies.Add(new PackageDependency(isFile ? DependencyType.File : DependencyType.Package, url, version));

            SaveProjectPackage(ProjectPackage);            
            RefreshPackages(true, true);
        }

        public void RefreshPackages(
            bool loadAgain = false, 
            bool packageIsLocated = false, 
            string targetFileNameOnCreating = "",
            string sourceDirOnCreating = "",
            string remoteSourcesDirOnCreating = ""
        ) {
            isBusy = true;
            ConsoleColorChanger.UseAccent();
            if(loadAgain) {
                Console.WriteLine("Refreshing Dependencies");
            } else {
                Console.WriteLine("Locating Dependencies");
            }
            ConsoleColorChanger.UsePrimary();

            List<PackageDependency> oldPackageDeps = new List<PackageDependency>();

            if(ProjectPackage != null) {
                oldPackageDeps = ProjectPackage.Dependencies;
            }

            if(!packageIsLocated) {
                ProjectPackage = LocateProjectPackage(
                    targetFileNameOnCreating, 
                    sourceDirOnCreating,
                    remoteSourcesDirOnCreating
                );
            }

            refreshPackageDir();
            
            if(VerboseLog) Console.WriteLine("-- Loading state lock file");
            JObject stateLock = loadStateLock();

            if(!loadAgain) {
                if(stateLock == null) {
                    loadAgain = true;
                } else if(stateLock.Type == JTokenType.Object 
                    && stateLock["dependencies"] != null 
                    && stateLock["dependencies"].Type == JTokenType.Object
                ) {
                    var newDeps = stateLock["dependencies"];
                    Dependencies.Clear();
                    foreach(JProperty d in newDeps) {
                        PackageDependency dep = new PackageDependency(d, ProjectStateLockName);
                        Dependencies.Add(dep.id, dep);
                        DependenciesOrderIndex.Add(dep.id);
                    }
                    foreach(PackageDependency oldDep in oldPackageDeps) {
                        if(newDeps[oldDep.Resource] == null) {
                            loadAgain = true; // TODO: remove required dependencies only
                            break;
                        }
                    }
                }

                if(!loadAgain) {
                    foreach(PackageDependency d in ProjectPackage.Dependencies) {
                        if(Dependencies.ContainsKey(d.id)) {
                            continue;
                        } else {
                            Console.Write("  New dependency found: ");
                            ConsoleColorChanger.UseSecondary();
                            Console.WriteLine(d.Resource+" "+d.Version);
                            ConsoleColorChanger.UsePrimary();
                            loadAgain = true; // TODO: download required dependencies only
                            break;
                        }
                    }
                }
            }

            if(!loadAgain) {
                Console.WriteLine("  Dependencies are OK");
            }

            if(loadAgain) {
                Dependencies.Clear();
                DependenciesOrderIndex.Clear();
                UpdatePackages();
            }

            isBusy = false;
        }

        private void UpdatePackages()
        {
            string stateLockPath = Path.Combine(ProjectDirectory, ProjectPackageDir, ProjectStateLockName);
            string tmpPath = Path.Combine(ProjectDirectory, ProjectPackageDir, "tmp");
            if(Directory.Exists(tmpPath)) {
                Directory.Delete(tmpPath, true);
            }
            Directory.CreateDirectory(tmpPath);

            Dependencies.Clear();
            DependenciesOrderIndex.Clear();

            foreach(PackageDependency dep in ProjectPackage.Dependencies) {
                LoadDependency(dep);
            }

            JObject jsonDeps = new JObject();
            foreach(KeyValuePair<string, PackageDependency> kv in Dependencies) {
                jsonDeps.Add(kv.Value.ToJson(true));
            }
            JObject jsonObj = new JObject(new JProperty("dependencies", jsonDeps));
            File.WriteAllText(stateLockPath, jsonObj.ToString());
        }

        private void LoadDependency(PackageDependency dep, int depth = 0)
        {
            if(depth > 512) {
                throw new PackageException("  Dependency loop detected");
            }

            string tmpRoot = Path.Combine(ProjectDirectory, ProjectPackageDir, "tmp");
            if(Directory.Exists(tmpRoot)) {
                Directory.Delete(tmpRoot, true);
            }
            Directory.CreateDirectory(tmpRoot);

            Package p = DownloadDependency(dep, tmpRoot, depth);
            foreach(PackageDependency d in p.Dependencies) {
                if(!d.sameAs(dep) && !Dependencies.ContainsKey(d.id)) {
                    LoadDependency(d, ++depth);
                }
            }
            dep.Sources = p.Source.Sources;
            dep.EntryPoint = p.Source.EntryPoint;
            Dependencies.Add(dep.id, dep);
            DependenciesOrderIndex.Add(dep.id);
        }

        private Package DownloadDependency(PackageDependency dep, string tmpRoot, int depth = 0)
        {
            string dirPath = Path.Combine(ProjectDirectory, GetDependenciesDir(), dep.id);
            var provider = rm.getProvider(dep.Resource);

            if(Directory.Exists(dirPath)) {
                Directory.Delete(dirPath, true);
            }
            if(depth == 0) {
                Console.Write("  -> Loading "+(dep.Type == DependencyType.File ? "file: " : "package: "));
            } else {
                Console.Write(new String(' ', depth*2)+"  -> Loading "+(dep.Type == DependencyType.File ? "file: " : "package: "));
            }
            ConsoleColorChanger.UseAccent();
            Console.Write(dep.Version+" ");
            ConsoleColorChanger.UseSecondary();
            Console.WriteLine(dep.Resource);
            ConsoleColorChanger.UsePrimary();

            if(dep.Type == DependencyType.File) {
                string srcPath = Path.Combine(dirPath, "src");
                string filePath = Path.Combine(srcPath, "file.src");
                var host = new Uri(dep.Resource).Host;

                Directory.CreateDirectory(dirPath);
                Directory.CreateDirectory(srcPath);

                if(dep.Resource.Length > 0 && (provider != null || ProjectPackage.AllowHosts.Contains(host))) {
                    Task.WaitAll(Downloader.downloadFileAsync(dep.Resource, filePath));

                    var result = new Package("", "", "");

                    result.Source.Sources.Add("src");
                    result.Title = Path.GetFileNameWithoutExtension(dep.Resource);

                    return result;
                } else {
                    throw new PackageException("  Cannot resolve package: " + dep.Resource + ", wrong URL host for file type: '" + dep.Resource + "'");
                }
            } else if(provider != null) {
                string tmpFilePath = Path.Combine(tmpRoot, "imp-repository.zip");
                string zipFileUrl = provider.GetZipFileUrl(dep.Resource, dep.Version);

                Task.WaitAll(Downloader.downloadFileAsync(zipFileUrl, tmpFilePath));
                if(VerboseLog) Console.WriteLine("-- From " + zipFileUrl);
                if(VerboseLog) Console.WriteLine("-- Unzipping");
                Unzipper.unzipFile(tmpFilePath, tmpRoot, dirPath);

                EmptyDir(new DirectoryInfo(tmpRoot));

                string packageConfigPath = Path.Combine(dirPath, ProjectPackageName);

                if(! File.Exists(packageConfigPath)) {
                    throw new PackageException("  Cannot resolve package: " + dep.Resource + ", file '" + ProjectPackageName + "' not found inside");
                }
                return new Package(File.ReadAllText(packageConfigPath));
            }
            throw new PackageException("  Cannot resolve package: " + dep.Resource + ", no suitable repository provider for this url");
        }

        private Package LocateProjectPackage(
            string targetFileNameOnCreating = "", 
            string sourceDirOnCreating = "",
            string remoteSourcesDirOnCreating = ""
        ) {
            Console.Write("  Locating ");
            ConsoleColorChanger.UseSecondary();
            Console.Write(ProjectPackageName);
            ConsoleColorChanger.UsePrimary();
            Console.Write(" ... ");
            
            string packageConfigPath = Path.Combine(ProjectDirectory, ProjectPackageName);
            if(! File.Exists(packageConfigPath)) {
                if(targetFileNameOnCreating == "") {
                     throw new PackageException("  Cannot find " + ProjectPackageName + ". Did you initialize the project?");
                }
                Console.Write("creating ... ");
                File.WriteAllText(packageConfigPath, 
                    Package.getDefaultConfiguration(
                        targetFileNameOnCreating, 
                        sourceDirOnCreating,
                        remoteSourcesDirOnCreating
                    )
                );
            }
            Console.Write("parsing ... ");

            string jsonStr = "";
            for (int i=1; i <= 30; ++i) {
                try {
                    jsonStr = File.ReadAllText(packageConfigPath);
                    break;
                } catch (IOException) when (i <= 30) { 
                    Console.Write(".");
                    Thread.Sleep(200);
                }
            }
            
            var result = new Package(jsonStr);
            Console.WriteLine("done.");
            return result;
        }

        private void SaveProjectPackage(Package pp)
        {
            string packageConfigPath = Path.Combine(ProjectDirectory, ProjectPackageName);
            Console.Write("  Saving ");
            ConsoleColorChanger.UseSecondary();
            Console.Write(ProjectPackageName);
            ConsoleColorChanger.UsePrimary();
            Console.Write(" ... ");

            File.WriteAllText(packageConfigPath, pp.ToJson().ToString());

            Console.WriteLine("done!");
        }

        private void refreshPackageDir()
        {
            if(VerboseLog) Console.WriteLine("-- Refreshing package dir");
            string packageDirPath = Path.Combine(ProjectDirectory, ProjectPackageDir);
            if(! Directory.Exists(packageDirPath)) {
                Directory.CreateDirectory(packageDirPath);
            }

            string packagesSubdir = Path.Combine(ProjectDirectory, GetDependenciesDir());

            if(! Directory.Exists(packagesSubdir)) {
                if(VerboseLog) Console.WriteLine("-- Creating "+packagesSubdir);
                Directory.CreateDirectory(packagesSubdir);
            }
        }

        private JObject loadStateLock()
        {
            string stateLockPath = Path.Combine(ProjectDirectory, ProjectPackageDir, ProjectStateLockName);
            if(! File.Exists(stateLockPath)) {
                return null;
            }
            string stateLockStr = File.ReadAllText(stateLockPath);

            return JObject.Parse(stateLockStr);
        }

        public static void EmptyDir(DirectoryInfo directory)
        {
            foreach(FileInfo file in directory.GetFiles()) file.Delete();
            foreach(DirectoryInfo subDirectory in directory.GetDirectories()) subDirectory.Delete(true);
        }
    }
}