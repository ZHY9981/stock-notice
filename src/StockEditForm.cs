using System;
using System.Drawing;
using System.Windows.Forms;

namespace StockNotice
{
    public class StockEditForm : Form
    {
        private readonly MarketDataService _marketData;
        private readonly bool _isNew;
        private TextBox _codeTextBox;
        private Label _identifiedLabel;
        private NumericUpDown _upThreshold;
        private NumericUpDown _downThreshold;
        private NumericUpDown _resetMargin;
        private CheckBox _enabledCheckBox;
        private CheckBox _customBaseCheckBox;
        private NumericUpDown _customBasePrice;
        private Label _lookupStatusLabel;
        private Button _lookupButton;
        private string _identifiedCode;
        private string _identifiedName;

        public StockConfig Stock { get; private set; }

        public StockEditForm(MarketDataService marketData, StockConfig existing)
        {
            _marketData = marketData;
            _isNew = existing == null;
            Stock = existing == null ? new StockConfig() : existing;

            Text = _isNew ? "添加股票" : "编辑股票";
            Icon = AppResources.CreateAppIcon();
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = AppResources.SS(560, 520);
            BackColor = AppResources.Surface;
            Font = AppResources.Font(9F, FontStyle.Regular);

            BuildInterface();
            LoadStock();
        }

        private void BuildInterface()
        {
            // Header and form live in a two-row root panel. Adding both straight
            // to the form would let the Fill-docked table take the whole client
            // area and hide its first row underneath the header.
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(74F)));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Controls.Add(root);

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = AppResources.Header;

            Label title = new Label();
            title.Text = _isNew ? "添加监控股票" : "编辑监控股票";
            title.ForeColor = AppResources.HeaderText;
            title.Font = AppResources.Font(15F, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = AppResources.SP(24, 13);
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "上涨、下跌阈值按百分比填写，重置幅度用于开启新一轮通知";
            subtitle.ForeColor = AppResources.HeaderMuted;
            subtitle.Font = AppResources.Font(8.5F, FontStyle.Regular);
            subtitle.AutoSize = true;
            subtitle.Location = AppResources.SP(25, 45);
            header.Controls.Add(subtitle);
            root.Controls.Add(header, 0, 0);

            TableLayoutPanel form = new TableLayoutPanel();
            form.Dock = DockStyle.Fill;
            form.Padding = AppResources.SPad(24, 20, 24, 12);
            form.ColumnCount = 3;
            form.RowCount = 10;
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AppResources.SF(116F)));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AppResources.SF(90F)));
            for (int i = 0; i < 9; i++)
            {
                form.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(44F)));
            }

            AddLabel(form, "代码 / 名称", 0);
            _codeTextBox = CreateTextBox();
            _codeTextBox.CharacterCasing = CharacterCasing.Upper;
            form.Controls.Add(_codeTextBox, 1, 0);

            _lookupButton = CreateActionButton("查询");
            _lookupButton.Click += LookupButton_Click;
            form.Controls.Add(_lookupButton, 2, 0);

            // Read-only on purpose: the name and market are always derived from
            // the code or the search box, and the monitor overwrites the stored
            // name from each quote, so an editable name box would be a lie.
            AddLabel(form, "识别结果", 1);
            _identifiedLabel = new Label();
            _identifiedLabel.TextAlign = ContentAlignment.MiddleLeft;
            _identifiedLabel.Dock = DockStyle.Fill;
            _identifiedLabel.AutoEllipsis = true;
            form.Controls.Add(_identifiedLabel, 1, 1);
            form.SetColumnSpan(_identifiedLabel, 2);

            AddLabel(form, "上涨阈值", 2);
            _upThreshold = CreatePercentInput();
            form.Controls.Add(_upThreshold, 1, 2);
            form.Controls.Add(CreateSuffixLabel("达到后开始提醒"), 2, 2);

            AddLabel(form, "下跌阈值", 3);
            _downThreshold = CreatePercentInput();
            form.Controls.Add(_downThreshold, 1, 3);
            form.Controls.Add(CreateSuffixLabel("达到后开始提醒"), 2, 3);

            AddLabel(form, "重置幅度", 4);
            _resetMargin = CreatePercentInput();
            form.Controls.Add(_resetMargin, 1, 4);
            form.Controls.Add(CreateSuffixLabel("回落多少后重置"), 2, 4);

            AddLabel(form, "当日自定义基准", 5);
            _customBaseCheckBox = new CheckBox();
            _customBaseCheckBox.Text = "启用";
            _customBaseCheckBox.AutoSize = true;
            _customBaseCheckBox.ForeColor = AppResources.Text;
            _customBaseCheckBox.CheckedChanged += CustomBaseCheckBox_CheckedChanged;
            form.Controls.Add(_customBaseCheckBox, 1, 5);

            _customBasePrice = new NumericUpDown();
            _customBasePrice.DecimalPlaces = 3;
            _customBasePrice.Increment = 0.01m;
            _customBasePrice.Minimum = 0.001m;
            _customBasePrice.Maximum = 100000m;
            _customBasePrice.Value = 1m;
            _customBasePrice.Dock = DockStyle.Fill;
            _customBasePrice.Font = AppResources.Font(10F, FontStyle.Regular);
            _customBasePrice.Enabled = false;
            form.Controls.Add(_customBasePrice, 2, 5);

            AddLabel(form, "监控状态", 6);
            _enabledCheckBox = new CheckBox();
            _enabledCheckBox.Text = "启用这只股票";
            _enabledCheckBox.AutoSize = true;
            _enabledCheckBox.ForeColor = AppResources.Text;
            form.Controls.Add(_enabledCheckBox, 1, 6);
            form.SetColumnSpan(_enabledCheckBox, 2);

            _lookupStatusLabel = new Label();
            _lookupStatusLabel.Text = "输入 6 位代码（如 600519）或公司名称（如 贵州茅台），点击“查询”。";
            _lookupStatusLabel.ForeColor = AppResources.Muted;
            _lookupStatusLabel.Font = AppResources.Font(8.5F, FontStyle.Regular);
            _lookupStatusLabel.AutoSize = false;
            _lookupStatusLabel.Dock = DockStyle.Fill;
            _lookupStatusLabel.TextAlign = ContentAlignment.MiddleLeft;
            form.Controls.Add(_lookupStatusLabel, 0, 7);
            form.SetColumnSpan(_lookupStatusLabel, 3);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;

            Button save = CreatePrimaryButton("保存");
            save.Click += SaveButton_Click;
            buttons.Controls.Add(save);

            Button cancel = CreateSecondaryButton("取消");
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            buttons.Controls.Add(cancel);
            form.Controls.Add(buttons, 0, 8);
            form.SetColumnSpan(buttons, 3);

            root.Controls.Add(form, 0, 1);

            AcceptButton = save;
            CancelButton = cancel;
        }

        private static void AddLabel(TableLayoutPanel form, string text, int row)
        {
            Label label = new Label();
            label.Text = text;
            label.ForeColor = AppResources.Text;
            label.Font = AppResources.Font(9F, FontStyle.Bold);
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Dock = DockStyle.Fill;
            form.Controls.Add(label, 0, row);
        }

        private static TextBox CreateTextBox()
        {
            TextBox textBox = new TextBox();
            textBox.Dock = DockStyle.Fill;
            textBox.Font = AppResources.Font(10F, FontStyle.Regular);
            textBox.BorderStyle = BorderStyle.FixedSingle;
            return textBox;
        }

        private static NumericUpDown CreatePercentInput()
        {
            NumericUpDown input = new NumericUpDown();
            input.DecimalPlaces = 2;
            input.Increment = 0.1m;
            input.Minimum = 0.01m;
            input.Maximum = 30m;
            input.Dock = DockStyle.Fill;
            input.Font = AppResources.Font(10F, FontStyle.Regular);
            return input;
        }

        private static Label CreateSuffixLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.ForeColor = AppResources.Muted;
            label.Font = AppResources.Font(8F, FontStyle.Regular);
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Dock = DockStyle.Fill;
            return label;
        }

        private static Button CreateActionButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.Dock = DockStyle.Fill;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = AppResources.ButtonBorder;
            button.BackColor = AppResources.FieldBackground;
            button.ForeColor = AppResources.Text;
            button.Font = AppResources.Font(9F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            return button;
        }

        private static Button CreatePrimaryButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = AppResources.S(92);
            button.Height = AppResources.S(38);
            button.Margin = AppResources.SPad(8, 0, 0, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = AppResources.Accent;
            button.ForeColor = AppResources.OnAccent;
            button.Font = AppResources.Font(9F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            return button;
        }

        private static Button CreateSecondaryButton(string text)
        {
            Button button = new Button();
            button.Text = text;
            button.Width = AppResources.S(82);
            button.Height = AppResources.S(38);
            button.Margin = AppResources.SPad(8, 0, 0, 0);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = AppResources.ButtonBorder;
            button.BackColor = AppResources.Surface;
            button.ForeColor = AppResources.Text;
            button.Font = AppResources.Font(9F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.UseVisualStyleBackColor = false;
            return button;
        }

        private void LoadStock()
        {
            _codeTextBox.Text = Stock.Code;
            _identifiedCode = Stock.Code;
            _identifiedName = Stock.Name;
            UpdateIdentifiedLabel();
            _upThreshold.Value = Clamp(Stock.UpThreshold, _upThreshold.Minimum, _upThreshold.Maximum);
            _downThreshold.Value = Clamp(Stock.DownThreshold, _downThreshold.Minimum, _downThreshold.Maximum);
            _resetMargin.Value = Clamp(Stock.ResetMargin, _resetMargin.Minimum, _resetMargin.Maximum);
            _enabledCheckBox.Checked = Stock.Enabled;
            _customBaseCheckBox.Checked = Stock.CustomBaseEnabled;
            if (Stock.CustomBasePrice >= _customBasePrice.Minimum)
            {
                _customBasePrice.Value = Math.Min(Stock.CustomBasePrice, _customBasePrice.Maximum);
            }
            _customBasePrice.Enabled = _customBaseCheckBox.Checked;
            UpdateBaseHint();
        }

        private void UpdateIdentifiedLabel()
        {
            bool named = !string.IsNullOrEmpty(Stock.Name)
                && !string.Equals(Stock.Name, Stock.Code, StringComparison.Ordinal);

            if (named)
            {
                _identifiedLabel.Text = Stock.Name + " · " + MarketNames.Display(Stock.Market);
                _identifiedLabel.ForeColor = AppResources.Text;
            }
            else
            {
                _identifiedLabel.Text = "尚未识别 · 点“查询”按代码或名称识别";
                _identifiedLabel.ForeColor = AppResources.Muted;
            }
        }

        private static decimal Clamp(decimal value, decimal minimum, decimal maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }

        private void CustomBaseCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            _customBasePrice.Enabled = _customBaseCheckBox.Checked;
        }

        private async void LookupButton_Click(object sender, EventArgs e)
        {
            string raw = _codeTextBox.Text.Trim();
            if (raw.Length == 0)
            {
                SetStatus("先输入股票代码或公司名称，再点“查询”。", AppResources.Up);
                return;
            }

            SetBusy(true);

            try
            {
                StockConfig probe;
                string error;
                if (MarketDataService.TryNormalizeStock(raw, out probe, out error))
                {
                    SetStatus("正在从免费行情源获取最新行情...", AppResources.Muted);
                    await LookupCodeAsync(raw);
                }
                else
                {
                    await SearchByNameAsync(raw);
                }
            }
            catch (Exception ex)
            {
                SetStatus("查询失败：" + ex.Message, AppResources.Up);
            }
            finally
            {
                SetBusy(false);
            }
        }

        // A name (or partial name / pinyin) is resolved through the suggestion
        // endpoint; a code goes straight to the quote lookup.
        private async System.Threading.Tasks.Task SearchByNameAsync(string keyword)
        {
            SetStatus("正在按名称搜索 A 股...", AppResources.Muted);

            System.Collections.Generic.List<StockCandidate> candidates = await _marketData.SearchAsync(keyword);
            if (candidates.Count == 0)
            {
                SetStatus("没有找到匹配的 A 股。换个关键词试试，或直接输入 6 位代码。", AppResources.Up);
                return;
            }

            StockCandidate chosen = candidates[0];
            if (candidates.Count > 1)
            {
                using (StockPickForm picker = new StockPickForm(candidates, keyword))
                {
                    if (picker.ShowDialog(this) != DialogResult.OK || picker.Selected == null)
                    {
                        SetStatus("已取消选择。", AppResources.Muted);
                        return;
                    }
                    chosen = picker.Selected;
                }
            }

            _codeTextBox.Text = chosen.Code;
            await LookupCodeAsync(chosen.Code);
        }

        private async System.Threading.Tasks.Task LookupCodeAsync(string code)
        {
            Quote quote = await _marketData.LookupAsync(code);
            if (quote != null && quote.IsValid)
            {
                _codeTextBox.Text = quote.Code;
                Stock.Code = quote.Code;
                Stock.Market = quote.Market;
                Stock.Name = quote.Name;
                _identifiedCode = quote.Code;
                _identifiedName = quote.Name;
                UpdateIdentifiedLabel();
                SetStatus(
                    "查询成功：最新价 " + AppResources.FormatPrice(quote.Price)
                        + "，昨日收盘 " + AppResources.FormatPrice(quote.PreviousClose),
                    AppResources.Down);
                return;
            }

            SetStatus(
                quote == null || string.IsNullOrEmpty(quote.Error)
                    ? "未查询到行情，请检查代码或网络。"
                    : quote.Error,
                AppResources.Up);
        }

        private void SetBusy(bool busy)
        {
            _lookupButton.Enabled = !busy;
            _lookupButton.Text = busy ? "查询中" : "查询";
        }

        private void SetStatus(string text, Color color)
        {
            _lookupStatusLabel.ForeColor = color;
            _lookupStatusLabel.Text = text;
        }

        private void SaveButton_Click(object sender, EventArgs e)
        {
            StockConfig normalized;
            string error;
            if (!MarketDataService.TryNormalizeStock(_codeTextBox.Text, out normalized, out error))
            {
                ShowError(error);
                return;
            }

            decimal up = _upThreshold.Value;
            decimal down = _downThreshold.Value;
            decimal margin = _resetMargin.Value;
            if (margin >= up || margin >= down)
            {
                ShowError("重置幅度必须小于上涨阈值和下跌阈值。");
                return;
            }

            if (_customBaseCheckBox.Checked && _customBasePrice.Value <= 0m)
            {
                ShowError("自定义基准价必须大于 0。");
                return;
            }

            Stock.Code = normalized.Code;
            Stock.Market = normalized.Market;
            // Only keep the resolved name when it belongs to the code that is
            // actually being saved; otherwise the user edited the code after the
            // last lookup and the stale name would follow the wrong stock.
            bool resolvedForThisCode = !string.IsNullOrEmpty(_identifiedCode)
                && string.Equals(_identifiedCode, normalized.Code, StringComparison.Ordinal)
                && !string.IsNullOrEmpty(_identifiedName);
            Stock.Name = resolvedForThisCode ? _identifiedName : normalized.Code;
            Stock.UpThreshold = up;
            Stock.DownThreshold = down;
            Stock.ResetMargin = margin;
            Stock.Enabled = _enabledCheckBox.Checked;
            Stock.CustomBaseEnabled = _customBaseCheckBox.Checked;
            Stock.CustomBasePrice = _customBaseCheckBox.Checked ? _customBasePrice.Value : 0m;
            Stock.CustomBaseDate = _customBaseCheckBox.Checked
                ? MarketClock.TodayKey()
                : string.Empty;

            DialogResult = DialogResult.OK;
            Close();
        }

        private void UpdateBaseHint()
        {
            if (Stock.CustomBaseEnabled && Stock.CustomBaseDate == MarketClock.TodayKey())
            {
                _lookupStatusLabel.Text = "当前使用今日自定义基准价；次日自动恢复为新的昨日收盘价。";
            }
        }

        private void ShowError(string message)
        {
            MessageBox.Show(this, message, "输入有误", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
