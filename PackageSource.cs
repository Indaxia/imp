using System.Collections.Generic;

namespace imp {

    public class PackageSource 
    {
        public List<string> Sources { get; set; }
        public string RemoteSources { get; set; }
        public string CustomExtensions { get; set; }
        public string Language { get; set; }
        public string EntryPoint { get; set; }
    }
}