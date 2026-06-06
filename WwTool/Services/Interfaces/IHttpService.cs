namespace WwTool.Services.Interfaces
{
    public interface IHttpService
    {
        Task<T?> GetAsync<T>(string url, Dictionary<string, string>? dynamicHeaders = null);
        Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest data, Dictionary<string, string>? dynamicHeaders = null);
        Task<TResponse?> PostFormAsync<TResponse>(string url, Dictionary<string, string> formData, Dictionary<string, string>? dynamicHeaders = null);
    }
}