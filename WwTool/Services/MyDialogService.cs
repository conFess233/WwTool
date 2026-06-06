using Prism.Dialogs;
using Prism.Ioc;
using System;
using System.Windows;
using WwTool.Services.Interfaces;

namespace WwTool.Services
{
    /// <summary>
    /// 自定义应用内弹窗服务，替换 Prism 默认的 Window 弹窗机制
    /// </summary>
    public class MyDialogService : IDialogService
    {
        private readonly IContainerExtension _container;
        private readonly IUIStateService _uiStateService;

        public MyDialogService(IContainerExtension container, IUIStateService uiStateService)
        {
            _container = container;
            _uiStateService = uiStateService;
        }

        public void Show(string name, IDialogParameters parameters, DialogCallback callback)
        {
            ShowDialogInternal(name, parameters, callback);
        }

        public void ShowDialog(string name, IDialogParameters parameters, DialogCallback callback)
        {
            ShowDialogInternal(name, parameters, callback);
        }

        private void ShowDialogInternal(string name, IDialogParameters parameters, DialogCallback callback)
        {
            // 确保在 UI 线程执行
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // 从 Prism 容器中解析 View
                    var view = _container.Resolve<object>(name) as FrameworkElement;
                    if (view == null)
                    {
                        throw new InvalidOperationException($"无法解析弹窗视图: '{name}'");
                    }

                    // 如果 DataContext 为空，调用 ViewModelLocator 进行自动绑定
                    if (view.DataContext == null)
                    {
                        Prism.Mvvm.ViewModelLocator.SetAutoWireViewModel(view, true);
                    }

                    var viewModel = view.DataContext as IDialogAware;
                    if (viewModel == null)
                    {
                        throw new InvalidOperationException($"弹窗 '{name}' 的 ViewModel 必须实现 IDialogAware 接口。");
                    }

                    // 使用 Prism 的 DialogUtilities 初始化 ViewModel 的 RequestClose 监听
                    DialogUtilities.InitializeListener(viewModel, (result) =>
                    {
                        // 确保在 UI 线程执行关闭逻辑
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            // 触发 ViewModel 的 OnDialogClosed
                            viewModel.OnDialogClosed();

                            // 隐藏应用内弹窗
                            _uiStateService.CloseDialog();

                            // 触发最初的调用方回调
                            callback.Invoke(result ?? new DialogResult());
                        });
                    });

                    // 初始化 Dialog
                    viewModel.OnDialogOpened(parameters ?? new DialogParameters());

                    // 显示应用内弹窗视图
                    _uiStateService.ShowDialog(view);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"显示应用内弹窗 '{name}' 时出错: {ex}");
                    throw;
                }
            });
        }
    }
}
