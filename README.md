# 股票涨跌提醒（Stock Notice）

[![build](https://github.com/ZHY9981/stock-notice/actions/workflows/build.yml/badge.svg)](https://github.com/ZHY9981/stock-notice/actions/workflows/build.yml)

Windows 桌面 A 股涨跌提醒程序。监控指定股票的涨跌幅，达到设定阈值后通过
Windows 系统通知提醒，点击通知可回到主窗口并定位到对应股票。

## 当前功能

- 自选股票管理：添加、编辑、删除、启用/暂停。添加时**代码和公司名称都可以**：
  输入 `600519`、`SH600519`、`600519.SH` 直接查代码，输入「贵州茅台」「茅台」
  甚至拼音 `gzmt` 则按名称搜索。名称命中多只时弹出选择框，只保留沪A / 深A /
  京A，基金、指数、港股、新三板会被过滤掉。
- 股票名称由行情源自动识别，显示在只读的「识别结果」里（名称 · 市场），不需要
  也不允许手填 —— 手动改的名字会在下一次行情刷新时被行情源覆盖，所以界面上
  不再提供第二个名称输入框。若保存时代码尚未查询过，名称先回落为代码，首次
  取到行情后会自动补全。
- 高 DPI 清晰显示：程序声明了系统 DPI 感知，并按当前缩放比例等比缩放所有尺寸，
  所以在 125% / 150% / 200% 缩放下文字是原生绘制的，不会被 Windows 拉伸变模糊。
- 专业行情终端布局：深色状态顶栏、紧凑指标条、高密度监控表、右侧股票详情和
  本地行情折线；蓝色表示操作，红色和绿色只表示上涨与下跌。
- 逐只配置阈值：上涨阈值、下跌阈值、重新触发所需的重置幅度都按百分比单独设置。
- 自定义基准：默认用昨日收盘价，可临时指定某天的自定义基准价，仅当天生效，次日自动回到昨日收盘价。
- 交易日历：使用 AKShare 提供的完整 A 股交易日数据，自动排除周末和法定休市。
- 定时检查：交易时段内默认每 5 秒检查一次（9:30–11:30、13:00–15:00）。
- 行情新鲜度：超过设置时间的行情不会参与提醒，界面显示数据年龄和来源健康状态。
- 自动恢复：行情连续失败时继续降级数据源，并定期重建 HTTP 连接。
- 通知规则：
  - 首次达到阈值时通知一次；
  - 之后每再上涨/下跌 1% 再通知一次；进入新档位会刷新额度，所以持续上涨或下跌
    会持续提醒，不会在 3 次后彻底静音；
  - 同一档位内最多 3 次；价格回落到明显低于阈值的水平（低于“阈值 − 重置幅度”）
    后允许重新触发，次数重新计。
- 支持同股提醒间隔和免打扰时段；被抑制的提醒仍写入触发记录。
- 收盘汇总：交易日下午 15:05 推送一条汇总通知，说明当日触发次数和每只股票的
  收盘涨跌幅；点通知直接打开「触发记录」。15:00 收盘后会补取一次收盘价再停止
  轮询，不会在收盘后继续刷「行情陈旧」。
- 通知内容包含股票名称、涨/跌方向、当前涨跌幅、当前价格，并可播放提示音。
- 通知触发时同步更新托盘状态和窗口提示，作为系统通知被专注助手拦截时的兜底。
- 命中阈值会写入“触发记录”，主界面显示监控数量、当日触发次数、更新时间、行情来源。
- 启动自检：检查交易日历、数据目录和日志目录，问题会写入状态栏和日志。
- 异常日志：运行错误、行情失败、恢复和通知投递决定都会记录到日志文件。
- 常驻系统托盘：关闭主窗口继续后台监控，托盘菜单可显示主界面或退出。
- 支持开机自启动（设置窗口内开关）。

## 目录结构

```
stock-notice/
  src/                 程序源码（C#，WinForms）
    Program.cs         入口，单实例、演示数据注入
    MainForm.cs        主窗口、托盘、通知
    SettingsForm.cs    设置窗口
    StockEditForm.cs   股票编辑窗口
    MonitorService.cs  轮询与调度
    RuleEngine.cs      涨跌阈值状态机
    AlertPolicy.cs     冷却、免打扰和投递决策
    TradingCalendar.cs A 股交易日历
    MarketDataProvider.cs  行情来源（AKShare / 东方财富 / 腾讯）
    IMarketQuoteProvider.cs 行情源接口
    AkShareQuoteParser.cs  AKShare 个股行情字段解析
    DailySummary.cs    收盘汇总文本构建
    UiControls.cs      圆角面板、状态标签、图标和行情折线控件
    AppLogger.cs       滚动异常日志
    StartupCheck.cs    启动自检
    trading-calendar.json  AKShare 交易日数据（嵌入程序）
    AppResources.cs    全部界面颜色与主题（改样式只动这里）
    Models.cs, JsonStateStore.cs, MarketClock.cs, RegistryHelper.cs
    app.ico            程序图标，由 tools/make-icon.ps1 生成
  tools/
    RuleEngineTests.cs 规则状态机测试
    run-tests.ps1      编译并运行测试
    check-marketdata.ps1  联网检查行情源
    make-icon.ps1      生成多尺寸 app.ico
  build.ps1            用 Windows 自带 C# 编译器构建
  run.cmd              构建并启动
  run-demo.cmd         用演示数据启动（不联网）
```

图标已经嵌进 `StockNotice.exe`，所以桌面快捷方式、任务栏和窗口标题栏显示的是
同一个图标。改了图标设计之后重新生成即可：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\make-icon.ps1
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

## 界面样式（主题）

所有颜色集中在 `src/AppResources.cs`，主题通过启动参数选择，无需改代码：

```bat
run.cmd                                   :: default 浅色
run.cmd --theme=ink                       :: 深色，夜间看盘
run.cmd --theme=graphite                  :: 高对比浅色，边框和文字更利落
run-demo.cmd --theme=ink                  :: 配合演示数据预览
```

也可以设置环境变量 `STOCKNOTICE_THEME` 达到同样效果。三个内置主题分别是：

| 主题 | 说明 |
| --- | --- |
| `default` | 深色顶栏 + 浅灰底 + 白卡片，红涨绿跌 |
| `ink` | 整体深色，适合晚上看盘 |
| `graphite` | 高对比浅色，接近纯黑文字、更清晰的表格线 |

要微调某个主题，只改 `AppResources.cs` 里对应调色板中的颜色值即可，全部界面
（主窗口、设置、编辑对话框、托盘图标）会一起变化。

## 构建与运行

程序使用 Windows 自带的 .NET Framework C# 编译器，无需安装 .NET SDK 或任何
第三方依赖。

```bat
run.cmd           :: 构建并正常启动
run-demo.cmd      :: 用演示数据启动，便于界面预览
```

也可以单独构建：

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1
.\build\StockNotice.exe
```

运行规则测试：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\run-tests.ps1
```

联网检查行情源：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\check-marketdata.ps1 600519
```

## 数据位置

配置与触发记录保存在 `build\data\state.json`。若该目录不可写，会自动回退到
`%LocalAppData%\StockNotice\data\state.json`。

运行日志保存在 `build\logs\`。若程序目录不可写，会自动回退到
`%LocalAppData%\StockNotice\logs\`。

## 行情来源

行情按 AKShare 的 `stock_bid_ask_em` 个股报价协议实现，依次尝试
AKShare（东方财富个股接口）、东方财富批量接口、腾讯行情，并自动切换。该实现
直接使用 AKShare 所封装的数据协议，不需要安装 Python 或 AKShare 运行时。
接口可能有数秒延迟或短时不可用，此时保留最后一次有效价格。程序只在交易时段
发起检查。

交易日历来自 AKShare 的 `akshare/file_fold/calendar.json`，当前覆盖到
`2026-12-31`。每年年底需要用新版 AKShare 中的同名文件更新
`src/trading-calendar.json`，然后重新构建。启动自检会在日历临近过期时提示。

## 后续计划

1. 收盘后汇总当日涨跌、触发次数与收盘涨跌幅，统一推送到手机。
2. 扩展价格突破、成交量和组合条件提醒。
3. 使用 SQLite 保存历史行情，并增加简单回测。
