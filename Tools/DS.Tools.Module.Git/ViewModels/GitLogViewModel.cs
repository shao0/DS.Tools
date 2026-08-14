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
    /// 日志条目数（当前选中仓库的提交数，与 SelectedRepository 同步维护）
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyLogCommand))]
    private int _logCount;

    /// <summary>
    /// 各仓库日志分组（根仓库第一，其余为嵌套子仓库；整批替换）
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<GitRepositoryLog> _repositories = [];

    /// <summary>
    /// 当前选中的仓库（Tab 切换；加载完成后默认选中根仓库）
    /// </summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyLogCommand))]
    private GitRepositoryLog? _selectedRepository;

    /// <summary>
    /// 提交人过滤选项（首项"全部提交人"，其余按提交数降序；当前 git 用户恒在列）
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<AuthorOption> _authorOptions = [AuthorOption.All];

    /// <summary>
    /// 当前选中的提交人过滤（null 名称 = 全部；加载后默认当前 git 用户）
    /// </summary>
    [ObservableProperty]
    private AuthorOption? _selectedAuthorOption = AuthorOption.All;

    /// <summary>服务返回的原始分组（未经提交人过滤——过滤在内存即时完成，切换不重跑 git）</summary>
    private IReadOnlyList<GitRepositoryLog> _allRepositories = [];

    /// <summary>当前 git 用户名（仓库级/全局 user.name），提交人过滤默认值</summary>
    private string? _currentUser;

    /// <summary>用户是否手动切换过提交人过滤（手动后不再自动重置为默认）</summary>
    private bool _authorFilterTouched;

    /// <summary>正在程序化设置默认过滤（不标记 touched）</summary>
    private bool _applyingDefaultAuthor;

    /// <summary>
    /// 是否为空状态（尚未加载任何日志——显示占位提示）
    /// </summary>
    public bool IsEmptyState => !HasLog && !HasErrors && !IsLoading;

    /// <summary>
    /// 是否已加载但当前选中仓库无提交（提示切换其他仓库）
    /// </summary>
    public bool IsNoCommitsState => HasLog && !HasErrors && !IsLoading && LogCount == 0;

    /// <summary>
    /// 提交人过滤切换：按选中提交人重建仓库分组（无匹配提交的仓库隐藏 Tab）
    /// </summary>
    partial void OnSelectedAuthorOptionChanged(AuthorOption? value)
    {
        if (!_applyingDefaultAuthor)
            _authorFilterTouched = true; // 用户手动切换后，重新加载/换仓库前保持其选择

        ApplyAuthorFilter();

        // 过滤切换即时反馈（加载流程随后会用加载摘要覆盖）
        if (!IsLoading && HasLog)
            StatusMessage = BuildSummaryMessage();
    }

    /// <summary>
    /// 按提交人过滤重建 <see cref="Repositories"/>（null = 全部，原样透传）；
    /// 无匹配提交的仓库不显示 Tab；选中 Tab 尽量按显示名保留，不可用则回退第一个
    /// </summary>
    private void ApplyAuthorFilter()
    {
        var author = SelectedAuthorOption?.Name;
        var filtered = author is null
            ? _allRepositories
            : _allRepositories
                .Select(r => r with { Entries = r.Entries.Where(e => e.AuthorName == author).ToList() })
                .Where(r => r.EntryCount > 0)
                .ToList();

        Repositories = filtered;
        SelectedRepository = filtered.FirstOrDefault(r => r.DisplayName == SelectedRepository?.DisplayName)
            ?? filtered.FirstOrDefault();
    }

    /// <summary>
    /// 依据原始分组重建提交人选项：全部 + 按提交数降序（同数按名字典序）；
    /// 当前 git 用户无范围内提交时仍附加在列（默认过滤需选中它）
    /// </summary>
    private void RebuildAuthorOptions()
    {
        var options = _allRepositories
            .SelectMany(r => r.Entries)
            .GroupBy(e => e.AuthorName)
            .OrderByDescending(g => g.Count())
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new AuthorOption(g.Key, g.Key))
            .ToList();

        if (_currentUser is not null && options.All(o => o.Name != _currentUser))
            options.Add(new AuthorOption(_currentUser, _currentUser));

        AuthorOptions = [AuthorOption.All, .. options];
        // 现选择与重建后的同名项值相等（record 值相等），ComboBox 自动匹配，无需回选
    }

    /// <summary>
    /// 加载摘要：✓ 共 N 条提交（M 个仓库）——N/M 为过滤后实际展示的数量
    /// </summary>
    private string BuildSummaryMessage()
    {
        var repoLabel = Repositories.Count > 1 ? $"（{Repositories.Count} 个仓库）" : string.Empty;
        return $"✓ 共 {Repositories.Sum(r => r.EntryCount)} 条提交{repoLabel}";
    }

    /// <summary>
    /// 选中仓库切换：同步日志条数（复制可执行性/空状态）并更新状态消息
    /// </summary>
    partial void OnSelectedRepositoryChanged(GitRepositoryLog? value)
    {
        LogCount = value?.EntryCount ?? 0;
        var limitSuffix = LogCount >= GitLogService.MaxEntries ? "（已达上限 1000 条）" : string.Empty;
        StatusMessage = value is null
            ? string.Empty
            : $"📂 {value.DisplayName}：{LogCount} 条提交{limitSuffix}";
    }

    /// <summary>
    /// 属性变化时联动刷新空状态（含基类 IsLoading/HasErrors/ErrorMessage）
    /// </summary>
    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName is nameof(IsLoading) or nameof(HasErrors) or nameof(HasLog) or nameof(LogCount)
            or nameof(Repositories) or nameof(SelectedRepository))
        {
            OnPropertyChanged(nameof(IsEmptyState));
            OnPropertyChanged(nameof(IsNoCommitsState));
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
    /// 复制当前选中仓库的日志到剪贴板命令（每条为元数据行 + 完整消息，条目间空行分隔）
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCopyLog))]
    private Task CopyLogAsync()
        => CopyToClipboardAsync(_clipboardService, BuildCopyText(), $"✓ 已复制 {LogCount} 条日志到剪贴板");

    private bool CanCopyLog() => LogCount > 0;

    /// <summary>
    /// 复制单条日志到剪贴板命令（仅该条：元数据行 + 完整消息）
    /// </summary>
    [RelayCommand]
    private Task CopyEntryAsync(GitLogEntry entry)
    {
        // null 参数（如 DataTemplate 内未解析绑定）安全早退，不触达剪贴板
        if (entry is null)
            return Task.CompletedTask;

        return CopyToClipboardAsync(_clipboardService, FormatEntry(entry), "✓ 已复制该条日志到剪贴板");
    }

    /// <summary>
    /// 生成剪贴板文本：每条为元数据行（hash | 作者 | 日期）+
    /// 完整提交消息（%B，含正文与换行），条目间空行分隔（仅当前选中仓库）
    /// </summary>
    private string BuildCopyText()
        => string.Join("\n\n", SelectedRepository?.Entries.Select(FormatEntry) ?? []);

    /// <summary>
    /// 格式化单条日志：元数据行 + 完整消息（消息尾部换行已由服务层去除）
    /// </summary>
    private static string FormatEntry(GitLogEntry e)
        => $"{e.Hash} | {e.AuthorName} | {e.Date:yyyy-MM-dd HH:mm}\n{e.Message}";

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
            // 当前 git 用户（user.name，仓库级/全局）作为提交人过滤默认值；
            // 重置 touched——新仓库恢复"默认过滤为当前用户"，而非沿用上一仓库的手动选择
            _currentUser = await _gitLogService.GetCurrentUserNameAsync(RepositoryPath);
            _authorFilterTouched = false;
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
                // 整批替换（避免逐条 Add 触发 N 次 CollectionChanged）。
                // 原始分组留存 _allRepositories，提交人过滤在内存即时完成（切换不重跑 git）
                _allRepositories = result.Repositories;
                HasErrors = false;
                ErrorMessage = null;
                HasLog = true;
                RebuildAuthorOptions();

                // 用户未手动切换过过滤 → 默认选中当前 git 用户（未配置则"全部"）；
                // 已手动选择 → 沿用其选择
                if (!_authorFilterTouched)
                {
                    _applyingDefaultAuthor = true;
                    try
                    {
                        SelectedAuthorOption = AuthorOptions.FirstOrDefault(o => o.Name == _currentUser)
                            ?? AuthorOption.All;
                    }
                    finally
                    {
                        _applyingDefaultAuthor = false;
                    }
                }

                // 无条件重建展示分组：默认值与当前选择值相等（record 值相等）时
                // OnSelectedAuthorOptionChanged 不触发，ApplyAuthorFilter 必须显式调用（幂等）。
                // 默认选中根仓库由 ApplyAuthorFilter 完成；加载摘要最后设置覆盖过滤切换消息
                ApplyAuthorFilter();
                StatusMessage = BuildSummaryMessage();
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
    /// 显示错误（基类三段式基础上追加：清空日志结果与提交人过滤状态）
    /// </summary>
    protected override void ShowError(string message)
    {
        base.ShowError(message);
        HasLog = false;
        LogCount = 0;
        _allRepositories = [];
        Repositories = [];
        SelectedRepository = null;

        // 过滤状态回到初始（"全部"，不标记 touched——错误后重新加载仍可恢复默认过滤）
        _applyingDefaultAuthor = true;
        try
        {
            AuthorOptions = [AuthorOption.All];
            SelectedAuthorOption = AuthorOption.All;
        }
        finally
        {
            _applyingDefaultAuthor = false;
        }
    }
}
