using System.Threading.Tasks;
using WwTool.Common.Models.Config;

namespace WwTool.Services.Interfaces
{
    public interface IConfigService
    {
        event EventHandler? UserAutoSaveFailed;

        AppConfig App { get; }
        ApiConfig Api { get; }
        UserConfig User { get; }

        // 异步
        Task SaveAllAsync();
        /// <summary>停止自动保存并排空最后一次配置写入。</summary>
        Task FlushAsync() => SaveAllAsync();

        // 同步
        void SaveAll();

        void LoadAll();
    }
}
