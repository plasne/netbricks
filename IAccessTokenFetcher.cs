using System.Threading.Tasks;

namespace NetBricks
{

    public interface IAccessTokenFetcher
    {

        Task<string> GetAccessToken(string resourceId, string type = null);

    }

}