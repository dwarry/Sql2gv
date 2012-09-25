namespace Sql2gv

open System
open System.Collections.Generic
open System.Data.SqlClient
open System.IO
open Arguments


module MyConsole =
        
        

    let private writeOutput (tables: seq<Table>) (foreignKeys: seq<ForeignKey>) (options: GenerationOptions) (filename: String option) =
        let write (w: TextWriter) = 
             w.WriteLine(GraphvizRenderer.generateDotFile options.EmitSimpleNodes tables foreignKeys) |> ignore
        match filename with
            | None -> write Console.Out
            | Some f -> using (File.CreateText(f)) write

    let Run args =
        let serverArg = "s"
        let dbArg = "d"
        let includeArg = "i"
        let excludeArg = "x"
        let outputArg = "o"

        // Define what arguments are expected
        let defs = [
                        {ArgInfo.Command=serverArg; Description="Sql Server name (including instance name)"; Required=true };
                        {ArgInfo.Command="d"; Description="Database name"; Required=true };
                        {ArgInfo.Command="i"; Description="Regular Expression pattern which specifies which tables 
should be included. Default value = 'dbo\..*'"; Required=false };
                        {ArgInfo.Command="x"; Description="Regular Expression pattern which specifies which tables 
should be excluded"; Required=false };
                        {ArgInfo.Command=outputArg; Description="Name of the output file. If not specified, output goes to
the standard output stream."; Required=false };
            
                   ]

        // Parse Arguments into a Dictionary
        let parsedArgs = Arguments.ParseArgs args defs
        
        let getArg (argName: string) =
            if parsedArgs.ContainsKey(argName) then Some(parsedArgs.[argName]) else None

        let db = parsedArgs.[dbArg]

        let connectionString =  String.Format("Server={0};Initial Catalog={1};Integrated Security=SSPI;MultipleActiveResultSets=True;",
                                              parsedArgs.[serverArg],
                                              db)

        let options = { Database= db; IncludePattern = getArg includeArg; ExcludePattern = getArg excludeArg; EmitSimpleNodes = false}

        use connection = new SqlConnection(connectionString)

        connection.Open() |> ignore

        let tables = Model.retrieveTables connection parsedArgs.[dbArg] options
        
        let foreignKeys = seq { for t in tables do yield! Model.retrieveForeignKeys connection db t.Id }
        
        writeOutput tables foreignKeys options (getArg outputArg) |> ignore

        ()