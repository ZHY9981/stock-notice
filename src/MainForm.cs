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
        private Label _sessionLabel;
        private Label _sessionHintLabel;
        private Label _statusLabel;
        private Button _monitorButton;
        private NotifyIcon _tray;
        private ContextMenuStrip _trayMenu;
        private Timer _uiTimer;
        private bool _allowExit;
        private bool _trayHintShown;
        private string _pendingNotificationStockId;
        private readonly Font _tabFont;
        private readonly Font _tabFontBold;

        public MainForm(AppState state, JsonStateStore store, MarketDataService marketData, MonitorService monitor, bool demoMode)
        {
            _state = state;
            _store = store;
            _marketData = marketData;
            _monitor = monitor;
            _demoMode = demoMode;
            _appIcon = AppResources.CreateAppIcon();
            _trayIcon = AppResources.CreateTrayIcon();
            _tabFont = AppResources.Font(9F, FontStyle.Regular);
            _tabFontBold = AppResources.Font(9F, FontStyle.Bold);

            Text = "股票涨跌提醒";
            Icon = _appIcon;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(1080, 680);
            Size = new Size(1240, 790);
            BackColor = AppResources.Background;
            Font = AppResources.Font(9F, FontStyle.Regular);

            BuildInterface();
            BuildTray();

            Load += MainForm_Load;
            FormClosing += MainForm_FormClosing;
            Resize += MainForm_Resize;

            _monitor.TriggerRaised += Monitor_TriggerRaised;
            _monitor.SnapshotUpdated += Monitor_SnapshotUpdated;

            _uiTimer = new Timer();
            _uiTimer.Interval = 1000;
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            _monitor.RebuildStockIndex();
            if (!_demoMode) _monitor.Start();
            RefreshAll();
        }

        private void BuildInterface()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 5;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 76F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 72F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            Controls.Add(root);

            root.Controls.Add(BuildHeader(), 0, 0);
            root.Controls.Add(BuildToolbar(), 0, 1);
            root.Controls.Add(BuildStatsStrip(), 0, 2);
            root.Controls.Add(BuildContent(), 0, 3);
            root.Controls.Add(BuildStatusBar(), 0, 4);
        }

        private Control BuildHeader()
        {
            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = AppResources.Header;
            header.Padding = new Padding(24, 14, 20, 10);

            Label title = new Label();
            title.Text = "A股涨跌提醒";
            title.ForeColor = AppResources.HeaderText;
            title.Font = AppResources.Font(17F, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(24, 13);
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "每 5 秒检查一次，触发后发送 Windows 通知";
            subtitle.ForeColor = AppResources.HeaderMuted;
            subtitle.Font = AppResources.Font(9F, FontStyle.Regular);
            subtitle.AutoSize = true;
            subtitle.Location = new Point(26, 46);
            header.Controls.Add(subtitle);

            FlowLayoutPanel right = new FlowLayoutPanel();
            right.Dock = DockStyle.Right;
            right.FlowDirection = FlowDirection.LeftToRight;
            right.WrapContents = false;
            right.AutoSize = true;
            right.Padding = new Padding(0, 12, 0, 0);

            _monitorButton = CreateButton("暂停监控", 112, 36, false);
            _monitorButton.Click += MonitorButton_Click;
            right.Controls.Add(_monitorButton);

            Button settings = CreateButton("设置", 76, 36, false);
            settings.Click += SettingsButton_Click;
            right.Controls.Add(settings);

            header.Controls.Add(right);
            return header;
        }

        private Control BuildToolbar()
        {
            Panel toolbar = new Panel();
            toolbar.Dock = DockStyle.Fill;
            toolbar.BackColor = AppResources.Surface;
            toolbar.Padding = new Padding(22, 12, 22, 12);

            FlowLayoutPanel left = new FlowLayoutPanel();
            left.Dock = DockStyle.Left;
            left.AutoSize = true;
            left.WrapContents = false;
            left.FlowDirection = FlowDirection.LeftToRight;

            Button add = CreateButton("添加股票", 106, 38, true);
            add.Click += AddButton_Click;
            left.Controls.Add(add);

            Button refresh = CreateButton("立即刷新", 106, 38, false);
            refresh.Click += RefreshButton_Click;
            left.Controls.Add(refresh);

            Button edit = CreateButton("编辑", 76, 38, false);
            edit.Click += EditButton_Click;
            left.Controls.Add(edit);

            Button delete = CreateButton("删除", 76, 38, false);
            delete.Click += DeleteButton_Click;
            left.Controls.Add(delete);

            toolbar.Controls.Add(left);

            Label hint = new Label();
            hint.Dock = DockStyle.Right;
            hint.TextAlign = ContentAlignment.MiddleRight;
            hint.ForeColor = AppResources.Muted;
            hint.Text = "通知点击后会自动定位到对应股票";
            hint.Font = AppResources.Font(8.5F, FontStyle.Regular);
            toolbar.Controls.Add(hint);

            return toolbar;
        }

        private Control BuildStatsStrip()
        {
            TableLayoutPanel stats = new TableLayoutPanel();
            stats.Dock = DockStyle.Fill;
            stats.ColumnCount = 4;
            stats.RowCount = 1;
            stats.BackColor = AppResources.Background;
            stats.Padding = new Padding(22, 8, 22, 8);
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
            stats.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            stats.Controls.Add(CreateStatBlock("监控股票", out _stockCountLabel), 0, 0);
            stats.Controls.Add(CreateStatBlock("今日触发", out _triggerCountLabel), 1, 0);
            stats.Controls.Add(CreateStatBlock("行情更新时间", out _updateLabel), 2, 0);
            stats.Controls.Add(CreateStatBlock("行情来源", out _providerLabel), 3, 0);
            return stats;
        }

        private Control CreateStatBlock(string caption, out Label valueLabel)
        {
            Panel block = new Panel();
            block.Dock = DockStyle.Fill;
            block.Margin = new Padding(0, 0, 10, 0);
            block.BackColor = AppResources.Surface;
            block.Padding = new Padding(14, 7, 14, 6);

            Label captionLabel = new Label();
            captionLabel.Text = caption;
            captionLabel.ForeColor = AppResources.Muted;
            captionLabel.Font = AppResources.Font(8F, FontStyle.Regular);
            captionLabel.AutoSize = true;
            captionLabel.Location = new Point(14, 7);
            block.Controls.Add(captionLabel);

            valueLabel = new Label();
            valueLabel.Text = "--";
            valueLabel.ForeColor = AppResources.Text;
            valueLabel.Font = AppResources.Font(11F, FontStyle.Bold);
            valueLabel.AutoEllipsis = true;
            valueLabel.AutoSize = false;
            valueLabel.Height = 23;
            valueLabel.Dock = DockStyle.Bottom;
            block.Controls.Add(valueLabel);
            valueLabel.BringToFront();

            return block;
        }

        private Control BuildContent()
        {
            Panel content = new Panel();
            content.Dock = DockStyle.Fill;
            content.Padding = new Padding(22, 0, 22, 0);
            content.BackColor = AppResources.Background;

            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Fill;
            _tabs.Font = AppResources.Font(9F, FontStyle.Regular);
            _tabs.Padding = new Point(18, 6);
            _tabs.DrawMode = TabDrawMode.OwnerDrawFixed;
            _tabs.SizeMode = TabSizeMode.Fixed;
            _tabs.ItemSize = new Size(116, 32);
            _tabs.DrawItem += Tabs_DrawItem;

            TabPage stocksTab = new TabPage("监控列表");
            stocksTab.BackColor = AppResources.Surface;
            stocksTab.Padding = new Padding(1);
            stocksTab.Controls.Add(BuildStockGrid());
            stocksTab.Controls.Add(BuildEmptyPanel());
            _tabs.TabPages.Add(stocksTab);

            TabPage historyTab = new TabPage("触发记录");
            historyTab.BackColor = AppResources.Surface;
            historyTab.Padding = new Padding(1);
            historyTab.Controls.Add(BuildHistoryGrid());
            _tabs.TabPages.Add(historyTab);

            content.Controls.Add(_tabs);
            return content;
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

                using (SolidBrush strip = new SolidBrush(AppResources.Background))
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

            using (SolidBrush back = new SolidBrush(selected ? AppResources.Surface : AppResources.Background))
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
            AddGridColumn(_stockGrid, "name", "股票", 190F);
            AddGridColumn(_stockGrid, "price", "最新价", 90F);
            AddGridColumn(_stockGrid, "change", "涨跌幅", 90F);
            AddGridColumn(_stockGrid, "base", "基准价", 90F);
            AddGridColumn(_stockGrid, "up", "上涨阈值", 90F);
            AddGridColumn(_stockGrid, "down", "下跌阈值", 90F);
            AddGridColumn(_stockGrid, "reset", "重置幅度", 90F);
            AddGridColumn(_stockGrid, "state", "状态", 120F);
            AddGridColumn(_stockGrid, "time", "更新时间", 90F);

            _stockGrid.CellDoubleClick += StockGrid_CellDoubleClick;
            _stockGrid.CellFormatting += StockGrid_CellFormatting;
            _stockGrid.CellMouseDown += StockGrid_CellMouseDown;
            return _stockGrid;
        }

        private Control BuildHistoryGrid()
        {
            _historyGrid = CreateGrid();
            AddGridColumn(_historyGrid, "time", "触发时间", 130F);
            AddGridColumn(_historyGrid, "stock", "股票", 190F);
            AddGridColumn(_historyGrid, "direction", "方向", 80F);
            AddGridColumn(_historyGrid, "change", "涨跌幅", 90F);
            AddGridColumn(_historyGrid, "price", "价格", 90F);
            AddGridColumn(_historyGrid, "level", "档位", 80F);
            AddGridColumn(_historyGrid, "number", "通知序号", 90F);
            AddGridColumn(_historyGrid, "reason", "触发原因", 120F);
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

            _emptyPanel.Resize += delegate
            {
                Label titleLabel = _emptyPanel.Controls["emptyTitle"] as Label;
                Label hintLabel = _emptyPanel.Controls["emptyHint"] as Label;
                if (titleLabel == null || hintLabel == null) return;
                int centerX = Math.Max(0, (_emptyPanel.ClientSize.Width - 360) / 2);
                titleLabel.Location = new Point(centerX, Math.Max(60, _emptyPanel.ClientSize.Height / 2 - 32));
                hintLabel.Location = new Point(centerX, titleLabel.Bottom + 10);
            };

            return _emptyPanel;
        }

        private Control BuildStatusBar()
        {
            Panel status = new Panel();
            status.Dock = DockStyle.Fill;
            status.BackColor = AppResources.Header;
            status.Padding = new Padding(22, 0, 22, 0);

            _sessionLabel = new Label();
            _sessionLabel.Text = "读取交易时段";
            _sessionLabel.ForeColor = AppResources.HeaderText;
            _sessionLabel.Font = AppResources.Font(8.5F, FontStyle.Bold);
            _sessionLabel.AutoSize = true;
            _sessionLabel.Location = new Point(22, 8);
            status.Controls.Add(_sessionLabel);

            _sessionHintLabel = new Label();
            _sessionHintLabel.Text = string.Empty;
            _sessionHintLabel.ForeColor = AppResources.HeaderMuted;
            _sessionHintLabel.Font = AppResources.Font(8F, FontStyle.Regular);
            _sessionHintLabel.AutoSize = true;
            _sessionHintLabel.Location = new Point(120, 8);
            status.Controls.Add(_sessionHintLabel);

            _statusLabel = new Label();
            _statusLabel.Text = "正在初始化";
            _statusLabel.TextAlign = ContentAlignment.MiddleRight;
            _statusLabel.Dock = DockStyle.Right;
            _statusLabel.Width = 560;
            _statusLabel.ForeColor = AppResources.HeaderMuted;
            _statusLabel.Font = AppResources.Font(8F, FontStyle.Regular);
            status.Controls.Add(_statusLabel);
            _statusLabel.BringToFront();
            return status;
        }

        private static DataGridView CreateGrid()
        {
            DataGridView grid = new DataGridView();
            grid.Dock = DockStyle.Fill;
            grid.BackgroundColor = AppResources.Surface;
            grid.BorderStyle = BorderStyle.None;
            grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
            grid.GridColor = AppResources.Border;
            grid.RowHeadersVisible = false;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.ReadOnly = true;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoGenerateColumns = false;
            grid.ColumnHeadersHeight = 40;
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            grid.RowTemplate.Height = 42;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = AppResources.GridHeaderBackground;
            grid.ColumnHeadersDefaultCellStyle.ForeColor = AppResources.Muted;
            grid.ColumnHeadersDefaultCellStyle.Font = AppResources.Font(8.5F, FontStyle.Bold);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = AppResources.GridHeaderBackground;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = AppResources.Muted;
            grid.DefaultCellStyle.BackColor = AppResources.Surface;
            grid.DefaultCellStyle.ForeColor = AppResources.Text;
            grid.DefaultCellStyle.SelectionBackColor = AppResources.GridRowSelection;
            grid.DefaultCellStyle.SelectionForeColor = AppResources.Text;
            grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            grid.AlternatingRowsDefaultCellStyle.BackColor = AppResources.GridAltRow;
            return grid;
        }

        private static void AddGridColumn(DataGridView grid, string name, string title, float weight)
        {
            DataGridViewTextBoxColumn column = new DataGridViewTextBoxColumn();
            column.Name = name;
            column.HeaderText = title;
            column.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            column.FillWeight = weight;
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
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
            Button button = new Button();
            button.Text = text;
            button.Width = width;
            button.Height = height;
            button.Margin = new Padding(0, 0, 8, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = primary ? 0 : 1;
            button.FlatAppearance.BorderColor = AppResources.ButtonBorder;
            button.BackColor = primary ? AppResources.Accent : AppResources.Surface;
            button.ForeColor = primary ? AppResources.OnAccent : AppResources.Text;
            button.Font = AppResources.Font(9F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
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
            if (_state.Settings.SoundEnabled) SystemSounds.Asterisk.Play();
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
                    basePrice > 0m ? AppResources.FormatPrice(basePrice) : "--",
                    "+" + stock.UpThreshold.ToString("0.00") + "%",
                    "-" + stock.DownThreshold.ToString("0.00") + "%",
                    stock.ResetMargin.ToString("0.00") + "%",
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
                    item.NotificationNumber.ToString() + "/3",
                    ReasonText(item.Reason));
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
            _updateLabel.Text = AppResources.FormatTime(_monitor.LastFetchAt);
            _providerLabel.Text = _monitor.ProviderName;
            _monitorButton.Text = _state.Settings.MonitoringEnabled ? "暂停监控" : "继续监控";

            DateTime now = MarketClock.Now();
            bool session = MarketClock.IsTradingSession(now);
            _sessionLabel.Text = session
                ? (_state.Settings.MonitoringEnabled ? "交易中 · 监控运行" : "交易中 · 已暂停")
                : MarketClock.GetSessionText(now) + " · 已暂停检查";
            _sessionLabel.ForeColor = session && _state.Settings.MonitoringEnabled
                ? AppResources.SessionActive
                : AppResources.HeaderText;
            _sessionHintLabel.Text = "下一时段 " + MarketClock.GetNextSessionText(now);
            _statusLabel.Text = _monitor.StatusMessage;
            _tray.Text = _state.Settings.MonitoringEnabled ? "股票涨跌提醒 · 监控中" : "股票涨跌提醒 · 已暂停";
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

        private static string BuildRuntimeStatus(StockConfig stock, Quote quote, decimal change, StockRuntimeState runtime)
        {
            if (!stock.Enabled) return "已停用";
            if (quote == null) return "等待行情";
            if (!quote.IsValid) return string.IsNullOrEmpty(quote.Error) ? "行情异常" : quote.Error;
            if (change > stock.UpThreshold)
            {
                return "上涨已通知 " + runtime.Up.NotificationCount.ToString() + "/3";
            }
            if (change < -stock.DownThreshold)
            {
                return "下跌已通知 " + runtime.Down.NotificationCount.ToString() + "/3";
            }
            return "监控中";
        }

        private static string ReasonText(string reason)
        {
            if (reason == "threshold") return "首次突破";
            if (reason == "level") return "新增 1% 档位";
            if (reason == "startup") return "启动即时检查";
            return reason;
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
            string title = record.StockName + " " + AppResources.DirectionText(record.Direction)
                + " " + record.ChangePercent.ToString("0.00") + "%";
            string body = "当前价格 " + AppResources.FormatPrice(record.Price);

            _tray.ShowBalloonTip(6000, title, body, ToolTipIcon.Info);
            if (_state.Settings.SoundEnabled)
            {
                SystemSounds.Asterisk.Play();
            }

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

            _statusLabel.Text = e.IsFetching ? "正在刷新行情..." : e.StatusMessage;
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
            ShowFromTray();
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            DateTime now = MarketClock.Now();
            bool session = MarketClock.IsTradingSession(now);
            _sessionLabel.Text = session
                ? (_state.Settings.MonitoringEnabled ? "交易中 · 监控运行" : "交易中 · 已暂停")
                : MarketClock.GetSessionText(now) + " · 已暂停检查";
            _sessionHintLabel.Text = "下一时段 " + MarketClock.GetNextSessionText(now);
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
