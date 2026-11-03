# Stock Notice 维护说明

## 项目定位

这是一个 Windows A 股涨跌提醒工具，使用 C#、WinForms 和 .NET Framework
构建。程序需要保持单文件部署，不引入 Python、Node.js、NuGet 或其他运行时依赖。

## 标准命令

在仓库根目录执行：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run-tests.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\check-marketdata.ps1 600519
```

- `build.ps1` 构建 `build\StockNotice.exe`，会关闭正在运行的 StockNotice。
- `run-tests.ps1` 编译并运行规则状态机和行情字段解析测试。
- `check-marketdata.ps1` 需要联网，不属于离线测试。
- 只修改文档时不必构建；修改代码后必须构建并运行完整离线测试。

## 核心模块

- `Program.cs`：入口、单实例和演示数据。
- `MainForm.cs`：主窗口、托盘、通知和用户交互。
- `MonitorService.cs`：轮询调度、状态保存和通知触发。
- `RuleEngine.cs`：涨跌阈值状态机，是核心业务逻辑。
- `AlertPolicy.cs`：冷却时间和免打扰时段的投递决策。
- `DailySummary.cs`：收盘汇总文本构建（纯逻辑，可离线测试）。
- `TradingCalendar.cs`：嵌入式 A 股交易日历。
- `MarketDataProvider.cs`：行情源选择和降级。
- `IMarketQuoteProvider.cs`：可替换行情源接口。
- `AkShareQuoteParser.cs`：AKShare 个股报价协议解析。
- `AppLogger.cs`：运行日志和日志目录回退。
- `StartupCheck.cs`：启动自检。
- `UiControls.cs`：通用视觉控件、本地图标绘制，以及对话框共用的 `DialogUi`。
- `JsonStateStore.cs`：`state.json` 持久化和容错。
- `AppResources.cs`：颜色、主题和高 DPI 尺寸。

## 行情约束

- 行情源顺序固定为 AKShare、东方财富批量接口、腾讯行情。
- AKShare 通过原生 HTTP 协议接入，不安装或调用 Python 版 AKShare。
- 某个行情源失败或没有有效价格时，必须继续尝试后续来源。
- 所有来源失败时保留最后一次有效行情，不能清空用户看到的价格。
- 所有行情请求必须设置明确超时，不能无限等待；调整轮询间隔时同步检查超时设置。
- 超过 `StaleQuoteSeconds` 的行情不得参与提醒，但可以继续显示为陈旧数据。
- 交易时段以下午 15:00 为界（15:00:00 本身不算交易中）；收盘后 2 分钟内补取
  一次收盘价，之后停止轮询。
- 行情陈旧只影响提醒判断；stale 日志每分钟最多写一条，避免收盘后刷屏。
- 收盘汇总在交易日 15:05–17:00 之间只发一次，日期写入 `Settings.LastSummaryDate`。
- 连续行情失败时必须继续重试，并在达到阈值后重建 HTTP 连接。
- 修改行情代码后，至少联网检查一只沪市股票和一只深市股票。
- 交易日历使用 `src/trading-calendar.json`，每年从新版 AKShare 更新并重新构建。

## 兼容约束

- 使用 Windows 自带 `csc.exe` 可编译的 C# 5 语法。
- 不使用字符串插值、表达式成员、记录类型、`nameof` 或其他新语法。
- 不修改 `state.json` 字段名和含义；必须变更时提供向后兼容的归一化逻辑。
- 不直接编辑或重置 `build\data\state.json` 中的用户配置和历史记录。
- 保留单实例、系统托盘、关闭后后台监控和开机启动行为。
- 不删除现有主题，不把界面颜色分散到各个窗口。
- 操作按钮使用蓝色 `Accent`；红色和绿色仅用于上涨、下跌和对应风险状态。
- 主窗口保持“顶部状态、紧凑指标、左侧监控表、右侧详情”的终端布局。
- 冷却和免打扰只能抑制通知投递，不能丢失触发记录。
- 提醒额度：首次越过阈值通知一次；之后每进入新的 1% 档位通知一次并刷新额度，
  同一档位内最多 3 次；回落到阈值减重置幅度以下重新计数。
- 日志写入失败不能让监控线程退出。

## 开发方式

- 优先在稳定的公开接缝测试：`RuleEngine.Evaluate`、
  `AkShareQuoteParser.Parse` 和未来的行情源接口。
- 修复复杂问题时先建立可重复的失败检查，再做最小修复。
- 新行为先补行为测试，避免测试私有实现细节。
- 网络接口测试与离线测试分开，CI 只运行稳定的离线测试。
- 对话框布局改动后用 `tools\DialogPreview.cs` 单独渲染截图验收：
  `csc /main:DialogPreview ... src\*.cs tools\DialogPreview.cs`，产物不进 `build.ps1`。
- 避免为当前不需要的扩展提前增加抽象。
- 未经用户明确要求，不提交 Git commit，不执行推送或重写历史。

## 完成标准

代码修改完成前至少确认：

1. 构建成功。
2. 离线测试全部通过。
3. `git diff --check` 没有空白错误。
4. 行情相关修改通过真实沪市、深市抽查。
5. README 和界面说明与行为保持一致。
