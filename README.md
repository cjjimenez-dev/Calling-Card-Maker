# Calling Card Studio

A Windows desktop application built with **C# / .NET 10 (WPF)** for designing double-sided calling cards (business cards) and managing print queues with automated sheet imposition, crop marks, cut lines, and mirrored duplex alignment.

---

## ✨ Features

### 1. Visual Calling Card Designer (Front & Back)
* **Templates & Presets:**
  * **Vantage Consulting (Landscape 3.5" × 2.0")** — pre-configured front and back card layout.
  * **Blank Canvas** — Landscape (`3.5" × 2.0"`) or Portrait (`2.0" × 3.5"`).
  * **In-App Saved Presets & Live Auto-Save:** Clicking **Save** stores your card in the app's preset library (`%LocalAppData%\CallingCardStudio\SavedTemplates\`), and edits auto-save continuously so your latest work is restored when you reopen the app. Use **Save As...** / **Open...** to export or load standalone `.cct.json` template files.
* **Elements & Tools:**
  * **Text:** Multi-line text, font family, size (pt), Bold / Italic / Underline, Left / Center / Right alignment, and color chips or custom Hex.
  * **Images & Logos:** Import PNG, JPG, BMP, or GIF; adjust opacity, stretch mode (`Uniform`, `UniformToFill`, `Fill`), rotate (`-90°`, `+90°`, `180°`, or custom angle), and flip horizontally/vertically.
  * **QR Codes (`QRCoder`):** Built-in vector-sharp QR code generator supporting **Website URLs**, **vCard Contact** (auto-filled from your card's text fields), and custom text with customizable foreground/background colors (including transparent).
  * **Shapes & Lines:** Divider lines and rectangles with customizable fill, stroke, thickness, and corner radius.
  * **Backgrounds:** Solid color swatches, live Hex input, or full-bleed background images independently for both **Front Side** and **Back Side**.
* **Canvas Productivity:**
  * **Multi-Select & Grouping:** Drag a selection box or `Ctrl+Click` / `Shift+Click` multiple elements, then **Group (`Ctrl+G`)** or **Ungroup (`Ctrl+Shift+G`)** to move, resize, align, duplicate, or reorder them together.
  * **Undo / Redo:** `Ctrl+Z` / `Ctrl+Y` (up to 50 steps).
  * **Alignment & Layer Order:** Snap elements to card edges/centers (`Left`, `Center H`, `Right`, `Top`, `Center V`, `Bottom`) and control layer order (**To Front**, **To Back**, **Bring Forward**, **Send Backward**, or editable **Layer Index**).
  * **Guides & Zoom:** Toggleable **Safe Area Margin** (green dashed guide) and **Bleed / Cut Line** (red dotted guide), plus `75%`–`300%` canvas zoom.
  * **300 DPI Export:** Export Front or Back side as high-resolution `300 DPI` PNG/JPG images.

### 2. Print Queue & Sheet Imposition Engine
* **Print Queue:** Add multiple card designs with custom quantities and per-item side options (**Front & Back**, **Front Only**, **Back Only**).
* **Paper & Grid Layout:**
  * Supports **Letter (`8.5" × 11"`)** and **A4 (`210 × 297 mm`)** in **Portrait** or **Landscape** paper orientation.
  * Grid presets: **Standard 10-Up (`2 × 5`)**, **8-Up (`2 × 4`)**, **9-Up (`3 × 3`)**, and **12-Up (`4 × 3`)**, with **Auto-Center Grid** and manual margin controls.
* **Duplex & Registration:**
  * **Manual 2-Pass Duplex:** Prints all Front sheets first, prompts you to flip the paper stack, then prints all Back sheets with **automatically mirrored columns** for front-to-back alignment.
  * **Automatic Hardware Duplex** & **Single-Sided** modes.
* **Cutting Guides & Zoomable Preview:**
  * Optional corner **Crop Marks (L-ticks)** and dashed **Cutting Grid Guidelines**.
  * **Fit Page** / `50%`–`200%` / `Ctrl + Mouse Wheel` zoomable sheet preview, **Export Sheets PNG** (`300 DPI`), and direct **Print Cards** via Windows Print Dialog.

---

## 🛠️ Requirements & Running from Source

* [.NET 10 SDK (Windows)](https://dotnet.microsoft.com/download) (`net10.0-windows`)

```powershell
# Run the application
dotnet run

# Run the built-in smoke test suite (templates, 300 DPI render, 10-up imposition)
dotnet run -- --smoke-test
```

### Build a Portable Self-Contained Release (No .NET Install Required for End Users)
```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o "Dist\Calling Card Studio"
```
Keep the `Assets` folder alongside `Calling Card Studio.exe` when distributing.

---

## 📁 Project Structure
```text
├── Assets/                 # App icon, logos, and default card graphics
├── Controls/               # Interactive WPF card canvas (selection, drag, resize, guides)
├── Models/                 # CardTemplate, CardSide, Text/Image/Shape/QrCode/Group elements, SheetLayout
├── Services/               # CardRenderer (300 DPI), PrintImpositionEngine, QrCodeHelper, TemplateService
├── MainWindow.xaml(.cs)    # Designer & Print Queue UI
└── App.xaml(.cs)           # Startup & CLI --smoke-test runner
```
