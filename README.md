# DeskTool - Windows OCR & PDF Tools

A native Windows 10/11 offline-first application for Image OCR and PDF manipulation.

![Windows](https://img.shields.io/badge/Windows-10%2F11-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-purple)
![WinUI](https://img.shields.io/badge/WinUI-3.0-green)

## Features

### 🖼️ Image OCR
- **Drag & drop** or `Ctrl+O` to open images (PNG, JPG, WEBP, TIFF)
- **Multi-language OCR**: English, Vietnamese, German
- **Image preprocessing**: Grayscale, threshold, rotate 90°/180°
- **Crop selection**: Select specific region for OCR
- **Export**: Copy to clipboard, save as .txt or .docx

### 📄 PDF Tools
- **Preview**: Thumbnail navigation + full page viewer
- **Merge**: Combine multiple PDFs into one
- **Split**: Extract pages by range (e.g., `1-3, 5, 7-10`)
- **Reorder**: Drag-and-drop page reordering
- **Rotate**: Rotate pages 90°/180°/270°
- **Extract Text**: Native text layer or OCR for scanned PDFs
- **Searchable PDF**: Create text layer from scanned documents

## Requirements

- Windows 10 version 1809 (build 17763) or higher
- Windows 11 supported
- ~200MB disk space (including Tesseract language data)

## Quick Start

### Option 1: Install MSIX (Recommended)
1. Download `DeskTool_x64.msix` from [Releases](../../releases)
2. Double-click to install
3. Launch from Start Menu

### Option 2: Build from Source

#### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Windows App SDK 1.6](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/)
- Visual Studio 2022 (optional, for development)

#### Build Steps

```powershell
# Clone repository
git clone https://github.com/yourusername/DeskTool.git
cd DeskTool

# Download Tesseract language data
mkdir tessdata
# Download from: https://github.com/tesseract-ocr/tessdata
# Required files: eng.traineddata, vie.traineddata, deu.traineddata

# Build
.\build\Build-DeskTool.ps1 -Configuration Release

# Run tests
.\build\Build-DeskTool.ps1 -Test

# Create MSIX package
.\build\Build-DeskTool.ps1 -Configuration Release -Package
```

## Project Structure

```
DeskTool/
├── src/
│   ├── DeskTool/              # WinUI 3 Application
│   │   ├── Views/             # XAML pages
│   │   ├── ViewModels/        # MVVM ViewModels
│   │   ├── Converters/        # XAML value converters
│   │   └── Styles/            # App-wide styles
│   ├── DeskTool.Core/         # Business logic
│   │   ├── Models/            # Data models
│   │   └── Services/          # OCR, PDF, Image services
│   └── DeskTool.Tests/        # Unit tests
├── build/                     # Build scripts
├── tessdata/                  # Tesseract language files
└── README.md
```

## Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+O` | Open file (image or PDF depending on current view) |
| `Ctrl+C` | Copy text from results panel |
| `Ctrl+1` | Switch to Image OCR |
| `Ctrl+2` | Switch to PDF Tools |

## Technology Stack

| Component | Technology |
|-----------|------------|
| UI Framework | WinUI 3 (Windows App SDK 1.6) |
| OCR Engine | Tesseract 5.x via Tesseract.NET |
| PDF Library | PDFsharp 6.x + PDFtoImage |
| Image Processing | SixLabors.ImageSharp |
| MVVM | CommunityToolkit.Mvvm |
| Logging | Serilog |

## Configuration

### Tesseract Language Data
Download trained data files from [tesseract-ocr/tessdata](https://github.com/tesseract-ocr/tessdata):
- `eng.traineddata` - English
- `vie.traineddata` - Vietnamese  
- `deu.traineddata` - German

Place in `tessdata/` folder next to the executable.

### Logs
Application logs are stored in:
```
%LOCALAPPDATA%\DeskTool\Logs\
```

## Development

### Running Tests
```powershell
cd src/DeskTool.Tests
dotnet test --logger "console;verbosity=detailed"
```

### Adding New OCR Languages
1. Download `.traineddata` file from [tessdata repository](https://github.com/tesseract-ocr/tessdata)
2. Add to `tessdata/` folder
3. Update `OcrLanguage` enum in `OcrModels.cs`
4. Update `LanguageCodes` dictionary in `TesseractOcrService.cs`

## Troubleshooting

### OCR Not Working
- Ensure `tessdata/` folder exists with required `.traineddata` files
- Check logs in `%LOCALAPPDATA%\DeskTool\Logs\`

### PDF Not Loading
- Ensure PDF is not password-protected
- Check file is not corrupted

### App Won't Start
- Install [Windows App SDK Runtime](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)
- Ensure Windows 10 1809+ or Windows 11

## License

MIT License - see [LICENSE](LICENSE) file.

## Contributing

1. Fork the repository
2. Create feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open Pull Request

## Acknowledgments

- [Tesseract OCR](https://github.com/tesseract-ocr/tesseract) - OCR engine
- [PDFsharp](http://www.pdfsharp.net/) - PDF manipulation
- [ImageSharp](https://github.com/SixLabors/ImageSharp) - Image processing
- [Windows App SDK](https://github.com/microsoft/WindowsAppSDK) - Modern Windows UI
