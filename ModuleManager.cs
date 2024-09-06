using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace imp
{
    public class ModuleManager: DelayedBusyState
    {
        public delegate void executionCallback();

        private DateTime targetLastChange;

        private bool VerboseLog = false;
        private PackageManager pm;
        private string ClientScriptStart = "imp-built-begin";
        private string ClientScriptEnd = "imp-built-end\n";
        private string AppVersion;

        public ModuleManager(PackageManager _pm, bool verboseLog, string appVersion)
        {
            pm = _pm;
            VerboseLog = verboseLog;
            AppVersion = appVersion;
            Clear();
        }

        public void RebuildModules(executionCallback onSuccess = null)
        {
            pm.invokeASAP("ModuleManager.RebuildModules", () => {
              _RebuildModules();
              if(onSuccess != null) onSuccess();
            });
        }

        public void Clear()
        {
            targetLastChange = DateTime.UtcNow;
            targetLastChange.AddDays(-1);
        }

        public bool IsTargetChangedOutside()
        {
            string targetFilename = Path.Combine(pm.ProjectDirectory, pm.ProjectPackage.Target);
            DateTime dt = File.GetLastWriteTimeUtc(targetFilename);
            return dt.CompareTo(targetLastChange) != 0;
        }

        private void _RebuildModules()
        {
            isBusy = true;
            
            ConsoleColorChanger.UseAccent();
            Console.WriteLine("Rebuilding modules");
            ConsoleColorChanger.UsePrimary();

            string targetFilename = Path.Combine(pm.ProjectDirectory, pm.ProjectPackage.Target);
            string targetOriginal = "";

            for (int i=1; i <= 30; ++i) {
                try {
                    targetOriginal = File.ReadAllText(targetFilename);
                    break;
                } catch (IOException) when (i <= 30) { Thread.Sleep(200); }
            }


            targetOriginal = RemoveBetween(
              targetOriginal, 
              pm.ProjectPackage.SourceCommentFormat+ClientScriptStart, 
              pm.ProjectPackage.SourceCommentFormat+ClientScriptEnd
            );

            string targetHeader = "";
            string targetTop = ""; 
            string targetBottom = "\n\n";

            targetHeader += "\n\n"+pm.ProjectPackage.SourceCommentFormat+" Indaxia Modules & Packages " + AppVersion;
            targetHeader += "\n"+pm.ProjectPackage.SourceCommentFormat+" Build time: " + DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss zzz");

            foreach(string index in pm.DependenciesOrderIndex) {
                if(! pm.Dependencies.ContainsKey(index)) {
                  throw new ModuleException("Dependencies collection has no index '"+index+"' but it's stored in indexes. Try to run 'imp update'");
                }
                PackageDependency dep = pm.Dependencies[index];
                string code = GetCodeFor(dep);
                if(dep.TopOrder) {
                    targetTop += "\n\n" + code;
                } else {
                    targetBottom += "\n\n" + code;
                }
            }

            targetBottom += GetCodeFor(pm.ProjectPackage.Sources.ToArray());

            string target = pm.ProjectPackage.SourceCommentFormat
              + ClientScriptStart 
              + targetHeader 
              + targetTop 
              + targetBottom 
              + "\n" 
              + pm.ProjectPackage.SourceCommentFormat
              + ClientScriptEnd
              + targetOriginal;

            for (int i=1; i <= 30; ++i) {
                try {
                    File.WriteAllText(targetFilename, target);
                    break;
                } catch (IOException) when (i <= 30) { Thread.Sleep(200); }
            }

            UnsubscribeASAPEvent("ModuleManager.RebuildModules");

            targetLastChange = File.GetLastWriteTimeUtc(targetFilename);
            
            ConsoleColorChanger.UseAccent();
            Console.WriteLine("Built at "+targetLastChange.ToString("yyyy.MM.dd HH:mm:ss zzz"));
            ConsoleColorChanger.UsePrimary();

            ExecuteAfterBuild();

            isBusy = false;
        }

        private void ExecuteAfterBuild()
        {
            if(pm.ProjectPackage.AfterBuild.Length > 0) {
                Console.WriteLine("");
                Console.Write("  Executing ");
                ConsoleColorChanger.UseSecondary();
                Console.WriteLine(pm.ProjectPackage.AfterBuild);
                ConsoleColorChanger.UsePrimary();

                Process cmd = new Process();
                cmd.StartInfo.FileName = "cmd.exe";
                cmd.StartInfo.Arguments = "/C " + pm.ProjectPackage.AfterBuild;
                cmd.StartInfo.RedirectStandardInput = true;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.StartInfo.CreateNoWindow = true;
                cmd.StartInfo.UseShellExecute = false;
                cmd.Start();

                cmd.StandardInput.Flush();
                cmd.StandardInput.Close();
                cmd.WaitForExit();
                ConsoleColorChanger.UseSecondary();
                Console.WriteLine("");
                Console.WriteLine(cmd.StandardOutput.ReadToEnd());
            }
        }

        private string GetCodeFor(string[] dirs)
        {
          if(dirs.Length < 1) { 
            return "";
          }

          string result = "";

          foreach(string dir in dirs) {
            if(dir == "" || dir == "/") {
              throw new ModuleException("It is forbidden to include root directory, check your \"sources\"");
            }
            string dirPath = Path.Combine(pm.ProjectDirectory, dir);
            string[] files = Directory.GetFiles(dirPath, pm.ProjectPackage.SourceExtensions, SearchOption.AllDirectories);
            foreach(string file in files) {
              Console.Write("  Building source ");
              ConsoleColorChanger.UseSecondary();
              Console.WriteLine(file);
              ConsoleColorChanger.UsePrimary();
              var shortName = file.Substring(pm.ProjectDirectory.Length+1);

              for (int i=1; i <= 30; ++i) {
                  try {
                      result += "\n\n" + pm.ProjectPackage.SourceCommentFormat + "imp-dep " + shortName + "\n" + File.ReadAllText(file);
                      break;
                  } catch (IOException) when (i <= 30) { Thread.Sleep(200); }
              }
            }
          }

          return result;
        }

        private string GetCodeFor(PackageDependency dep)
        {
            Console.Write("  Building ");
            ConsoleColorChanger.UseSecondary();
            Console.WriteLine(dep.Resource);
            ConsoleColorChanger.UsePrimary();

            var dirs = new List<string>();
            var resSplit = dep.Resource.Split(new char[] {'/' , '\\'});
            string result = pm.ProjectPackage.SourceCommentFormat+"imp-dep "+resSplit[resSplit.Length-1];

            if(dep.Type == DependencyType.Package) {
                foreach(var src in dep.Sources) {
                    if(VerboseLog) Console.WriteLine("-- Generating code for source: "+src);
                    dirs.Add(pm.GetDependencyDir(dep));
                }
                
                var filenames = new List<string>();
                foreach(var dir in dirs) {
                    filenames.AddRange(Directory.GetFiles(dir, pm.ProjectPackage.SourceExtensions, SearchOption.AllDirectories));
                }

                foreach(var filename in filenames) {
                    if(VerboseLog) Console.WriteLine("-- Loading code from: "+filename);
                    result += "\n\n" + File.ReadAllText(filename);
                }
            } else {
                result += File.ReadAllText(pm.GetDependencyFile(dep));
            }

            return result;
        }

        private string RemoveBetween(string source, string start, string end)
        {
            var starts = source.IndexOf(start);
            if(starts == -1) {
                return source;
            }
            var ends = source.IndexOf(end);
            if(ends == -1) {
                throw new ModuleException("  Cannot clean target file: end tag not found: "+end);
            }
            return source.Substring(0, starts) + source.Substring(ends + end.Length);
        }
    }
}