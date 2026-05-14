# Playwright.Activities Release Note

本文档说明当前版本 `Playwright.Activities` 的 Activity 清单、用途、入参、出参和参数含义。

## 说明

- 所有 `Timeout` 单位均为毫秒（ms）。
- `Validate`（点击后校验）可选值：
  - `None`: 不做点击后状态校验
  - `CurrentElementDisappear`: 点击后当前元素消失即成功
  - `SelectorAppear`: 点击后指定 `ValidationSelector` 出现即成功
- `Cookies` 是自定义对象，提供格式转换方法：
  - `AsList()`: `List<Dictionary<string,string>>`
  - `AsDict()`: `Dictionary<string,string>`
  - `AsString()`: `string`

---

## Browser 类 Activity

### Browser.Open
- 功能：打开浏览器并返回 `PwBrowser` 实例。
- 入参：
  - `Headless` (`bool`, 默认 `false`)：是否无头模式。
  - `BrowserPath` (`string`, 默认 `C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe`)：浏览器可执行文件路径。
  - `StartMaximized` (`bool`, 默认 `true`)：是否最大化启动。
  - `RemoteDebuggingPort` (`int?`)：远程调试端口。
  - `UseSystemDir` (`bool`, 默认 `true`)：
    - `true` 使用系统 Edge 用户目录（持久化）
    - `false` 使用 `UserDataDir`（有值）或 Playwright 临时上下文（无值）
  - `UserDataDir` (`string`)：自定义用户数据目录（仅 `UseSystemDir=false` 时有意义）。
- 出参：
  - `Browser` (`PwBrowser`)

### Browser.NewTab
- 功能：新建标签页，可选自动导航 URL。
- 入参：
  - `Browser` (`PwBrowser`)：浏览器对象。
  - `Url` (`string`)：可选初始地址。
  - `Timeout` (`int?`, 默认 `15000`)：导航超时。
- 出参：
  - `Tab` (`PwTab`)

### Browser.GetAllTab
- 功能：获取所有标签页。
- 入参：
  - `Browser` (`PwBrowser`)
- 出参：
  - `Tabs` (`PwTab[]`)

### Browser.SwitchTabByIndex
- 功能：按索引切换标签页。
- 入参：
  - `Browser` (`PwBrowser`)
  - `Index` (`int`)：目标索引。
- 出参：
  - `Tab` (`PwTab`)

### Browser.SwitchTab
- 功能：按条件切换标签页（多条件匹配）。
- 入参：
  - `Browser` (`PwBrowser`)
  - `Index` (`int?`)：索引（可选）
  - `Title` (`string`)：标题精确匹配
  - `TitleRe` (`string`)：标题正则
  - `Url` (`string`)：URL 精确匹配
  - `UrlRe` (`string`)：URL 正则
- 出参：
  - `Tab` (`PwTab`)

### Browser.GetLatestTab
- 功能：获取最新标签页并切到前台。
- 入参：
  - `Browser` (`PwBrowser`)
- 出参：
  - `Tab` (`PwTab`)

### Browser.GetCookies
- 功能：获取浏览器级 Cookie 集合对象。
- 入参：
  - `Browser` (`PwBrowser`)
- 出参：
  - `Cookies` (`Cookies`)：可通过 `AsList/AsDict/AsString` 转换。

### Browser.Close
- 功能：关闭浏览器并释放相关资源。
- 入参：
  - `Browser` (`PwBrowser`)
- 出参：无

---

## Tab 类 Activity

### Tab.Activate
- 功能：激活标签页（Bring to front）。
- 入参：
  - `Tab` (`PwTab`)
- 出参：无

### Tab.FindElement
- 功能：在页面中查找元素。
- 入参：
  - `Tab` (`PwTab`)
  - `Selector` (`string`)：Playwright 选择器。
  - `Index` (`int`, 默认 `0`)：匹配集合索引。
  - `Timeout` (`int?`, 默认 `15000`)：等待超时。
  - `WaitState` (`string`)：`visible/attached/hidden/detached`。
  - `DelayBefore` (`int`, 默认 `0`)：执行前延迟。
- 出参：
  - `Element` (`PwElement`)

### Tab.ElementExists
- 功能：判断元素是否存在。
- 入参：
  - `Tab` (`PwTab`)
  - `Selector` (`string`)
  - `Index` (`int`, 默认 `0`)
- 出参：
  - `Exists` (`bool`)

### Tab.RunJs
- 功能：在页面执行 JS 并返回结果。
- 入参：
  - `Tab` (`PwTab`)
  - `Script` (`string`)
  - `Arg` (`object`)：脚本参数。
- 出参：
  - `Result` (`object`)

### Tab.GetCookies
- 功能：获取标签页级 Cookie 集合对象。
- 入参：
  - `Tab` (`PwTab`)
- 出参：
  - `Cookies` (`Cookies`)

### Tab.NavigateUrl
- 功能：导航到指定 URL。
- 入参：
  - `Tab` (`PwTab`)
  - `Url` (`string`)
  - `Timeout` (`int?`, 默认 `15000`)
- 出参：无

### Tab.Back
- 功能：后退。
- 入参：
  - `Tab` (`PwTab`)
- 出参：无

### Tab.Forward
- 功能：前进。
- 入参：
  - `Tab` (`PwTab`)
- 出参：无

### Tab.Refresh
- 功能：刷新。
- 入参：
  - `Tab` (`PwTab`)
- 出参：无

### Tab.Close
- 功能：关闭当前标签页。
- 入参：
  - `Tab` (`PwTab`)
- 出参：无

### Tab.GetInfo
- 功能：获取标签页信息。
- 入参：
  - `Tab` (`PwTab`)
- 出参：
  - `Info` (`TabInfo`)

### Tab.SendKeys
- 功能：向页面发送按键/组合键（如 `Control+A`, `Enter`）。
- 入参：
  - `Tab` (`PwTab`)
  - `Keys` (`string`)
  - `Delay` (`int?`)：按键延迟。
- 出参：无

---

## Element 类 Activity

### Element.Exists
- 功能：判断元素是否存在。
- 入参：`Element` (`PwElement`)
- 出参：`Exists` (`bool`)

### Element.Click
- 功能：点击元素（支持后置校验循环）。
- 入参：
  - `Element` (`PwElement`)
  - `Button` (`MouseButton`, 默认 `Left`)
  - `Count` (`int`, 默认 `1`)
  - `Interval` (`int`, 默认 `500`)：点击重试间隔。
  - `ValidationSelector` (`string`)：`Validate=SelectorAppear` 时使用。
  - `Modifiers` (`string[]`)：修饰键，支持 `alt/ctrl/control/meta/shift`。
  - `Force` (`bool`, 默认 `false`)
  - `Validate` (`ClickValidateMode`, 默认 `None`)
  - `Timeout` (`int`, 默认 `15000`)：点击校验总超时。
- 出参：无

### Element.DoubleClick
- 功能：双击元素（支持后置校验循环）。
- 入参与 `Element.Click` 基本一致。
- 出参：无

### Element.ClickForNewTab
- 功能：点击并等待新标签页。
- 入参：同 `Element.Click`。
- 出参：
  - `Tab` (`PwTab`)

### Element.ClickForDownload
- 功能：点击并等待下载。
- 入参：
  - `Element` (`PwElement`)
  - `SaveAsPath` (`string`)：可选保存路径。
  - 其他参数同 `Element.Click`。
- 出参：
  - `Download` (`DownloadInfo`)

### Element.Input
- 功能：输入内容（`Fill` / `Type`）。
- 入参：
  - `Element` (`PwElement`)
  - `Value` (`string`)
  - `InputMethod` (`InputMethod`, 默认 `Fill`)
  - `TypeDelay` (`float?`)：仅 `Type` 模式使用。
  - `ValidateContentAfterInputted` (`bool`, 默认 `false`)：`Fill` 后校验值是否一致。
  - `Interval` (`int`, 默认 `500`)：输入校验重试间隔。
- 出参：无

### Element.InputGetValue
- 功能：读取输入控件值。
- 入参：`Element` (`PwElement`)
- 出参：`Value` (`string`)

### Element.Select
- 功能：选择下拉选项（值/文本/索引）。
- 入参：
  - `Element` (`PwElement`)
  - `Values` (`string[]`)
  - `Texts` (`string[]`)
  - `Indices` (`int[]`)
  - `ValidateContentAfterSelected` (`bool`, 默认 `false`)：选择后校验。
  - `Interval` (`int`, 默认 `500`)：选择校验重试间隔。
- 出参：无

### Element.SelectGetSelected
- 功能：获取当前已选项信息。
- 入参：`Element` (`PwElement`)
- 出参：
  - `Selected` (`List<Dictionary<string, object>>`)

### Element.Check
- 功能：勾选。
- 入参：`Element` (`PwElement`)
- 出参：无

### Element.Uncheck
- 功能：取消勾选。
- 入参：`Element` (`PwElement`)
- 出参：无

### Element.IsChecked
- 功能：判断是否勾选。
- 入参：`Element` (`PwElement`)
- 出参：`IsChecked` (`bool`)

### Element.FindElement
- 功能：在当前元素内部查找子元素。
- 入参：
  - `Element` (`PwElement`)
  - `Selector` (`string`)
  - `Index` (`int`, 默认 `0`)
  - `Timeout` (`int?`, 默认 `15000`)
  - `WaitState` (`string`)
  - `DelayBefore` (`int`, 默认 `0`)
- 出参：
  - `FoundElement` (`PwElement`)

### Element.GetParent
- 功能：获取父元素。
- 入参：
  - `Element` (`PwElement`)
  - `Level` (`int`, 默认 `1`)：向上层级。
- 出参：
  - `Parent` (`PwElement`)

### Element.GetChildren
- 功能：获取子元素集合。
- 入参：
  - `Element` (`PwElement`)
  - `Selector` (`string`)：可选过滤选择器。
  - `Deepdive` (`bool`, 默认 `false`)：是否深度查找。
- 出参：
  - `Children` (`PwElement[]`)

### Element.GetText
- 功能：读取元素文本。
- 入参：`Element` (`PwElement`)
- 出参：`Text` (`string`)

### Element.GetAttribute
- 功能：读取元素属性。
- 入参：
  - `Element` (`PwElement`)
  - `Name` (`string`)：属性名。
- 出参：`Value` (`string`)

### Element.SetAttribute
- 功能：设置元素属性。
- 入参：
  - `Element` (`PwElement`)
  - `Name` (`string`)
  - `Value` (`string`)
- 出参：无

### Element.TakeScreenshot
- 功能：元素截图。
- 入参：
  - `Element` (`PwElement`)
  - `Path` (`string`)
- 出参：无

### Element.GetRect
- 功能：获取元素矩形信息。
- 入参：`Element` (`PwElement`)
- 出参：`Rect` (`ElementRect`)

### Element.RunJs
- 功能：在元素上下文执行 JS。
- 入参：
  - `Element` (`PwElement`)
  - `Script` (`string`)
  - `Arg` (`object`)
- 出参：`Result` (`object`)

### Element.SendKeys
- 功能：向元素发送按键/组合键（如 `Control+A`, `Enter`）。
- 入参：
  - `Element` (`PwElement`)
  - `Keys` (`string`)
  - `Delay` (`int?`)
- 出参：无

---

## 主要数据对象

- `PwBrowser`: 浏览器对象
- `PwTab`: 标签页对象
- `PwElement`: 元素对象
- `Cookies`: Cookie 集合包装对象（`AsList/AsDict/AsString`）
- `CookieInfo`: Cookie 结构
- `TabInfo`: 标签页信息
- `DownloadInfo`: 下载信息
- `ElementRect`: 元素坐标信息

