# SMB快连

基于 .NET MAUI 的 Android SMB 客户端，用于连接局域网 SMB 服务器，浏览共享目录、预览文件、单个/批量下载。

## 功能

- SMB 连接：支持 IP、端口、共享名，SMB2/3 优先、自动回退 SMB1
- 文件管理：目录浏览、搜索、文件预览（文本/图片，视频/音频调起系统播放器）
- 下载：单个下载与批量下载，保存到手机「下载」目录
- 主题：暗色/亮色切换
- 语言：中文/英文切换

## 快速开始

环境要求：JDK 21、Android SDK、.NET 10 SDK（含 maui-android workload）。

```bash
cd /home/lds7871/CodeSave/Fast_smb
dotnet build -f net10.0-android -c Debug        # 构建
dotnet build -t:Run -f net10.0-android -c Debug  # 构建并部署到已连接设备
```

## 使用说明

1. 打开应用，输入服务器 IP（可带 `:端口` 或 `/共享名`）、用户名、密码
2. 连接成功后选择共享，进入文件管理
3. 点击文件夹进入，点击文件预览；右侧按钮单个下载，顶栏「批量」可多选下载
4. 更多说明见应用内「帮助」按钮

## 技术文档

架构说明、每个文件的用途、所用包、主题与语言切换机制、常见踩坑点，详见 [Markdown/技术文档.md](Markdown/技术文档.md)。
