using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace StockNotice
{
    // Shown when a name search matches more than one A-share.
    public class StockPickForm : Form
    {
        private ListBox _list;

        public StockCandidate Selected { get; private set; }

        public StockPickForm(IList<StockCandidate> candidates, string keyword)
        {
            Selected = null;

            Text = "选择股票";
            Icon = AppResources.CreateAppIcon();
            AutoScaleMode = AutoScaleMode.None;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            ClientSize = AppResources.SS(440, 400);
            BackColor = AppResources.Surface;
            Font = AppResources.Font(9F, FontStyle.Regular);

            BuildInterface(candidates, keyword);
        }

        private void BuildInterface(IList<StockCandidate> candidates, string keyword)
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(66F)));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Controls.Add(root);

            Panel header = new Panel();
            header.Dock = DockStyle.Fill;
            header.BackColor = AppResources.Header;

            Label title = new Label();
            title.Text = "匹配到多只股票";
            title.ForeColor = AppResources.HeaderText;
            title.Font = AppResources.Font(14F, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = AppResources.SP(24, 11);
            header.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "“" + keyword + "” 匹配到 " + candidates.Count + " 只 A 股，选一个";
            subtitle.ForeColor = AppResources.HeaderMuted;
            subtitle.Font = AppResources.Font(8.5F, FontStyle.Regular);
            subtitle.AutoSize = true;
            subtitle.Location = AppResources.SP(25, 40);
            header.Controls.Add(subtitle);
            root.Controls.Add(header, 0, 0);

            _list = new ListBox();
            _list.Dock = DockStyle.Fill;
            _list.Font = AppResources.Font(10F, FontStyle.Regular);
            _list.BorderStyle = BorderStyle.FixedSingle;
            _list.BackColor = AppResources.Surface;
            _list.ForeColor = AppResources.Text;
            _list.IntegralHeight = false;
            _list.DisplayMember = "DisplayText";
            for (int i = 0; i < candidates.Count; i++)
            {
                _list.Items.Add(candidates[i]);
            }
            if (_list.Items.Count > 0) _list.SelectedIndex = 0;
            _list.DoubleClick += delegate { Accept(); };

            Panel listHost = new Panel();
            listHost.Dock = DockStyle.Fill;
            listHost.Padding = AppResources.SPad(22, 14, 22, 8);
            listHost.Controls.Add(_list);

            FlowLayoutPanel buttons = new FlowLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.FlowDirection = FlowDirection.RightToLeft;
            buttons.WrapContents = false;
            buttons.Padding = AppResources.SPad(0, 6, 22, 0);

            Button ok = CreatePrimaryButton("确定");
            ok.Click += delegate { Accept(); };
            buttons.Controls.Add(ok);

            Button cancel = CreateSecondaryButton("取消");
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            buttons.Controls.Add(cancel);

            TableLayoutPanel body = new TableLayoutPanel();
            body.Dock = DockStyle.Fill;
            body.ColumnCount = 1;
            body.RowCount = 2;
            body.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(56F)));
            body.Controls.Add(listHost, 0, 0);
            body.Controls.Add(buttons, 0, 1);
            root.Controls.Add(body, 0, 1);

            AcceptButton = ok;
            CancelButton = cancel;
        }

        private void Accept()
        {
            StockCandidate chosen = _list.SelectedItem as StockCandidate;
            if (chosen == null) return;
            Selected = chosen;
            DialogResult = DialogResult.OK;
            Close();
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
    }
}
