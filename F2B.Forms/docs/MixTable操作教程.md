# F2B.Forms MixTable 操作教程

本文说明如何在 **F2B.Forms Designer** 与 **OpenRPA（AsyncForm）** 中使用 MixTable：列定义、初始行、增删改查、事件、以及与 `Get Control Value` 等 Activity 的配合方式。

---

## 1. MixTable 是什么

MixTable 是面向「业务表格」的控件，适合：

- 有明确表头
- 同一列统一为某种形态（只读文字 / 输入框 / 下拉 / 勾选 / 按钮 / 链接）
- 行数由数据或流程动态增减

它与现有控件的分工：

| 控件 | 用途 |
|------|------|
| **MixTable** | 混控件动态业务表（推荐本教程场景） |
| **DataGrid** | 偏纯数据绑定表格 |
| **TableLayout** | 静态表单排版格子，不是数据行模型 |

**重要模型：**

- **列**决定单元格里是什么（Label / TextBox / …）
- **行**只提供各列的**值**
- 不能「同一列里，第 1 行是下拉、第 2 行是纯文字」——列类型对所有行一致

---

## 2. 设计器中创建 MixTable

1. 打开 **F2B.Forms.Designer**（使用与当前代码一致的编译版本）
2. 工具箱 **Operators** 中拖入 **MixTable**
3. 设置 `Id`（如 `mixtable1`）、`Width` / `Height`
4. 选中画布上的 **MixTable**，在右侧属性 **Data** 中：
   - **Columns Json**：列定义。点右侧 **…** 打开 Columns Generator（增删改列、设 type/width 等），OK 后写回本字段；也可直接改 JSON。再次打开会按当前内容加载；语法无效时提示错误且不打开 Generator。
   - **Initial Rows Json**：初始行。点右侧 **…** 打开 Initial Rows Generator（按当前 Columns 增删改行）。**须先有至少 1 个有效 Columns Json 列**，否则会提示先完成 Columns Json；亦可手改 JSON，合法内容可被 Generator 回读。

画布会按列类型画出表头与初始行示意；`Preview` 按钮会打开真实 WinForms 窗体并加载初始行。

> 换 DLL 后请重启 Designer / OpenRPA，否则可能仍是旧行为。

---

## 3. Columns Json（列定义）

### 3.1 字段说明

每个列是一个对象：

| 字段 | 必填 | 说明 |
|------|------|------|
| `name` | 是 | 列键。Bind / Get Cell / 行 JSON 的 key 都用它 |
| `type` | 是 | 列类型，**必须与设计器控件名完全一致（大小写敏感）** |
| `text` | 否 | 表头文字；Button/Link 在值为空时也可作默认显示 |
| `items` | 否 | 仅 `ComboBox`：下拉选项数组 |
| `width` | 否 | 固定列宽（像素）。不写则按内容 autofit |
| `headerAlign` | 否 | 表头对齐，默认 `middle-center`；设计期固定，运行时 Activity 不可改 |
| `contentAlign` | 否 | **单元格内容**对齐，默认近似 `middle-left`；可用 **Set MixTable Align** 再改列/行/格 |
| `readOnly` | 否 | 部分列可用（如 TextBox） |

### 3.2 合法 `type`（禁止缩写）

| type | 单元格表现 | 单元格「值」含义 |
|------|------------|------------------|
| `Label` | 只读文字 | 字符串 |
| `TextBox` | 可编辑输入 | 字符串 |
| `ComboBox` | 下拉 | 当前选中项字符串（须在 `items` 中） |
| `CheckBox` | 勾选 | `true` / `false` |
| `Button` | 按钮 | 按钮上显示的文字 |
| `Link` | 超链接文字 | 链接文字（MixTable 专用类型，工具箱无独立 Link 控件） |

**错误示例（会报错）：** `Text`、`Combo`、`Check`、`combobox` 等。

### 3.3 `headerAlign` / `contentAlign` 取值

不区分大小写，任选其一（两者共用同一套 token）：

- `left-top` / `middle-top` / `right-top`
- `left-center` / `middle-center` / `right-center`
- `left-bottom` / `middle-bottom` / `right-bottom`

也支持 WinForms 名：`TopLeft`、`MiddleCenter` 等。  
- `headerAlign` 未写时默认 **`middle-center`**（仅表头）  
- `contentAlign` 未写时单元格用网格默认（约 **`middle-left`**）

### 3.4 完整列表示例

```json
[
  { "name": "Select", "type": "Button", "text": "-", "width": 50, "headerAlign": "middle-center" },
  { "name": "AccountNo", "type": "Label", "text": "Account No", "width": 120 },
  { "name": "CCY", "type": "Label", "text": "CCY", "width": 60 },
  { "name": "CR", "type": "Label", "text": "CR", "width": 90, "headerAlign": "right-center", "contentAlign": "right-center" },
  { "name": "DR", "type": "Label", "text": "DR", "width": 90, "headerAlign": "right-center", "contentAlign": "right-center" },
  { "name": "CLSLedger", "type": "Link", "text": "CLSLedger_Rm" },
  {
    "name": "DisposalType",
    "type": "ComboBox",
    "text": "Disposal Type",
    "items": ["TypeA", "TypeB", "TypeC"],
    "width": 140
  },
  { "name": "SecA", "type": "CheckBox", "text": "SecA", "width": 60 },
  { "name": "SecondAccount", "type": "TextBox", "text": "Second Account" },
  { "name": "Memo", "type": "TextBox", "text": "Memo/In" }
]
```

### 3.5 滚动与列宽

- 行多超出 `Height` → **纵向**滚动条
- 列总宽超出 `Width` → **横向**滚动条
- 有 `width`：固定宽；无 `width`：autofit

---

## 4. Initial Rows Json（初始行）

用于业务固定起始数据，也会在 Designer 画布与 **Preview / 运行时打开表单** 时加载。

设计器中可点 **Initial Rows Json** 右侧 **…** 用表格编辑；打开前会检查 **Columns Json** 至少有 1 个有效列（含非空 `name`），否则提示先完成列设计。Initial Rows Json 自身语法无效时也会报错且不打开。

格式：对象数组，key = 列的 `name`。

### 4.1 ComboBox 单元格：列默认 vs 行覆盖

Columns Json 的 `items` 是该列**默认**可选项。Initial Rows 里同一列支持两种写法：

| 写法 | 含义 |
|------|------|
| `"DisposalType": "TypeA"` | 简写：选中值；可选项用列上的 `items` |
| `"DisposalType": { "value": "TypeX", "items": ["TypeX", "TypeY"] }` | 本行覆盖可选项；`value` 须在本行 `items` 中（可空） |

规则：

- 仅当对象里的 `items` **非空**时才算行级覆盖；只有 `{ "value": "..." }` 仍用列默认
- Generator 默认输出简写；仅在你改过该行 options 时才写对象形态
- 设计器：Combo 格内点 **…** / 双击 / 工具栏 **Edit Combo Items…** 编辑本行 options；**Reset to column default** 清除覆盖；有覆盖的单元格浅底提示

```json
[
  {
    "Select": "-",
    "AccountNo": "00123456",
    "CCY": "USD",
    "CR": "100.00",
    "DR": "0",
    "CLSLedger": "#TMD INT AF",
    "DisposalType": "TypeA",
    "SecA": true,
    "SecondAccount": "",
    "Memo": "row1"
  },
  {
    "Select": "-",
    "AccountNo": "00999999",
    "CCY": "HKD",
    "CR": "0",
    "DR": "50",
    "CLSLedger": "#PER CUSTO",
    "DisposalType": { "value": "TypeX", "items": ["TypeX", "TypeY"] },
    "SecA": false,
    "SecondAccount": "ACC-2",
    "Memo": "row2"
  }
]
```

说明：

- 未出现的列用默认值（空字符串 / `false` / 列上的 `text` 等）
- 若之后在 Init 里执行 **Bind MixTable**，会**整表替换**这些初始行；行对象里同样可带 Combo 的 `{value,items}` 形态

JSON 中亦可写作 `initialRows`；旧字段名 `previewRows` 仍可读入。

---

## 5. OpenRPA 流程结构建议

```
AsyncForm
├── Init Scope          ← 推荐：批量 Bind / 补初始数据
├── BindEvents
│   ├── Bind  mixtable1 / Click            ← 按钮、链接列
│   ├── Bind  mixtable1 / Change           ← TextBox、ComboBox、CheckBox
│   ├── Bind  mixtable1 / CellClick        ← 任意单元格点击（可选）
│   └── Bind  otherButton / Click          ← 表单上「新增行」等按钮
└── Close Scope（可选）
```

- **Init Scope**：表单打开时跑一次（生命周期，不是控件 Click）
- **Bind Handler**：用户操作后跑

请将新版 `F2B.Forms.dll` 放入 OpenRPA 可加载路径后再设计流程。

---

## 6. MixTable Activity 一览

工具箱路径：**MixTable**

| Activity | 作用 |
|----------|------|
| Set MixTable Columns | 运行时用 JSON 重设列（会清空行） |
| Bind MixTable | 用 `DataTable` 整表填充行（列名对齐 `name`） |
| Add MixTable Row | 追加一行；输出新行 `Row Index` |
| Remove MixTable Row | 按行号删除一行 |
| Get MixTable Cell | 读单元格值（行号 + 列名） |
| Set MixTable Cell | 写单元格值 |
| Set MixTable Column | 批量写一列（单值整列，或序列按行索引对齐） |
| Get MixTable Row Count | 当前行数 |
| Get MixTable Cell Control Id | 得到虚拟控件 Id，供 Get/Set Control Value/Text 使用 |
| Set MixTable Style | 设置表默认 / 列 / 行 / 单元格的 Font 与颜色 |
| Set MixTable Align | 设置列 / 行 / 单元格的**内容**对齐（不改表头；表头只用 Columns Json `headerAlign`） |
| Set MixTable Cell Display | 指定某格展示为控件 / 纯文本 / 隐藏 |
| Get MixTable Cell Display | 读取某格当前展示模式 |

---

## 6.0 按格控制是否展示控件

列的 `type` 仍决定「这一列默认是什么控件」，但**某一行**可以改该格的展示方式。

使用 **Set MixTable Cell Display**：

| 参数 | 说明 |
|------|------|
| Control Id | MixTable Id |
| Row Index | 行号（可用 `GetLastRowIndex()`） |
| Column | 列 `name` |
| Mode | **`Control`** \| **`Text`** \| **`Hidden`**（大小写敏感） |

| Mode | 效果 |
|------|------|
| `Control` | 显示列上的正常控件（ComboBox / CheckBox / Button…） |
| `Text` | 不显示编辑控件，只读文字（值仍可 Get/Set） |
| `Hidden` | 格子空白不展示控件（值仍保留，Get 能读到） |

示例：某行不需要 Disposal 下拉时：

- Column = `"DisposalType"`
- Mode = `"Text"` 或 `"Hidden"`

再用 Mode = `"Control"` 可恢复。

可用 **Get MixTable Cell Display** 查询当前模式。

**注意：** `Bind MixTable` / 重新灌数后，各格会恢复为 `Control`，需按数据再次 Set Display。

---

## 6.1 字体与颜色（Font / Color）

概念上对应 WinForms：

- **`System.Drawing.Font`**：字体名、字号、粗体 / 斜体 / 下划线
- **颜色**：不在 Font 里，而在样式的 **`ForeColor` / `BackColor`**（表格里是 `DataGridViewCellStyle`）

### 默认字体

1. **整表默认**：在 Designer 里选中 MixTable，设置 Font / Fore Color（与其它控件相同）。打开表单时会同步到单元格默认样式。  
2. **某列默认**：在 Columns Json 中可选：

```json
{
  "name": "CR",
  "type": "Label",
  "fontFamily": "Consolas",
  "fontSize": 10,
  "fontBold": true,
  "foreColor": "#006600"
}
```

也可用 **Set Font**（Control Id = MixTable Id）改整表默认。

### 事件里改某格 / 某行 / 某列

使用 **Set MixTable Style**：

| 参数 | 说明 |
|------|------|
| Scope | `Default`（整表）\| `Column` \| `Row` \| `Cell` |
| Row Index | Scope 为 Row / Cell 时必填 |
| Column | Scope 为 Column / Cell 时必填 |
| Font Family / Size / Bold / Italic / Underline | 空 = 保持不变；样式用 `True` / `None` |
| Fore Color / Back Color | 空 = 保持；如 `#FF0000`、`Red` |

示例：把当前事件行的 `AccountNo` 单元格标红加粗：

- Scope = `Cell`
- Row Index = `F2B.Forms.GetLastRowIndex()`
- Column = `"AccountNo"`
- Bold = `True`
- Fore Color = `Red`

**注意：** `Bind MixTable` 整表重绑后，**行/单元格**上的样式可能丢失，需按需重设；**列默认**与**表默认**一般仍在。

### 事件里改内容对齐

使用 **Set MixTable Align**（只改**单元格内容**，**不改表头**；表头对齐只在 Columns Json 的 `headerAlign` 里定）：

| 参数 | 说明 |
|------|------|
| Scope | `Column` \| `Row` \| `Cell` |
| Row Index | Scope 为 Row / Cell 时必填 |
| Column | Scope 为 Column / Cell 时必填 |
| Align | 与 `contentAlign` 相同，如 `right-center`、`MiddleRight` |

列级默认内容对齐也可在 Columns Json 写 `contentAlign`，运行时再用本 Activity 覆盖列/行/格。

---

## 7. 增删行

### 7.1 加行（事件或 Init）

使用 **Add MixTable Row**：

- **Control Id**：`mixtable1`
- **Values**（可选）：
  - JSON 字符串，例如：  
    `{"Select":"-","AccountNo":"123","CCY":"USD","DisposalType":"TypeA","SecA":false}`
  - 或 `Dictionary(Of String, Object)` / `DataRow`
- **Row Index**（输出）：新行的零基下行号

### 7.2 删行

使用 **Remove MixTable Row**：

- **Control Id**：`mixtable1`
- **Row Index**：要删的行号

典型「点行内 `-` 按钮删当前行」：

1. `Bind`：Control Id = `mixtable1`，Event Name = `Click`
2. Handler 中：`Remove MixTable Row`  
   - Row Index = `F2B.Forms.GetLastRowIndex()`

### 7.3 批量灌数

使用 **Bind MixTable**，传入列名与 MixTable `name` 一致的 `DataTable`。  
会替换当前所有行（含 Initial Rows）。

---

## 8. 读写单元格

### 8.1 直接按行号 + 列名（推荐日常使用）

**Get MixTable Cell** / **Set MixTable Cell**：

| 参数 | 示例 |
|------|------|
| Control Id | `mixtable1` |
| Row Index | `0`、`2`、或变量 |
| Column | `"AccountNo"` 或列下标 `"0"` |
| Value | 读出 / 写入 |

指定索引示例：读第 2 行（下标 1）的账户号 → Row Index=`1`，Column=`AccountNo`。

### 8.1.1 整列批量写（Set MixTable Column）

适合同一列统一结构（Label / TextBox / ComboBox / CheckBox…）：

| Values 形态 | 行为 |
|-------------|------|
| **单个值**（如 `"USD"`、`True`、`"TypeA"`） | 该列**所有行**都设为这个值 |
| **序列**（数组 / List 等） | 从第 0 行起按索引一一对应更新 |

序列长度与行数不一致时：

- **序列更长**：多出的元素忽略，不写入
- **序列更短**：只更新已对齐的行，**后面的 MixTable 行保持原值**

`string` 一律当作单个值（不会按字符拆成序列）。输出 **Updated Count** 为实际写入的行数。

示例：

- 整列 CCY 设为 USD → Column=`CCY`，Values=`"USD"`
- 按列表灌 CR → Column=`CR`，Values=`{"100","0","50"}`（3 个元素；表有 5 行则只改前 3 行）

### 8.2 「最近一次事件」的行/列

仅表示**当前这次** MixTable 事件触达的位置：

| 表达式（VB / OpenRPA） | 含义 |
|------------------------|------|
| `F2B.Forms.GetLastRowIndex()` | 最近事件行号，无则为 -1 |
| `F2B.Forms.GetLastColumnName()` | 最近事件列名 |
| `F2B.Forms.GetLastControlId()` | 最近事件控件 Id（一般为 MixTable Id） |
| `F2B.Forms.GetLastEventName()` | 最近事件名 |

它们**不是**「按列名查找行」的 API。要读固定格子，请用 Get MixTable Cell 并自己传入行号、列名。

### 8.3 虚拟 Control Id + 现有 Get/Set Control Activity

若希望沿用 **Get Control Value** / **Set Control Value** / **Get Text** / **Set Text**：

1. **Get MixTable Cell Control Id**  
   - Control Id = MixTable Id  
   - Row Index + Column  
   - 输出 **Cell Control Id**（形如 `mixcell|mixtable1|0|AccountNo`）
2. 将输出的 Id 传给 **Get Control Value** 等

说明：

- 这是**逻辑 Id**，不是独立 WinForms 控件句柄
- Id 按**当前行号**合成；删行后行号会变，**不要长期缓存**，用时再取
- 无侧车 UUID 表，反复增删行不会堆积无用 Id；释放的是表格行数据本身

**不能**把该 Id 交给 **Bind Event**（见下一节）。

---

## 9. 事件（不能绑在单个格子上）

### 9.1 结论

MixTable **单元格内控件不能单独 Bind Event**。  
事件一律绑在 **MixTable 控件**（如 `mixtable1`）上，再用 `GetLastRowIndex` / `GetLastColumnName` 区分格子。

### 9.2 可绑定事件

| Event Name | 触发条件 |
|------------|----------|
| `Click` | 点击 **Button** 或 **Link** 列内容 |
| `Change` | **TextBox** / **ComboBox** / **CheckBox** 值变化 |
| `CellClick` | 点击任意单元格 |
| `SelectionChanged` | 选中行变化 |

### 9.3 示例：删除按钮列

1. 列：`{ "name": "Select", "type": "Button", "text": "-" }`
2. Bind：`mixtable1` + `Click`
3. Handler（建议先判断列，再删行）：

   - 若 `F2B.Forms.GetLastColumnName()` = `"Select"`
   - 则 `Remove MixTable Row`，Row Index = `F2B.Forms.GetLastRowIndex()`

### 9.4 示例：下拉变更后写回另一列

1. Bind：`mixtable1` + `Change`
2. Handler：  
   - row = `GetLastRowIndex()`  
   - 若 `GetLastColumnName()` = `"DisposalType"`  
   - `Set MixTable Cell`：Column=`Memo`，Value=某提示文字，Row Index=row

---

## 10. 与「真实控件」Activity 的关系

| 操作 | MixTable 单元格 |
|------|-----------------|
| Get/Set MixTable Cell | ✅ 支持 |
| Get/Set Control Value、Get/Set Text（配合 Cell Control Id） | ✅ 支持 |
| Enable / ShowHide / Update 任意属性 / Bind Event | ❌ 不支持（无真实子控件） |
| Get Child Controls 枚举出每个格子 | ❌ 不会列出 mixcell Id |

需要「每个格子都是独立可 Bind 的控件」时，应使用 **TableLayout + Create In Cell**，而不是 MixTable。

---

## 11. 推荐操作清单（速查）

| 目标 | 做法 |
|------|------|
| 设计表头与列类型 | Designer → Columns Json |
| 固定起始数据 | Initial Rows Json，或 Init 里 Bind/Add |
| 打开时批量加载 | Init → Bind MixTable |
| 用户点按钮加一行 | 按钮 Click → Add MixTable Row |
| 用户点行内删除 | MixTable Click → Remove（LastRowIndex） |
| 读/写指定格 | Get/Set MixTable Cell（行号+列名） |
| 复用 Get Control Value | 先 Get MixTable Cell Control Id |
| 区分谁点了哪格 | MixTable 事件 + GetLastRowIndex/ColumnName |

---

## 12. 常见问题

**Q: Designer 有数据，Preview 没有？**  
A: 请使用已支持 Initial Rows 运行时加载的版本；属性名现为 **Initial Rows Json**。保存后重开 Preview。

**Q: Columns Json 填了但画布仍像空表？**  
A: 检查 JSON 是否合法；`type` 是否大小写完全匹配；改属性后失焦以刷新画布。

**Q: ComboBox 选不中？**  
A: 值必须出现在**有效** items 中：若该行写了非空的 `items` 覆盖则用行级列表，否则用 Columns Json 列上的 `items`。

**Q: 缓存了 Cell Control Id，删行后读错？**  
A: Id 含行号，删行后需重新 Get MixTable Cell Control Id。

**Q: 想给某一格单独 Bind Click？**  
A: 当前不支持；绑 MixTable 的 Click/Change，用 LastColumnName 分支。

---

## 13. 版本与部署提醒

- 编译项目：`F2B.Forms`、`F2B.Forms.Designer`
- OpenRPA 加载新的 `F2B.Forms.dll`
- Designer 使用新的 `F2B.Forms.Designer.exe`

文档对应能力以当前仓库实现为准；若 Activity 显示名有本地化差异，以工具箱 **MixTable** 分组下实际名称为准。
