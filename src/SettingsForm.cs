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
            ClientSize = AppResources.SS(620, 660);
            BackColor = AppResources.Surface;
            Font = AppResources.Font(9F, FontStyle.Regular);

            BuildInterface();
        }

        private void BuildInterface()
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(70F)));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(62F)));
            Controls.Add(root);

            root.Controls.Add(DialogUi.CreateHeader(
                "运行设置",
                "程序常驻托盘，关闭主窗口后仍会继续监控"), 0, 0);

            TableLayoutPanel content = new TableLayoutPanel();
            content.Dock = DockStyle.Fill;
            content.BackColor = AppResources.Background;
            content.ColumnCount = 1;
            content.RowCount = 4;
            content.Padding = AppResources.SPad(22, 16, 22, 6);
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(194F)));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(130F)));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(156F)));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(content, 0, 1);

            TableLayoutPanel body;

            RoundedPanel quoteCard = DialogUi.CreateCard("行情与提醒", 194, out body);
            Label interval = new Label();
            interval.Text = "5 秒（固定）";
            interval.ForeColor = AppResources.Muted;
            interval.Font = AppResources.Font(9F, FontStyle.Regular);
            interval.AutoSize = true;
            DialogUi.AddRow(body, "行情检查间隔", interval);

            _cooldownSeconds = CreateSecondsInput(0, 3600, _state.Settings.AlertCooldownSeconds);
            DialogUi.AddRow(body, "同股提醒间隔", WithUnit(_cooldownSeconds));

            _staleSeconds = CreateSecondsInput(5, 300, _state.Settings.StaleQuoteSeconds);
            DialogUi.AddRow(body, "行情有效期", WithUnit(_staleSeconds));

            _soundCheckBox = new CheckBox();
            _soundCheckBox.Text = "触发时播放提示音";
            _soundCheckBox.Checked = _state.Settings.SoundEnabled;
            _soundCheckBox.AutoSize = true;
            _soundCheckBox.ForeColor = AppResources.Text;
            _soundCheckBox.BackColor = AppResources.Surface;
            DialogUi.AddSpanRow(body, _soundCheckBox, 38f);
            content.Controls.Add(quoteCard, 0, 0);

            RoundedPanel quietCard = DialogUi.CreateCard("免打扰", 130, out body);
            _quietHoursCheckBox = new CheckBox();
            _quietHoursCheckBox.Text = "启用免打扰";
            _quietHoursCheckBox.Checked = _state.Settings.QuietHoursEnabled;
            _quietHoursCheckBox.AutoSize = true;
            _quietHoursCheckBox.ForeColor = AppResources.Text;
            _quietHoursCheckBox.BackColor = AppResources.Surface;
            _quietHoursCheckBox.CheckedChanged += delegate { UpdateQuietEditors(); };
            DialogUi.AddSpanRow(body, _quietHoursCheckBox, 44f);

            FlowLayoutPanel quietEditors = new FlowLayoutPanel();
            quietEditors.Dock = DockStyle.Fill;
            quietEditors.FlowDirection = FlowDirection.LeftToRight;
            quietEditors.WrapContents = false;
            quietEditors.BackColor = AppResources.Surface;

            _quietStartPicker = CreateTimePicker(_state.Settings.QuietHoursStart);
            quietEditors.Controls.Add(_quietStartPicker);

            Label separator = new Label();
            separator.Text = "至";
            separator.ForeColor = AppResources.Muted;
            separator.TextAlign = ContentAlignment.MiddleCenter;
            separator.Width = AppResources.S(30);
            quietEditors.Controls.Add(separator);

            _quietEndPicker = CreateTimePicker(_state.Settings.QuietHoursEnd);
            quietEditors.Controls.Add(_quietEndPicker);
            DialogUi.AddRow(body, "时段", quietEditors, true, 44f);
            content.Controls.Add(quietCard, 0, 1);

            RoundedPanel startupCard = DialogUi.CreateCard("启动与数据", 156, out body);
            _startupCheckBox = new CheckBox();
            _startupCheckBox.Text = "登录 Windows 后自动启动";
            _startupCheckBox.Checked = RegistryHelper.IsStartupEnabled();
            _startupCheckBox.AutoSize = true;
            _startupCheckBox.ForeColor = AppResources.Text;
            _startupCheckBox.BackColor = AppResources.Surface;
            DialogUi.AddSpanRow(body, _startupCheckBox, 38f);
            DialogUi.AddRow(body, "数据位置", CreatePathBox(_store.StatePath), true, 38f);
            DialogUi.AddRow(body, "日志位置", CreatePathBox(AppLogger.LogPath), true, 38f);
            content.Controls.Add(startupCard, 0, 2);

            Button test = DialogUi.CreateLinkButton("发送测试通知");
            test.Click += delegate
            {
                if (_testNotification != null) _testNotification();
            };
            Button save = DialogUi.CreatePrimaryButton("保存", 88);
            save.Click += SaveButton_Click;
            Button cancel = DialogUi.CreateSecondaryButton("取消", 88);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            root.Controls.Add(DialogUi.CreateFooter(test, save, cancel), 0, 2);

            AcceptButton = save;
            CancelButton = cancel;
            UpdateQuietEditors();
        }

        private static Control WithUnit(Control input)
        {
            FlowLayoutPanel row = new FlowLayoutPanel();
            row.Dock = DockStyle.Fill;
            row.FlowDirection = FlowDirection.LeftToRight;
            row.WrapContents = false;
            row.BackColor = AppResources.Surface;
            input.Width = AppResources.S(96);
            row.Controls.Add(input);

            Label unit = new Label();
            unit.Text = "秒";
            unit.ForeColor = AppResources.Muted;
            unit.Font = AppResources.Font(9F, FontStyle.Regular);
            unit.AutoSize = true;
            unit.Margin = AppResources.SPad(8, 6, 0, 0);
            row.Controls.Add(unit);
            return row;
        }

        private static TextBox CreatePathBox(string text)
        {
            TextBox box = new TextBox();
            box.Text = text;
            box.ReadOnly = true;
            box.BorderStyle = BorderStyle.FixedSingle;
            box.BackColor = AppResources.FieldBackground;
            box.ForeColor = AppResources.Muted;
            box.Font = AppResources.Font(8.5F, FontStyle.Regular);
            return box;
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
