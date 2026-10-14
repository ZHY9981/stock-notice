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
            ClientSize = AppResources.SS(560, 410);
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
            form.ColumnCount = 2;
            form.RowCount = 6;
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AppResources.SF(150F)));
            form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(48F)));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(48F)));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(48F)));
            form.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(58F)));
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

            AddCaption(form, "通知声音", 1);
            _soundCheckBox = new CheckBox();
            _soundCheckBox.Text = "触发时播放提示音";
            _soundCheckBox.Checked = _state.Settings.SoundEnabled;
            _soundCheckBox.AutoSize = true;
            _soundCheckBox.ForeColor = AppResources.Text;
            form.Controls.Add(_soundCheckBox, 1, 1);

            AddCaption(form, "开机启动", 2);
            _startupCheckBox = new CheckBox();
            _startupCheckBox.Text = "登录 Windows 后自动启动";
            _startupCheckBox.Checked = RegistryHelper.IsStartupEnabled();
            _startupCheckBox.AutoSize = true;
            _startupCheckBox.ForeColor = AppResources.Text;
            form.Controls.Add(_startupCheckBox, 1, 2);

            AddCaption(form, "数据位置", 3);
            TextBox dataPath = new TextBox();
            dataPath.Text = _store.StatePath;
            dataPath.ReadOnly = true;
            dataPath.Dock = DockStyle.Fill;
            dataPath.BorderStyle = BorderStyle.FixedSingle;
            dataPath.BackColor = AppResources.FieldBackground;
            dataPath.ForeColor = AppResources.Text;
            form.Controls.Add(dataPath, 1, 3);

            Label note = new Label();
            note.Text = "行情来源于免费公开接口，可能出现数秒延迟或短时不可用。程序会自动在东方财富和腾讯行情之间切换，并保留最后一次有效价格。";
            note.ForeColor = AppResources.Muted;
            note.Font = AppResources.Font(8.5F, FontStyle.Regular);
            note.AutoSize = false;
            note.Dock = DockStyle.Fill;
            note.Padding = AppResources.SPad(0, 10, 0, 0);
            form.Controls.Add(note, 0, 4);
            form.SetColumnSpan(note, 2);

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

            form.Controls.Add(buttons, 0, 5);
            form.SetColumnSpan(buttons, 2);
            AcceptButton = save;
            CancelButton = cancel;
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
