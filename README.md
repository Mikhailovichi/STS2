# STS2 Mods

**语言 / Language / 言語**: **简体中文** | [English](./README.en.md) | [日本語](./README.ja.md)

面向《Slay the Spire 2》的 mod 源码仓库，重点放在联机协作、战斗信息可视化和局内沟通效率。

## 当前活跃模组

- [CombatQuill](./CombatQuill): 在战斗与相关界面进行战术标注和即时沟通
- [DefeatBGM](./DefeatBGM): 替换失败结算音乐，支持从 mod 目录加载自定义本地曲目
- [FrozenEye](./FrozenEye): 让抽牌堆预览按真实抽牌顺序显示
- [PartyObserver](./PartyObserver): 查看队友当前已同步到本地的关键选择信息与界面状态
- [RandomVision](./RandomVision): 为 Crystal Sphere 与事件页面提供更透明的预览信息
- [RelicRpsChoice](./RelicRpsChoice): 将多人共享遗物冲突改成可见的石头剪刀布决胜流程

## 归档模组

- [legacy/DamageMeter](./legacy/DamageMeter): 保留历史源码与构建脚本，不再作为当前主维护模组

## 发布与版本规则

- 统一版本号规范见 [RELEASE_VERSIONING.md](./RELEASE_VERSIONING.md)
- 版本号遵循 `x.y.z`
- `x` 表示重大架构变更，`y` 表示功能新增，`z` 表示 Bug 修复与优化

## 本地构建说明

- 仓库默认只跟踪源码与文档，不包含 `.tools`、`_workspace`、`_release`、`mods/` 等本地工作目录
- `build-combatquill.ps1` 与 `build-partyobserver.ps1` 会优先使用仓库内 `.tools`，找不到时会继续尝试游戏目录 `modding/.tools` 或系统 `dotnet 9`
- 如果仓库不在游戏根目录下，构建时可通过 `-Sts2Path` 指定《Slay the Spire 2》安装目录
- 其他模组可直接使用各自目录下的 `*.csproj` 或 `*.sln` 进行构建

## 仓库约定

- 根目录只保留当前维护中的源码、文档和必要脚本
- `legacy/` 用于存放已归档但仍需保留历史的模组
- 打包产物、缓存、编辑器目录和游戏运行目录不纳入版本管理
