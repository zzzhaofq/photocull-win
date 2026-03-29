## 项目：PhotoCull Windows 版

### 概述
Windows 桌面应用，从 macOS 原生 PhotoCull 完整移植。面向专业摄影师的照片筛选（Culling）工具。处理 RAW 照片，通过 AI 评分 + 智能分组辅助摄影师快速完成选片工作流。

### 技术栈
- **平台**: Windows 10+ (x64)
- **语言**: C# 12
- **UI**: WPF (.NET 8)
- **数据**: EF Core 8 + SQLite
- **MVVM**: CommunityToolkit.Mvvm
- **RAW 处理**: LibRaw (P/Invoke)
- **EXIF**: MetadataExtractor.NET
- **AI 评分**: OpenCvSharp4
- **人脸检测**: OpenCvSharp Haar Cascade
- **场景分组**: OpenCvSharp 颜色直方图相关性
- **加密**: System.Security.Cryptography (HMAC-SHA256)

### 项目结构
```
photocull-win/
├── PhotoCull.sln
├── CLAUDE.md
├── src/PhotoCull/
│   ├── PhotoCull.csproj
│   ├── App.xaml / App.xaml.cs
│   ├── Models/
│   │   ├── Photo.cs, PhotoGroup.cs, CullingSession.cs
│   │   ├── Enums.cs
│   │   └── LicenseValidator.cs
│   ├── Data/
│   │   ├── PhotoCullDbContext.cs
│   │   └── DesignTimeDbContextFactory.cs
│   ├── Services/
│   │   ├── LibRaw/ (LibRawInterop.cs, RawPreviewExtractor.cs)
│   │   ├── ExifReader.cs, AiScorer.cs
│   │   ├── PhotoGrouper.cs, XmpExporter.cs
│   ├── ViewModels/
│   │   ├── MainViewModel.cs, ImportViewModel.cs
│   │   ├── CullingViewModel.cs, ExportViewModel.cs
│   ├── Views/ (11 个 XAML 视图)
│   ├── Converters/CommonConverters.cs
│   ├── Helpers/ (ThumbnailCache.cs, UndoRedoManager.cs)
│   ├── Themes/Generic.xaml
│   └── libs/libraw.dll
└── tests/PhotoCull.Tests/ (xUnit 测试)
```

### 核心工作流
1. **导入**: 选择文件夹 → 扫描 RAW/JPEG → 提取预览(8并发) → 读 EXIF → AI 评分 → 自动淘汰最低 20% → 智能分组
2. **快速筛选**: 双 Tab（淘汰/保留），X 淘汰、P 保留、1-5 星标、Z 撤销
3. **组内精选**: 左右分栏，AI 推荐最佳，Enter 接受/C 对比/Tab 下组
4. **导出**: 复制/移动文件 + XMP sidecar (Lightroom 兼容)

### 授权系统
- 算法: HMAC-SHA256，密钥 `PhotoCull-2024-SecretKey-X9k2mP`
- 格式: `base64(payload).base64(hmac)`
- 与 macOS 版完全兼容

### AI 评分算法
- 锐度: Laplacian 方差 (OpenCvSharp)
- 曝光: 直方图分析
- 构图: 固定 50.0
- 人脸: Haar Cascade 检测
- 权重: 有人脸 0.40/0.25/0.20/0.15 / 无人脸 0.47/0.29/0.24

### 智能分组算法
- 连拍: 间隔 < 3 秒
- 场景: 60 秒窗 + HSV 颜色直方图相关性 > 0.7
- 剩余 → 单张组

### 构建
```bash
dotnet build photocull-win/PhotoCull.sln
dotnet test photocull-win/tests/PhotoCull.Tests/PhotoCull.Tests.csproj
```

### 注意事项
- libraw.dll 需要放在 libs/ 目录，会自动复制到输出目录
- haarcascade_frontalface_default.xml 需要从 OpenCV 数据目录获取
- 数据库存储在 %LocalAppData%\PhotoCull\photocull.db
