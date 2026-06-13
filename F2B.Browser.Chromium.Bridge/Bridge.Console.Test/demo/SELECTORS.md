# F2B Bridge Demo — Selector XML 对照表（待审核）

> Demo 根地址：`http://127.0.0.1:19223/`  
> 主页面标题：`GWIS SYSTEM Demo`  
> 本文档列出 **全量 Activity 回归** 将使用的 Selector XML。请逐条确认语法与元素对应关系；**你确认无误后再通知开始 Console 全量测试**。

---

## 1. Selector XML 语法速查

每行一个自闭合标签，从上到下依次为：**wnd → frm（可多层）→ ctrl（可多层）**。

| XML 属性 | 内部属性名 | 说明 |
|----------|------------|------|
| `role` | ControlType | `button` / `edit` / `checkbox` / `radiobutton` / `combobox` / `link` / `text` / `pane` / `window` |
| `name` | Name | 优先匹配 HTML `name`；iframe 还匹配 `name`/`title` |
| `automationid` | AutomationId | 优先匹配 HTML `id` |
| `tag` | TagName | 标签名，如 `input` / `button` / `select` / `a` / `span` |
| `type` | Type | input 的 type，如 `checkbox` / `radio` / `text` |
| `cls` | ClassName | class 名（整词匹配） |
| `href` | Href | 链接 href（可 `-re` 正则） |
| `title` / `title-re` | Title | Tab 标题或 iframe title |
| `url` / `url-re` | Url | Tab URL |
| `idx` | IndexInParent | 同级匹配结果中的索引（从 0 开始） |

**三种用法：**

| 模式 | 结构 | C# 调用 |
|------|------|---------|
| **A. 完整** | `<wnd>` + 可选 `<frm>*` + `<ctrl>*` | `BridgeHost.*(selectorXml)` — 自动找 Tab |
| **B. 指定 Tab** | `<frm>*` + `<ctrl>*` 或仅 `<ctrl>*` | 同上，但传入 `BwTab demoTab` |
| **C. 仅 Tab 级** | 仅 `<wnd>` | Tab 解析 / SwitchTab 测试 |

正则示例：`title-re='GWIS SYSTEM.*'`、`url-re='http://127\.0\.0\.1:19223/.*'`

---

## 2. 公共片段（常量）

### 2.1 Tab（wnd）

```xml
<wnd title-re='GWIS SYSTEM.*' />
```

Nav A / Nav B 页面（Back/Forward 测试用）：

```xml
<wnd title='GWIS Nav A' />
```

```xml
<wnd title='GWIS Nav B' />
```

按 URL 匹配 Demo 主页（备选）：

```xml
<wnd url-re='http://127\.0\.0\.1:19223/?$' />
```

### 2.2 单层 iframe（LoginWinMain）

```xml
<frm name='LoginWinMain' />
```

### 2.3 双层 iframe（LoginWinMain → InnerPanel）

```xml
<frm name='LoginWinMain' />
<frm name='InnerPanel' />
```

---

## 3. 顶层元素（index.html，模式 A / B）

以下均在 **Demo 主页** 上。模式 **B** = 去掉首行 `<wnd>`，调用时传 `BwTab demoTab`。

### 3.1 基础 Click / GetText

| 元素 | HTML | 覆盖 Activity |
|------|------|----------------|
| 顶层按钮 | `<button id="topBtn" name="topAction">` | Click, GetText（via status） |
| 状态文本 | `<span id="topStatus">` | GetText |

**模式 A — 按钮 Click：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<ctrl role='button' automationid='topBtn' />
```

**模式 B — 按钮 Click：**

```xml
<ctrl role='button' automationid='topBtn' />
```

**模式 A — 状态 GetText：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<ctrl automationid='topStatus' />
```

---

### 3.2 DoubleClick

| 元素 | HTML | Activity |
|------|------|----------|
| 双击按钮 | `<button id="dblClickBtn">` | DoubleClick |
| 双击结果 | `<span id="dblClickStatus">` | GetText |

**DoubleClick 目标（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<ctrl role='button' automationid='dblClickBtn' />
```

**验证文本（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<ctrl automationid='dblClickStatus' />
```

---

### 3.3 Check / Uncheck / IsChecked

| 元素 | HTML | Activity |
|------|------|----------|
| 同意条款 | `<input type="checkbox" id="agreeTerms" name="agreeTerms">` | Check, Uncheck, IsChecked |

**Checkbox（A）— 推荐 role + automationid：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<ctrl role='checkbox' automationid='agreeTerms' />
```

**等价写法（tag + type + name）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<ctrl tag='input' type='checkbox' name='agreeTerms' />
```

---

### 3.4 Select / GetSelected

| 元素 | HTML | Activity |
|------|------|----------|
| 国家下拉 | `<select id="topCountry" name="topCountry">` | Select, GetSelected |
| 选项 | value=`HK` / `CN` / `SG` | Select by Value |
| 变更提示 | `<span id="topCountryStatus">` | GetText（可选） |

**Combobox（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<ctrl role='combobox' automationid='topCountry' />
```

**等价：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<ctrl tag='select' name='topCountry' />
```

---

### 3.5 Radio（通过 Click 选中）

| 元素 | HTML | Activity |
|------|------|----------|
| Basic 方案 | `<input type="radio" id="planBasic" value="basic">` | Click → IsChecked |
| Pro 方案 | `<input type="radio" id="planPro" value="pro">` | Click |

**Radio Basic（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<ctrl role='radiobutton' automationid='planBasic' />
```

**Radio Pro（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<ctrl role='radiobutton' automationid='planPro' />
```

---

### 3.6 Input / GetInputValue / Textarea

| 元素 | HTML | Activity |
|------|------|----------|
| 备注 | `<textarea id="topNotes" name="topNotes">` | Input, GetInputValue |
| SendKeys 输入框 | `<input type="text" id="sendKeysTarget">` | SendKeys, Input |

**Textarea Input（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<ctrl role='edit' automationid='topNotes' />
```

**SendKeys 目标（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<ctrl role='edit' automationid='sendKeysTarget' />
```

---

### 3.7 GetParent / GetChildren / GetRect / SetAttribute

| 元素 | HTML | Activity |
|------|------|----------|
| 父容器 | `<div id="parentBox">` | GetParent 起点 |
| 子节点 | `<span id="childTarget" class="demo-child">` | GetChildren, GetText, GetParent |
| 矩形区域 | `<div id="rectBox">` | GetRect, TakeScreenshot(element) |
| 属性按钮 | `<button id="setAttrBtn" data-test="before">` | SetAttribute, GetAttribute |

**子元素 FindElement 起点（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<ctrl automationid='childTarget' />
```

**GetChildren 子选择器（scoped，相对 parent 查找）：**

```xml
<ctrl cls='demo-child' />
```

**GetRect 目标（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<ctrl automationid='rectBox' />
```

**SetAttribute 目标（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<ctrl role='button' automationid='setAttrBtn' />
```

**GetAttribute 读取 `data-test`（A，同上 selector）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<ctrl role='button' automationid='setAttrBtn' />
```

---

### 3.8 ParallelFindElement

| 元素 | HTML | 说明 |
|------|------|------|
| 候选 A | `<button id="btnCandidateA">` | selector 集合 index 0 |
| 候选 B | `<button id="btnCandidateB">` | selector 集合 index 1 |

**候选 A（仅 ctrl，Parallel 用）：**

```xml
<ctrl role='button' automationid='btnCandidateA' />
```

**候选 B（仅 ctrl）：**

```xml
<ctrl role='button' automationid='btnCandidateB' />
```

> ParallelFindElement 的 `ParentObject` 接受 `BwTab` 或 `BwElement`。
> - **Tab**：在页面根（或 frm 内）轮询多个 selector XML（通常无需 wnd）
> - **Element**：在父元素子树内轮询多个相对 selector XML（仅 `<ctrl>` 层级）

---

### 3.9 ClickForNewTab / ClickForDownload

| 元素 | HTML | Activity | Bridge 状态 |
|------|------|----------|-------------|
| 新 Tab 链接 | `<a id="openNewTabLink" href="nav-a.html" target="_blank">` | ClickForNewTab | ⚠ 扩展 RPC 尚未实现 |
| 下载链接 | `<a id="downloadLink" href="sample.txt" download>` | ClickForDownload | ⚠ 扩展 RPC 尚未实现 |

**新 Tab 链接（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<ctrl role='link' automationid='openNewTabLink' />
```

**等价 href：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<ctrl role='link' href='nav-a.html' />
```

**下载链接（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<ctrl role='link' automationid='downloadLink' />
```

---

### 3.10 页内导航（Navigate / Back / Forward）

| 元素 | HTML | Activity |
|------|------|----------|
| 去 Nav A | `<a id="gotoNavA" href="nav-a.html">` | NavigateUrl（或直接 Click） |
| Nav A → B | `<a id="gotoNavB" href="nav-b.html">`（在 nav-a.html） | Click + Back/Forward |

**Go Nav A 链接 Click（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<ctrl role='link' automationid='gotoNavA' />
```

**Nav A 页提示 GetText：**

```xml
<wnd title='GWIS Nav A' />
<ctrl automationid='navAHint' />
```

**Nav B 页提示 GetText：**

```xml
<wnd title='GWIS Nav B' />
<ctrl automationid='navBHint' />
```

---

## 4. iframe 内元素（LoginWinMain，模式 A / B）

### 4.1 登录表单

| 元素 | HTML | Activity |
|------|------|----------|
| User ID | `<input id="userID" name="userID">` | Input, GetInputValue, ElementExists |
| Password | `<input id="password" name="password">` | Input |
| Login 按钮 | `<button id="btnLogin">` | Click |
| 状态 | `<span id="statusLabel">` | GetText |

**User ID Input（A）— name 属性写法：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<frm name='LoginWinMain' />
<ctrl tag='input' name='userID' />
```

**User ID（A）— automationid 写法：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<frm name='LoginWinMain' />
<ctrl role='edit' automationid='userID' />
```

**Password Input（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<frm name='LoginWinMain' />
<ctrl tag='input' name='password' />
```

**Login Click（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<frm name='LoginWinMain' />
<ctrl role='button' automationid='btnLogin' />
```

**Status GetText（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<frm name='LoginWinMain' />
<ctrl automationid='statusLabel' />
```

**模式 B 示例（显式 BwTab，无 wnd）：**

```xml
<frm name='LoginWinMain' />
<ctrl tag='input' name='userID' />
```

---

### 4.2 iframe 内 Select

| 元素 | HTML | Activity |
|------|------|----------|
| 部门下拉 | `<select id="deptSelect" name="deptSelect">` | Select, GetSelected |
| 变更提示 | `<span id="deptStatus">` | GetText |

**Department Select（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<frm name='LoginWinMain' />
<ctrl role='combobox' automationid='deptSelect' />
```

---

### 4.3 iframe 内 Checkbox

| 元素 | HTML | Activity |
|------|------|----------|
| Remember me | `<input type="checkbox" id="rememberMe">` | Check, Uncheck, IsChecked |

**RememberMe（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<frm name='LoginWinMain' />
<ctrl role='checkbox' automationid='rememberMe' />
```

---

### 4.4 iframe 内 Radio

| 元素 | HTML | Activity |
|------|------|----------|
| Male | `<input type="radio" id="genderM" value="M">` | Click |
| Female | `<input type="radio" id="genderF" value="F">` | Click |

**Gender Male（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<frm name='LoginWinMain' />
<ctrl role='radiobutton' automationid='genderM' />
```

---

### 4.5 iframe 内 Textarea

| 元素 | HTML | Activity |
|------|------|----------|
| Notes | `<textarea id="loginNotes" name="loginNotes">` | Input, GetInputValue |

**Login Notes（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<frm name='LoginWinMain' />
<ctrl role='edit' automationid='loginNotes' />
```

---

## 5. 双层 iframe（LoginWinMain → InnerPanel）

| 元素 | HTML | Activity |
|------|------|----------|
| Inner Code | `<input id="innerCode" name="innerCode">`（在 inner-panel.html） | Input, GetInputValue |

**双层 frm + Input（A）：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<frm name='LoginWinMain' />
<frm name='InnerPanel' />
<ctrl tag='input' name='innerCode' />
```

**等价 automationid：**

```xml
<wnd title-re='GWIS SYSTEM.*' />
<frm name='LoginWinMain' />
<frm name='InnerPanel' />
<ctrl role='edit' automationid='innerCode' />
```

---

## 6. 不依赖 ctrl 的 Activity（无 Selector 或仅 wnd）

| Activity | 测试方式 | 说明 |
|----------|----------|------|
| **BrowserOpen** | N/A | Bridge 由扩展连入，非 Playwright 启动浏览器 |
| **BrowserClose** | N/A | 同上 |
| **NewTab** | `browser.NewTab(url)` | 无 selector |
| **BrowserGetAllTab** | API | 无 selector |
| **BrowserGetActivatedTab** | API | 无 selector |
| **BrowserGetLatestTab** | API | 无 selector |
| **BrowserSwitchTab** | wnd 或 tabId/index | 可用 `<wnd title='GWIS Nav A' />` |
| **TabGetInfo** | `BwTab.GetInfo()` | 无 selector |
| **NavigateUrl** | `BwTab.NavigateUrl(url)` | 无 selector |
| **Back / Forward / Refresh / TabClose** | Tab API | 无 selector |
| **RunJs** | `BwTab.RunJs("return document.title;")` | 无 selector（CSP 限制复杂脚本） |
| **GetCookies** | Tab / Browser API | 无 selector |
| **GetStorage** | local=`demoKey`→`demoValue`；session=`sessionKey`→`sessionValue` | 页面 load 时写入 |
| **TakeScreenshot** | Tab 或 element（见 rectBox / 全页） | element 用 §3.7 selector |
| **FindElements** | 瞬时查询全部匹配 | Tab/Element + selector，返回 `BwElement[]`（无 timeout） |
| **FindElement** | 见上文各 ctrl | 带 timeout，返回单个 `BwElement` |
| **ElementExists** | 见上文各 ctrl | — |
| **ClickForNewTab** | §3.9 | ⚠ RPC 待实现 |
| **ClickForDownload** | §3.9 | ⚠ RPC 待实现 |

---

## 7. Activity → Selector 速查矩阵

| # | Playwright Activity | 主要 Selector（模式 A 示例） |
|---|---------------------|------------------------------|
| 1 | Click | topBtn / btnLogin / gotoNavA 等 |
| 2 | DoubleClick | dblClickBtn |
| 3 | Input | userID, topNotes, loginNotes, innerCode, sendKeysTarget |
| 4 | Select | topCountry, deptSelect |
| 5 | Check | agreeTerms, rememberMe |
| 6 | Uncheck | agreeTerms, rememberMe |
| 7 | IsChecked | agreeTerms, rememberMe |
| 8 | GetText | topStatus, statusLabel, dblClickStatus, navAHint… |
| 9 | GetInputValue | userID, topNotes, loginNotes, innerCode |
| 10 | GetSelected | topCountry, deptSelect |
| 11 | GetAttribute | setAttrBtn (`data-test`) |
| 12 | SetAttribute | setAttrBtn |
| 13 | GetRect | rectBox |
| 14 | GetParent | childTarget（起点） |
| 15 | GetChildren | childTarget + child `<ctrl cls='demo-child' />` |
| 16 | FindElement | 任意 ctrl 行 |
| 17 | ElementExists | 任意 ctrl 行 |
| 18 | ParallelFindElement | btnCandidateA / btnCandidateB |
| 19 | SendKeys | sendKeysTarget |
| 20 | RunJs | 无 selector |
| 21 | TakeScreenshot | Tab 或 rectBox |
| 22 | GetCookies | 无 selector |
| 23 | GetStorage | 无 selector |
| 24 | NavigateUrl | 无 selector |
| 25 | Back / Forward / Refresh | 无 selector（Nav A↔B 流程） |
| 26 | TabGetInfo | 无 selector |
| 27 | TabClose | 无 selector |
| 28 | NewTab | 无 selector |
| 29 | BrowserGetAllTab / GetActivated / GetLatest | 无 selector |
| 30 | BrowserSwitchTab | `<wnd title='GWIS Nav A' />` 等 |
| 31 | ClickForNewTab | openNewTabLink ⚠ |
| 32 | ClickForDownload | downloadLink ⚠ |
| 33 | BrowserOpen / BrowserClose | N/A（Bridge 模式） |
| 34 | FindElements | topBtn / parentBox+childScoped |

---

## 8. 语法自检清单（请审核时关注）

- [ ] `<wnd title-re='GWIS SYSTEM.*' />` 能否稳定匹配 Demo 主页 Tab（含 `NewTab` 打开页）
- [ ] `<frm name='LoginWinMain' />` 与 iframe `name="LoginWinMain"` 一致
- [ ] `<frm name='InnerPanel' />` 与 login.html 内 iframe `name="InnerPanel"` 一致
- [ ] iframe 内 `name='userID'` 与 HTML `name="userID"` 一致（已支持 HTML name 匹配）
- [ ] `role='checkbox'` / `role='combobox'` / `role='radiobutton'` 与 dom-selector roleMap 一致
- [ ] `automationid='…'` 与 HTML `id="…"` 一致
- [ ] 正则中的 `.` 已转义：`url-re='http://127\.0\.0\.1:19223/.*'`
- [ ] 双层 frm 顺序：先 LoginWinMain，再 InnerPanel

---

## 9. Demo 文件结构

```
demo/www/
├── index.html          # 主页面 GWIS SYSTEM Demo
├── login.html          # iframe LoginWinMain
├── inner-panel.html    # iframe InnerPanel（嵌在 login 内）
├── nav-a.html          # GWIS Nav A
├── nav-b.html          # GWIS Nav B
├── sample.txt          # 下载测试文件
└── SELECTORS.md        # 本文档
```

---

**下一步：** 请你审核上述 Selector XML。确认无误后回复，我将基于本文档编写 Console **全量测试** 用例并执行回归。
