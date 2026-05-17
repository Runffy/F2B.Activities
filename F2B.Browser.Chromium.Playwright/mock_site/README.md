# Mock Site（Playwright/OpenRPA E2E 测试站）

## 启动方式

- 双击 `start_mock_site.bat`
- 或命令行运行：`start_mock_site.bat 8000`

启动后访问：`http://127.0.0.1:8000`

## 设计目标

- 用一个站点覆盖 Browser / Tab / Element 大部分活动的端到端测试
- 页面行为有可观察结果（文本、属性、DOM变化、日志、cookie、storage）
- 便于 OpenRPA 在 Close Tab / Close Browser 前统一做成功断言

## 核心验证点（对应 Activity）

- Browser 级
  - `Open/Close/GetCookies/GetLocalStorage/GetSessionStorage`：主页与新标签页均写入 cookie/storage
  - `GetAllTab/SwitchTab/GetLatestTab/GetActivatedTab`：主页按钮可打开多个命名标签页

- Tab 级
  - `NewTab`：可导航到 `/new-tab?name=...` 得到可区分标题
  - `NavigateUrl`：可导航到 `/page2`、`/query?...`
  - `Back/Forward/Refresh`：主页与 `/page2` 间切换即可测试
  - `GetCookies/GetLocalStorage/GetSessionStorage`：可直接读取页面写入结果
  - `RunJs`：页面中有多个可读写 DOM 节点（如 `#main-title`、`#click-result`）
  - `FindElement`：全页面都有可定位的 id/class 元素
  - `Close`：标签可直接关闭

- Element 级
  - `Click`：`#btn-click`（点击次数变化）
  - `Click.Double`：`#btn-dblclick`（双击后文本变化）
  - `Click` 后校验：
    - `CurrentElementDisappear`：`#btn-click-disappear` 会移除 `#click-disappear-target`
    - `SelectorAppear`：`#btn-click-appear` 会产生 `#marker-appeared`
  - `Click.ForNewTab`：`#link-new-tab`
  - `Click.ForDownload`：`#link-download`（下载 `/download`）
  - `Input / Input.GetValue`：`#input-text`
  - `Select / Select.GetSelected`：`#select-fruit`
  - `Check / Uncheck / IsChecked`：`#check-accept`
  - `FindElement`：`#parent-box` 下查找 `#child-a/#child-b/#child-c-inner`
  - `GetParent / GetChildren`：用 `#parent-box` 与其子元素测试
  - `GetText`：读取任意文本节点
  - `GetAttribute / SetAttribute`：如 `#parent-box` 的 `data-custom`
  - `TakeScreenshot`：任意元素都可截图
  - `GetRect`：任意可见元素都可取位置尺寸
  - `RunJS`：可在元素上下文执行 JS

## 主要路由

- `/`：主页（几乎所有测试元素都在这里）
- `/page2`：用于前进后退测试
- `/new-tab?name=alpha|beta|...`：新标签页目标（标题可区分）
- `/download`：下载文件
- `/query?from=main&ts=...`：带查询参数页面
