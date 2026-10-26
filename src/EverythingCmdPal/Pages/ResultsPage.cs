using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EverythingCmdPal.Actions;
using EverythingCmdPal.Items;
using EverythingCmdPal.Services;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace EverythingCmdPal.Pages;

/// <summary>
/// UI adapter for the search: owns the item list shown to the host and schedules
/// queries on a background consumer. Pagination state lives in <see cref="SearchSession"/>;
/// details metadata lives in <see cref="ResultMetadata"/>.
/// </summary>
internal sealed partial class ResultsPage : DynamicListPage, IDisposable
{
    private readonly object _itemsLock = new();
    private readonly List<IListItem> _items = [];

    /// <summary>一次搜索请求：查询词 + 排序。排序切换也走这条队列，保证搜索串行。</summary>
    private readonly record struct SearchRequest(string Query, SortOrder.SortSpec Sort);

    private readonly Channel<SearchRequest> _queries = Channel.CreateBounded<SearchRequest>(
        new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

    private readonly SettingsManager _settings;
    private readonly EverythingClient _client;
    private readonly SearchSession _session;
    private readonly CancellationTokenSource _cts = new();
    private readonly CommandItem _empty;
    private readonly int _pageSize;
    private readonly int _previewWidth;
    private readonly ContentSize _detailsSize;

    private Task? _consumer;
    private bool _deferred;
    private int _loadingMore;
    private string _initialQuery = string.Empty;
    private ResultsPage? _returnPage;
    private ResultsPage? _togglePage;
    private readonly ResultContext _context;
    private SortOrder.SortSpec _sort;

    public ResultsPage(SettingsManager settings, string? initialQuery = null, bool? showDetails = null, ResultsPage? returnPage = null)
    {
        Id = "com.everything.cmdpal.results";
        Title = "Everything";
        Name = showDetails == false ? "隐藏详情" : "详细信息";
        Icon = Icons.App;
        PlaceholderText = "输入文件名、通配符或 Everything 语法（ext:psd dm:thisweek）";
        ShowDetails = showDetails ?? settings.ShowDetails;

        _settings = settings;
        _pageSize = settings.PageSize;
        _previewWidth = settings.PreviewWidth;
        _detailsSize = settings.DetailsSize;
        _sort = settings.Sort;
        _returnPage = returnPage;
        _client = new EverythingClient(settings.InstanceName);
        _session = new SearchSession(_client, _pageSize);

        // 顶部过滤条：名称 / 路径 / 大小 / 修改时间 × 升降序，切换即重搜并写回设置。
        Filters = new SortFilters(_sort, OnSortChanged);

        _empty = new CommandItem(new NoOpCommand())
        {
            Title = "输入关键词开始搜索",
            Subtitle = "需要 Everything 1.5 正在运行",
        };
        EmptyContent = _empty;

        if (!string.IsNullOrWhiteSpace(initialQuery))
        {
            _initialQuery = initialQuery;
            _deferred = true;
        }

        _context = new ResultContext
        {
            PreviewWidth = _previewWidth,
            DetailsSize = _detailsSize,
            ToggleItem = GetToggleItem(),
            QueryProvider = () => _session.Query,
            EverythingExe = EverythingLocator.FindExe(),
            InstanceName = settings.InstanceName,
        };

        HostLog.Info($"page created (pageSize={_pageSize}, details={ShowDetails}, previewWidth={_previewWidth})");
    }

    /// <summary>Sets the query this page will run when it is opened (used by the toggle).</summary>
    internal void SetPendingQuery(string query)
    {
        _initialQuery = query ?? string.Empty;
        _deferred = !string.IsNullOrWhiteSpace(_initialQuery);
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        EnsureConsumer();
        _queries.Writer.TryWrite(new SearchRequest(newSearch ?? string.Empty, _sort));
    }

    public override IListItem[] GetItems()
    {
        if (_deferred)
        {
            _deferred = false;
            HostLog.Info($"running deferred query '{_initialQuery}'");
            EnsureConsumer();
            _queries.Writer.TryWrite(new SearchRequest(_initialQuery, _sort));
        }

        lock (_itemsLock)
        {
            return [.. _items];
        }
    }

    public override void LoadMore()
    {
        if (IsLoading) return;

        lock (_itemsLock)
        {
            if (!_session.HasMore) return;
        }

        if (Interlocked.Exchange(ref _loadingMore, 1) == 1) return;

        try
        {
            var hits = _session.LoadNext();
            lock (_itemsLock)
            {
                foreach (var hit in hits)
                {
                    _items.Add(new ResultItem(hit, _context));
                }
            }

            HasMoreItems = _session.HasMore;
        }
        catch (Exception ex)
        {
            HostLog.Error($"load more failed: {ex}");
            HasMoreItems = false;
        }
        finally
        {
            Interlocked.Exchange(ref _loadingMore, 0);

            int count;
            lock (_itemsLock)
            {
                count = _items.Count;
            }

            RaiseItemsChanged(count);
        }
    }

    /// <summary>
    /// 过滤条切换排序：写回设置，并对当前查询从 offset 0 重搜（换排序必须重置分页）。
    /// </summary>
    private void OnSortChanged(SortOrder.SortSpec sort)
    {
        if (sort == _sort) return;

        _sort = sort;
        _settings.SetSort(sort.Id);

        var query = _session.Query;
        if (string.IsNullOrWhiteSpace(query)) return;

        EnsureConsumer();
        _queries.Writer.TryWrite(new SearchRequest(query, sort));
    }

    private void EnsureConsumer()
    {
        _consumer ??= Task.Run(ConsumeAsync);
    }

    private async Task ConsumeAsync()
    {
        try
        {
            await foreach (var request in _queries.Reader.ReadAllAsync(_cts.Token))
            {
                IsLoading = true;
                try
                {
                    lock (_itemsLock)
                    {
                        _items.Clear();
                    }

                    RaiseItemsChanged(0);

                    if (string.IsNullOrWhiteSpace(request.Query))
                    {
                        SetEmpty("输入关键词开始搜索", "需要 Everything 1.5 正在运行");
                        HasMoreItems = false;
                        continue;
                    }

                    var hits = _session.Start(request.Query, request.Sort);
                    _togglePage?.SetPendingQuery(request.Query);
                    lock (_itemsLock)
                    {
                        foreach (var hit in hits)
                        {
                            _items.Add(new ResultItem(hit, _context));
                        }
                    }

                    HasMoreItems = _session.HasMore;

                    if (hits.Count == 0)
                    {
                        // 连不上时要说清原因：未运行 / 进程在但没 1.5 管道（1.4 或实例名不对），
                        // 不能一律显示「没有找到结果」。
                        if (_client.State != EverythingConnectionState.Connected)
                        {
                            var (title, subtitle) = ConnectionDiagnostics.FailureText(_client.State);
                            SetEmpty(title, subtitle);
                        }
                        else if (_session.Total == 0)
                        {
                            SetEmpty("没有找到结果", "换个关键词，或用 Everything 语法（ext:psd dm:thisweek）");
                        }
                        else
                        {
                            SetEmpty("没有更多结果", "已经到底了");
                        }
                    }
                }
                catch (Exception ex)
                {
                    HostLog.Error($"search '{request.Query}' failed: {ex}");
                    SetEmpty("搜索失败", ex.Message);
                    HasMoreItems = false;
                }
                finally
                {
                    IsLoading = false;

                    int count;
                    lock (_itemsLock)
                    {
                        count = _items.Count;
                    }

                    RaiseItemsChanged(count);
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Page disposed.
        }
        catch (Exception ex)
        {
            HostLog.Error($"consumer crashed: {ex}");
        }
    }

    /// <summary>
    /// A shared page that opens with the opposite details mode and the current query.
    /// Created lazily so "pure list" pages do not recursively create toggles.
    /// </summary>
    private IContextItem? GetToggleItem()
    {
        // 纯列表页：弹回上一层详情页（GoBack），不再压新页面，避免返回栈越切越深。
        if (_returnPage is not null)
        {
            return new CommandContextItem(new GoBackCommand());
        }

        // 详情页：打开无详情版本，标题显示「隐藏详细信息」。
        _togglePage ??= new ResultsPage(_settings, null, showDetails: !ShowDetails, returnPage: this);
        _togglePage.SetPendingQuery(_session.Query);

        return new CommandContextItem(_togglePage)
        {
            Title = "隐藏详细信息",
        };
    }

    private void SetEmpty(string title, string subtitle)
    {
        _empty.Title = title;
        _empty.Subtitle = subtitle;
        OnPropertyChanged(nameof(EmptyContent));
    }

    public void Dispose()
    {
        _queries.Writer.TryComplete();
        _cts.Cancel();
        _cts.Dispose();
        _client.Dispose();
    }
}
