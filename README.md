# Tech-Cosmos Require One Of

> **包名**：`com.techcosmos.requireoneof`  
> **版本**：**1.1.0**  
> **Unity**：2022.3+  
> **依赖**：无。不引用 Spatial2D 或其它 Tech-Cosmos 包。

一组组件里有且仅有一个。导入就能用。

---

## 安装

Package Manager → **Add package from disk**，选本包的 `package.json`。

或把本仓库根目录当作 UPM 包放进工程。

游戏工程里目前也有一份嵌入：`Assets/Package-TechCosmos/Tech-Cosmos.Editor.RequireOneOf`。

---

## 怎么用

两种写法，不要混在同一个特性里。

### 点名具体类

名单就是你写的那几个，顺序你定。没有就补**第一个**。

```csharp
using TechCosmos.RequireOneOf;
using UnityEngine;

[RequireOneOf(typeof(RectArea2D), typeof(CircleArea2D))]
public class Unit : MonoBehaviour { }
```

### 写抽象类或接口

只写一个抽象类或接口。编辑器会收集所有可挂的具体实现，按**类名字母序**排。没有就补字母序第一个。

```csharp
[RequireOneOf(typeof(Area2D))]
public class Unit : MonoBehaviour { }
```

`CircleArea2D` 排在 `RectArea2D` 前面，所以默认会补圆。

---

- 再挂同组另一个，旧的会被拆掉
- 两个都删光，会补回默认那个
- 可以写多组

Inspector 里，宿主或互斥组件上会有一排点选按钮，点哪个换哪个。切换时两边**同名同类型**的序列化字段会拷过去。

自定义 Editor 自己画面板时，在 `OnInspectorGUI` 开头加一行：

```csharp
RequireOneOfInspector.Draw(target);
```

然后若 `target == null` 就 return，避免切换后还去画已拆掉的组件。
