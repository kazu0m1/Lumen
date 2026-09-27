using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.VisualBasic.FileIO;

namespace LumenApp
{
    internal static class ShellHelpers
    {
        public static bool SendToRecycleBin(string path)
        {
            try
            {
                FileSystem.DeleteFile(path, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static int RestoreBatch(DeleteBatch batch)
        {
            if (batch == null || batch.Paths == null || batch.Paths.Count == 0)
                return 0;

            int restored = 0;
            int i;
            for (i = batch.Paths.Count - 1; i >= 0; i--)
            {
                if (TryRestore(batch.Paths[i]))
                    restored++;
            }
            return restored;
        }

        private static bool TryRestore(string originalPath)
        {
            object shell = null;
            object recycle = null;
            object items = null;
            try
            {
                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null) return false;
                shell = Activator.CreateInstance(shellType);
                recycle = Invoke(shell, "NameSpace", new object[] { 10 });
                if (recycle == null) return false;
                items = Invoke(recycle, "Items", null);
                if (items == null) return false;

                object countObj = Get(items, "Count");
                int count = Convert.ToInt32(countObj);
                string wantedName = Path.GetFileName(originalPath);
                string wantedFolder = NormalizeFolder(Path.GetDirectoryName(originalPath));

                int i;
                for (i = count - 1; i >= 0; i--)
                {
                    object item = null;
                    try
                    {
                        item = Invoke(items, "Item", new object[] { i });
                        if (item == null) continue;
                        string name = Convert.ToString(Get(item, "Name"));
                        if (!string.Equals(name, wantedName, StringComparison.OrdinalIgnoreCase))
                            continue;

                        object deletedFromObj = Invoke(item, "ExtendedProperty", new object[] { "System.Recycle.DeletedFrom" });
                        string deletedFrom = NormalizeFolder(Convert.ToString(deletedFromObj));
                        if (!string.Equals(deletedFrom, wantedFolder, StringComparison.OrdinalIgnoreCase))
                            continue;

                        try { Invoke(item, "InvokeVerb", new object[] { "undelete" }); }
                        catch { Invoke(item, "InvokeVerb", new object[] { "RESTORE" }); }
                        return true;
                    }
                    catch { }
                    finally { ReleaseCom(item); }
                }
            }
            catch { }
            finally
            {
                ReleaseCom(items);
                ReleaseCom(recycle);
                ReleaseCom(shell);
            }
            return false;
        }

        public static void OpenInExplorer(string folder)
        {
            try
            {
                System.Diagnostics.Process.Start("explorer.exe", "\"" + folder + "\"");
            }
            catch { }
        }

        private static string NormalizeFolder(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            try { return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); }
            catch { return path.TrimEnd('\\', '/'); }
        }

        private static object Invoke(object target, string name, object[] args)
        {
            return target.GetType().InvokeMember(name, BindingFlags.InvokeMethod, null, target, args);
        }

        private static object Get(object target, string name)
        {
            return target.GetType().InvokeMember(name, BindingFlags.GetProperty, null, target, null);
        }

        private static void ReleaseCom(object value)
        {
            if (value == null) return;
            try
            {
                if (System.Runtime.InteropServices.Marshal.IsComObject(value))
                    System.Runtime.InteropServices.Marshal.FinalReleaseComObject(value);
            }
            catch { }
        }
    }
}
