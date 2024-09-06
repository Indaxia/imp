using System.Threading.Tasks;

namespace imp.Repository
{
    public interface RepositoryProviderInterface
    {
        bool IdentifyURL(string repositoryUrl);
        string GetZipFileUrl(string repositoryUrl, string version);
    }
}