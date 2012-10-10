using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sql2gv.Ui
{
    public interface IFileDialogManager
    {
        String ShowFileSaveAsDialog(String filename);
    }

    public class FileDialogManager : IFileDialogManager
    {
        private String _previousDirectoryPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        public String ShowFileSaveAsDialog(String filename)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog()
                {
                        AddExtension = false,
                        CheckPathExists = true,
                        CreatePrompt = false,
                        DefaultExt = ".svg",
                        Filter = "Portable Network Graphics|*.png|Structured Vector Graphics|*.svg",
                        FileName = filename,
                        InitialDirectory = _previousDirectoryPath
                };

            var result =  dlg.ShowDialog().GetValueOrDefault(false);

            if(result)
            {
                _previousDirectoryPath = Path.GetDirectoryName(dlg.FileName);
                return dlg.FileName;
            }

            return null;
        }

    }
}
