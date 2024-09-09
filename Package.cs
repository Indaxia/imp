using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace imp
{
    public class Package
    {
        public const string DefaultCommentFormat = "--";
        public const string DefaultSourceExtensions = "*.lua";
        public const string DefaultModuleManager = "https://github.com/Indaxia/imp-lua-mm";

        public string Title { get; set; }
        public string Author { get; set; }  
        public string License { get; set; }
        public List<PackageDependency> Dependencies { get; set; }
        public List<string> Sources { get; set; }
        public string Target { get; set; }
        public List<string> WatchExtra { get; set; }
        public string BeforeBuild { get; set; }
        public string AfterBuild { get; set; }
        public List<string> AllowHosts { get; set; }
        public string SourceExtensions { get; set; }
        public string SourceCommentFormat { get; set; }
        
        public Package(string targetFileName, string sourceDir)
        {
            initDefaults(targetFileName, sourceDir);
        } 

        public Package(string jsonStr)   
        {
            initDefaults();
            fromJson(jsonStr);
        }

        private void initDefaults(string targetFileName = "", string sourceDir = "")
        {
            Title = "Just another IMP project";
            Author = System.Environment.UserName;
            License = "";
            Dependencies = new List<PackageDependency>();
            Sources = new List<string>();
            if(sourceDir != "") {
                Sources.Add(sourceDir);
            }
            Target = targetFileName;
            WatchExtra = new List<string>();
            AfterBuild = "";
            SourceExtensions = DefaultSourceExtensions;
            SourceCommentFormat = DefaultCommentFormat;
            AllowHosts = new List<string>();
        }

        private void fromJson(string jsonStr)
        {
            JObject json = JObject.Parse(jsonStr);

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
            SourceExtensions = json["sourceExtensions"] == null 
                ? DefaultSourceExtensions 
                : (string)json["sourceExtensions"];
            SourceCommentFormat = json["sourceCommentFormat"] == null 
                ? DefaultCommentFormat
                : (string)json["sourceCommentFormat"];
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
            Sources.Clear();
            if(json["sources"] != null && json["sources"].Type == JTokenType.Array) {
                foreach(string src in json["sources"]) {
                    Sources.Add(src);
                }
            }
        }

        public JObject ToJson()
        {
            var result = new JObject();
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
            if(Sources.Count > 0) {
                result.Add(new JProperty("sources", new JArray(Sources.ToArray())));
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
            if(SourceExtensions.Length > 0) {
                result.Add(new JProperty("sourceExtensions", SourceExtensions));
            }
            if(SourceCommentFormat.Length > 0) {
                result.Add(new JProperty("sourceCommentFormat", SourceCommentFormat));
            }
            if(AllowHosts.Count > 0) {
                result.Add(new JProperty("allowHosts", AllowHosts.ToArray()));
            }
            return result;
        }

        public static string getDefaultConfiguration(string targetFileName, string sourceDir)
        {
            return new Package(targetFileName, sourceDir).ToJson().ToString();
        }
    }
}