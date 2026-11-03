using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using StockNotice;

// Renders the settings and stock dialogs and saves a screenshot of each.
// Build with the same sources as the app, /main:DialogPreview.
internal static class DialogPreview
{
    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    [STAThread]
    private static void Main(string[] args)
    {
        SetProcessDPIAware();
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        AppLogger.Initialize();

        string outputDirectory = args.Length > 0 ? args[0] : ".";
        JsonStateStore store = new JsonStateStore();

        AppState state = new AppState();
        StockConfig first = new StockConfig();
        first.Code = "600150";
        first.Market = MarketNames.Shanghai;
        first.Name = "中国船舶";
        first.UpThreshold = 3.0m;
        first.DownThreshold = 2.0m;
        first.ResetMargin = 1.0m;
        state.Stocks.Add(first);

        using (SettingsForm settings = new SettingsForm(state, store, null))
        {
            Capture(settings, System.IO.Path.Combine(outputDirectory, "settings-window.png"));
        }

        using (MarketDataService marketData = new MarketDataService())
        using (StockEditForm editor = new StockEditForm(marketData, null))
        {
            Capture(editor, System.IO.Path.Combine(outputDirectory, "stock-edit-window.png"));
        }

        System.Collections.Generic.List<StockCandidate> candidates =
            new System.Collections.Generic.List<StockCandidate>();
        candidates.Add(Candidate("600519", "贵州茅台", MarketNames.Shanghai));
        candidates.Add(Candidate("600809", "山西汾酒", MarketNames.Shanghai));
        candidates.Add(Candidate("000858", "五粮液", MarketNames.Shenzhen));
        using (StockPickForm picker = new StockPickForm(candidates, "酒"))
        {
            Capture(picker, System.IO.Path.Combine(outputDirectory, "stock-pick-window.png"));
        }
    }

    private static StockCandidate Candidate(string code, string name, string market)
    {
        StockCandidate candidate = new StockCandidate();
        candidate.Code = code;
        candidate.Name = name;
        candidate.Market = market;
        return candidate;
    }

    private static void Capture(Form form, string path)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(40, 40);
        form.TopMost = true;
        form.Show();
        form.Activate();
        form.BringToFront();
        for (int i = 0; i < 15; i++)
        {
            Application.DoEvents();
            Thread.Sleep(60);
        }

        Rectangle bounds = form.Bounds;
        using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
        using (Graphics graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, form.Size);
            bitmap.Save(path, ImageFormat.Png);
        }

        form.TopMost = false;
        form.Hide();
    }
}
