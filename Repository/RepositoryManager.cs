using System.Collections.Generic;
using imp.Repository.Provider;

namespace imp.Repository
{
    public class RepositoryManager
    {
        private List<RepositoryProviderInterface> providers;

        public RepositoryManager()
        {
            providers = new List<RepositoryProviderInterface>();
            providers.Add(new Github());
            providers.Add(new Bitbucket());
        }

        public RepositoryProviderInterface getProvider(string repositoryUrl)
        {
            foreach(RepositoryProviderInterface provider in providers) {
                if(provider.IdentifyURL(repositoryUrl)) {
                    return provider;
                }
            }
            return null;
        }
    }
}