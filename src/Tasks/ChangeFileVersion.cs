using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
using WixToolset.Dtf.WindowsInstaller;

namespace Slido.Build
{
    public class ChangeFileVersion : Task
    {
        [Required]
        public ITaskItem[] Files { get; set; }
        public string MsiPath { get; set; }
        public override bool Execute()
        {
            try
            {
                if (string.IsNullOrEmpty(MsiPath))
                {
                    Log.LogError("MSI file not found in the output directory.");
                    return false;
                }

                if (Files == null || Files.Length == 0)
                {
                    Log.LogWarning("No files specified for version change.");
                    return true;
                }
                Log.LogMessage(MessageImportance.Normal, $"Updating file version in MSI: {MsiPath}");

                int updatedCount = 0;
                using (var database = new Database(MsiPath, DatabaseOpenMode.Transact))
                {
                    foreach (var file in Files)
                    {
                        var fileName = file.ItemSpec;
                        var version = file.GetMetadata("Version");

                        Log.LogMessage(MessageImportance.High, $"Setting version {version} for the file {fileName}");

                        if (string.IsNullOrEmpty(version))
                        {
                            Log.LogWarning($"No version specified for the file: {fileName}");
                            continue;
                        }

                        if (UpdateFileVersion(database, fileName, version))
                        {
                            updatedCount++;
                            Log.LogMessage(MessageImportance.High, $"Updated {fileName} to version {version}");
                        }
                        else
                        {
                            Log.LogWarning($"File not found in MSI File table: {fileName}");
                        }
                    }
                    database.Commit();
                }

                Log.LogMessage(MessageImportance.Normal, "File version update completed successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Failed to update file version: {ex.Message}");
                return false;
            }
        }

        private bool UpdateFileVersion(Database database, string fileName, string version)
        {
            try
            {
                var sql = "UPDATE `File` SET `Version` = ? WHERE `File` = ?";

                using var view = database.OpenView(sql);
                using var record = new Record(2);
                record[1] = version;
                record[2] = fileName;

                view.Execute(record);
                return true;
            }
            catch (Exception ex)
            {
                Log.LogError($"Error updating file version for {fileName}: {ex.Message}");
                return false;
            }
        }
    }
}
