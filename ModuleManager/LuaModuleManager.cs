using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace imp.ModuleManager {
    public class LuaModuleManager: AbstractModuleManager
    {
        public LuaModuleManager(PackageManager _pm, bool verboseLog, string appVersion)
        : base(_pm, verboseLog, appVersion)
        {
        }

        override public string GetModuleManagerPackageURL()
        {
            return "https://github.com/Indaxia/imp-lua-mm";
        }

        override public string GetSourceExtensions()
        {
            return "*.lua";
        }

        override public string GetCommentFormatStart()
        {
            return "--";
        }

        override public string GetCommentFormatEnd()
        {
            return "";
        }

        override protected void RebuildLanguageModules()
        {
            isBusy = true;
            
            ConsoleColorChanger.UseAccent();
            Console.WriteLine("Rebuilding modules");
            ConsoleColorChanger.UsePrimary();

            string targetFilename = Path.Combine(pm.ProjectDirectory, pm.ProjectPackage.Target);
            string targetOriginal = "";

            ExecuteCommand(pm.ProjectPackage.BeforeBuild);

            for (int i=1; i <= 30; ++i) {
                try {
                    targetOriginal = File.ReadAllText(targetFilename);
                    break;
                } catch (IOException) when (i <= 30) { 
                    Console.Write(".");
                    Thread.Sleep(200);
                }
            }


            targetOriginal = RemoveBetween(
              targetOriginal, 
              GetCommentFormatStart()+ClientScriptStart+GetCommentFormatEnd(), 
              GetCommentFormatStart()+ClientScriptEnd+GetCommentFormatEnd()
            );

            string targetHeader = "";
            string targetTop = ""; 
            string targetBottom = "\n\n";

            targetHeader += "\n\n"+GetCommentFormatStart()+" Indaxia Modules & Packages " + AppVersion+GetCommentFormatEnd();
            targetHeader += "\n"+GetCommentFormatStart()+" Build time: " + DateTime.Now.ToString("yyyy.MM.dd HH:mm:ss zzz")+GetCommentFormatEnd();

            foreach(string index in pm.DependenciesOrderIndex) {
                if(! pm.Dependencies.ContainsKey(index)) {
                  throw new ModuleException("Dependencies collection has no index '"+index+"' but it's stored in indexes. Try to run 'imp update'");
                }
                PackageDependency dep = pm.Dependencies[index];
                string code = BuildSourcesFor(dep);
                if(dep.TopOrder) {
                    targetTop += "\n\n" + code;
                } else {
                    targetBottom += "\n\n" + code;
                }
            }

            targetBottom += BuildSourcesFor(pm.ProjectPackage.Source.Sources.ToArray());

            string target = GetCommentFormatStart()
              + ClientScriptStart 
              + GetCommentFormatEnd()
              + targetHeader 
              + targetTop 
              + targetBottom 
              + "\n" 
              + GetCommentFormatStart()
              + ClientScriptEnd
              + GetCommentFormatEnd()
              + targetOriginal;

            for (int i=1; i <= 30; ++i) {
                try {
                    File.WriteAllText(targetFilename, target);
                    break;
                } catch (IOException) when (i <= 30) { 
                    Console.Write(".");
                    Thread.Sleep(200);
                }
            }

            UnsubscribeASAPEvent("ModuleManager.RebuildModules");

            targetLastChange = File.GetLastWriteTimeUtc(targetFilename);
            
            ConsoleColorChanger.UseAccent();
            Console.WriteLine("Built at "+targetLastChange.ToString("yyyy.MM.dd HH:mm:ss zzz"));
            ConsoleColorChanger.UsePrimary();

            ExecuteCommand(pm.ProjectPackage.AfterBuild);

            isBusy = false;
        }

        protected string BuildSourcesFor(string[] dirs)
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
            string filter = (pm.ProjectPackage.Source.CustomExtensions == "")
                ? GetSourceExtensions() 
                : pm.ProjectPackage.Source.CustomExtensions;
            
            string[] files = Directory.GetFiles(dirPath, filter, SearchOption.AllDirectories);
            foreach(string file in files) {
              Console.Write("  Building source ");
              ConsoleColorChanger.UseSecondary();
              Console.WriteLine(file);
              ConsoleColorChanger.UsePrimary();
              var shortPath = file.Substring(pm.ProjectDirectory.Length+1);

              for (int i=1; i <= 30; ++i) {
                  try {
                      result += "\n\n" + GetCommentFormatStart() + "imp-dep " + shortPath + GetCommentFormatEnd() + "\n" + File.ReadAllText(file);
                      break;
                  } catch (IOException) when (i <= 30) { 
                    Console.Write(".");
                    Thread.Sleep(200);
                }
              }
            }
          }

          return result;
        }

        protected string BuildSourcesFor(PackageDependency dep)
        {
            Console.Write("  Building ");
            ConsoleColorChanger.UseSecondary();
            Console.WriteLine(dep.Resource);
            ConsoleColorChanger.UsePrimary();

            var dirs = new List<string>();
            var resSplit = dep.Resource.Split(new char[] {'/' , '\\'});
            string result = GetCommentFormatStart() + "imp-dep " + resSplit[resSplit.Length-1] + GetCommentFormatEnd();

            if(dep.Type == DependencyType.Package) {
                foreach(var src in dep.Sources) {
                    if(VerboseLog) Console.WriteLine("-- Generating code for source: "+src);
                    dirs.Add(Path.Combine(pm.GetDependencyDir(dep), src));
                }
                
                var filenames = new List<string>();
                string filter = (pm.ProjectPackage.Source.CustomExtensions == "")
                    ? GetSourceExtensions() 
                    : pm.ProjectPackage.Source.CustomExtensions;
                    
                foreach(var dir in dirs) {
                    filenames.AddRange(Directory.GetFiles(dir, filter, SearchOption.AllDirectories));
                }

                foreach(var filename in filenames) {
                    if(VerboseLog) Console.WriteLine("-- Loading code from: "+filename);
                    result += "\n\n" + File.ReadAllText(filename);
                }
            } else if(dep.Type == DependencyType.File) {
                result += File.ReadAllText(pm.GetDependencyFile(dep));
            }

            return result;
        }
    }
}