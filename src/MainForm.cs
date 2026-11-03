using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Media;
using System.Windows.Forms;

namespace StockNotice
{
    public class MainForm : Form
    {
        private readonly AppState _state;
        private readonly JsonStateStore _store;
        private readonly MarketDataService _marketData;
        private readonly MonitorService _monitor;
        private readonly bool _demoMode;
        private readonly StartupCheckResult _startupCheck;
        private readonly Icon _appIcon;
        private readonly Icon _trayIcon;

        private DataGridView _stockGrid;
        private DataGridView _historyGrid;
        private TabControl _tabs;
        private Panel _emptyPanel;
        private Label _stockCountLabel;
        private Label _triggerCountLabel;
        private Label _updateLabel;
        private Label _providerLabel;
        private Label _healthLabel;
        private StatusPill _sessionLabel;
        private StatusPill _headerHealthPill;
        private Label _sessionHintLabel;
        private Label _statusLabel;
        private Button _monitorButton;
        private ToolTip _toolTip;
        private Label _detailNameLabel;
        private Label _detailCodeLabel;
        private StatusPill _detailStatusPill;
        private Label _detailPriceLabel;
        private Label _detailChangeLabel;
        private Label _detailChangeValueLabel;
        private QuoteSparkline _detailChart;
        private Label _detailBaseLabel;
        private Label _detailUpLabel;
        private Label _detailDownLabel;
        private Label _detailResetLabel;
        private Label _detailFreshnessLabel;
        private Label _detailCooldownLabel;
        private Label _detailQuietLabel;
        private Label _detailTodayLabel;
        private Button _detailEditButton;
        private NotifyIcon _tray;
        private ContextMenuStrip _trayMenu;
        private Timer _uiTimer;
        private bool _allowExit;
        private bool _trayHintShown;
        private string _pendingNotificationStockId;
        private bool _pendingSummaryView;
        private string _alertBannerText;
        private DateTime _alertBannerUntil;
        private readonly Font _tabFont;
        private readonly Font _tabFontBold;

        public MainForm(
            AppState state,
            JsonStateStore store,
            MarketDataService marketData,
            MonitorService monitor,
            bool demoMode,
            StartupCheckResult startupCheck)
        {
            _state = state;
            _store = store;
            _marketData = marketData;
            _monitor = monitor;
            _demoMode = demoMode;
            _startupCheck = startupCheck;
            _appIcon = AppResources.CreateAppIcon();
            _trayIcon = AppResources.CreateTrayIcon();
            _tabFont = AppResources.Font(9F, FontStyle.Regular);
            _tabFontBold = AppResources.Font(9F, FontStyle.Bold);
            _toolTip = new ToolTip();
            _toolTip.AutoPopDelay = 5000;
            _toolTip.InitialDelay = 350;
            _toolTip.ReshowDelay = 100;

            Text = "股票涨跌提醒";
            Icon = _appIcon;
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = AppResources.SS(1180, 760);
            Size = AppResources.SS(1360, 840);
            BackColor = AppResources.Background;
            Font = AppResources.Font(9F, FontStyle.Regular);

            BuildInterface();
            BuildTray();

            Load += MainForm_Load;
            FormClosing += MainForm_FormClosing;
            Resize += MainForm_Resize;

            _monitor.TriggerRaised += Monitor_TriggerRaised;
            _monitor.SnapshotUpdated += Monitor_SnapshotUpdated;
            _monitor.DailySummaryRaised += Monitor_DailySummaryRaised;

            _uiTimer = new Timer();
            _uiTimer.Interval = 1000;
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            _monitor.RebuildStockIndex();
            if (!_demoMode) _monitor.Start();
            if (_startupCheck != null && !_startupCheck.IsHealthy)
            {
                ShowAlertBanner(_startupCheck.Summary, 12);
            }
            RefreshAll();
        }

        private void BuildInterface()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 4;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(64F)));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(52F)));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(32F)));
            Controls.Add(root);

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildToolbar(), 0, 1);
            root.Controls.Add(BuildContent(), 0, 2);
            root.Controls.Add(BuildStatusBar(), 0, 3);
        }

        private Control BuildHeader()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = AppResources.Header;
            header.Padding = AppResources.SPad(22, 0, 20, 0);

            RoundedPanel logo = new RoundedPanel();
            logo.DrawBorder = false;
            logo.CornerRadius = AppResources.S(17);
            logo.BackColor = AppResources.HeaderText;
            logo.Location = AppResources.SP(22, 15);
            logo.Size = AppResources.SS(34, 34);
            Label logoText = new Label();
            logoText.Text = "A";
            logoText.Dock = DockStyle.Fill;
            logoText.TextAlign = ContentAlignment.MiddleCenter;
            logoText.ForeColor = AppResources.Header;
            logoText.Font = AppResources.Font(12F, FontStyle.Bold);
            logo.Controls.Add(logoText);
            header.Controls.Add(logo);

            Label title = new Label();
            title.Text = "A股涨跌提醒";
            title.ForeColor = AppResources.HeaderText;
            title.Font = AppResources.Font(17F, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = AppResources.SP(68, 9);
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "每 5 秒检查行情 · 本地隐私优先";
            subtitle.ForeColor = AppResources.HeaderMuted;
            subtitle.Font = AppResources.Font(8F, FontStyle.Regular);
            subtitle.AutoSize = true;
            subtitle.Location = AppResources.SP(70, 35);
            header.Controls.Add(subtitle);

            header.Controls.Add(CreateHeaderMetric("监控", out _stockCountLabel, 58, 316));
            header.Controls.Add(CreateHeaderMetric("今日触发", out _triggerCountLabel, 78, 384));
            header.Controls.Add(CreateHeaderMetric("行情时间", out _updateLabel, 96, 472));
            header.Controls.Add(CreateHeaderMetric("状态", out _healthLabel, 140, 578));

            _sessionLabel = new StatusPill();
            _sessionLabel.Text = "交易中 · --:--:--";
            _sessionLabel.PillColor = Color.FromArgb(30, 77, 61);
            _sessionLabel.ForeColor = Color.FromArgb(126, 240, 184);
            _sessionLabel.Location = AppResources.SP(736, 21);
            header.Controls.Add(_sessionLabel);

            _headerHealthPill = new StatusPill();
            _headerHealthPill.Text = "等待行情";
            _headerHealthPill.PillColor = Color.FromArgb(31, 41, 55);
            _headerHealthPill.ForeColor = AppResources.HeaderMuted;
            _headerHealthPill.DrawPillBorder = true;
            _headerHealthPill.PillBorderColor = Color.FromArgb(55, 65, 81);
            _headerHealthPill.Visible = false;

            FlowLayoutPanel right = new FlowLayoutPanel();
            right.Dock = DockStyle.Right;
            right.FlowDirection = FlowDirection.LeftToRight;
            right.WrapContents = false;
            right.AutoSize = true;
            right.Padding = AppResources.SPad(0, 15, 0, 0);

            _monitorButton = CreateHeaderButton("暂停监控", 112, 34, AppGlyph.Pause);
            _monitorButton.Click += MonitorButton_Click;
            right.Controls.Add(_monitorButton);

            Button settings = CreateHeaderButton(string.Empty, 34, 34, AppGlyph.Settings);
            settings.Name = "settingsButton";
            settings.AccessibleName = "设置";
            _toolTip.SetToolTip(settings, "打开运行设置");
            settings.Click += SettingsButton_Click;
            right.Controls.Add(settings);

            header.Controls.Add(right);
            return header;
        }

        private Panel CreateHeaderMetric(string caption, out Label valueLabel, int width, int left)
        {
            Panel block = new Panel();
            block.Width = AppResources.S(width);
            block.Height = AppResources.S(42);
            block.Location = AppResources.SP(left, 11);
            block.BackColor = AppResources.Header;

            Label captionLabel = new Label();
            captionLabel.Text = caption;
            captionLabel.ForeColor = AppResources.HeaderMuted;
            captionLabel.Font = AppResources.Font(7.5F, FontStyle.Regular);
            captionLabel.AutoSize = true;
            captionLabel.Location = AppResources.SP(0, 1);
            block.Controls.Add(captionLabel);

            valueLabel = new Label();
            valueLabel.Text = "--";
            valueLabel.ForeColor = AppResources.HeaderText;
            valueLabel.Font = AppResources.Font(10F, FontStyle.Bold);
            valueLabel.AutoSize = false;
            valueLabel.AutoEllipsis = true;
            valueLabel.Location = AppResources.SP(0, 17);
            valueLabel.Size = AppResources.SS(width, 22);
            block.Controls.Add(valueLabel);
            return block;
        }

        private Control BuildToolbar()
        {
            Panel toolbar = new Panel();
            toolbar.Dock = DockStyle.Fill;
            toolbar.BackColor = AppResources.Surface;
            toolbar.Padding = AppResources.SPad(22, 9, 22, 9);

            FlowLayoutPanel left = new FlowLayoutPanel();
            left.Dock = DockStyle.Left;
            left.AutoSize = true;
            left.WrapContents = false;
            left.FlowDirection = FlowDirection.LeftToRight;

            Button add = CreateButton("添加股票", 106, 34, true, AppGlyph.Add);
            add.Click += AddButton_Click;
            left.Controls.Add(add);

            Button refresh = CreateButton("立即刷新", 106, 34, false, AppGlyph.Refresh);
            refresh.Click += RefreshButton_Click;
            left.Controls.Add(refresh);

            Button edit = CreateButton(string.Empty, 34, 34, false, AppGlyph.Edit);
            edit.AccessibleName = "编辑";
            _toolTip.SetToolTip(edit, "编辑当前股票");
            edit.Click += EditButton_Click;
            left.Controls.Add(edit);

            Button delete = CreateButton(string.Empty, 34, 34, false, AppGlyph.Delete);
            delete.AccessibleName = "删除";
            _toolTip.SetToolTip(delete, "删除当前股票");
            delete.Click += DeleteButton_Click;
            left.Controls.Add(delete);

            toolbar.Controls.Add(left);

            Label hint = new Label();
            hint.Dock = DockStyle.Right;
            hint.Width = AppResources.S(210);
            hint.TextAlign = ContentAlignment.MiddleRight;
            hint.ForeColor = AppResources.Muted;
            hint.Text = "双击编辑 · 右键更多操作";
            hint.Font = AppResources.Font(8F, FontStyle.Regular);
            toolbar.Controls.Add(hint);

            return toolbar;
        }

        private Control BuildStatsStrip()
        {
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.BackColor = AppResources.Background;
            host.Padding = AppResources.SPad(22, 6, 22, 8);

            RoundedPanel card = new RoundedPanel();
            card.Dock = DockStyle.Fill;
            card.BackColor = AppResources.Surface;
            card.BorderColor = AppResources.Border;
            host.Controls.Add(card);

            TableLayoutPanel stats = new TableLayoutPanel();
            stats.Dock = DockStyle.Fill;
            stats.ColumnCount = 5;
            stats.RowCount = 1;
            stats.BackColor = AppResources.Surface;
            stats.Padding = Padding.Empty;
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20F));
            stats.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            stats.Controls.Add(CreateStatBlock("监控股票", out _stockCountLabel, true), 0, 0);
            stats.Controls.Add(CreateStatBlock("今日触发", out _triggerCountLabel, true), 1, 0);
            stats.Controls.Add(CreateStatBlock("行情更新时间", out _updateLabel, true), 2, 0);
            stats.Controls.Add(CreateStatBlock("数据状态", out _healthLabel, true), 3, 0);
            stats.Controls.Add(CreateStatBlock("行情来源", out _providerLabel, false), 4, 0);
            card.Controls.Add(stats);
            return host;
        }

        private Control CreateStatBlock(string caption, out Label valueLabel, bool showDivider)
        {
            Panel block = new Panel();
            block.Dock = DockStyle.Fill;
            block.Margin = Padding.Empty;
            block.BackColor = AppResources.Surface;
            block.Padding = AppResources.SPad(14, 6, 14, 5);
            if (showDivider)
            {
                block.Paint += delegate(object sender, PaintEventArgs e)
                {
                    using (Pen pen = new Pen(AppResources.Border))
                    {
                        e.Graphics.DrawLine(
                            pen,
                            block.Width - 1,
                            AppResources.S(8),
                            block.Width - 1,
                            block.Height - AppResources.S(8));
                    }
                };
            }

            Label captionLabel = new Label();
            captionLabel.Text = caption;
            captionLabel.ForeColor = AppResources.Muted;
            captionLabel.Font = AppResources.Font(8F, FontStyle.Regular);
            captionLabel.AutoSize = true;
            captionLabel.Location = AppResources.SP(14, 6);
            block.Controls.Add(captionLabel);

            valueLabel = new Label();
            valueLabel.Text = "--";
            valueLabel.ForeColor = AppResources.Text;
            valueLabel.Font = AppResources.Font(10.5F, FontStyle.Bold);
            valueLabel.AutoEllipsis = true;
            valueLabel.AutoSize = false;
            valueLabel.Height = AppResources.S(22);
            valueLabel.Dock = DockStyle.Bottom;
            block.Controls.Add(valueLabel);
            valueLabel.BringToFront();

            return block;
        }

        private Control BuildContent()
        {
            Panel content = new Panel();
            content.Dock = DockStyle.Fill;
            content.Padding = AppResources.SPad(22, 0, 22, 0);
            content.BackColor = AppResources.Background;

            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Fill;
            _tabs.Font = AppResources.Font(9F, FontStyle.Regular);
            _tabs.Padding = new Point(18, 6);
            _tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            _tabs.SizeMode = TabSizeMode.Fixed;
            _tabs.ItemSize = AppResources.SS(116, 34);
            _tabs.DrawItem += Tabs_DrawItem;

            TabPage stocksTab = new TabPage("监控列表");
            stocksTab.BackColor = AppResources.Background;
            stocksTab.Padding = AppResources.SPad(0, 8, 0, 0);
            stocksTab.Controls.Add(BuildStockWorkspace());
            _tabs.TabPages.Add(stocksTab);

            TabPage historyTab = new TabPage("触发记录");
            historyTab.BackColor = AppResources.Background;
            historyTab.Padding = AppResources.SPad(0, 8, 0, 0);
            RoundedPanel historyCard = new RoundedPanel();
            historyCard.Dock = DockStyle.Fill;
            historyCard.BackColor = AppResources.Surface;
            historyCard.Controls.Add(BuildHistoryGrid());
            historyTab.Controls.Add(historyCard);
            _tabs.TabPages.Add(historyTab);

            content.Controls.Add(_tabs);
            return content;
        }

        private Control BuildStockWorkspace()
        {
            SplitContainer split = new SplitContainer();
            split.Dock = DockStyle.Fill;
            split.Orientation = Orientation.Vertical;
            split.FixedPanel = FixedPanel.Panel2;
            split.Panel1MinSize = 0;
            split.Panel2MinSize = 0;
            split.SplitterWidth = AppResources.S(12);
            split.BackColor = AppResources.Background;

            RoundedPanel tableCard = new RoundedPanel();
            tableCard.Dock = DockStyle.Fill;
            tableCard.BackColor = AppResources.Surface;
            tableCard.Padding = new Padding(1);

            Panel tableHost = new Panel();
            tableHost.Dock = DockStyle.Fill;
            tableHost.BackColor = AppResources.Surface;
            tableHost.Controls.Add(BuildStockGrid());
            tableHost.Controls.Add(BuildEmptyPanel());
            tableCard.Controls.Add(tableHost);
            split.Panel1.Controls.Add(tableCard);
            split.Panel2.Controls.Add(BuildDetailPanel());

            split.Resize += delegate
            {
                if (split.Width <= split.SplitterWidth + AppResources.S(80)) return;
                int target = split.Width - AppResources.S(312) - split.SplitterWidth;
                int minimum = AppResources.S(80);
                int maximum = split.Width - split.SplitterWidth - AppResources.S(80);
                if (maximum < minimum) return;
                try
                {
                    split.SplitterDistance = Math.Max(minimum, Math.Min(maximum, target));
                }
                catch (ArgumentOutOfRangeException)
                {
                    // WinForms can raise this during an intermediate layout pass.
                }
            };
            return split;
        }

        private Control BuildDetailPanel()
        {
            RoundedPanel detail = new RoundedPanel();
            detail.Dock = DockStyle.Fill;
            detail.BackColor = AppResources.Surface;
            detail.Padding = AppResources.SPad(16, 15, 16, 14);

            _detailNameLabel = new Label();
            _detailNameLabel.Text = "请选择股票";
            _detailNameLabel.AutoSize = true;
            _detailNameLabel.ForeColor = AppResources.Text;
            _detailNameLabel.Font = AppResources.Font(15F, FontStyle.Bold);
            _detailNameLabel.Location = AppResources.SP(16, 15);
            detail.Controls.Add(_detailNameLabel);

            _detailStatusPill = new StatusPill();
            _detailStatusPill.Text = "等待行情";
            _detailStatusPill.PillColor = AppResources.FieldBackground;
            _detailStatusPill.ForeColor = AppResources.Muted;
            _detailStatusPill.Location = AppResources.SP(210, 16);
            detail.Controls.Add(_detailStatusPill);

            _detailCodeLabel = new Label();
            _detailCodeLabel.Text = "--";
            _detailCodeLabel.AutoSize = true;
            _detailCodeLabel.ForeColor = AppResources.Muted;
            _detailCodeLabel.Font = AppResources.Font(8.5F, FontStyle.Regular);
            _detailCodeLabel.Location = AppResources.SP(16, 44);
            detail.Controls.Add(_detailCodeLabel);

            _detailPriceLabel = new Label();
            _detailPriceLabel.Text = "--";
            _detailPriceLabel.AutoSize = true;
            _detailPriceLabel.ForeColor = AppResources.Muted;
            _detailPriceLabel.Font = new Font("Cascadia Mono", 24F, FontStyle.Bold, GraphicsUnit.Point);
            _detailPriceLabel.Location = AppResources.SP(16, 65);
            detail.Controls.Add(_detailPriceLabel);

            _detailChangeLabel = new Label();
            _detailChangeLabel.Text = "--";
            _detailChangeLabel.AutoSize = true;
            _detailChangeLabel.ForeColor = AppResources.Muted;
            _detailChangeLabel.Font = AppResources.Font(9.5F, FontStyle.Bold);
            _detailChangeLabel.Location = AppResources.SP(17, 105);
            detail.Controls.Add(_detailChangeLabel);

            _detailChart = new QuoteSparkline();
            _detailChart.Location = AppResources.SP(16, 132);
            _detailChart.Size = AppResources.SS(270, 92);
            _detailChart.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            detail.Controls.Add(_detailChart);

            AddDetailValue(detail, "基准价", out _detailBaseLabel, 16, 238);
            AddDetailValue(detail, "当前涨跌", out _detailChangeValueLabel, 16, 273);
            AddDetailValue(detail, "上涨阈值", out _detailUpLabel, 16, 308);
            AddDetailValue(detail, "下跌阈值", out _detailDownLabel, 154, 238);
            AddDetailValue(detail, "重置幅度", out _detailResetLabel, 154, 273);
            AddDetailValue(detail, "提醒间隔", out _detailCooldownLabel, 154, 308);

            Panel divider = new Panel();
            divider.BackColor = AppResources.Border;
            divider.Height = 1;
            divider.Location = AppResources.SP(16, 354);
            divider.Width = AppResources.S(270);
            divider.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            detail.Controls.Add(divider);

            AddDetailLine(detail, "数据新鲜度", out _detailFreshnessLabel, 370);
            AddDetailLine(detail, "免打扰", out _detailQuietLabel, 397);

            Label todayCaption = new Label();
            todayCaption.Text = "今日汇总";
            todayCaption.ForeColor = AppResources.Muted;
            todayCaption.Font = AppResources.Font(8.5F, FontStyle.Regular);
            todayCaption.AutoSize = true;
            todayCaption.Location = AppResources.SP(16, 430);
            detail.Controls.Add(todayCaption);

            _detailTodayLabel = new Label();
            _detailTodayLabel.Text = "--";
            _detailTodayLabel.ForeColor = AppResources.Text;
            _detailTodayLabel.Font = AppResources.Font(8.5F, FontStyle.Regular);
            _detailTodayLabel.AutoSize = false;
            _detailTodayLabel.Location = AppResources.SP(16, 448);
            _detailTodayLabel.Size = AppResources.SS(268, 34);
            _detailTodayLabel.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            detail.Controls.Add(_detailTodayLabel);

            _detailEditButton = CreateButton("编辑设置", 118, 34, true, AppGlyph.Edit);
            _detailEditButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            _detailEditButton.Location = AppResources.SP(16, 490);
            _detailEditButton.Click += EditButton_Click;
            detail.Controls.Add(_detailEditButton);

            Button more = CreateButton(string.Empty, 34, 34, false, AppGlyph.More);
            _toolTip.SetToolTip(more, "更多操作");
            more.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            more.Location = AppResources.SP(144, 490);
            more.Click += delegate
            {
                ContextMenuStrip menu = new ContextMenuStrip();
                menu.Items.Add("编辑", null, delegate { EditSelectedStock(); });
                menu.Items.Add("删除", null, delegate { DeleteSelectedStock(); });
                menu.Show(more, new Point(0, more.Height));
            };
            detail.Controls.Add(more);

            detail.Resize += delegate
            {
                _detailStatusPill.Left = Math.Max(
                    AppResources.S(16),
                    detail.ClientSize.Width - _detailStatusPill.Width - AppResources.S(16));
                _detailChart.Width = Math.Max(
                    AppResources.S(120),
                    detail.ClientSize.Width - AppResources.S(32));
                divider.Width = _detailChart.Width;
                _detailEditButton.Top = detail.ClientSize.Height - AppResources.S(50);
                more.Top = _detailEditButton.Top;
            };
            return detail;
        }

        private void AddDetailValue(Control parent, string caption, out Label value, int x, int y)
        {
            Label captionLabel = new Label();
            captionLabel.Text = caption;
            captionLabel.AutoSize = true;
            captionLabel.ForeColor = AppResources.Muted;
            captionLabel.Font = AppResources.Font(7.5F, FontStyle.Regular);
            captionLabel.Location = AppResources.SP(x, y);
            parent.Controls.Add(captionLabel);

            value = new Label();
            value.Text = "--";
            value.AutoSize = true;
            value.ForeColor = AppResources.Text;
            value.Font = AppResources.Font(8.5F, FontStyle.Bold);
            value.Location = AppResources.SP(x, y + 17);
            parent.Controls.Add(value);
        }

        private void AddDetailLine(Control parent, string caption, out Label value, int y)
        {
            Label captionLabel = new Label();
            captionLabel.Text = caption;
            captionLabel.AutoSize = true;
            captionLabel.ForeColor = AppResources.Muted;
            captionLabel.Font = AppResources.Font(8F, FontStyle.Regular);
            captionLabel.Location = AppResources.SP(16, y);
            parent.Controls.Add(captionLabel);

            value = new Label();
            value.Text = "--";
            value.AutoSize = false;
            value.TextAlign = ContentAlignment.MiddleRight;
            value.ForeColor = AppResources.Text;
            value.Font = AppResources.Font(8.5F, FontStyle.Bold);
            value.Location = AppResources.SP(126, y - 3);
            value.Size = AppResources.SS(160, 24);
            value.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            parent.Controls.Add(value);
        }

        private void Tabs_DrawItem(object sender, DrawItemEventArgs e)
        {
            Rectangle tab = _tabs.GetTabRect(e.Index);
            bool selected = _tabs.SelectedIndex == e.Index;

            if (e.Index == 0)
            {
                // Paint only the empty parts of the tab strip. Filling the whole
                // strip here would erase whichever tab the control drew first.
                int firstLeft = tab.Left;
                int lastRight = tab.Right;
                for (int i = 1; i < _tabs.TabPages.Count; i++)
                {
                    Rectangle other = _tabs.GetTabRect(i);
                    if (other.Left < firstLeft) firstLeft = other.Left;
                    if (other.Right > lastRight) lastRight = other.Right;
                }

                using (SolidBrush strip = new SolidBrush(AppResources.Surface))
                {
                    if (firstLeft > 0)
                    {
                        e.Graphics.FillRectangle(strip, 0, tab.Top, firstLeft, tab.Height);
                    }
                    if (lastRight < _tabs.Width)
                    {
                        e.Graphics.FillRectangle(strip, lastRight, tab.Top, _tabs.Width - lastRight, tab.Height);
                    }
                }
            }

            using (SolidBrush back = new SolidBrush(AppResources.Surface))
            {
                e.Graphics.FillRectangle(back, tab);
            }

            using (Pen border = new Pen(AppResources.Border))
            {
                e.Graphics.DrawLine(border, tab.Left, tab.Bottom - 1, tab.Right, tab.Bottom - 1);
            }

            if (selected)
            {
                using (SolidBrush accent = new SolidBrush(AppResources.Accent))
                {
                    e.Graphics.FillRectangle(accent, tab.Left + 1, tab.Top + 1, tab.Width - 2, 2);
                }
            }

            TextRenderer.DrawText(
                e.Graphics,
                _tabs.TabPages[e.Index].Text,
                selected ? _tabFontBold : _tabFont,
                tab,
                selected ? AppResources.Text : AppResources.Muted,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private Control BuildStockGrid()
        {
            _stockGrid = CreateGrid();
            AddGridColumn(_stockGrid, "name", "股票", 235F, false);
            AddGridColumn(_stockGrid, "price", "最新价", 88F, true);
            AddGridColumn(_stockGrid, "change", "涨跌幅", 88F, true);
            AddGridColumn(_stockGrid, "open", "开盘价", 86F, true);
            AddGridColumn(_stockGrid, "base", "昨收", 86F, true);
            AddGridColumn(_stockGrid, "state", "状态", 150F, false);
            AddGridColumn(_stockGrid, "time", "更新", 84F, true);

            _stockGrid.CellDoubleClick += StockGrid_CellDoubleClick;
            _stockGrid.CellFormatting += StockGrid_CellFormatting;
            _stockGrid.CellMouseDown += StockGrid_CellMouseDown;
            _stockGrid.SelectionChanged += delegate { RefreshDetailPanel(); };
            return _stockGrid;
        }

        private Control BuildHistoryGrid()
        {
            _historyGrid = CreateGrid();
            AddGridColumn(_historyGrid, "time", "触发时间", 130F, true);
            AddGridColumn(_historyGrid, "stock", "股票", 190F, false);
            AddGridColumn(_historyGrid, "direction", "方向", 80F, false);
            AddGridColumn(_historyGrid, "change", "涨跌幅", 90F, true);
            AddGridColumn(_historyGrid, "price", "价格", 90F, true);
            AddGridColumn(_historyGrid, "level", "档位", 80F, true);
            AddGridColumn(_historyGrid, "number", "通知序号", 90F, true);
            AddGridColumn(_historyGrid, "reason", "触发原因", 120F, false);
            _historyGrid.CellFormatting += HistoryGrid_CellFormatting;
            return _historyGrid;
        }

        private Panel BuildEmptyPanel()
        {
            _emptyPanel = new Panel();
            _emptyPanel.Dock = DockStyle.Fill;
            _emptyPanel.BackColor = AppResources.Surface;

            Label title = new Label();
            title.Text = "还没有监控股票";
            title.Font = AppResources.Font(14F, FontStyle.Bold);
            title.ForeColor = AppResources.Text;
            title.AutoSize = true;
            title.Location = new Point(0, 0);
            title.Name = "emptyTitle";
            _emptyPanel.Controls.Add(title);

            Label hint = new Label();
            hint.Text = "点击“添加股票”，设置各自的上涨、下跌阈值和重置幅度。";
            hint.Font = AppResources.Font(9F, FontStyle.Regular);
            hint.ForeColor = AppResources.Muted;
            hint.AutoSize = true;
            hint.Name = "emptyHint";
            _emptyPanel.Controls.Add(hint);

            Button emptyAdd = CreateButton("添加股票", 106, 34, true, AppGlyph.Add);
            emptyAdd.Name = "emptyAdd";
            emptyAdd.Click += AddButton_Click;
            _emptyPanel.Controls.Add(emptyAdd);

            _emptyPanel.Resize += delegate
            {
                Label titleLabel = _emptyPanel.Controls["emptyTitle"] as Label;
                Label hintLabel = _emptyPanel.Controls["emptyHint"] as Label;
                Button addLabel = _emptyPanel.Controls["emptyAdd"] as Button;
                if (titleLabel == null || hintLabel == null || addLabel == null) return;
                int centerX = Math.Max(0, (_emptyPanel.ClientSize.Width - AppResources.S(360)) / 2);
                titleLabel.Location = new Point(
                    centerX,
                    Math.Max(AppResources.S(60), _emptyPanel.ClientSize.Height / 2 - AppResources.S(32)));
                hintLabel.Location = new Point(centerX, titleLabel.Bottom + AppResources.S(10));
                addLabel.Location = new Point(centerX, hintLabel.Bottom + AppResources.S(18));
            };

            return _emptyPanel;
        }

        private Control BuildStatusBar()
        {
            Panel status = new Panel();
            status.Dock = DockStyle.Fill;
            status.BackColor = AppResources.Header;
            status.Padding = AppResources.SPad(22, 0, 22, 0);

            _sessionHintLabel = new Label();
            _sessionHintLabel.Text = "下一时段：--";
            _sessionHintLabel.ForeColor = AppResources.HeaderMuted;
            _sessionHintLabel.Font = AppResources.Font(8F, FontStyle.Regular);
            _sessionHintLabel.AutoSize = true;
            _sessionHintLabel.Location = AppResources.SP(22, 8);
            status.Controls.Add(_sessionHintLabel);

            _statusLabel = new Label();
            _statusLabel.Text = "正在初始化";
            _statusLabel.TextAlign = ContentAlignment.MiddleRight;
            _statusLabel.Dock = DockStyle.Right;
            _statusLabel.Width = AppResources.S(560);
            _statusLabel.ForeColor = AppResources.HeaderMuted;
            _statusLabel.Font = AppResources.Font(8F, FontStyle.Regular);
            status.Controls.Add(_statusLabel);

            _providerLabel = new Label();
            _providerLabel.Text = "--";
            _providerLabel.TextAlign = ContentAlignment.MiddleRight;
            _providerLabel.Dock = DockStyle.Right;
            _providerLabel.Width = AppResources.S(190);
            _providerLabel.ForeColor = Color.FromArgb(140, 154, 179);
            _providerLabel.Font = AppResources.Font(8F, FontStyle.Regular);
            status.Controls.Add(_providerLabel);

            _statusLabel.BringToFront();
            return status;
        }

        private static DataGridView CreateGrid()
        {
            DataGridView grid = new BufferedDataGridView();
            grid.Dock = DockStyle.Fill;
            grid.BackgroundColor = AppResources.Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = AppResources.Border;
            grid.RowHeadersVisible = false;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoGenerateColumns = false;
            grid.ColumnHeadersHeight = AppResources.S(38);
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.RowTemplate.Height = AppResources.S(46);
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = AppResources.GridHeaderBackground;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = AppResources.Muted;
            grid.ColumnHeadersDefaultCellStyle.Font = AppResources.Font(8.5F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = AppResources.GridHeaderBackground;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = AppResources.Muted;
            grid.DefaultCellStyle.BackColor = AppResources.Surface;
            grid.DefaultCellStyle.ForeColor = AppResources.Text;
            grid.DefaultCellStyle.Font = AppResources.Font(9F, FontStyle.Regular);
            grid.DefaultCellStyle.SelectionBackColor = AppResources.GridRowSelection;
            grid.DefaultCellStyle.SelectionForeColor = AppResources.Text;
            grid.DefaultCellStyle.Padding = AppResources.SPad(8, 0, 8, 0);
            grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.AlternatingRowsDefaultCellStyle.BackColor = AppResources.GridAltRow;
            return grid;
        }

        private static void AddGridColumn(
            DataGridView grid,
            string name,
            string title,
            float weight,
            bool numeric)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = title;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column.FillWeight = weight;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            DataGridViewContentAlignment alignment = numeric
                ? DataGridViewContentAlignment.MiddleRight
                : DataGridViewContentAlignment.MiddleLeft;
            column.DefaultCellStyle.Alignment = alignment;
            column.HeaderCell.Style.Alignment = alignment;
            grid.Columns.Add(column);
        }

        private void BuildTray()
        {
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Font = AppResources.Font(9F, FontStyle.Regular);
            _trayMenu.Items.Add("打开主窗口", null, delegate { ShowFromTray(); });
            _trayMenu.Items.Add("立即刷新", null, async delegate { await RefreshNowAsync(); });
            _trayMenu.Items.Add("暂停 / 继续监控", null, delegate { ToggleMonitoring(); });
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("退出", null, delegate { ExitApplication(); });

            _tray = new NotifyIcon();
            _tray.Icon = _trayIcon;
            _tray.Text = "股票涨跌提醒";
            _tray.Visible = true;
            _tray.ContextMenuStrip = _trayMenu;
            _tray.DoubleClick += delegate { ShowFromTray(); };
            _tray.BalloonTipClicked += Tray_BalloonTipClicked;
        }

        private Button CreateButton(string text, int width, int height, bool primary)
        {
            return CreateButton(text, width, height, primary, null);
        }

        private Button CreateButton(string text, int width, int height, bool primary, AppGlyph? glyph)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = AppResources.S(width);
            button.Height = AppResources.S(height);
            button.Margin = AppResources.SPad(0, 0, 8, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = AppResources.ButtonBorder;
            button.BackColor = primary ? AppResources.Accent : AppResources.Surface;
            button.ForeColor = primary ? AppResources.OnAccent : AppResources.Text;
            button.Font = AppResources.Font(9F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.ImageAlign = string.IsNullOrEmpty(text) ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Padding = string.IsNullOrEmpty(text)
                ? Padding.Empty
                : AppResources.SPad(8, 0, 9, 0);
            if (glyph.HasValue)
            {
                button.Image = UiIconFactory.Create(
                    glyph.Value,
                    primary ? AppResources.OnAccent : AppResources.Text);
            }
            return button;
        }

        private Button CreateHeaderButton(string text, int width, int height, AppGlyph glyph)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = AppResources.S(width);
            button.Height = AppResources.S(height);
            button.Margin = AppResources.SPad(0, 0, 8, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 1;
            button.FlatAppearance.BorderColor = Color.FromArgb(55, 65, 81);
            button.FlatAppearance.MouseOverBackColor = Color.FromArgb(31, 41, 55);
            button.BackColor = Color.FromArgb(24, 32, 46);
            button.ForeColor = AppResources.HeaderText;
            button.Font = AppResources.Font(8.5F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            button.TextImageRelation = TextImageRelation.ImageBeforeText;
            button.ImageAlign = string.IsNullOrEmpty(text) ? ContentAlignment.MiddleCenter : ContentAlignment.MiddleLeft;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Padding = string.IsNullOrEmpty(text)
                ? Padding.Empty
                : AppResources.SPad(8, 0, 9, 0);
            button.Image = UiIconFactory.Create(glyph, AppResources.HeaderText);
            return button;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (_allowExit) return;
            e.Cancel = true;
            Hide();
            if (!_trayHintShown)
            {
                _trayHintShown = true;
                _tray.ShowBalloonTip(
                    3500,
                    "股票涨跌提醒仍在后台运行",
                    "程序会继续检查行情。双击托盘图标可重新打开主窗口。",
                    ToolTipIcon.Info);
            }
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (WindowState == FormWindowState.Minimized)
            {
                Hide();
            }
        }

        private void MonitorButton_Click(object sender, EventArgs e)
        {
            ToggleMonitoring();
        }

        private void SettingsButton_Click(object sender, EventArgs e)
        {
            using (SettingsForm form = new SettingsForm(_state, _store, ShowTestNotification))
            {
                if (form.ShowDialog(this) == DialogResult.OK)
                {
                    _tray.Text = _state.Settings.MonitoringEnabled ? "股票涨跌提醒 · 监控中" : "股票涨跌提醒 · 已暂停";
                    RefreshAll();
                }
            }
        }

        private void ShowTestNotification()
        {
            _pendingNotificationStockId = null;
            _tray.ShowBalloonTip(
                5000,
                "贵州茅台 上涨 3.20%",
                "当前价格 1588.00",
                ToolTipIcon.Info);
            if (_state.Settings.SoundEnabled) SystemSounds.Exclamation.Play();
            _tray.Text = "股票涨跌提醒 · 测试通知";
            ShowAlertBanner("测试通知 · 贵州茅台上涨 3.20% · 当前价格 1588.00", 12);
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            using (StockEditForm form = new StockEditForm(_marketData, null))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;

                _state.Stocks.Add(form.Stock);
                _monitor.RebuildStockIndex();
                _monitor.ResetRuntimeForStock(form.Stock.Id);
                SaveState();
                _tabs.SelectedIndex = 0;
                RefreshAll();
                SelectStock(form.Stock.Id);
            }
        }

        private void EditButton_Click(object sender, EventArgs e)
        {
            EditSelectedStock();
        }

        private void DeleteButton_Click(object sender, EventArgs e)
        {
            DeleteSelectedStock();
        }

        private void RefreshButton_Click(object sender, EventArgs e)
        {
            RefreshNowButtonAsync();
        }

        private async void RefreshNowButtonAsync()
        {
            await RefreshNowAsync();
        }

        private async System.Threading.Tasks.Task RefreshNowAsync()
        {
            try
            {
                await _monitor.RefreshNowAsync();
                RefreshAll();
            }
            catch (Exception ex)
            {
                _statusLabel.Text = "刷新失败：" + ex.Message;
            }
        }

        private void StockGrid_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0) EditSelectedStock();
        }

        private void StockGrid_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
            _stockGrid.ClearSelection();
            _stockGrid.Rows[e.RowIndex].Selected = true;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("编辑", null, delegate { EditSelectedStock(); });
            menu.Items.Add("删除", null, delegate { DeleteSelectedStock(); });
            menu.Show(_stockGrid, e.Location);
        }

        private void EditSelectedStock()
        {
            StockConfig selected = GetSelectedStock();
            if (selected == null)
            {
                MessageBox.Show(this, "请先选择一只股票。", "股票涨跌提醒", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using (StockEditForm form = new StockEditForm(_marketData, CloneStock(selected)))
            {
                if (form.ShowDialog(this) != DialogResult.OK) return;

                StockConfig edited = form.Stock;
                selected.Code = edited.Code;
                selected.Market = edited.Market;
                selected.Name = edited.Name;
                selected.Enabled = edited.Enabled;
                selected.UpThreshold = edited.UpThreshold;
                selected.DownThreshold = edited.DownThreshold;
                selected.ResetMargin = edited.ResetMargin;
                selected.CustomBaseEnabled = edited.CustomBaseEnabled;
                selected.CustomBasePrice = edited.CustomBasePrice;
                selected.CustomBaseDate = edited.CustomBaseDate;

                _monitor.RebuildStockIndex();
                _monitor.ResetRuntimeForStock(selected.Id);
                SaveState();
                RefreshAll();
                SelectStock(selected.Id);
            }
        }

        private void DeleteSelectedStock()
        {
            StockConfig selected = GetSelectedStock();
            if (selected == null)
            {
                MessageBox.Show(this, "请先选择一只股票。", "股票涨跌提醒", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            DialogResult answer = MessageBox.Show(
                this,
                "确定删除“" + selected.Name + "（" + selected.Code + "）”吗？",
                "删除股票",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (answer != DialogResult.Yes) return;

            _state.Stocks.Remove(selected);
            _state.Runtime.Remove(selected.Id);
            _monitor.ClearQuoteHistory(selected.Id);
            _monitor.RebuildStockIndex();
            SaveState();
            RefreshAll();
        }

        private StockConfig GetSelectedStock()
        {
            if (_stockGrid == null || _stockGrid.SelectedRows.Count == 0) return null;
            string id = Convert.ToString(_stockGrid.SelectedRows[0].Tag);
            return _state.Stocks.FirstOrDefault(delegate(StockConfig item) { return item.Id == id; });
        }

        private void ToggleMonitoring()
        {
            _state.Settings.MonitoringEnabled = !_state.Settings.MonitoringEnabled;
            SaveState();
            RefreshAll();
        }

        private void SaveState()
        {
            try
            {
                _store.Save(_state);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "配置保存失败：" + ex.Message, "股票涨跌提醒", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RefreshAll()
        {
            RefreshStockGrid();
            RefreshHistoryGrid();
            RefreshStats();
        }

        private void RefreshStockGrid()
        {
            string selectedId = null;
            if (_stockGrid.SelectedRows.Count > 0)
            {
                selectedId = Convert.ToString(_stockGrid.SelectedRows[0].Tag);
            }

            _stockGrid.Rows.Clear();
            for (int i = 0; i < _state.Stocks.Count; i++)
            {
                StockConfig stock = _state.Stocks[i];
                Quote quote = _monitor.GetQuote(stock.Id);
                StockRuntimeState runtime = _state.GetRuntime(stock.Id, MarketClock.TodayKey());

                decimal change = quote != null && quote.IsValid
                    ? CalculateChange(stock, quote)
                    : (quote != null ? quote.ChangePercent : 0m);
                decimal basePrice = ResolveDisplayBase(stock, quote);

                int index = _stockGrid.Rows.Add(
                    BuildStockName(stock),
                    quote == null || !quote.IsValid ? "--" : AppResources.FormatPrice(quote.Price),
                    quote == null || !quote.IsValid ? "--" : AppResources.FormatPercent(change),
                    quote == null || quote.Open <= 0m ? "--" : AppResources.FormatPrice(quote.Open),
                    basePrice > 0m ? AppResources.FormatPrice(basePrice) : "--",
                    BuildRuntimeStatus(stock, quote, change, runtime),
                    quote == null ? "--" : AppResources.FormatTime(quote.Timestamp));

                _stockGrid.Rows[index].Tag = stock.Id;
            }

            bool empty = _state.Stocks.Count == 0;
            _emptyPanel.Visible = empty;
            _stockGrid.Visible = !empty;
            if (empty) _emptyPanel.BringToFront();

            if (!string.IsNullOrEmpty(selectedId))
            {
                SelectStock(selectedId);
            }
            else if (_stockGrid.Rows.Count > 0)
            {
                SelectStock(Convert.ToString(_stockGrid.Rows[0].Tag));
            }
            RefreshDetailPanel();
        }

        private void RefreshDetailPanel()
        {
            if (_detailNameLabel == null) return;

            StockConfig stock = GetSelectedStock();
            if (stock == null)
            {
                _detailNameLabel.Text = "请选择股票";
                _detailCodeLabel.Text = "--";
                _detailPriceLabel.Text = "--";
                _detailPriceLabel.ForeColor = AppResources.Muted;
                _detailChangeLabel.Text = "--";
                _detailChangeLabel.ForeColor = AppResources.Muted;
                _detailStatusPill.Text = "等待行情";
                _detailStatusPill.PillColor = AppResources.FieldBackground;
                _detailStatusPill.ForeColor = AppResources.Muted;
                _detailBaseLabel.Text = "--";
                _detailChangeValueLabel.Text = "--";
                _detailUpLabel.Text = "--";
                _detailDownLabel.Text = "--";
                _detailResetLabel.Text = "--";
                _detailFreshnessLabel.Text = "--";
                _detailCooldownLabel.Text = "--";
                _detailQuietLabel.Text = "--";
                _detailChart.SetQuotes(null, 0m);
                LayoutDetailStatus();
                return;
            }

            Quote quote = _monitor.GetQuote(stock.Id);
            StockRuntimeState runtime = _state.GetRuntime(stock.Id, MarketClock.TodayKey());
            decimal change = quote != null && quote.IsValid
                ? CalculateChange(stock, quote)
                : 0m;
            decimal basePrice = ResolveDisplayBase(stock, quote);
            string status = BuildRuntimeStatus(stock, quote, change, runtime);
            Color statusColor = ResolveDetailStatusColor(stock, quote, change, status);

            _detailNameLabel.Text = string.IsNullOrEmpty(stock.Name) ? stock.Code : stock.Name;
            _detailCodeLabel.Text = stock.Symbol + " · " + MarketNames.Display(stock.Market);
            _detailStatusPill.Text = status;
            _detailStatusPill.PillColor = BlendColor(statusColor, AppResources.Surface, 0.13f);
            _detailStatusPill.ForeColor = statusColor;
            LayoutDetailStatus();

            if (quote != null && quote.IsValid)
            {
                _detailPriceLabel.Text = AppResources.FormatPrice(quote.Price);
                _detailPriceLabel.ForeColor = AppResources.ChangeColor(change);
                decimal amount = basePrice > 0m ? quote.Price - basePrice : 0m;
                string arrow = change > 0m ? "↑ +" : (change < 0m ? "↓ " : string.Empty);
                _detailChangeLabel.Text = amount == 0m
                    ? AppResources.FormatPercent(change)
                    : arrow + amount.ToString("0.00") + "   " + AppResources.FormatPercent(change);
                _detailChangeLabel.ForeColor = AppResources.ChangeColor(change);
            }
            else
            {
                _detailPriceLabel.Text = "--";
                _detailPriceLabel.ForeColor = AppResources.Muted;
                _detailChangeLabel.Text = quote == null ? "等待行情" : (quote.Error ?? "行情异常");
                _detailChangeLabel.ForeColor = AppResources.Muted;
            }

            _detailBaseLabel.Text = basePrice > 0m ? AppResources.FormatPrice(basePrice) : "--";
            _detailChangeValueLabel.Text = quote != null && quote.IsValid
                ? AppResources.FormatPercent(change)
                : "--";
            _detailChangeValueLabel.ForeColor = AppResources.ChangeColor(change);
            _detailUpLabel.Text = "+" + stock.UpThreshold.ToString("0.00") + "%";
            _detailUpLabel.ForeColor = AppResources.Up;
            _detailDownLabel.Text = "-" + stock.DownThreshold.ToString("0.00") + "%";
            _detailDownLabel.ForeColor = AppResources.Down;
            _detailResetLabel.Text = stock.ResetMargin.ToString("0.00") + "%";
            _detailCooldownLabel.Text = _state.Settings.AlertCooldownSeconds.ToString() + " 秒";
            _detailQuietLabel.Text = _state.Settings.QuietHoursEnabled
                ? _state.Settings.QuietHoursStart + "–" + _state.Settings.QuietHoursEnd
                : "未启用";

            int todayCount = 0;
            TriggerRecord latest = null;
            DateTime today = MarketClock.Now().Date;
            for (int i = 0; i < _state.History.Count; i++)
            {
                TriggerRecord item = _state.History[i];
                if (item.TriggeredAt.Date != today) continue;
                todayCount++;
                if (latest == null || item.TriggeredAt > latest.TriggeredAt) latest = item;
            }
            _detailTodayLabel.Text = latest == null
                ? "今天还没有触发记录"
                : "今日触发 " + todayCount.ToString() + " 次 · 最近 "
                    + latest.TriggeredAt.ToString("HH:mm") + " "
                    + latest.StockName + " " + AppResources.FormatPercent(latest.ChangePercent);

            if (quote == null || quote.Timestamp == DateTime.MinValue)
            {
                _detailFreshnessLabel.Text = "等待行情";
                _detailFreshnessLabel.ForeColor = AppResources.Muted;
            }
            else
            {
                DateTime now = MarketClock.Now();
                int age = Math.Max(0, (int)(now - quote.Timestamp).TotalSeconds);
                bool fresh = MarketClock.IsQuoteFresh(
                    quote,
                    now,
                    TimeSpan.FromSeconds(_state.Settings.StaleQuoteSeconds));

                if (!fresh && !MarketClock.IsTradingSession(now))
                {
                    // Outside the session every quote is "stale" by design; the
                    // useful information is the market state, not a huge age.
                    _detailFreshnessLabel.Text = MarketClock.GetSessionText(now)
                        + " · " + AppResources.FormatTime(quote.Timestamp);
                    _detailFreshnessLabel.ForeColor = AppResources.Muted;
                }
                else
                {
                    _detailFreshnessLabel.Text = age.ToString() + " 秒";
                    _detailFreshnessLabel.ForeColor = fresh ? AppResources.Text : AppResources.Up;
                }
            }

            _detailChart.SetQuotes(_monitor.GetQuoteHistory(stock.Id), basePrice);
        }

        private void LayoutDetailStatus()
        {
            if (_detailStatusPill == null) return;
            Control parent = _detailStatusPill.Parent;
            if (parent == null) return;
            _detailStatusPill.Left = Math.Max(
                AppResources.S(16),
                parent.ClientSize.Width - _detailStatusPill.Width - AppResources.S(16));
        }

        private Color ResolveDetailStatusColor(
            StockConfig stock,
            Quote quote,
            decimal change,
            string status)
        {
            if (!stock.Enabled) return AppResources.Muted;
            if (quote == null) return AppResources.Muted;
            if (!quote.IsValid) return AppResources.Up;
            if (status.StartsWith("行情陈旧", StringComparison.Ordinal)) return AppResources.Up;
            if (change > stock.UpThreshold) return AppResources.Up;
            if (change < -stock.DownThreshold) return AppResources.Down;
            return AppResources.Down;
        }

        private static Color BlendColor(Color foreground, Color background, float foregroundAmount)
        {
            float amount = Math.Max(0f, Math.Min(1f, foregroundAmount));
            return Color.FromArgb(
                (int)Math.Round(foreground.R * amount + background.R * (1f - amount)),
                (int)Math.Round(foreground.G * amount + background.G * (1f - amount)),
                (int)Math.Round(foreground.B * amount + background.B * (1f - amount)));
        }

        private void RefreshHistoryGrid()
        {
            _historyGrid.Rows.Clear();
            for (int i = 0; i < _state.History.Count; i++)
            {
                TriggerRecord item = _state.History[i];
                string level = item.LevelIndex <= 0
                    ? "首次"
                    : "+" + item.LevelIndex.ToString() + "%";
                _historyGrid.Rows.Add(
                    item.TriggeredAt.ToString("MM-dd HH:mm:ss"),
                    item.StockName + " (" + item.StockCode + ")",
                    AppResources.DirectionText(item.Direction),
                    AppResources.FormatPercent(item.ChangePercent),
                    AppResources.FormatPrice(item.Price),
                    level,
                    item.NotificationNumber.ToString() + " 次",
                    ReasonText(item));
            }
        }

        private void RefreshStats()
        {
            _stockCountLabel.Text = _state.Stocks.Count.ToString();
            int todayTriggers = _state.History.Count(delegate(TriggerRecord item)
            {
                return item.TriggeredAt.Date == MarketClock.Now().Date;
            });
            _triggerCountLabel.Text = todayTriggers.ToString();
            _updateLabel.Text = _monitor.LastFetchAt == DateTime.MinValue
                ? "--"
                : AppResources.FormatTime(_monitor.LastFetchAt);
            _providerLabel.Text = _monitor.ProviderName;
            RefreshDataHealth();
            _monitorButton.Text = _state.Settings.MonitoringEnabled ? "暂停监控" : "继续监控";
            SetButtonGlyph(
                _monitorButton,
                _state.Settings.MonitoringEnabled ? AppGlyph.Pause : AppGlyph.Play,
                AppResources.HeaderText);

            DateTime now = MarketClock.Now();
            bool session = MarketClock.IsTradingSession(now);
            bool quiet = AlertPolicy.IsQuietTime(_state.Settings, now);
            _sessionLabel.Text = quiet
                ? "免打扰 · " + now.ToString("HH:mm:ss")
                : session
                ? (_state.Settings.MonitoringEnabled ? "交易中 · " : "已暂停 · ") + now.ToString("HH:mm:ss")
                : MarketClock.GetSessionText(now);
            _sessionLabel.PillColor = quiet
                ? Color.FromArgb(82, 61, 26)
                : (session && _state.Settings.MonitoringEnabled
                    ? Color.FromArgb(30, 77, 61)
                    : Color.FromArgb(31, 41, 55));
            _sessionLabel.ForeColor = quiet
                ? Color.FromArgb(245, 196, 108)
                : (session && _state.Settings.MonitoringEnabled
                    ? Color.FromArgb(126, 240, 184)
                    : AppResources.HeaderMuted);
            _sessionHintLabel.Text = "下一时段：" + MarketClock.GetNextSessionText(now);
            UpdateStatusDisplay(_monitor.StatusMessage);
            _tray.Text = _state.Settings.MonitoringEnabled ? "股票涨跌提醒 · 监控中" : "股票涨跌提醒 · 已暂停";
        }

        private static void SetButtonGlyph(Button button, AppGlyph glyph, Color color)
        {
            Image old = button.Image;
            button.Image = UiIconFactory.Create(glyph, color);
            if (old != null) old.Dispose();
        }

        private void RefreshDataHealth()
        {
            if (_monitor.LastFetchAt == DateTime.MinValue)
            {
                _healthLabel.Text = "等待行情";
                _healthLabel.ForeColor = AppResources.HeaderMuted;
                SetHeaderHealth("等待行情", false);
                return;
            }

            if (_monitor.ConsecutiveFailures > 0)
            {
                _healthLabel.Text = "行情异常 · "
                    + _monitor.ConsecutiveFailures.ToString() + " 次失败";
                _healthLabel.ForeColor = Color.FromArgb(255, 142, 142);
                SetHeaderHealth("行情异常 · "
                    + _monitor.ConsecutiveFailures.ToString() + " 次失败", false);
                return;
            }

            int age = _monitor.DataAgeSeconds;
            if (_monitor.IsDataStale)
            {
                DateTime now = MarketClock.Now();
                if (!MarketClock.IsTradingSession(now))
                {
                    string session = MarketClock.GetSessionText(now);
                    string closedText = session + " · " + AppResources.FormatTime(_monitor.LastFetchAt);
                    _healthLabel.Text = closedText;
                    _healthLabel.ForeColor = AppResources.HeaderMuted;
                    SetHeaderHealth(closedText, true);
                    return;
                }

                _healthLabel.Text = "数据陈旧 · " + age.ToString() + " 秒前";
                _healthLabel.ForeColor = Color.FromArgb(255, 142, 142);
                SetHeaderHealth("数据陈旧 · " + age.ToString() + " 秒前", false);
                return;
            }

            _healthLabel.Text = "正常 · " + Math.Max(0, age).ToString() + " 秒前";
            _healthLabel.ForeColor = AppResources.HeaderText;
            SetHeaderHealth(
                _monitor.ProviderName + " · 正常 · " + Math.Max(0, age).ToString() + " 秒前",
                true);
        }

        private void SetHeaderHealth(string text, bool healthy)
        {
            _headerHealthPill.Text = text;
            _headerHealthPill.PillColor = healthy
                ? Color.FromArgb(28, 48, 42)
                : Color.FromArgb(58, 35, 39);
            _headerHealthPill.ForeColor = healthy
                ? Color.FromArgb(126, 240, 184)
                : Color.FromArgb(255, 142, 142);
            _headerHealthPill.PillBorderColor = healthy
                ? Color.FromArgb(45, 76, 63)
                : Color.FromArgb(91, 51, 58);
            _headerHealthPill.DrawPillBorder = true;
        }

        private static decimal CalculateChange(StockConfig stock, Quote quote)
        {
            decimal basePrice = ResolveDisplayBase(stock, quote);
            if (basePrice <= 0m) return quote.ChangePercent;
            return (quote.Price / basePrice - 1m) * 100m;
        }

        private static decimal ResolveDisplayBase(StockConfig stock, Quote quote)
        {
            if (stock.CustomBaseEnabled
                && stock.CustomBasePrice > 0m
                && stock.CustomBaseDate == MarketClock.TodayKey())
            {
                return stock.CustomBasePrice;
            }
            if (quote != null && quote.PreviousClose > 0m) return quote.PreviousClose;
            return 0m;
        }

        private static string BuildStockName(StockConfig stock)
        {
            string name = string.IsNullOrEmpty(stock.Name) ? stock.Code : stock.Name;
            return name + Environment.NewLine + stock.Symbol;
        }

        private string BuildRuntimeStatus(StockConfig stock, Quote quote, decimal change, StockRuntimeState runtime)
        {
            if (!stock.Enabled) return "已停用";
            if (quote == null) return "等待行情";
            if (!quote.IsValid) return string.IsNullOrEmpty(quote.Error) ? "行情异常" : quote.Error;
            DateTime now = MarketClock.Now();
            if (!MarketClock.IsQuoteFresh(
                quote,
                now,
                TimeSpan.FromSeconds(_state.Settings.StaleQuoteSeconds)))
            {
                if (!MarketClock.IsTradingSession(now))
                {
                    return MarketClock.GetSessionText(now);
                }
                int age = quote.Timestamp == DateTime.MinValue
                    ? -1
                    : Math.Max(0, (int)(now - quote.Timestamp).TotalSeconds);
                return age < 0 ? "行情时间未知" : "行情陈旧 " + age.ToString() + " 秒";
            }
            if (change > stock.UpThreshold)
            {
                return "上涨已通知 " + runtime.Up.NotificationCount.ToString() + " 次";
            }
            if (change < -stock.DownThreshold)
            {
                return "下跌已通知 " + runtime.Down.NotificationCount.ToString() + " 次";
            }
            return "监控中";
        }

        private static string ReasonText(TriggerRecord item)
        {
            string reason = item == null ? string.Empty : item.Reason;
            string text;
            if (reason == "threshold") text = "首次突破";
            else if (reason == "level") text = "新增 1% 档位";
            else if (reason == "startup") text = "启动即时检查";
            else text = reason;

            if (item == null || string.IsNullOrEmpty(item.SuppressedReason)) return text;
            if (item.SuppressedReason == AlertPolicy.QuietHoursReason) return text + "（免打扰）";
            if (item.SuppressedReason == AlertPolicy.CooldownReason) return text + "（冷却中）";
            return text + "（" + item.SuppressedReason + "）";
        }

        private void StockGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string column = _stockGrid.Columns[e.ColumnIndex].Name;
            if (column != "change" && column != "price") return;

            DataGridViewRow row = _stockGrid.Rows[e.RowIndex];
            string priceText = Convert.ToString(row.Cells["price"].Value);
            decimal price;
            if (!decimal.TryParse(priceText, out price))
            {
                e.CellStyle.ForeColor = AppResources.Muted;
                return;
            }

            string changeText = Convert.ToString(row.Cells["change"].Value).Replace("%", string.Empty).Replace("+", string.Empty);
            decimal change;
            if (!decimal.TryParse(changeText, out change))
            {
                return;
            }
            e.CellStyle.ForeColor = AppResources.ChangeColor(change);
            e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
        }

        private void HistoryGrid_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            string column = _historyGrid.Columns[e.ColumnIndex].Name;
            if (column != "change" && column != "direction") return;

            string direction = Convert.ToString(_historyGrid.Rows[e.RowIndex].Cells["direction"].Value);
            e.CellStyle.ForeColor = direction == "上涨" ? AppResources.Up : AppResources.Down;
            e.CellStyle.SelectionForeColor = e.CellStyle.ForeColor;
        }

        private void Monitor_TriggerRaised(object sender, TriggerEventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate { Monitor_TriggerRaised(sender, e); }));
                return;
            }

            TriggerRecord record = e.Record;
            _pendingNotificationStockId = record.StockId;
            _pendingSummaryView = false;
            string title = record.StockName + " " + AppResources.DirectionText(record.Direction)
                + " " + record.ChangePercent.ToString("0.00") + "%";
            string body = "当前价格 " + AppResources.FormatPrice(record.Price);

            _tray.ShowBalloonTip(6000, title, body, ToolTipIcon.Info);
            if (_state.Settings.SoundEnabled)
            {
                SystemSounds.Exclamation.Play();
            }

            _tray.Text = "股票涨跌提醒 · 刚刚触发";
            ShowAlertBanner(title + " · " + body, 30);
            AppLogger.Info("Notification shown: " + title + " " + body);
            RefreshAll();
        }

        private void Monitor_DailySummaryRaised(object sender, DailySummaryEventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate { Monitor_DailySummaryRaised(sender, e); }));
                return;
            }

            DailySummaryReport report = e.Report;
            _pendingNotificationStockId = null;
            _pendingSummaryView = true;

            _tray.ShowBalloonTip(8000, report.Title, report.Body, ToolTipIcon.Info);
            if (_state.Settings.SoundEnabled)
            {
                SystemSounds.Exclamation.Play();
            }

            _tray.Text = "股票涨跌提醒 · 收盘汇总";
            ShowAlertBanner(report.ShortText, 60);
            RefreshAll();
        }

        private void Monitor_SnapshotUpdated(object sender, MonitorSnapshotEventArgs e)
        {
            if (IsDisposed) return;
            if (InvokeRequired)
            {
                BeginInvoke(new MethodInvoker(delegate { Monitor_SnapshotUpdated(sender, e); }));
                return;
            }

            UpdateStatusDisplay(e.IsFetching ? "正在刷新行情..." : e.StatusMessage);
            if (!e.IsFetching)
            {
                RefreshAll();
            }
        }

        private void Tray_BalloonTipClicked(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(_pendingNotificationStockId))
            {
                SelectStock(_pendingNotificationStockId);
                _pendingNotificationStockId = null;
            }
            else if (_pendingSummaryView)
            {
                _tabs.SelectedIndex = 1;
            }

            _pendingSummaryView = false;
            ShowFromTray();
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            DateTime now = MarketClock.Now();
            bool session = MarketClock.IsTradingSession(now);
            bool quiet = AlertPolicy.IsQuietTime(_state.Settings, now);
            _sessionLabel.Text = quiet
                ? "免打扰 · " + now.ToString("HH:mm:ss")
                : session
                ? (_state.Settings.MonitoringEnabled ? "交易中 · " : "已暂停 · ") + now.ToString("HH:mm:ss")
                : MarketClock.GetSessionText(now);
            _sessionLabel.PillColor = quiet
                ? Color.FromArgb(82, 61, 26)
                : (session && _state.Settings.MonitoringEnabled
                    ? Color.FromArgb(30, 77, 61)
                    : Color.FromArgb(31, 41, 55));
            _sessionLabel.ForeColor = quiet
                ? Color.FromArgb(245, 196, 108)
                : (session && _state.Settings.MonitoringEnabled
                    ? Color.FromArgb(126, 240, 184)
                    : AppResources.HeaderMuted);
            _sessionHintLabel.Text = "下一时段：" + MarketClock.GetNextSessionText(now);
            if (DateTime.Now >= _alertBannerUntil)
            {
                UpdateStatusDisplay(_monitor.StatusMessage);
                if (_state.Settings.MonitoringEnabled)
                {
                    _tray.Text = "股票涨跌提醒 · 监控中";
                }
            }
        }

        private void ShowAlertBanner(string text, int seconds)
        {
            _alertBannerText = text;
            _alertBannerUntil = DateTime.Now.AddSeconds(seconds);
            UpdateStatusDisplay(_monitor.StatusMessage);
        }

        private void UpdateStatusDisplay(string fallback)
        {
            if (DateTime.Now < _alertBannerUntil && !string.IsNullOrEmpty(_alertBannerText))
            {
                _statusLabel.Text = _alertBannerText;
                _statusLabel.ForeColor = AppResources.HeaderText;
                return;
            }

            _statusLabel.Text = fallback;
            _statusLabel.ForeColor = _monitor.ConsecutiveFailures > 0 || _monitor.IsDataStale
                ? AppResources.Up
                : AppResources.HeaderMuted;
        }

        public void SelectStock(string stockId)
        {
            if (string.IsNullOrEmpty(stockId)) return;
            _tabs.SelectedIndex = 0;
            for (int i = 0; i < _stockGrid.Rows.Count; i++)
            {
                if (Convert.ToString(_stockGrid.Rows[i].Tag) != stockId) continue;
                _stockGrid.ClearSelection();
                _stockGrid.Rows[i].Selected = true;
                _stockGrid.CurrentCell = _stockGrid.Rows[i].Cells["name"];
                _stockGrid.FirstDisplayedScrollingRowIndex = i;
                break;
            }
        }

        public void ShowFromTray()
        {
            Show();
            if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        private void ExitApplication()
        {
            _allowExit = true;
            _uiTimer.Stop();
            _monitor.Stop();
            _tray.Visible = false;
            Close();
            Application.Exit();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_uiTimer != null) _uiTimer.Dispose();
                if (_tray != null) _tray.Dispose();
                if (_trayMenu != null) _trayMenu.Dispose();
                if (_appIcon != null) _appIcon.Dispose();
                if (_trayIcon != null) _trayIcon.Dispose();
                if (_tabFont != null) _tabFont.Dispose();
                if (_tabFontBold != null) _tabFontBold.Dispose();
                if (_toolTip != null) _toolTip.Dispose();
                if (_marketData != null) _marketData.Dispose();
            }
            base.Dispose(disposing);
        }

        private static StockConfig CloneStock(StockConfig source)
        {
            StockConfig item = new StockConfig();
            item.Id = source.Id;
            item.Code = source.Code;
            item.Market = source.Market;
            item.Name = source.Name;
            item.Enabled = source.Enabled;
            item.UpThreshold = source.UpThreshold;
            item.DownThreshold = source.DownThreshold;
            item.ResetMargin = source.ResetMargin;
            item.CustomBaseEnabled = source.CustomBaseEnabled;
            item.CustomBasePrice = source.CustomBasePrice;
            item.CustomBaseDate = source.CustomBaseDate;
            item.CreatedAt = source.CreatedAt;
            return item;
        }
    }
}
