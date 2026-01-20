# NAPS2 - Not Another PDF Scanner

<p align="center">
<img src="https://www.naps2.com/images/naps2-desktop-win.png?1" width="400" alt="NAPS2 on Windows" /> <img src="https://www.naps2.com/images/naps2-desktop-mac.png?1" width="400" alt="NAPS2 on Mac" /> <img src="https://www.naps2.com/images/naps2-desktop-linux.png?1" width="400" alt="NAPS2 on Linux" />
  <br/>
  <i>NAPS2 on Windows, Mac, and Linux</i>
</p>

NAPS2 is a document scanning application with a focus on simplicity and ease of use. Scan your documents from WIA, TWAIN, SANE, and ESCL scanners, organize the pages as you like, and save them as PDF, TIFF, JPEG, or PNG. Optical character recognition (OCR) is available using [Tesseract](https://github.com/tesseract-ocr/tesseract).

System requirements:
- Windows 7+ (x64, x86)
- macOS 10.15+ (x64, arm64)
- Linux (x64, arm64) (GTK 3.20+, glibc 2.27+, libsane)

Visit the NAPS2 home page at [www.naps2.com](http://www.naps2.com).

Other links:
- [Downloads](https://www.naps2.com/download)
- [Documentation](https://www.naps2.com/support)
- [Translations](https://translate.naps2.com/)
- [File a Ticket](https://sourceforge.net/p/naps2/tickets/)
- [Donate](https://www.naps2.com/donate?src=readme)

## NAPS2.Sdk (for developers)

[![NuGet](https://img.shields.io/nuget/v/NAPS2.Sdk)](https://www.nuget.org/packages/NAPS2.Sdk/)

[NAPS2.Sdk](https://github.com/cyanfish/naps2/tree/master/NAPS2.Sdk) is a fully-featured scanning library, supporting WIA, TWAIN, SANE, and ESCL scanners on Windows, Mac, and Linux.
[Read more.](https://github.com/cyanfish/naps2/tree/master/NAPS2.Sdk)

## Build Instructions
Looking to contribute to NAPS2 or NAPS2.Sdk? Have a look at the [Github wiki](https://github.com/cyanfish/naps2/wiki/1.-Building-&-Development-Environment) for build instructions and more.

## License

NAPS2 is licensed under the GNU GPL 2.0 (or later). Some projects have additional license options:
- NAPS2.Escl.* - GNU LGPL 2.1 (or later)
- NAPS2.Images.* - GNU LGPL 2.1 (or later)
- NAPS2.Internals - GNU LGPL 2.1 (or later)
- NAPS2.Sdk - GNU LGPL 2.1 (or later)
- NAPS2.Sdk.Samples - MIT

## Custom Changes (Roll Management Fork)

This fork includes custom modifications for roll document scanning workflow:

### Scanner Error Handling Enhancement
**Files Modified:**
- `NAPS2.Sdk/Scan/Internal/Apple/DeviceOperator.cs`
- `NAPS2.Lib/ImportExport/AutoSaver.cs`

**Changes:**
1. **Flush Pending Images Before Error Propagation**
   - When a scanner error occurs during roll scanning (e.g., paper jam, cover open), the system now ensures all previously scanned images are saved before throwing the error
   - Previously, images that were scanned but still in the internal callback queue would be lost when a subsequent page encountered an error
   - Implementation: Modified `DeviceOperator.Scan()` to catch scan exceptions, wait for `_writeToCallback` to complete (flushing all pending images), then rethrow the exception

2. **Enhanced Logging for Debugging**
   - Added detailed logging markers in AutoSaver for tracking scan and save pipeline:
     - ✅ `RECEIVED` - Image received from scanner
     - 💾 `START SAVING` - Begin save operation
     - ✅ `SAVED` - Save completed successfully
     - 📤 `PRODUCED` - Image sent to UI
     - ⏭️ `COMPLETED` - Processing finished
     - ⚠️ `EXCEPTION` - Error occurred with statistics

3. **HTTP Server CORS Support**
   - Added CORS headers support for cross-origin requests
   - Enables web-based management interfaces to communicate with local scanner instances

### Use Case
These changes are designed for high-volume roll document scanning scenarios where:
- Multiple pages are scanned continuously
- Scanner errors may occur mid-batch (paper jams, document feeding issues)
- All successfully scanned pages must be preserved even when errors occur
- Web-based monitoring and control is required

### Build Configuration
See `.claude/claude.md` for detailed build and deployment instructions for the roll management setup.
