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

    private readonly Channel<string> _queries = Channel.CreateBounded<string>(
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
    private ResultsPage? _togglePage;
    private readonly ResultContext _context;

    public ResultsPage(SettingsManager settings, string? initialQuery = null, bool? showDetails = null)
    {
        Id = "com.everything.cmdpal.results";
        Title = "Everything";
        Name = showDetails == false ? "纯列表" : "搜索";
        Icon = Icons.App;
        PlaceholderText = "输入文件名、通配符或 Everything 语法（ext:psd dm:thisweek）";
        ShowDetails = showDetails ?? settings.ShowDetails;

        _settings = settings;
        _pageSize = settings.PageSize;
        _previewWidth = settings.PreviewWidth;
        _detailsSize = settings.DetailsSize;
        _client = new EverythingClient(settings.InstanceName);
        _session = new SearchSession(_client, _pageSize);

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
        _queries.Writer.TryWrite(newSearch ?? string.Empty);
    }

    public override IListItem[] GetItems()
    {
        if (_deferred)
        {
            _deferred = false;
            HostLog.Info($"running deferred query '{_initialQuery}'");
            EnsureConsumer();
            _queries.Writer.TryWrite(_initialQuery);
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

    private void EnsureConsumer()
    {
        _consumer ??= Task.Run(ConsumeAsync);
    }

    private async Task ConsumeAsync()
    {
        try
        {
            await foreach (var query in _queries.Reader.ReadAllAsync(_cts.Token))
            {
                IsLoading = true;
                try
                {
                    lock (_itemsLock)
                    {
                        _items.Clear();
                    }

                    RaiseItemsChanged(0);

                    if (string.IsNullOrWhiteSpace(query))
                    {
                        SetEmpty("输入关键词开始搜索", "需要 Everything 1.5 正在运行");
                        HasMoreItems = false;
                        continue;
                    }

                    var hits = _session.Start(query);
                    _togglePage?.SetPendingQuery(query);
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
                        SetEmpty(
                            _session.Total == 0 ? "没有找到结果" : "没有更多结果",
                            "检查 Everything 是否正在运行（需要 1.5 或更高版本）");
                    }
                }
                catch (Exception ex)
                {
                    HostLog.Error($"search '{query}' failed: {ex}");
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
        if (!ShowDetails)
        {
            // Host adds its own "Show details" action when the pane is off.
            return null;
        }

        _togglePage ??= new ResultsPage(_settings, null, showDetails: !ShowDetails);
        _togglePage.SetPendingQuery(_session.Query);

        return new CommandContextItem(_togglePage)
        {
            Title = "隐藏预览",
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
