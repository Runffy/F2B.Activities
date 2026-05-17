# F2B.Browser.Chromium.Playwright 单工作流 E2E 测试指南（连续步骤版）

> 目标：在 **一个 OpenRPA Workflow** 中，0 到结束连续执行，不回跳、不前后引用，覆盖当前 `F2B.Browser.Chromium.Playwright` 全部活动。  
> 测试站：`D:\Projects\CSharp Projects\TestPlayWright\mock_site\server.py`

---

## A. 变量清单（先建好）

建议在 Workflow 级创建：

- `vBrowser` / `PwBrowser`
- `vTabMain` / `PwTab`
- `vTabAlpha` / `PwTab`
- `vTabBeta` / `PwTab`
- `vTabLatest` / `PwTab`
- `vTabActivated` / `PwTab`
- `vTabSwitch` / `PwTab`
- `vTabs` / `PwTab[]`
- `vTabInfoMain` / `TabInfo`
- `vTabInfoLatest` / `TabInfo`
- `vTabInfoActivated` / `TabInfo`
- `vTabInfoSwitch` / `TabInfo`
- `vElemParentBox` / `PwElement`
- `vElemChild` / `PwElement`
- `vElemInput` / `PwElement`
- `vElemCheckbox` / `PwElement`
- `vElemParent` / `PwElement`
- `vElemChildren` / `PwElement[]`
- `vExists` / `Boolean`
- `vChecked` / `Boolean`
- `vText` / `String`
- `vAttr` / `String`
- `vInputValue` / `String`
- `vRect` / `ElementRect`
- `vJsResult` / `Object`
- `vSelected` / `List<Dictionary<String,Object>>`
- `vDownload` / `DownloadInfo`
- `vCookiesBrowser` / `Cookies`
- `vCookiesTab` / `Cookies`
- `vStorageBrowserLocal` / `Storages`
- `vStorageBrowserSession` / `Storages`
- `vStorageTabLocal` / `Storages`
- `vStorageTabSession` / `Storages`
- `vCookieHasServer` / `Boolean`
- `vCookieServerValue` / `String`
- `vStorageHasSeed` / `Boolean`
- `vStorageSeedValue` / `String`

---

## B. 连续执行步骤（Step 000 开始）

### B1. 启动与建浏览器

- **Step 000** 启动测试站（`start_mock_site.bat`），确认 `http://127.0.0.1:8000` 可打开。
- **Step 001** `Browser.Open`
  - `Headless=False`
  - `BrowserPath="C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe"`
  - `StartMaximized=True`
  - `UseSystemDir=False`
  - `UserDataDir=""`
  - 输出 `Browser -> vBrowser`
- **Step 002** 断言 `vBrowser != null`。

### B2. 先造 3 个可区分 tab（后续所有 Browser/Tab 断言都基于这一步）

- **Step 003** `Browser.NewTab(Browser=vBrowser, Url="http://127.0.0.1:8000/") -> vTabMain`
- **Step 004** `Browser.NewTab(Browser=vBrowser, Url="http://127.0.0.1:8000/new-tab?name=alpha") -> vTabAlpha`
- **Step 005** `Browser.NewTab(Browser=vBrowser, Url="http://127.0.0.1:8000/new-tab?name=beta") -> vTabBeta`
- **Step 006** 断言三个 Tab 均不为 null。

### B3. Browser 级（含你强调的 3.3 优化版）

- **Step 007** `Browser.GetAllTab(Browser=vBrowser) -> vTabs`
- **Step 008** 断言 `vTabs.Length >= 3`（明确至少两个以上）。

- **Step 009** `Browser.GetLatestTab(Browser=vBrowser) -> vTabLatest`
- **Step 010** `Tab.GetInfo(Tab=vTabLatest) -> vTabInfoLatest`
- **Step 011** 断言 `vTabInfoLatest.Title == "Mock New Tab - beta"` 或 `vTabInfoLatest.Url` 包含 `name=beta`。

- **Step 012** `Browser.SwitchTab(Browser=vBrowser, ByType=Tab, InputTab=vTabAlpha) -> vTabActivated`（故意切到非 latest）
- **Step 013** `Browser.GetActivatedTab(Browser=vBrowser) -> vTabActivated`
- **Step 014** `Tab.GetInfo(Tab=vTabActivated) -> vTabInfoActivated`
- **Step 015** 断言：
  - `vTabInfoActivated.Title == "Mock New Tab - alpha"` 或 Url 含 `name=alpha`
  - `vTabInfoActivated.Title != vTabInfoLatest.Title`

- **Step 016** `Browser.SwitchTab(ByType=Title, Title="Mock New Tab - alpha", Browser=vBrowser) -> vTabSwitch`
- **Step 017** `Tab.GetInfo(vTabSwitch) -> vTabInfoSwitch`，通过 Url 断言包含 `name=alpha`
- **Step 018** `Browser.SwitchTab(ByType=Title Regex, TitleRe="Mock New Tab - b.*", Browser=vBrowser) -> vTabSwitch`
- **Step 019** `Tab.GetInfo(vTabSwitch) -> vTabInfoSwitch`，通过 Url 断言包含 `name=beta`
- **Step 020** `Browser.SwitchTab(ByType=Url, Url="http://127.0.0.1:8000/new-tab?name=alpha", Browser=vBrowser) -> vTabSwitch, vTabInfoSwitch`
- **Step 021** 使用 `vTabInfoSwitch` 直接断言 Title 为 `Mock New Tab - alpha`（无需再调用 `Tab.GetInfo`）
- **Step 022** `Browser.SwitchTab(ByType=Url Regex, UrlRe=".*/new-tab\\?name=beta$", Browser=vBrowser) -> vTabSwitch, vTabInfoSwitch`
- **Step 023** 使用 `vTabInfoSwitch` 直接断言 Title 为 `Mock New Tab - beta`（无需再调用 `Tab.GetInfo`）
- **Step 024** `Browser.SwitchTab(ByType=Index, Index=0, Browser=vBrowser) -> vTabSwitch, vTabInfoSwitch`
- **Step 025** 使用 `vTabInfoSwitch` 直接断言 `Title` 不等于 `Mock New Tab - alpha` 且不等于 `Mock New Tab - beta`（无需再调用 `Tab.GetInfo`）

### B4. Tab 级（统一在主页 tab 上执行）

- **Step 026** `Browser.SwitchTab(Browser=vBrowser, ByType=Tab, InputTab=vTabMain) -> vTabSwitch`
- **Step 027** `Tab.NavigateUrl(Tab=vTabMain, Url="http://127.0.0.1:8000/?nav_case=tab_main")`
- **Step 028** `Tab.GetInfo(Tab=vTabMain) -> vTabInfoMain`（精确断言 Url 包含 `nav_case=tab_main`，且不包含 `name=alpha`/`name=beta`）


- **Step 029** `RunJs(BaseOn=Tab, InputTab=vTabMain, Script="() => document.title") -> vJsResult`
- **Step 030** 断言 `vJsResult` 非空，且 `vJsResult.ToString() == "Playwright Activities E2E Mock Site"`
- **Step 031** `FindElement(BaseOn=Tab, Tab=vTabMain, Selector="#main-title", Index=0, WaitState=Visible, DelayBefore=300) -> vElemParentBox`
- **Step 032** 断言 `vElemParentBox != null`
- **Step 033** `Tab.ElementExists(Tab=vTabMain, Selector="#main-title", Index=0) -> vExists`
- **Step 034** 断言 `vExists == True`
- **Step 035** `SendKeys(BaseOn=Tab, InputTab=vTabMain, Keys="Tab", Delay=20)`
- **Step 036** `Tab.NavigateUrl(Tab=vTabMain, Url="http://127.0.0.1:8000/page2")`
- **Step 037** `Tab.GetInfo(Tab=vTabMain) -> vTabInfoMain`
- **Step 038** 断言 `vTabInfoMain.Url` 包含 `/page2`
- **Step 039** `Tab.Back(Tab=vTabMain)`
- **Step 040** `Tab.GetInfo(Tab=vTabMain) -> vTabInfoMain`
- **Step 041** 断言 `vTabInfoMain.Url` 包含 `nav_case=tab_main`
- **Step 042** `Tab.Forward(Tab=vTabMain)`
- **Step 043** `Tab.GetInfo(Tab=vTabMain) -> vTabInfoMain`
- **Step 044** 断言 `vTabInfoMain.Url` 包含 `/page2`
- **Step 045** `Tab.Refresh(Tab=vTabMain)`
- **Step 046** `Tab.GetInfo(Tab=vTabMain) -> vTabInfoMain`
- **Step 047** 断言 `vTabInfoMain.Url` 仍包含 `/page2`

### B5. Element 级（Selector 模式，全量主流程）

- **Step 048** `Tab.NavigateUrl(Tab=vTabMain, Url="http://127.0.0.1:8000/")`

- **Step 049** `Element.Click(TargetType=Selector, InputTab=vTabMain, Selector="#btn-click", Validate=None)`
- **Step 050** `Element.Click(TargetType=Selector, InputTab=vTabMain, Selector="#btn-click-disappear", Validate=ElementDisappear, ValidationSelector="#click-disappear-target")`
- **Step 051** `Element.Click(TargetType=Selector, InputTab=vTabMain, Selector="#btn-click-appear", Validate=ElementAppear, ValidationSelector="#marker-appeared")`
- **Step 052** `Element.DoubleClick(TargetType=Selector, InputTab=vTabMain, Selector="#btn-dblclick", Validate=None)`

> Click 系列校验模式说明：`Validate=None/ElementDisappear/ElementAppear`。当 Validate 不是 `None` 时，`ValidationSelector` 必填。

- **Step 053** `Element.ClickForNewTab(TargetType=Selector, InputTab=vTabMain, Selector="#link-new-tab") -> vTabSwitch, vTabInfoSwitch`
- **Step 054** 断言 `vTabInfoSwitch` 非空，且标题包含 `Mock New Tab`
- **Step 055** `Element.ClickForDownload(TargetType=Selector, InputTab=vTabMain, Selector="#link-download", SaveAsPath="D:\\Temp\\mock-download.txt") -> vDownload`
- **Step 056** 断言 `vDownload != null` 且 `vDownload.SavedPath` 非空
- **Step 056.1** `Element.DoubleClick(..., Selector="#btn-dblclick-disappear", Validate=ElementDisappear, ValidationSelector="#dbl-disappear-target")`
- **Step 056.2** `Element.DoubleClick(..., Selector="#btn-dblclick-appear", Validate=ElementAppear, ValidationSelector="#dbl-marker-appeared")`
- **Step 056.3** `Element.ClickForNewTab(..., Selector="#link-new-tab-disappear", Validate=ElementDisappear, ValidationSelector="#newtab-disappear-target") -> vTabSwitch, vTabInfoSwitch`
- **Step 056.4** 断言 `vTabInfoSwitch.Url` 包含 `from-link-disappear`
- **Step 056.5** `Tab.Close(Tab=vTabSwitch)`，避免测试中间产生的 tab 干扰后续步骤
- **Step 056.6** `Browser.SwitchTab(ByType=Tab, InputTab=vTabMain)` 切回主 tab
- **Step 056.7** `Element.ClickForNewTab(..., Selector="#link-new-tab-appear", Validate=ElementAppear, ValidationSelector="#newtab-marker-appeared") -> vTabSwitch, vTabInfoSwitch`
- **Step 056.8** 断言 `vTabInfoSwitch.Url` 包含 `from-link-appear`
- **Step 056.9** `Tab.Close(Tab=vTabSwitch)`
- **Step 056.10** `Browser.SwitchTab(ByType=Tab, InputTab=vTabMain)` 再切回主 tab
- **Step 056.11** `Element.ClickForDownload(..., Selector="#link-download-disappear", Validate=ElementDisappear, ValidationSelector="#download-disappear-target", SaveAsPath="D:\\Temp\\mock-download-disappear.txt") -> vDownload`
- **Step 056.12** 断言 `vDownload` 非空且 `SavedPath` 非空
- **Step 056.13** `Element.ClickForDownload(..., Selector="#link-download-appear", Validate=ElementAppear, ValidationSelector="#download-marker-appeared", SaveAsPath="D:\\Temp\\mock-download-appear.txt") -> vDownload`
- **Step 056.14** 断言 `vDownload` 非空且 `SavedPath` 非空

- **Step 057** `Element.Input(TargetType=Selector, InputTab=vTabMain, Selector="#input-text", InputMethod=Fill, Value="hello-fill", ValidateContentAfterInputted=True)`
- **Step 058** `Element.Input(TargetType=Selector, InputTab=vTabMain, Selector="#input-text", InputMethod=Type, Value="-typed", TypeDelay=30)`
- **Step 059** `Element.InputGetValue(TargetType=Selector, InputTab=vTabMain, Selector="#input-text") -> vInputValue`
- **Step 060** 断言 `vInputValue` 包含 `typed`

- **Step 061** `Element.Select(TargetType=Selector, InputTab=vTabMain, Selector="#select-fruit", ValType=Text, Texts={"banana"})`
- **Step 062** `Element.Select(TargetType=Selector, InputTab=vTabMain, Selector="#select-fruit", ValType=Value, Values={"orange"})`
- **Step 063** `Element.Select(TargetType=Selector, InputTab=vTabMain, Selector="#select-fruit", ValType=Index, Indices={1})`
- **Step 064** `Element.SelectGetSelected(TargetType=Selector, InputTab=vTabMain, Selector="#select-fruit") -> vSelected`
- **Step 065** 精确断言 `vSelected`：
  - `vSelected != null` 且 `vSelected.Count == 1`
  - `vSelected(0)` 包含 `text/value/index`
  - `vSelected(0)("text") == "apple"`、`vSelected(0)("value") == "apple"`、`Convert.ToInt32(vSelected(0)("index")) == 1`

- **Step 066** `Element.Check(TargetType=Selector, InputTab=vTabMain, Selector="#check-accept")`
- **Step 067** `Element.IsChecked(TargetType=Selector, InputTab=vTabMain, Selector="#check-accept") -> vChecked`
- **Step 068** 断言 `vChecked == True`
- **Step 069** `Element.Uncheck(TargetType=Selector, InputTab=vTabMain, Selector="#check-accept")`
- **Step 070** `Element.IsChecked(TargetType=Selector, InputTab=vTabMain, Selector="#check-accept") -> vChecked`
- **Step 071** 断言 `vChecked == False`

- **Step 072** `SendKeys(BaseOn=Element, TargetType=Selector, InputTab=vTabMain, Selector="#key-capture", Keys="A")`

- **Step 073** `FindElement(BaseOn=Tab, Tab=vTabMain, Selector="#parent-box") -> vElemParentBox`
- **Step 074** `FindElement(BaseOn=Element, Element=vElemParentBox, Selector=".child-node", Index=0, WaitState=Visible) -> vElemChild`
- **Step 075** 断言 `vElemChild != null`
- **Step 076** `Tab.ElementExists(Tab=vTabMain, Selector="#toggle-target", Index=0) -> vExists`
- **Step 077** 断言 `vExists == True`
- **Step 078** `Element.GetText(TargetType=Selector, InputTab=vTabMain, Selector="#child-a") -> vText`
- **Step 079** 断言 `vText` 非空
- **Step 080** `Element.GetAttribute(TargetType=Selector, InputTab=vTabMain, Selector="#attr-target", Name="data-custom") -> vAttr`
- **Step 081** `Element.SetAttribute(TargetType=Selector, InputTab=vTabMain, Selector="#attr-target", Name="data-custom", Value="from-openrpa")`
- **Step 082** 再执行一次 `Element.GetAttribute(...) -> vAttr`
- **Step 083** 断言 `vAttr == "from-openrpa"`
- **Step 084** `Element.GetRect(TargetType=Selector, InputTab=vTabMain, Selector="#screenshot-target") -> vRect`
- **Step 085** 断言 `vRect != null` 且 `vRect.Width > 0` 且 `vRect.Height > 0`
- **Step 086** `Element.TakeScreenshot(TargetType=Selector, InputTab=vTabMain, Selector="#screenshot-target", Path="D:\\Temp\\element-shot.png")`
- **Step 087** `RunJs(BaseOn=Element, TargetType=Selector, InputTab=vTabMain, Selector="#main-title", Script="(el)=>el.textContent") -> vJsResult`
- **Step 088** 断言 `vJsResult` 非空
- **Step 089** `Element.GetChildren(TargetType=Selector, InputTab=vTabMain, Selector="#parent-box", ChildSelector=".child-node", Deepdive=True) -> vElemChildren`
- **Step 090** 断言 `vElemChildren != null` 且 `vElemChildren.Length > 0`
- **Step 091** `Element.GetParent(TargetType=Selector, InputTab=vTabMain, Selector="#child-c-inner", Level=1) -> vElemParent`
- **Step 092** 断言 `vElemParent != null`

### B6. Element 级（Element 模式抽样）

- **Step 093** `FindElement(BaseOn=Tab, Tab=vTabMain, Selector="#input-text") -> vElemInput`
- **Step 094** `Element.Input(TargetType=Element, Element=vElemInput, Value="element-mode", InputMethod=Fill)`
- **Step 095** `Element.InputGetValue(TargetType=Element, Element=vElemInput) -> vInputValue`
- **Step 096** 断言 `vInputValue == "element-mode"`
- **Step 097** `SendKeys(BaseOn=Element, TargetType=Element, InputElement=vElemInput, Keys="B")`
- **Step 098** `RunJs(BaseOn=Element, TargetType=Element, InputElement=vElemInput, Script="(el)=>el.value") -> vJsResult`
- **Step 099** 断言 `vJsResult` 非空

- **Step 100** `FindElement(BaseOn=Tab, Tab=vTabMain, Selector="#check-accept") -> vElemCheckbox`
- **Step 101** `Element.Check(TargetType=Element, Element=vElemCheckbox)`
- **Step 102** `Element.IsChecked(TargetType=Element, Element=vElemCheckbox) -> vChecked`
- **Step 103** 断言 `vChecked == True`
- **Step 104** `Element.Uncheck(TargetType=Element, Element=vElemCheckbox)`
- **Step 105** `Element.IsChecked(TargetType=Element, Element=vElemCheckbox) -> vChecked`
- **Step 106** 断言 `vChecked == False`

### B7. 触发 Cookie/Storage 后做 Browser/Tab 读取与包装类方法验证

- **Step 107** `Element.Click(TargetType=Selector, InputTab=vTabMain, Selector="#btn-set-cookie", Validate=None)`
- **Step 108** `Element.Click(TargetType=Selector, InputTab=vTabMain, Selector="#btn-set-storage", Validate=None)`

- **Step 109** `GetCookies(BaseOn=Tab, InputTab=vTabMain) -> vCookiesTab`
- **Step 110** `GetStorage(BaseOn=Tab, Scope=Local, InputTab=vTabMain) -> vStorageTabLocal`
- **Step 111** `GetStorage(BaseOn=Tab, Scope=Session, InputTab=vTabMain) -> vStorageTabSession`

- **Step 112** `GetCookies(BaseOn=Browser, InputBrowser=vBrowser) -> vCookiesBrowser`
- **Step 113(前置)** `Browser.SwitchTab(ByType=Tab, InputTab=vTabMain)`（强制激活主 tab，避免 Browser 级 Storage 读错上下文）
- **Step 113** `GetStorage(BaseOn=Browser, Scope=Local, InputBrowser=vBrowser) -> vStorageBrowserLocal`
- **Step 114** `GetStorage(BaseOn=Browser, Scope=Session, InputBrowser=vBrowser) -> vStorageBrowserSession`

- **Step 115** `Assign vCookieHasServer = vCookiesBrowser.ContainsKey("server_cookie")`
- **Step 116** `Assign vCookieServerValue = vCookiesBrowser.Get("server_cookie")`
- **Step 117** `Assign vStorageHasSeed = vStorageBrowserLocal.ContainsKey("mock.local.seed")`
- **Step 118** `Assign vStorageSeedValue = vStorageBrowserLocal.Get("mock.local.seed")`
- **Step 119** 断言 `vCookieHasServer == True`
- **Step 120** 断言 `vStorageHasSeed == True`
- **Step 121** 断言 `vStorageSeedValue == "LOCAL_SEED_V1"`

- **Step 122.1（可选）** 断言 `vCookiesBrowser.AsDict() != null` 且 `.Count > 0`
- **Step 122.2（可选）** 断言 `vCookiesBrowser.AsList() != null` 且 `.Count > 0`
- **Step 122.3（可选）** 断言 `vCookiesBrowser.AsString()` 包含 `server_cookie`
- **Step 122.4（可选）** 断言 `vStorageBrowserLocal.AsDict() != null` 且 `.Count > 0`
- **Step 122.5（可选）** 断言 `vStorageBrowserLocal.AsList() != null` 且 `.Count > 0`
- **Step 122.6（可选）** 断言 `vStorageBrowserLocal.AsString()` 包含 `mock.local.seed`

### B8. 最终统一断言与清理

- **Step 123** `Tab.ElementExists(Tab=vTabMain, Selector="#op-log li") -> vExists`
- **Step 124** 断言 `vExists == True`
- **Step 125** 断言 `vDownload != null` 且 `vDownload.SavedPath` 非空
- **Step 126** 断言 `vCookieHasServer == True`
- **Step 127** 断言 `vStorageBrowserSession.ContainsKey("mock.session.seed") == True`
- **Step 128** 断言 `vStorageBrowserSession.Get("mock.session.seed") == "SESSION_SEED_V1"`

- **Step 129** `Tab.Close(Tab=vTabAlpha)`（若未关闭）
- **Step 130** `Tab.Close(Tab=vTabBeta)`（若未关闭）
- **Step 131** `Tab.Close(Tab=vTabSwitch)`（若与前两者不同且未关闭）
- **Step 132** 可选：直接 `Tab.Close(Tab=vTabMain)`（按你习惯）
- **Step 133** `Browser.Close(Browser=vBrowser)`（最终收尾）

---

## C. 说明（Browser 与 Tab 的 Storage 都测的原因）

- `localStorage/sessionStorage` 绑定页面上下文（tab + origin）；
- `Browser.Get*Storage` 读取的是当前激活 tab；
- `Tab.Get*Storage` 读取的是指定 tab；
- 两层都测能同时覆盖“业务易用性”和“技术准确性”。

---

## D. Browser.Open 多模式补测（建议单独跑）

建议把下面 3 组各自做成一个小 workflow（避免互相污染）：

- **模式 A：系统目录（高风险）**
  - `UseSystemDir=True`
  - `UserDataDir` 留空
  - 预期：尝试使用系统 Edge User Data；若本机已有 Edge 进程，可能冲突（你办公机曾出现过）。

- **模式 B：临时隔离目录（推荐默认）**
  - `UseSystemDir=False`
  - `UserDataDir` 留空
  - 预期：Playwright 自动创建临时 profile，和用户手动 Edge 隔离；跨机器最稳定。

- **模式 C：指定隔离目录（部署常用）**
  - `UseSystemDir=False`
  - `UserDataDir="C:\\Users\\zc\\Documents\\OpenRPA\\extensions\\pw-profile"`（示例）
  - 预期：目录可复用，仍与系统日常 Edge 隔离；便于排障和持久化会话。

每组最少验证：
- `Browser.Open -> Browser.NewTab -> Tab.GetInfo -> Browser.Close` 全链路可过；
- 关闭后任务管理器中不残留异常 Edge 进程；
- 若失败，记录 `UseSystemDir/UserDataDir` 组合和完整错误日志，便于回归比较。
