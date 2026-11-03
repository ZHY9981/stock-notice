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
            ClientSize = AppResources.SS(460, 340);
            BackColor = AppResources.Surface;
            Font = AppResources.Font(9F, FontStyle.Regular);

            BuildInterface(candidates, keyword);
        }

        private void BuildInterface(IList<StockCandidate> candidates, string keyword)
        {
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.ColumnCount = 1;
            root.RowCount = 3;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(74F)));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, AppResources.SF(62F)));
            Controls.Add(root);

            root.Controls.Add(DialogUi.CreateHeader(
                "匹配到多只股票",
                "“" + keyword + "” 匹配到 " + candidates.Count.ToString() + " 只 A 股，选一个"), 0, 0);

            TableLayoutPanel content = new TableLayoutPanel();
            content.Dock = DockStyle.Fill;
            content.BackColor = AppResources.Background;
            content.ColumnCount = 1;
            content.RowCount = 1;
            content.Padding = AppResources.SPad(22, 16, 22, 6);
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            root.Controls.Add(content, 0, 1);

            TableLayoutPanel body;
            RoundedPanel listCard = DialogUi.CreateCard("搜索结果", 250, out body);

            _list = new ListBox();
            _list.Dock = DockStyle.Fill;
            _list.Font = AppResources.Font(10F, FontStyle.Regular);
            _list.BorderStyle = BorderStyle.None;
            _list.BackColor = AppResources.Surface;
            _list.ForeColor = AppResources.Text;
            _list.IntegralHeight = false;
            _list.ItemHeight = AppResources.S(28);
            _list.DrawMode = DrawMode.OwnerDrawFixed;
            _list.DrawItem += List_DrawItem;
            _list.DisplayMember = "DisplayText";
            for (int i = 0; i < candidates.Count; i++)
            {
                _list.Items.Add(candidates[i]);
            }
            if (_list.Items.Count > 0) _list.SelectedIndex = 0;
            _list.DoubleClick += delegate { Accept(); };
            DialogUi.AddSpanRow(body, _list, 190f);
            body.RowStyles[0] = new RowStyle(SizeType.Percent, 100F);
            content.Controls.Add(listCard, 0, 0);

            Button ok = DialogUi.CreatePrimaryButton("确定", 88);
            ok.Click += delegate { Accept(); };
            Button cancel = DialogUi.CreateSecondaryButton("取消", 88);
            cancel.Click += delegate { DialogResult = DialogResult.Cancel; Close(); };
            root.Controls.Add(DialogUi.CreateFooter(null, ok, cancel), 0, 2);

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

        private void List_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= _list.Items.Count) return;

            bool selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
            using (SolidBrush background = new SolidBrush(
                selected ? AppResources.GridRowSelection : AppResources.Surface))
            {
                e.Graphics.FillRectangle(background, e.Bounds);
            }

            StockCandidate item = _list.Items[e.Index] as StockCandidate;
            if (item == null) return;

            Rectangle textBounds = new Rectangle(
                e.Bounds.Left + AppResources.S(10),
                e.Bounds.Top,
                e.Bounds.Width - AppResources.S(10),
                e.Bounds.Height);
            TextRenderer.DrawText(
                e.Graphics,
                item.DisplayText,
                _list.Font,
                textBounds,
                AppResources.Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }
    }
}
