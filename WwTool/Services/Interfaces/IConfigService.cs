using System.Threading.Tasks;
using WwTool.Common.Models.Config;

namespace WwTool.Services.Interfaces
{
    public interface IConfigService
    {
        AppConfig App { get; }
        ApiConfig Api { get; }
        UserConfig User { get; }

        // 异步
        Task SaveAllAsync();

        // 同步

        void LoadAll();
    }
}