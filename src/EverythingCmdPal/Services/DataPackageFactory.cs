using System;
using System.IO;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace EverythingCmdPal.Services;

internal static class DataPackageFactory
{
    /// <summary>
    /// Builds a package that exposes the file/folder as StorageItems so it can be
    /// dragged from Command Palette into Explorer, Outlook, mail clients, etc.
    /// </summary>
    public static DataPackage ForPath(string path)
    {
        var package = new DataPackage
        {
            RequestedOperation = DataPackageOperation.Copy,
            Properties =
            {
                Title = Path.GetFileName(path.TrimEnd('\\')),
                Description = Path.GetDirectoryName(path) ?? string.Empty,
            },
        };

        package.SetDataProvider(StandardDataFormats.StorageItems, async request =>
        {
            var deferral = request.GetDeferral();
            try
            {
                if (Directory.Exists(path))
                {
                    IStorageItem[] items = [await StorageFolder.GetFolderFromPathAsync(path)];
                    request.SetData(items);
                }
                else if (File.Exists(path))
                {
                    IStorageItem[] items = [await StorageFile.GetFileFromPathAsync(path)];
                    request.SetData(items);
                }
            }
            catch
            {
                // Fall through: target simply receives no StorageItems.
            }
            finally
            {
                deferral.Complete();
            }
        });

        return package;
    }
}
