namespace Sql2gv

open System
open System.Collections.Generic
open Arguments


module MyConsole =
        
    let Run args =
        let serverArg = "s"
        let dbArg = "d"
        let includeArg = "i"
        let excludeArg = "x"
        // Define what arguments are expected
        let defs = [
                        {ArgInfo.Command=serverArg; Description="Sql Server name (including instance name)"; Required=true };
                        {ArgInfo.Command="d"; Description="Database name"; Required=true };
                        {ArgInfo.Command="i"; Description="Regular Expression pattern which specifies which tables 
should be included. Default value = 'dbo\..*'"; Required=false };
                        {ArgInfo.Command="x"; Description="Regular Expression pattern which specifies which tables 
should be excluded"; Required=false };
                        {ArgInfo.Command="o"; Description="Name of the output file. If not specified, output goes to
the standard output stream."; Required=false };
            
             ]

        // Parse Arguments into a Dictionary
        let parsedArgs = Arguments.ParseArgs args defs
        
        let connectionString =  String.Format("Server={0};Initial Catalog={1};Integrated Security=SSPI;MultipleActiveResultSets=True;",
                                              parsedArgs.[serverArg],
                                              parsedArgs.[dbArg])

        let options = {IncludePattern = parsedArgs.[includeArg]; ExcludePattern = parsedArgs.[excludeArg]; EmitSimpleNodes = true}

        // TODO add your code here 
        Arguments.DisplayArgs parsedArgs
        Console.ReadLine() |> ignore