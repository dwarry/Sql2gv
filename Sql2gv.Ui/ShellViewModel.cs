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

using System.Windows.Controls;
using Caliburn.Micro;

using Microsoft.FSharp.Core;

namespace Sql2gv.Ui
{
    public interface IShellViewModel
    {
        Boolean IsBusy { get; }

        String BusyMessage { get; }

        String SqlInstance { get; set; }
        
        IObservableCollection<String> Databases { get; }
        
        String SelectedDatabase { get; set; }
    }

    public class ShellViewModel : Screen, IShellViewModel
    {
        private String _sqlInstance = "";
        private readonly BindableCollection<String> _databases = new BindableCollection<String>();
        private String _selectedDatabase;
        private readonly BindableCollection<ListBoxItem> _tables = new BindableCollection<ListBoxItem>();


        protected void NotifyPropertyChanged([CallerMemberName] String propertyName = null)
        {
            NotifyOfPropertyChange(propertyName);    
        }


        private Boolean _isBusy;

        public virtual Boolean IsBusy
        {
            get { return _isBusy; }
            set
            {

                if (_isBusy != value)
                {
                    _isBusy = value;
                    NotifyPropertyChanged();
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
                    NotifyPropertyChanged();
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
                    NotifyPropertyChanged();
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
                    NotifyPropertyChanged();
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
                    NotifyPropertyChanged();
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
                    NotifyPropertyChanged();
                //}
            }
        }

        private static readonly String OutputFile = Environment.ExpandEnvironmentVariables("%TMP%\\database{0}.png");

        private static readonly String PathToGraphviz = ConfigurationManager.AppSettings["PathToGraphviz"];
        private static readonly string GvFile = Environment.ExpandEnvironmentVariables("%TMP%\\database.gv");
    }
}
