using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Permissions;
using System.Timers;
using imp.ModuleManager;

namespace imp
{
    public class Application
    {
        public delegate void executionCallback();
        
        private bool VerboseLog = false;
        private PackageManager pm = null;
        private AbstractModuleManager mm = null;
        private string Version = "0.0.0.1"; // this is dynamically retrieved from assembly info
        private int RetryAttempt = 0;
        private List<FileSystemWatcher> Watchers = null;
        private Dictionary<string, DateTime> WatcherChangedFilesCache;

        public Application(string projectDir, string[] args, string version) 
        {
            Version = version;
            ConsoleColorChanger.SetPrimary(Console.ForegroundColor);
            var noExit = hasArg(args, "--noexit");

            if(args.Length < 1 || noExit || hasArg(args, "--help")) {
                Console.WriteLine("Indaxia Modules & Packages "+Version+" (IMP) by ScorpioT1000");
                Console.WriteLine("Get more info at: https://github.com/Indaxia/imp");
                Console.WriteLine("Arguments:");
                Console.WriteLine("  init build.lua");
                Console.WriteLine("  init src build.lua");
                Console.WriteLine("  init src war3map.lua");
                Console.WriteLine("  init includes/src war3map.as includes/packages");
                Console.WriteLine("  - initializes a new project with the source dir (optional), the target file name and remote sources dir (optional, default is .imp/packages)");
                Console.WriteLine("  init-clean");
                Console.WriteLine("  init-clean build.lua");
                Console.WriteLine("  - initializes a new project with a minimal configuration and without IMP Module Manager");
                Console.WriteLine("  update");
                Console.WriteLine("  - removes any package data and re-downloads it from the internet");
                Console.WriteLine("  install <package> [<version>] [file]");
                Console.WriteLine("  - adds a new package or file to your package file and installs dependencies. Omit version to require head revision. To add a file type 'file' as a third parameter");
                Console.WriteLine("  build");
                Console.WriteLine("  - builds all downloaded modules and sources into a target file");
                Console.WriteLine("  update build");
                Console.WriteLine("  - runs 'update' then 'build'");
                Console.WriteLine("  watch");
                Console.WriteLine("  - watches for changes of the sources and target and performs update or build");
                Console.WriteLine("Options:");
                Console.WriteLine("  --detailed");
                Console.WriteLine("  - add this option to get more detailed info about the internal processes");
                Console.WriteLine("");

                if(noExit) {
                    ConsoleColorChanger.UseAccent();
                    Console.Error.WriteLine("Press any key to exit.");
                    ConsoleColorChanger.UsePrimary();
                    Console.ReadKey();
                }
                return;
            }

            if(hasArg(args, "--detailed")) {
                VerboseLog = true;
            }

            pm = new PackageManager(projectDir, VerboseLog);

            for(; RetryAttempt < 3; ++RetryAttempt) {
                try {
                    if(hasArg(args, "build")) {
                        pm.RefreshPackages();
                        TryCreateModuleManager();
                        if(mm != null) {
                            mm.RebuildModules();
                        }
                        return;
                    } else if(hasArg(args, "install")) {
                        if(args.Length < 2) {
                            Console.WriteLine("install format: install url [version]");
                        } else {
                            pm.InstallDependency(args[1], args.Length > 2 ? args[2].Trim() : "*", args.Length > 3 && args[3] == "file");
                        }
                        return;
                    } else if(hasArg(args, "watch")) {
                        pm.RefreshPackages();
                        TryCreateModuleManager();
                        if(mm != null) {
                            mm.RebuildModules();
                        }
                        WatchForChanges();
                    } else if(hasArg(args, "update")) {
                        pm.RefreshPackages(true);
                        return;
                    } else if(hasArg(args, "init")) {
                        if(args.Length < 2) {
                            Console.WriteLine("init format: init [sourceDir] filename [remoteSourcesDir]");
                        } else {
                            pm.RefreshPackages(
                                true, 
                                false, 
                                args.Length > 2 ? args[2] : args[1], 
                                args.Length > 2 ? args[1] : "",
                                args.Length > 3 ? args[3] : ""
                            );
                            TryCreateModuleManager();
                            if(mm != null && mm.GetModuleManagerPackageURL().Length > 0) {
                                pm.InstallDependency(mm.GetModuleManagerPackageURL(), "*");
                            }
                        }
                        return;
                    } else if(hasArg(args, "init-clean")) {
                        pm.RefreshPackages(true, false, args.Length > 1 ? args[1] : "");
                        return;
                    } else {
                        Console.WriteLine("Wrong command. Execute the program without arguments or with --help to get help");
                        return;
                    }
                } catch(Exception e) {
                    ConsoleColorChanger.UseWarning();
                    Console.Error.WriteLine("General Error: " + e.Message);
                    Console.Error.WriteLine("Source: " + e.Source);
                    Console.Error.WriteLine("");
                    Console.Error.WriteLine("Press any key to try again. Press CTRL+C to stop.");
                    ConsoleColorChanger.UsePrimary();
                    Console.ReadKey();
                    pm.Clear();
                    if(mm != null) {
                        mm.Clear();
                    }
                }
                Console.Error.WriteLine("Retry attempt: " + RetryAttempt);
            }
        }

        private bool hasArg(string[] args, string key)
        {
            foreach(string arg in args) {
                if(arg == key) { return true; }
            }
            return false;
        }

        private void TryCreateModuleManager()
        {
            string language = pm.ProjectPackage.GetLanguage();
            if(language == "lua") {
                mm = new LuaModuleManager(pm, VerboseLog, Version);
            } else if(language == "angelscript") {
                mm = new AngelscriptModuleManager(pm, VerboseLog, Version);
            } else {
                mm = null;
                ConsoleColorChanger.UseWarning();
                Console.Error.WriteLine("Warning: Cannot find ModuleManager for the language: " + language + ". Please specify \"language\" option in your imp-config.json");
                ConsoleColorChanger.UsePrimary();
            }
        }

        private void WatchForChanges()
        {
            if(mm == null) {
                throw new Exception("Cannot watch without ModuleManager");
            }
            if(WatcherChangedFilesCache != null) {
                WatcherChangedFilesCache.Clear();
            } else {
                WatcherChangedFilesCache = new Dictionary<string, DateTime>();
            }
            if(Watchers != null) {
                foreach(var w in Watchers) {
                    w.Dispose();
                }
                Watchers.Clear();
            } else {
                Watchers = new List<FileSystemWatcher>();
            }
            
            string filter = (pm.ProjectPackage.Source.CustomExtensions == "")
                ? mm.GetSourceExtensions()
                : pm.ProjectPackage.Source.CustomExtensions;

            foreach(string d in pm.ProjectPackage.Source.Sources) {
                if(Path.IsPathRooted(d)) {
                    if(VerboseLog) Console.WriteLine("-- Watching "+d);
                    WatchDirectory(d, filter);
                } else {
                    if(VerboseLog) Console.WriteLine("-- Watching "+d);
                    WatchDirectory(Path.Combine(pm.ProjectDirectory, d), filter);
                }
            }
            WatchProjectPackage();
            WatchTargetFile();
            foreach(string fileToWatch in pm.ProjectPackage.WatchExtra) {
                WatchExtraFile(fileToWatch);
            }
            PrintReadyMessage();
            Console.ReadKey();
            System.Environment.Exit(0);
        }

        // [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        private void WatchDirectory(string path, string filter) 
        {
            string targetPath = Path.GetFullPath(path);
            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = targetPath;
            watcher.IncludeSubdirectories = true;
            watcher.NotifyFilter = NotifyFilters.LastWrite
                                | NotifyFilters.FileName
                                | NotifyFilters.DirectoryName;
            watcher.Filter = filter;

            watcher.Changed += OnSrcChanged;
            watcher.Created += OnSrcChanged;
            watcher.Deleted += OnSrcChanged;
            watcher.Renamed += OnSrcRenamed;

            watcher.EnableRaisingEvents = true;
            Watchers.Add(watcher);
        }

        // [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        private void WatchProjectPackage() 
        {
            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = pm.ProjectDirectory;
            watcher.NotifyFilter = NotifyFilters.LastWrite
                                | NotifyFilters.FileName
                                | NotifyFilters.DirectoryName;

            watcher.Changed += OnPackageChanged;
            watcher.Created += OnPackageChanged;
            watcher.Deleted += OnPackageChanged;
            watcher.Renamed += OnPackageRenamed;

            watcher.EnableRaisingEvents = true;
            Watchers.Add(watcher);
        }

        // [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        private void WatchTargetFile()
        {
            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = Path.GetDirectoryName(Path.Combine(pm.ProjectDirectory, pm.ProjectPackage.Target));
            watcher.NotifyFilter = NotifyFilters.LastWrite
                                | NotifyFilters.FileName
                                | NotifyFilters.DirectoryName;

            watcher.Changed += OnTargetChanged;
            watcher.Created += OnTargetChanged;
            watcher.Deleted += OnTargetChanged;
            watcher.Renamed += OnTargetRenamed;

            watcher.EnableRaisingEvents = true;
            Watchers.Add(watcher);
        }

        // [PermissionSet(SecurityAction.Demand, Name = "FullTrust")]
        private void WatchExtraFile(string relativeName)
        {
            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = Path.GetDirectoryName(Path.Combine(pm.ProjectDirectory, relativeName));
            watcher.NotifyFilter = NotifyFilters.LastWrite;
            watcher.Filter = Path.GetFileName(relativeName);

            watcher.Changed += OnExtraChanged;
            watcher.Created += OnExtraChanged;
            watcher.Deleted += OnExtraChanged;

            watcher.EnableRaisingEvents = true;
            Watchers.Add(watcher);
        }

        private void OnExtraChanged(object source, FileSystemEventArgs e)
        {
            if(!CheckIsFileReallyChanged(e.FullPath)) { return; }

            PrintWatcherEvent("Extra", getChangeType(e.ChangeType), e.FullPath);

            mm.invokeASAP("ModuleManager.RebuildModules", () => {
                mm.RebuildModules();
                PrintReadyMessage();
            });
        }

        private void OnTargetChanged(object source, FileSystemEventArgs e)
        {
            if(!CheckIsFileReallyChanged(e.FullPath) || !mm.IsTargetChangedOutside() || !e.Name.EndsWith(pm.ProjectPackage.Target)) { return; }

            PrintWatcherEvent("Target", getChangeType(e.ChangeType), e.FullPath);

            mm.invokeASAP("ModuleManager.RebuildModules", () => {
                mm.RebuildModules();
                PrintReadyMessage();
            });
        }

        private void OnTargetRenamed(object source, RenamedEventArgs e)
        {
            if(!CheckIsFileReallyChanged(e.FullPath) || !mm.IsTargetChangedOutside() || !e.Name.EndsWith(pm.ProjectPackage.Target)) { return; }

            PrintWatcherEvent("Target", "renamed", e.OldFullPath, e.FullPath);

            mm.invokeASAP("ModuleManager.RebuildModules", () => {
                mm.RebuildModules(() => {
                    PrintReadyMessage();
                });
            });
        }

        private void OnSrcChanged(object source, FileSystemEventArgs e)
        {
            if(!CheckIsFileReallyChanged(e.FullPath)) { return; }
            PrintWatcherEvent("Source", getChangeType(e.ChangeType), e.FullPath);

            mm.invokeASAP("ModuleManager.RebuildModules", () => {
                mm.RebuildModules(() => {
                    PrintReadyMessage();
                });
            });
        }

        private void OnSrcRenamed(object source, RenamedEventArgs e)
        {
            if(!CheckIsFileReallyChanged(e.FullPath)) { return; }
            PrintWatcherEvent("Source", getChangeType(e.ChangeType), e.FullPath);

            mm.invokeASAP("ModuleManager.RebuildModules", () => {
                mm.RebuildModules(() => {
                    PrintReadyMessage();
                });
            });
        }

        private void OnPackageChanged(object source, FileSystemEventArgs e)
        {
            if(e.Name.EndsWith(pm.ProjectPackageName)) {
                if(!CheckIsFileReallyChanged(e.FullPath)) { return; }
                PrintWatcherEvent("Package config", getChangeType(e.ChangeType), e.Name);
                if(VerboseLog) Console.WriteLine("-- Package changed: " + e.Name);

                pm.invokeASAP("PackageManager.RefreshPackages", () => {
                    pm.RefreshPackages(false);
                    mm.RebuildModules(() => {
                        PrintReadyMessage();
                    });
                });
            }
        }

        private void OnPackageRenamed(object source, RenamedEventArgs e)
        {
            if(e.Name.EndsWith(pm.ProjectPackageName)) {
                if(!CheckIsFileReallyChanged(e.FullPath)) { return; }
                PrintWatcherEvent("Package config", "renamed", e.OldName, e.Name);

                pm.invokeASAP("PackageManager.RefreshPackages", () => {
                    pm.RefreshPackages(false);
                    mm.RebuildModules();
                });
            }
        }

        private void PrintReadyMessage()
        {
            if(Watchers.Count > 0) {
                var sources = String.Join(',', pm.ProjectPackage.Source.Sources.ToArray());
                var watchExtra = String.Join(',', pm.ProjectPackage.WatchExtra.ToArray());
                Console.WriteLine("");
                Console.WriteLine("Nice! Watching for changes:");

                ConsoleColorChanger.UseSecondary();
                Console.Write("  " + pm.ProjectPackageName);
                ConsoleColorChanger.UsePrimary();
                Console.WriteLine(" -> refresh packages");

                if(sources.Length > 0) {
                    ConsoleColorChanger.UseSecondary();
                    Console.Write("  " + sources);
                    ConsoleColorChanger.UsePrimary();
                    Console.WriteLine(" -> rebuild modules");
                }

                ConsoleColorChanger.UseSecondary();
                Console.Write("  " + pm.ProjectPackage.Target);
                ConsoleColorChanger.UsePrimary();
                Console.WriteLine(" -> rebuild modules");

                if(watchExtra.Length > 0) {
                    ConsoleColorChanger.UseSecondary();
                    Console.Write("  " + watchExtra);
                    ConsoleColorChanger.UsePrimary();
                    Console.WriteLine(" -> rebuild modules");
                }
            }

            Console.WriteLine("");
            ConsoleColorChanger.UseAccent();
            Console.WriteLine("Now you are free to work with your map directory. Press any key to stop.");
            ConsoleColorChanger.UsePrimary();
            Console.WriteLine("");
        }

        private void PrintWatcherEvent(string prefix, string action, string filename = "", string anotherFilename = "")
        {
            if(prefix.Length > 0) Console.Write("  "+prefix+" ");
            ConsoleColorChanger.UseAccent();
            Console.Write(action+" ");
            ConsoleColorChanger.UsePrimary();
            if(filename.Length > 0) {
                ConsoleColorChanger.UseSecondary();
                Console.Write(filename+" ");
                ConsoleColorChanger.UsePrimary();
            }
            if(anotherFilename.Length > 0) {
                Console.Write(" -> ");
                ConsoleColorChanger.UseSecondary();
                Console.Write(anotherFilename+" ");
                ConsoleColorChanger.UsePrimary();
            }
            Console.WriteLine("");
        }

        private string getChangeType(WatcherChangeTypes t) 
        {
            switch(t) {
                case WatcherChangeTypes.Created:
                    return "created";
                case WatcherChangeTypes.Deleted:
                    return "deleted";
                case WatcherChangeTypes.Changed:
                    return "changed";
                case WatcherChangeTypes.Renamed:
                    return "renamed";
                case WatcherChangeTypes.All:
                    return "changed";
            }
            return "(unknown change)";
        }

        private bool CheckIsFileReallyChanged(string fileFullPath)
        {
            bool isChanged = false;
            DateTime changedAt = File.GetLastAccessTimeUtc(fileFullPath);
            if(WatcherChangedFilesCache.ContainsKey(fileFullPath)) {
                isChanged = WatcherChangedFilesCache[fileFullPath] < changedAt;
                WatcherChangedFilesCache.Remove(fileFullPath);
            }
            WatcherChangedFilesCache.Add(fileFullPath, changedAt);
            return isChanged;
        }
        
    }
}