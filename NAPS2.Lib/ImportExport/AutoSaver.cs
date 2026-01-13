using NAPS2.EtoForms;
using NAPS2.EtoForms.Notifications;
using NAPS2.ImportExport.Images;
using NAPS2.Pdf;
using NAPS2.Scan;

namespace NAPS2.ImportExport;

public class AutoSaver
{
    private readonly ErrorOutput _errorOutput;
    private readonly DialogHelper _dialogHelper;
    private readonly OperationProgress _operationProgress;
    private readonly ISaveNotify _notify;
    private readonly PdfExporter _pdfExporter;
    private readonly IOverwritePrompt _overwritePrompt;
    private readonly Naps2Config _config;
    private readonly ImageContext _imageContext;
    private readonly UiImageList _imageList;

    public AutoSaver(ErrorOutput errorOutput, DialogHelper dialogHelper,
        OperationProgress operationProgress, ISaveNotify notify, PdfExporter pdfExporter,
        IOverwritePrompt overwritePrompt, Naps2Config config, ImageContext imageContext, UiImageList imageList)
    {
        _errorOutput = errorOutput;
        _dialogHelper = dialogHelper;
        _operationProgress = operationProgress;
        _notify = notify;
        _pdfExporter = pdfExporter;
        _overwritePrompt = overwritePrompt;
        _config = config;
        _imageContext = imageContext;
        _imageList = imageList;
    }

    public IAsyncEnumerable<ProcessedImage> Save(AutoSaveSettings settings, IAsyncEnumerable<ProcessedImage> images)
    {
        return AsyncProducers.RunProducer<ProcessedImage>(async produceImage =>
        {
            int imageIndex = 0;
            var placeholders = Placeholders.All.WithDate(DateTime.Now);

            try
            {
                await foreach (var img in images)
                {
                    // Save each image immediately as it arrives
                    bool success = await SaveSingleImage(settings, placeholders, imageIndex++, img);

                    if (!success)
                    {
                        Log.Error($"Failed to save image {imageIndex}");
                    }

                    // Pass image through if not clearing after save
                    if (!settings.ClearImagesAfterSaving)
                    {
                        produceImage(img.Clone());
                    }

                    // Dispose original image if clearing after save
                    if (settings.ClearImagesAfterSaving)
                    {
                        img.Dispose();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.ErrorException(MiscResources.AutoSaveError, ex);
                // Don't show error dialog - it blocks UI thread
                // _errorOutput.DisplayError(MiscResources.AutoSaveError, ex);
            }
        });
    }

    private async Task<bool> SaveSingleImage(AutoSaveSettings settings, Placeholders placeholders, int imageIndex, ProcessedImage image)
    {
        try
        {
            string subPath = placeholders.Substitute(settings.FilePath, true, imageIndex);
            Log.Info($"[AutoSaver] Saving image {imageIndex}: {subPath}");

            if (settings.PromptForFilePath)
            {
                string? newPath = null!;
                if (Invoker.Current.InvokeGet(() => _dialogHelper.PromptToSavePdfOrImage(subPath, out newPath)))
                {
                    subPath = placeholders.Substitute(newPath!, true, imageIndex);
                }
                else
                {
                    return false;
                }
            }

            var extension = Path.GetExtension(subPath);
            Log.Info($"[AutoSaver] Detected extension: {extension}");

            // For JPEG/PNG/etc, save immediately (not PDF which requires all pages)
            if (extension != null && !extension.Equals(".pdf", StringComparison.InvariantCultureIgnoreCase))
            {
                var op = new SaveImagesOperation(_overwritePrompt, _imageContext);
                if (op.Start(subPath, placeholders, new[] { image }, _config.Get(c => c.ImageSettings)))
                {
                    // Run in background - no progress dialog
                    // _operationProgress.ShowProgress(op);  // Commented out to run silently
                }
                bool success = await op.Success;
                if (success)
                {
                    _imageList.MarkSaved(_imageList.CurrentState, new[] { image });
                }
                return success;
            }
            else
            {
                Log.Error("[AutoSaver] PDF format requires buffering all images. Consider using image format (JPG/PNG) for immediate save.");
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.ErrorException($"[AutoSaver] Error saving image {imageIndex}", ex);
            // Don't show error dialog - it blocks UI thread during auto save
            // _errorOutput.DisplayError(MiscResources.AutoSaveError, ex);
            return false;
        }
    }

    private async Task<bool> InternalSave(AutoSaveSettings settings, List<ProcessedImage> images)
    {
        try
        {
            bool ok = true;
            var placeholders = Placeholders.All.WithDate(DateTime.Now);
            int i = 0;
            string? firstFileSaved = null;
            var scans = SaveSeparatorHelper.SeparateScans(new[] { images }, settings.Separator).ToList();
            foreach (var imagesToSave in scans)
            {
                (bool success, string? filePath) =
                    await SaveOneFile(settings, placeholders, i++, imagesToSave, scans.Count == 1);
                if (success)
                {
                    // Normally we're supposed to take the CurrentState before the save operation starts, but that
                    // doesn't really work here since populating the UiImageList happens asynchronously so the images
                    // we're saving might not be present yet. In practice waiting until after saving will ensure the
                    // list is populated so that this logic works correctly.
                    _imageList.MarkSaved(_imageList.CurrentState, imagesToSave);
                    firstFileSaved ??= filePath;
                }
                else
                {
                    ok = false;
                }
            }
            // TODO: Shouldn't this give duplicate notifications?
            if (scans.Count > 1 && ok)
            {
                // Can't just do images.Count because that includes patch codes
                int imageCount = scans.SelectMany(x => x).Count();
                _notify.ImagesSaved(imageCount, firstFileSaved!);
            }
            return ok;
        }
        catch (Exception ex)
        {
            Log.ErrorException(MiscResources.AutoSaveError, ex);
            _errorOutput.DisplayError(MiscResources.AutoSaveError, ex);
            return false;
        }
    }

    private async Task<(bool, string?)> SaveOneFile(AutoSaveSettings settings, Placeholders placeholders, int i,
        List<ProcessedImage> images, bool doNotify)
    {
        if (images.Count == 0)
        {
            return (true, null);
        }
        string subPath = placeholders.Substitute(settings.FilePath, true, i);
        Log.Info($"[AutoSaver] Original FilePath: {settings.FilePath}");
        Log.Info($"[AutoSaver] Substituted subPath: {subPath}");
        if (settings.PromptForFilePath)
        {
            string? newPath = null!;
            if (Invoker.Current.InvokeGet(() => _dialogHelper.PromptToSavePdfOrImage(subPath, out newPath)))
            {
                subPath = placeholders.Substitute(newPath!, true, i);
            }
            else
            {
                return (false, null);
            }
        }
        // TODO: This placeholder handling is complex and wrong in some cases (e.g. FilePerScan with ext = "jpg")
        // TODO: Maybe have initial placeholders that replace date, then rely on the ops to increment the file num
        var extension = Path.GetExtension(subPath);
        Log.Info($"[AutoSaver] Detected extension: {extension}");
        if (extension != null && extension.Equals(".pdf", StringComparison.InvariantCultureIgnoreCase))
        {
            if (File.Exists(subPath))
            {
                subPath = placeholders.Substitute(subPath, true, 0, 1);
            }
            var op = new SavePdfOperation(_pdfExporter, _overwritePrompt);
            if (op.Start(subPath, placeholders, images, _config.Get(c => c.PdfSettings), _config.DefaultOcrParams()))
            {
                _operationProgress.ShowProgress(op);
            }
            bool success = await op.Success;
            if (success && doNotify)
            {
                _notify.PdfSaved(subPath);
            }
            return (success, subPath);
        }
        else
        {
            var op = new SaveImagesOperation(_overwritePrompt, _imageContext);
            if (op.Start(subPath, placeholders, images, _config.Get(c => c.ImageSettings)))
            {
                _operationProgress.ShowProgress(op);
            }
            bool success = await op.Success;
            if (success && doNotify && op.FirstFileSaved != null)
            {
                _notify.ImagesSaved(images.Count, op.FirstFileSaved);
            }
            return (success, subPath);
        }
    }
}