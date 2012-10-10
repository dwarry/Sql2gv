using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

using Caliburn.Micro;

using Microsoft.FSharp.Core;

namespace Sql2gv.Ui
{
    public class ShellViewModel : Screen, IDisposable
    {
        private readonly IWindowManager _windowManager;
        private static readonly String OutputFile = Environment.ExpandEnvironmentVariables("%TMP%\\database{0}.png");

        private static readonly String PathToGraphviz = ConfigurationManager.AppSettings["PathToGraphviz"];
        private static readonly string GvFile = Environment.ExpandEnvironmentVariables("%TMP%\\database.gv");
        private readonly BindableCollection<String> _databases = new BindableCollection<String>();
        private readonly IMessageBoxManager _messageBoxManager;
        public readonly List<Table> _selectedTables = new List<Table>();
        private readonly BindableCollection<Table> _tables = new BindableCollection<Table>();
        private String _busyMessage;
        private Boolean _canCopyToClipboard;
        private Boolean _canEdit;
        private Boolean _canSave;
        private string _diagram;
        private Int32 _imageFileNumber;


        private Boolean _isBusy;
        private String _selectedDatabase;
        private String _sqlInstance = "";
        private IDisposable _tableSelectionObserver;
        private Boolean _useSimpleNodes;


        public ShellViewModel(IMessageBoxManager messageBoxManager, IWindowManager windowManager)
        {
            _windowManager = windowManager ?? new WindowManager();
            _messageBoxManager = messageBoxManager ?? new MessageBoxManager();

            _tableSelectionObserver = Observable.FromEventPattern<PropertyChangedEventArgs>(this,
                                                                                            "PropertyChanged")
                    .Where(x => x.EventArgs.PropertyName == "TableSelection")
                    .Throttle(TimeSpan.FromMilliseconds(750))
                    .Subscribe(x => Generate());
        }


        public ShellViewModel()
                : this(new MessageBoxManager(), new WindowManager()) {}

        #region Implementation of IDisposable

        private Boolean _disposed;


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }


        ~ShellViewModel()
        {
            Dispose(false);
        }


        protected virtual void Dispose(Boolean disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _tableSelectionObserver.Dispose();
                _tableSelectionObserver = null;
            }

            _disposed = true;
        }

        #endregion

        /// <summary>
        /// Flag that indicates that the ViewModel is busy with some operation
        /// and the UI should be disabled until it has been completed.
        /// </summary>
        public virtual Boolean IsBusy
        {
            get { return _isBusy; }
            set
            {
                if (_isBusy != value)
                {
                    _isBusy = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        public virtual String BusyMessage
        {
            get { return _busyMessage; }
            set
            {
                if (_busyMessage != value)
                {
                    _busyMessage = value;
                    NotifyOfPropertyChange();
                }
            }
        }


        public virtual String SqlInstance
        {
            get { return _sqlInstance; }
            set
            {
                if (_sqlInstance != value)
                {
                    _sqlInstance = value;
                    NotifyOfPropertyChange();
                    RetrieveDatabases();
                }
            }
        }

        private String ConnectionString
        {
            get
            {
                return String.Format(
                        "Server={0};Integrated Security=true;Initial Catalog={1};MultipleActiveResultSets=true",
                        SqlInstance,
                        SelectedDatabase);
            }
        }


        public IObservableCollection<String> Databases
        {
            get { return _databases; }
        }


        public virtual String SelectedDatabase
        {
            get { return _selectedDatabase; }
            set
            {
                if (_selectedDatabase != value)
                {
                    _selectedDatabase = value;
                    NotifyOfPropertyChange();
                    RetrieveTablesAsync();
                }
            }
        }


        public IObservableCollection<Table> Tables
        {
            get { return _tables; }
        }


        public virtual Boolean UseSimpleNodes
        {
            get { return _useSimpleNodes; }
            set
            {
                if (_useSimpleNodes != value)
                {
                    _useSimpleNodes = value;
                    NotifyOfPropertyChange();
                    Generate();
                }
            }
        }


        public virtual Boolean CanSave
        {
            get { return _canSave; }
            set
            {
                if (_canSave != value)
                {
                    _canSave = value;
                    NotifyOfPropertyChange();
                }
            }
        }


        public virtual Boolean CanCopyToClipboard
        {
            get { return _canCopyToClipboard; }
            set
            {
                if (_canCopyToClipboard != value)
                {
                    _canCopyToClipboard = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        public virtual Boolean CanEdit
        {
            get { return _canEdit; }
            set
            {
                if (_canEdit != value)
                {
                    _canEdit = value;
                    NotifyOfPropertyChange();
                }
            }
        }

        public virtual string Diagram
        {
            get { return _diagram; }
            set
            {
                //if (_diagram != value)
                //{
                _diagram = value;
                NotifyOfPropertyChange();
                //}
            }
        }


        /// <summary>
        /// Shadow the inherited version to use <see cref="CallerMemberNameAttribute"/>
        /// to provide the name of the property that has changed, rather than
        /// unpacking expressions or using magic strings.
        /// </summary>
        /// <param name="propertyName"></param>
        protected new void NotifyOfPropertyChange([CallerMemberName] String propertyName = null)
        {
            ((PropertyChangedBase) this).NotifyOfPropertyChange(propertyName);
        }


        private async void RetrieveDatabases()
        {
            if (String.IsNullOrWhiteSpace(SqlInstance))
            {
                return;
            }

            IsBusy = true;
            BusyMessage = "Retrieving Databases for " + SqlInstance;
            Databases.IsNotifying = false;
            try
            {
                Databases.Clear();
                IEnumerable<string> dbs = await RetrieveDatabasesAsync();
                Databases.AddRange(dbs);
            }
            finally
            {
                IsBusy = false;
                Databases.IsNotifying = true;
                Databases.Refresh();
            }
        }


        private async Task<IEnumerable<String>> RetrieveDatabasesAsync()
        {
            return await Task.Run(() =>
                {
                    try
                    {
                        return Model.retrieveDatabases(SqlInstance);
                    }
                    catch (Exception e)
                    {
                        _messageBoxManager.ShowMessage("Could not connect to database at " + SqlInstance);

                        return new string[0];
                    }
                });
        }


        private async void RetrieveTablesAsync()
        {
            IsBusy = true;
            BusyMessage = "Retrieving tables...";
            Tables.IsNotifying = false;
            Tables.Clear();
            _selectedTables.Clear();
            try
            {
                if (SelectedDatabase != null)
                {
                    using (var conn = new SqlConnection(ConnectionString))
                    {
                        await conn.OpenAsync();
                        IEnumerable<Table> tbls = await Task.Run(
                                () => Model.retrieveTables(conn,
                                                           new GenerationOptions(SelectedDatabase,
                                                                                 FSharpOption<String>.None,
                                                                                 new FSharpOption<string>("Id$"),
                                                                                 true)));
                        Tables.AddRange(tbls);
                    }
                }
            }
            finally
            {
                IsBusy = false;
                BusyMessage = String.Empty;
                Tables.IsNotifying = true;
                Tables.Refresh();
            }
        }


        public void TableSelectionChanged(SelectionChangedEventArgs args)
        {
            _selectedTables.AddRange(args.AddedItems.Cast<Table>());

            foreach (Table tbl in args.RemovedItems)
            {
                _selectedTables.Remove(tbl);
            }

            CanEdit = CanSave = CanCopyToClipboard = _selectedTables.Any();
            NotifyOfPropertyChange("TableSelection");
        }


        public void Save() {}

        public void ShowCredits()
        {
            var credits = IoC.Get<CreditsViewModel>();

            _windowManager.ShowDialog(credits);
        }

        public void CopyToClipboard()
        {
            BitmapSource bs = new BitmapImage(new Uri(Diagram));

            Clipboard.SetImage(bs);
        }


        private void Generate()
        {
            IsBusy = true;
            BusyMessage = "Generating Graphviz file";
            try
            {
                GenerateAsync();
            }
            finally
            {
                IsBusy = false;
                BusyMessage = "";
            }
        }


        private async void GenerateAsync()
        {
            using (var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();


                ForeignKey[] fks = await Task.Run(() =>
                                                  _selectedTables.SelectMany(x => Model.retrieveForeignKeys(conn,
                                                                                                            x.Id))
                                                          .ToArray()
                                                 );

                String gv = await Task.Run(() => GraphvizRenderer.generateDotFile(UseSimpleNodes,
                                                                                  _selectedTables,
                                                                                  fks));


                await Task.Run(() => File.WriteAllText(GvFile,
                                                       gv));
            }

            string oldFile = Diagram;

            string newFile = String.Format(OutputFile,
                                           ++_imageFileNumber);

            await GenerateImage("png",
                                newFile);

            if (File.Exists(newFile))
            {
                Diagram = newFile;
            }
            else
            {
                Diagram = null;
            }
        }


        private static async Task GenerateImage(String fileType, String outputFile)
        {
            string args = String.Format("-T{0} -o\"{1}\" \"{2}\"",
                                        fileType,
                                        outputFile,
                                        GvFile);


            var psi = new ProcessStartInfo(Path.Combine(PathToGraphviz,
                                                        "dot.exe"),
                                           args)
                {
                        UseShellExecute = false,
                        CreateNoWindow = true
                };

            await Task.Run(() =>
                {
                    Process proc = Process.Start(psi);

                    proc.WaitForExit();
                });
        }


        public void Edit()
        {
            string pathToGvedit = Path.Combine(PathToGraphviz,
                                               "gvedit.exe");

            string args = String.Concat("\"",
                                        GvFile,
                                        "\"");

            Process.Start(pathToGvedit,
                          args);
        }
    }
}
