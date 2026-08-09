using System.ComponentModel;
using Microsoft.Extensions.Logging;
using DS.Tools.Module.Base.Interfaces;
using DS.Tools.Module.Git.Models;
using DS.Tools.Module.Git.Services;

namespace DS.Tools.Module.Git.ViewModels;

/// <summary>
/// Git 日志工具 ViewModel
/// 使用 CommunityToolkit.Mvvm 源生成器，AOT 兼容，无反射调用
/// </summary>
public sealed partial class GitLogViewModel : ToolViewModelBase, ISubTool
{
    // 子工具元数据（ISubTool 静态抽象接口实现）：经 ToolRegistration.AddSubTool<T, TView>() 编译期读取注册
    static string ISubTool.ModuleId => GitModule.ToolIds.Module;
    static string ISubTool.Id => GitModule.ToolIds.Log;
    static string ISubTool.Name => "Git 日志";
    static string ISubTool.Icon => "📜";

    private readonly IGitLogService _gitLogService;
    private readonly IGitSettingsService _settingsService;
    private readonly IFolderPickerService _folderPickerService;
    private readonly IClipboardService _clipboardService;
    private readonly ILogger<GitLogViewModel> _logger;

    /// <summary>
    /// 构造函数 —— 通过 DI 注入服务；启动时恢复上次选择的文件夹并自动加载
    /// </summary>
    public GitLogViewModel(
        IGitLogService gitLogService,
        IGitSettingsService settingsService,
        IFolderPickerService folderPickerService,
        IClipboardService clipboardService,
        ILogger<GitLogViewModel> logger)
    {
        _gitLogService = gitLogService ?? throw new ArgumentNullException(nameof(gitLogService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        _folderPickerService = folderPickerService ?? throw new ArgumentNullException(nameof(folderPickerService));
        _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        DisplayName = "Git 日志";

        // 默认时间范围：本周一至本周日（自动加载/首次查询默认只看本周提交）
        SetDefaultDateRange();

        // 启动时恢复上次选择的文件夹并自动加载（fire-and-forget，全程 try/catch 无未观察异常）
        var saved = _settingsService.Load();
        if (!string.IsNullOrWhiteSpace(saved.LastFolderPath))
        {
            RepositoryPath = saved.LastFolderPath;
            _ = RefreshRepoStateAsync();
        }
    }

    /// <summary>
    /// 仓库路径（可手动编辑，或经文件夹选择器）
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoadLogCommand))]
    private string? _repositoryPath;

    /// <summary>
    /// 当前分支名称
    /// </summary>
    [ObservableProperty]
    private string? _branchName;

    /// <summary>
    /// 起始日期（null = 不限）
    /// </summary>
    [ObservableProperty]
    private DateTime? _sinceDate;

    /// <summary>
    /// 结束日期（null = 不限）
    /// </summary>
    [ObservableProperty]
    private DateTime? _untilDate;

    /// <summary>
    /// 是否已有日志结果
    /// </summary>
    [ObservableProperty]
    private bool _hasLog;

    /// <summary>
    /// 日志条目数（与 LogEntries 同步维护）
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyLogCommand))]
    private int _logCount;

    /// <summary>
    /// 提交日志条目（整批替换，避免逐条 Add 触发 1000 次 CollectionChanged）
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<GitLogEntry> _logEntries = [];

    /// <summary>
    /// 是否为空状态（无错误、非加载中、无日志条目——显示占位提示）
    /// </summary>
    public bool IsEmptyState => !HasErrors && !IsLoading && LogEntries.Count == 0;

    /// <summary>
    /// 属性变化时联动刷新空状态（含基类 IsLoading/HasErrors/ErrorMessage）
    /// </summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName is nameof(IsLoading) or nameof(HasErrors) or nameof(HasLog) or nameof(LogCount) or nameof(LogEntries))
        {
            OnPropertyChanged(nameof(IsEmptyState));
        }
    }

    /// <summary>
    /// 选择文件夹命令：打开系统对话框 → 保存设置 → 加载仓库状态与日志
    /// </summary>
    [RelayCommand]
    private async Task PickFolderAsync()
    {
        var path = await _folderPickerService.PickFolderAsync(RepositoryPath, "选择 Git 仓库文件夹");
        if (string.IsNullOrWhiteSpace(path))
            return; // 用户取消

        RepositoryPath = path;
        _settingsService.Save(new GitSettings { LastFolderPath = path });
        await RefreshRepoStateAsync();
    }

    /// <summary>
    /// 获取日志命令：按当前起止日期重新拉取日志（返回 Task + CancellationToken 参数，自动生成可取消异步命令）
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanLoadLog))]
    private async Task LoadLogAsync(CancellationToken token)
    {
        if (IsLoading)
            return; // 防重入

        await LoadLogCoreAsync(token);
    }

    private bool CanLoadLog() => !string.IsNullOrWhiteSpace(RepositoryPath);

    /// <summary>
    /// 复制全部日志到剪贴板命令
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCopyLog))]
    private Task CopyLogAsync()
        => CopyToClipboardAsync(_clipboardService, BuildCopyText(), $"✓ 已复制 {LogCount} 条日志到剪贴板");

    private bool CanCopyLog() => LogCount > 0;

    /// <summary>
    /// 生成剪贴板文本：每条日志一行（hash | 作者 | 日期 | 主题）
    /// </summary>
    private string BuildCopyText()
        => string.Join(Environment.NewLine, LogEntries.Select(e =>
            $"{e.Hash} | {e.AuthorName} | {e.Date:yyyy-MM-dd HH:mm} | {e.Subject}"));

    /// <summary>
    /// 加载仓库状态：校验仓库 → 获取分支 → 拉取日志
    /// </summary>
    private async Task RefreshRepoStateAsync()
    {
        if (string.IsNullOrWhiteSpace(RepositoryPath))
            return;

        IsLoading = true;
        try
        {
            if (!await _gitLogService.IsGitRepositoryAsync(RepositoryPath))
            {
                BranchName = null;
                ShowError("所选文件夹不是 Git 仓库");
                return;
            }

            BranchName = await _gitLogService.GetCurrentBranchAsync(RepositoryPath);
            await LoadLogCoreAsync(CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "加载仓库状态失败（{RepoPath}）", RepositoryPath);
            ShowError($"加载失败: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 拉取日志（起止日期经本地时区偏移转换为 DateTimeOffset）
    /// </summary>
    private async Task LoadLogCoreAsync(CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(RepositoryPath))
            return;

        DateTimeOffset? since = SinceDate is { } s ? ToLocalOffset(s) : null;
        // 结束日期按"含当天"处理：次日零点作为 git --until 的排他边界，否则当天（如周日）提交会被排除
        DateTimeOffset? until = UntilDate is { } u ? ToLocalOffset(u.AddDays(1)) : null;

        IsLoading = true;
        try
        {
            var result = await _gitLogService.GetLogAsync(RepositoryPath, since, until, token);

            if (result.IsSuccess)
            {
                // 整批替换（避免逐条 Add 触发 N 次 CollectionChanged）
                LogEntries = result.Entries;
                HasErrors = false;
                ErrorMessage = null;
                HasLog = true;
                LogCount = result.Entries.Count;
                StatusMessage = $"✓ 共 {result.Entries.Count} 条提交" +
                    (result.Entries.Count >= GitLogService.MaxEntries ? "（已达上限 1000 条）" : string.Empty);
            }
            else
            {
                ShowError(result.ErrorMessage ?? "获取日志失败");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "获取 Git 日志失败（{RepoPath}）", RepositoryPath);
            ShowError($"获取日志异常: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>
    /// 本地时间转 DateTimeOffset（使用本地时区偏移）
    /// </summary>
    private static DateTimeOffset ToLocalOffset(DateTime value)
        => new(value, TimeZoneInfo.Local.GetUtcOffset(value));

    /// <summary>
    /// 设置默认时间范围：本周一至本周日
    /// </summary>
    private void SetDefaultDateRange()
    {
        var today = DateTime.Today;
        // DayOfWeek：周日=0、周一=1 … 周六=6 → 到本周一的偏移 = (dayOfWeek + 6) % 7
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        SinceDate = monday;
        UntilDate = monday.AddDays(6);
    }

    /// <summary>
    /// 显示错误（基类三段式基础上追加：清空日志结果）
    /// </summary>
    protected override void ShowError(string message)
    {
        base.ShowError(message);
        HasLog = false;
        LogCount = 0;
        LogEntries = [];
    }
}
