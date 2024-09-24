using System;
using System.Diagnostics;
using System.IO;

namespace imp.ModuleManager
{
    public abstract class AbstractModuleManager: DelayedBusyState
    {
        public delegate void executionCallback();

        protected DateTime targetLastChange;

        protected bool VerboseLog = false;
        protected PackageManager pm;
        protected string ClientScriptStart = "imp-built-begin";
        protected string ClientScriptEnd = "imp-built-end\n";
        protected string AppVersion;

        public AbstractModuleManager(PackageManager _pm, bool verboseLog, string appVersion)
        {
            pm = _pm;
            VerboseLog = verboseLog;
            AppVersion = appVersion;
            Clear();
        }

        abstract public string GetSourceExtensions();
        abstract public string GetCommentFormatStart();
        abstract public string GetCommentFormatEnd();
        abstract public string GetModuleManagerPackageURL();
        abstract protected void RebuildLanguageModules();

        public void RebuildModules(executionCallback onSuccess = null)
        {
            pm.invokeASAP("ModuleManager.RebuildModules", () => {
              RebuildLanguageModules();
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

        protected void ExecuteCommand(string command)
        {
            if(command.Length == 0) {
                return;
            }
            var commandLine = command.Trim().Split(" ", 2);
            var arguments = (commandLine.Length > 1) ? commandLine[1] : "";
            arguments = arguments.Replace("%target%", Path.Combine(pm.ProjectDirectory, pm.ProjectPackage.Target));
            if(commandLine.Length > 0) {
                Console.WriteLine("");
                Console.Write("  Executing: ");
                ConsoleColorChanger.UseSecondary();
                Console.WriteLine(commandLine[0] + " " + arguments);
                ConsoleColorChanger.UsePrimary();

                Process cmd = new Process();
                cmd.StartInfo.FileName = commandLine[0];
                cmd.StartInfo.Arguments = arguments;
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

        protected string RemoveBetween(string source, string start, string end)
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