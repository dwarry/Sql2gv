using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

using Caliburn.Micro;

using Microsoft.FSharp.Core;

namespace Sql2gv.Ui
{
   

    public class ShellViewModel : Screen
    {
        private String _sqlInstance = "";
        private readonly BindableCollection<String> _databases = new BindableCollection<String>();
        private String _selectedDatabase;
        private readonly BindableCollection<ListBoxItem> _tables = new BindableCollection<ListBoxItem>();


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


        private Boolean _isBusy;

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

        private String _busyMessage;

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
                var dbs = await RetrieveDatabasesAsync();
                Databases.AddRange(dbs);
            }
            finally
            {
                IsBusy = false;
                Databases.IsNotifying = true;
                Databases.Refresh();
            }
        }


        private async Task<IEnumerable<String>>  RetrieveDatabasesAsync()
        {
            return await Task.Run(() => Model.retrieveDatabases(SqlInstance));


        }

        public IObservableCollection<String> Databases { get { return _databases; } }


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

        private async void RetrieveTablesAsync()
        {
            IsBusy = true;
            BusyMessage = "Retrieving tables...";
            Tables.IsNotifying = false;
            Tables.Clear();
            try
            {
                if (SelectedDatabase != null)
                {
                    using (var conn = new SqlConnection(ConnectionString))
                    {
                        await conn.OpenAsync();
                        var tbls = await Task.Run(
                                () => Model.retrieveTables(conn,
                                                           SelectedDatabase,
                                                           new GenerationOptions(SelectedDatabase,
                                                                                 FSharpOption<String>.None,
                                                                                 new FSharpOption<string>("Id$"), 
                                                                                 true)));
                        Tables.AddRange(tbls.Select(x => new ListBoxItem {Content = x.Id.Schema + "." + x.Id.Name, Tag=x}));
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


        public IObservableCollection<ListBoxItem> Tables { get { return _tables; } } 

        public void TableSelectionChanged()
        {
            Generate();
            CanSave = CanCopyToClipboard = Tables.Any(x => x.IsSelected);
        }


        private Boolean _useSimpleNodes;

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

        public void Save(){
        }


        private Boolean _canSave;

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

        public void CopyToClipboard()
        {
            BitmapSource bs = new BitmapImage(new Uri(Diagram));
            
            Clipboard.SetImage(bs);
        }


        private Boolean _canCopyToClipboard;

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
            var selectedTables = (from t in _tables
                                 where t.IsSelected
                                  select (Table)t.Tag).ToArray();

            using(var conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();


                var fks = await Task.Run(() =>

                                         selectedTables.SelectMany(x => Model.retrieveForeignKeys(conn,
                                                                                                  SelectedDatabase,
                                                                                                  x.Id))
                                                       .ToArray()
                                        );

                String gv = await Task.Run(() => GraphvizRenderer.generateDotFile(UseSimpleNodes,
                                                                                  selectedTables,
                                                                                  fks));


                await Task.Run(() => File.WriteAllText(GvFile,
                                                      gv));
                

            }

            var oldFile = Diagram;

            var newFile = String.Format(OutputFile,
                                        ++_imageFileNumber);

            await Task.Run(() =>
                {

                    var args = String.Format("-Tpng -o\"{0}\" \"{1}\"",
                                             newFile,
                                             GvFile);

                    
                    var psi = new ProcessStartInfo(PathToGraphviz,
                                                   args)
                        {
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };

                    var proc = Process.Start(psi);
                    
                    proc.WaitForExit();
                });

            if(File.Exists(newFile))
            {
                Diagram = newFile;
            }
            else
            {
                Diagram = null;
            }
        }


        private Int32 _imageFileNumber = 0;

        private string _diagram;

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

        private static readonly String OutputFile = Environment.ExpandEnvironmentVariables("%TMP%\\database{0}.png");

        private static readonly String PathToGraphviz = ConfigurationManager.AppSettings["PathToGraphviz"];
        private static readonly string GvFile = Environment.ExpandEnvironmentVariables("%TMP%\\database.gv");
    }
}
