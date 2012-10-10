using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows;
using System.Windows.Threading;

namespace Sql2gv.Ui
{
    public interface IMessageBoxManager
    {
        void ShowMessage(String message);
    }

    public class MessageBoxManager : IMessageBoxManager
    {
        public void ShowMessage(String message)
        {
            Dispatcher.CurrentDispatcher.Invoke(() => MessageBox.Show(message));
        }

        
    }
}
