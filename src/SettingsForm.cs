using System;
using System.Drawing;
using System.Windows.Forms;

namespace StockNotice
{
    public class SettingsForm : Form
    {
        private readonly AppState _state;
        private readonly JsonStateStore _store;
        private readonly Action _testNotification;
        private CheckBox _soundCheckBox;
        private CheckBox _startupCheckBox;
        private CheckBox _quietHoursCheckBox;
        private NumericUpDown _cooldownSeconds;
        private NumericUpDown _staleSeconds;
        private DateTimePicker _quietStartPicker;
        private DateTimePicker _quietEndPicker;

        public SettingsForm(AppState state, JsonStateStore store, Action testNotification)
        {
            _state = state;
            _store = store;
            _testNotification = testNotification;

            Text = "设置";
            Icon = AppResources.CreateAppIcon();
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = AppResources.SS(600, 570);
            BackColor = AppResources.Surface;
            Font = AppResources.Font(9F, FontStyle.Regular);

            BuildInterface();
        }

        private void BuildInterface()
        {
            // Same two-row root panel as the stock editor: without it the
            // Fill-docked settings table slides under the header and its first
            // row gets covered.
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(70F)));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Controls.Add(root);

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = AppResources.Header;
            root.Controls.Add(header, 0, 0);

            Label title = new Label();
            title.Text = "运行设置";
            title.ForeColor = AppResources.HeaderText;
            title.Font = AppResources.Font(15F, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = AppResources.SP(24, 12);
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "程序常驻托盘，关闭主窗口后仍会继续监控";
            subtitle.ForeColor = AppResources.HeaderMuted;
            subtitle.Font = AppResources.Font(8.5F, FontStyle.Regular);
            subtitle.AutoSize = true;
            subtitle.Location = AppResources.SP(25, 44);
            header.Controls.Add(subtitle);

            TableLayoutPanel form = new TableLayoutPanel();
            form.Dock = DockStyle.Fill;
            form.Padding = AppResources.SPad(24, 20, 24, 18);
            form.ColumnCount = 4;
            form.RowCount = 10;
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AppResources.SF(150F)));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AppResources.SF(120F)));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AppResources.SF(100F)));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(46F)));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(46F)));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(46F)));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(46F)));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(52F)));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(46F)));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(46F)));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(46F)));
            form.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(46F)));
            root.Controls.Add(form, 0, 1);

            AddCaption(form, "行情检查间隔", 0);
            Label interval = new Label();
            interval.Text = "5 秒（第一版固定）";
            interval.ForeColor = AppResources.Text;
            interval.TextAlign = ContentAlignment.MiddleLeft;
            interval.Dock = DockStyle.Fill;
            form.Controls.Add(interval, 1, 0);
            form.SetColumnSpan(interval, 3);

            AddCaption(form, "同股提醒间隔", 1);
            _cooldownSeconds = CreateSecondsInput(0, 3600, _state.Settings.AlertCooldownSeconds);
            form.Controls.Add(_cooldownSeconds, 1, 1);
            Label cooldownHint = CreateHintLabel("秒，同一方向触发后需等待");
            form.Controls.Add(cooldownHint, 2, 1);
            form.SetColumnSpan(cooldownHint, 2);

            AddCaption(form, "行情新鲜度", 2);
            _staleSeconds = CreateSecondsInput(5, 300, _state.Settings.StaleQuoteSeconds);
            form.Controls.Add(_staleSeconds, 1, 2);
            Label staleHint = CreateHintLabel("秒，超过后不参与提醒");
            form.Controls.Add(staleHint, 2, 2);
            form.SetColumnSpan(staleHint, 2);

            AddCaption(form, "通知声音", 3);
            _soundCheckBox = new CheckBox();
            _soundCheckBox.Text = "触发时播放提示音";
            _soundCheckBox.Checked = _state.Settings.SoundEnabled;
            _soundCheckBox.AutoSize = true;
            _soundCheckBox.ForeColor = AppResources.Text;
            form.Controls.Add(_soundCheckBox, 1, 3);
            form.SetColumnSpan(_soundCheckBox, 3);

            AddCaption(form, "免打扰时段", 4);
            _quietHoursCheckBox = new CheckBox();
            _quietHoursCheckBox.Text = "启用";
            _quietHoursCheckBox.Checked = _state.Settings.QuietHoursEnabled;
            _quietHoursCheckBox.AutoSize = true;
            _quietHoursCheckBox.ForeColor = AppResources.Text;
            _quietHoursCheckBox.CheckedChanged += delegate { UpdateQuietEditors(); };
            form.Controls.Add(_quietHoursCheckBox, 1, 4);

            FlowLayoutPanel quietEditors = new FlowLayoutPanel();
            quietEditors.Dock = DockStyle.Fill;
            quietEditors.FlowDirection = FlowDirection.LeftToRight;
            quietEditors.WrapContents = false;

            _quietStartPicker = CreateTimePicker(_state.Settings.QuietHoursStart);
            quietEditors.Controls.Add(_quietStartPicker);

            Label separator = new Label();
            separator.Text = "至";
            separator.ForeColor = AppResources.Muted;
            separator.TextAlign = ContentAlignment.MiddleCenter;
            separator.Width = AppResources.S(28);
            quietEditors.Controls.Add(separator);

            _quietEndPicker = CreateTimePicker(_state.Settings.QuietHoursEnd);
            quietEditors.Controls.Add(_quietEndPicker);
            form.Controls.Add(quietEditors, 2, 4);
            form.SetColumnSpan(quietEditors, 2);

            AddCaption(form, "开机启动", 5);
            _startupCheckBox = new CheckBox();
            _startupCheckBox.Text = "登录 Windows 后自动启动";
            _startupCheckBox.Checked = RegistryHelper.IsStartupEnabled();
            _startupCheckBox.AutoSize = true;
            _startupCheckBox.ForeColor = AppResources.Text;
            form.Controls.Add(_startupCheckBox, 1, 5);
            form.SetColumnSpan(_startupCheckBox, 3);

            AddCaption(form, "数据位置", 6);
            TextBox dataPath = new TextBox();
            dataPath.Text = _store.StatePath;
            dataPath.ReadOnly = true;
            dataPath.Dock = DockStyle.Fill;
            dataPath.BorderStyle = BorderStyle.FixedSingle;
            dataPath.BackColor = AppResources.FieldBackground;
            dataPath.ForeColor = AppResources.Text;
            form.Controls.Add(dataPath, 1, 6);
            form.SetColumnSpan(dataPath, 3);

            AddCaption(form, "日志位置", 7);
            TextBox logPath = new TextBox();
            logPath.Text = AppLogger.LogPath;
            logPath.ReadOnly = true;
            logPath.Dock = DockStyle.Fill;
            logPath.BorderStyle = BorderStyle.FixedSingle;
            logPath.BackColor = AppResources.FieldBackground;
            logPath.ForeColor = AppResources.Text;
            form.Controls.Add(logPath, 1, 7);
            form.SetColumnSpan(logPath, 3);

            Label note = new Label();
            note.Text = "行情按 AKShare 个股报价协议获取，并按 AKShare、东方财富、腾讯行情顺序自动降级。免打扰和冷却只会抑制弹窗与声音，触发记录仍会保留。";
            note.ForeColor = AppResources.Muted;
            note.Font = AppResources.Font(8.5F, FontStyle.Regular);
            note.AutoSize = false;
            note.Dock = DockStyle.Fill;
            note.Padding = AppResources.SPad(0, 10, 0, 0);
            form.Controls.Add(note, 0, 8);
            form.SetColumnSpan(note, 4);

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

            Button test = CreateSecondaryButton("测试通知");
            test.Click += delegate
            {
                if (_testNotification != null) _testNotification();
            };
            buttons.Controls.Add(test);

            form.Controls.Add(buttons, 0, 9);
            form.SetColumnSpan(buttons, 4);
            AcceptButton = save;
            CancelButton = cancel;
            UpdateQuietEditors();
        }

        private static void AddCaption(TableLayoutPanel form, string text, int row)
        {
            Label label = new Label();
            label.Text = text;
            label.ForeColor = AppResources.Text;
            label.Font = AppResources.Font(9F, FontStyle.Bold);
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Dock = DockStyle.Fill;
            form.Controls.Add(label, 0, row);
        }

        private static NumericUpDown CreateSecondsInput(int minimum, int maximum, int value)
        {
            NumericUpDown input = new NumericUpDown();
            input.Minimum = minimum;
            input.Maximum = maximum;
            input.Increment = 5m;
            input.Value = Math.Min(maximum, Math.Max(minimum, value));
            input.Dock = DockStyle.Fill;
            input.Font = AppResources.Font(10F, FontStyle.Regular);
            return input;
        }

        private static Label CreateHintLabel(string text)
        {
            Label label = new Label();
            label.Text = text;
            label.ForeColor = AppResources.Muted;
            label.Font = AppResources.Font(8F, FontStyle.Regular);
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Dock = DockStyle.Fill;
            return label;
        }

        private static DateTimePicker CreateTimePicker(string value)
        {
            DateTimePicker picker = new DateTimePicker();
            picker.Width = AppResources.S(92);
            picker.Format = DateTimePickerFormat.Custom;
            picker.CustomFormat = "HH:mm";
            picker.ShowUpDown = true;

            TimeSpan parsed;
            picker.Value = AlertPolicy.TryParseClock(value, out parsed)
                ? DateTime.Today.Add(parsed)
                : DateTime.Today;
            return picker;
        }

        private void UpdateQuietEditors()
        {
            bool enabled = _quietHoursCheckBox != null && _quietHoursCheckBox.Checked;
            if (_quietStartPicker != null) _quietStartPicker.Enabled = enabled;
            if (_quietEndPicker != null) _quietEndPicker.Enabled = enabled;
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
            button.Width = AppResources.S(102);
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

        private void SaveButton_Click(object sender, EventArgs e)
        {
            _state.Settings.PollIntervalSeconds = 5;
            _state.Settings.SoundEnabled = _soundCheckBox.Checked;
            _state.Settings.StartWithWindows = _startupCheckBox.Checked;
            _state.Settings.AlertCooldownSeconds = Decimal.ToInt32(_cooldownSeconds.Value);
            _state.Settings.StaleQuoteSeconds = Decimal.ToInt32(_staleSeconds.Value);
            _state.Settings.QuietHoursEnabled = _quietHoursCheckBox.Checked;
            _state.Settings.QuietHoursStart = _quietStartPicker.Value.ToString("HH:mm");
            _state.Settings.QuietHoursEnd = _quietEndPicker.Value.ToString("HH:mm");

            if (_state.Settings.QuietHoursEnabled
                && _state.Settings.QuietHoursStart == _state.Settings.QuietHoursEnd)
            {
                MessageBox.Show(
                    this,
                    "免打扰开始和结束时间不能相同。",
                    "设置",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }
            _state.Settings.Normalize();

            bool startupSaved = RegistryHelper.SetStartupEnabled(_startupCheckBox.Checked);
            try
            {
                _store.Save(_state);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "设置保存失败：" + ex.Message, "设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!startupSaved)
            {
                MessageBox.Show(this, "通知和配置已保存，但开机启动设置失败。", "设置", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
