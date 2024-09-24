using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace imp.ModuleManager {
    public class AngelscriptModuleManager: AbstractModuleManager
    {
        public AngelscriptModuleManager(PackageManager _pm, bool verboseLog, string appVersion)
        : base(_pm, verboseLog, appVersion)
        {
        }

        override public string GetModuleManagerPackageURL()
        {
            return "";
        }

        override public string GetSourceExtensions()
        {
            return "*.as";
        }

        override public string GetCommentFormatStart()
        {
            return "//";
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

            if(File.Exists(targetFilename)) {
                for (int i=1; i <= 30; ++i) {
                    try {
                        targetOriginal = File.ReadAllText(targetFilename);
                        break;
                    } catch (IOException) when (i <= 30) { 
                        Console.Write(".");
                        Thread.Sleep(200);
                    }
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
                    targetTop += "\n" + code;
                } else {
                    targetBottom += "\n" + code;
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

          string result = "\n\n";

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
              var shortPath = file.Substring(pm.ProjectDirectory.Length+1).Replace('\\', '/');

              for (int i=1; i <= 30; ++i) {
                  try {
                      result += "#include \"" + shortPath + "\"\n";
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
                if(VerboseLog) Console.WriteLine("-- Generating code for source: "+pm.GetDependencyDir(dep));
                if(dep.EntryPoint.Length == 0) {
                    ConsoleColorChanger.UseWarning();
                    Console.WriteLine(" -- Dependency has no entryPoint, skipping ("+dep.Resource+")");
                    ConsoleColorChanger.UsePrimary();
                    return "";
                }
                string filename = Path.Combine(pm.GetDependencyDir(dep, false), dep.EntryPoint);

                if(VerboseLog) Console.WriteLine("-- Loading code from: "+filename);
                result += "\n#include \"" + filename.Replace('\\', '/') + "\"";
            } else if(dep.Type == DependencyType.File) {
                result += File.ReadAllText(pm.GetDependencyFile(dep));
            }

            return result;
        }
    }
}