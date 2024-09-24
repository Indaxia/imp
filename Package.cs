using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace imp
{
    public class Package
    {
        public string Title { get; set; }
        public string Author { get; set; }  
        public string License { get; set; }
        public List<PackageDependency> Dependencies { get; set; }
        public PackageSource Source { get; set; }
        public string Target { get; set; }
        public List<string> WatchExtra { get; set; }
        public string BeforeBuild { get; set; }
        public string AfterBuild { get; set; }
        public List<string> AllowHosts { get; set; }
        
        public Package(string targetFileName, string sourceDir, string remoteSourcesDir)
        {
            initDefaults(targetFileName, sourceDir, remoteSourcesDir);
        } 

        public Package(string jsonStr)   
        {
            initDefaults();
            fromJson(jsonStr);
        }

        private void initDefaults(string targetFileName = "", string sourceDir = "", string remoteSourcesDir = "")
        {
            Title = "Just another IMP project";
            Author = System.Environment.UserName;
            License = "";
            Dependencies = new List<PackageDependency>();
            Target = targetFileName;
            WatchExtra = new List<string>();
            AfterBuild = "";
            BeforeBuild = "";
            Source = new PackageSource();
            Source.RemoteSources = remoteSourcesDir;
            Source.EntryPoint = "";
            Source.Language = "";
            Source.CustomExtensions = "";
            Source.Sources = new List<string>();
            if(sourceDir.Length > 0) {
                Source.Sources.Add(sourceDir);
            }
            AllowHosts = new List<string>();
        }

        private void fromJson(string jsonStr)
        {
            JObject json = JObject.Parse(jsonStr);

            if(json["language"] != null) {
                Source.Language = (string)json["language"];
            }
            Title = json["title"] == null ? "" : (string)json["title"];
            Author = json["author"] == null ? "" : (string)json["author"];
            License = json["license"] == null ? "" : (string)json["license"];
            Target = json["target"] == null ? "" : (string)json["target"];
            WatchExtra.Clear();
            if(json["watchExtra"] != null && json["watchExtra"].Type == JTokenType.Array) {
                foreach(string extra in json["watchExtra"]) {
                    WatchExtra.Add(extra);
                }
            }
            BeforeBuild = json["beforeBuild"] == null ? "" : (string)json["beforeBuild"];
            AfterBuild = json["afterBuild"] == null ? "" : (string)json["afterBuild"];
            if(json["sourceExtensions"] != null) {
                Source.CustomExtensions = (string)json["sourceExtensions"];
            }
            if(json["entryPoint"] != null) {
                Source.EntryPoint = (string)json["entryPoint"];
            }
            if(json["remoteSources"] != null) {
                Source.RemoteSources = (string)json["remoteSources"];
            }
            Dependencies.Clear();
            if(json["dependencies"] != null) {
                if(json["dependencies"].Type != JTokenType.Object) {
                    throw new PackageException("Cannot parse \"" + Title + "\" package. The value of the property 'dependencies' must be an object, if exists");
                }
                foreach(JProperty d in json["dependencies"]) {
                    Dependencies.Add(new PackageDependency(d, Title));
                }
            }
            AllowHosts.Clear();
            if(json["allowHosts"] != null) {
                if(json["allowHosts"].Type != JTokenType.Array) {
                    throw new PackageException("Cannot parse \"" + Title + "\" package. The value of the property 'allowHosts' must be an array, if exists");
                }
                foreach(string h in json["allowHosts"]) {
                    AllowHosts.Add(h);
                }
            }
            Source.Sources.Clear();
            if(json["sources"] != null && json["sources"].Type == JTokenType.Array) {
                foreach(string src in json["sources"]) {
                    Source.Sources.Add(src);
                }
            }
        }

        public JObject ToJson()
        {
            var result = new JObject();
            result.Add(new JProperty("language", GetLanguage()));
            if(Title.Length > 0) {
                result.Add(new JProperty("title", Title));
            }
            if(Author.Length > 0) {
                result.Add(new JProperty("author", Author));
            }
            if(License.Length > 0) {
                result.Add(new JProperty("license", License));
            }
            var deps = new JObject();
            foreach(PackageDependency dep in Dependencies) {
                deps.Add(dep.ToJson());
            }
            result.Add(new JProperty("dependencies", deps));
            if(Source.Sources.Count > 0) {
                result.Add(new JProperty("sources", new JArray(Source.Sources.ToArray())));
            }
            if(Source.RemoteSources.Length > 0) {
                result.Add(new JProperty("remoteSources", Source.RemoteSources));
            }
            if(Target.Length > 0) {
                result.Add(new JProperty("target", Target));
            }
            if(WatchExtra.Count > 0) {
                result.Add(new JProperty("watchExtra", new JArray(WatchExtra.ToArray())));
            }
            if(BeforeBuild.Length > 0) {
                result.Add(new JProperty("beforeBuild", BeforeBuild));
            }
            if(AfterBuild.Length > 0) {
                result.Add(new JProperty("afterBuild", AfterBuild));
            }
            if(Source.CustomExtensions.Length > 0) {
                result.Add(new JProperty("sourceExtensions", Source.CustomExtensions));
            }
            if(Source.EntryPoint.Length > 0) {
                result.Add(new JProperty("entryPoint", Source.EntryPoint));
            }
            if(AllowHosts.Count > 0) {
                result.Add(new JProperty("allowHosts", AllowHosts.ToArray()));
            }
            return result;
        }

        public string GetLanguage()
        {
            if(Source.Language.Length == 0) {
                string ext = Path.GetExtension(Target);
                if(ext == null || ext.Length == 0) {
                    return "";
                }
                ext = ext.Substring(1);

                switch(ext) {
                    case "as":
                        return "angelscript";
                    case "lua":
                        return "lua";
                }
                return ext;
            }
            return Source.Language;
        }

        public static string getDefaultConfiguration(string targetFileName, string sourceDir, string remoteSourcesDir)
        {
            return new Package(targetFileName, sourceDir, remoteSourcesDir).ToJson().ToString();
        }
    }
}