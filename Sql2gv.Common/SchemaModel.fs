namespace Sql2gv

open System
open System.Collections.Generic
open System.Data
open System.Data.SqlClient
open System.Linq

/// <summary>
/// Details of a column in a table.
/// </summary>
type Column = { 
    
    /// <summary>
    /// Name of the column
    /// </summary>
    Name: string; 
    
    /// <summary>
    /// Datatype of the column, in usual SQL style (e.g. NVARCHAR(123), Decimal etc.)
    /// </summary>
    DataType: string; 
    
    /// <summary>
    /// Whether or not the column can be null.
    /// </summary>
    IsNullable: Boolean
}

/// <summary>
/// Identifier for a table.
/// </summary>
type TableId = 
    {
        /// <summary>
        /// Schema that contains the table.
        /// </summary>
        Schema : string; 
        
        /// <summary>
        /// Name of the table.
        /// </summary>
        Name : String
    }
    override this.ToString() = this.Schema + "." + this.Name
    
/// <summary>
/// Details of the structure of a table.
/// </summary>
type Table = 
    {
        /// <summary>
        /// Id of the table
        /// </summary> 
        Id: TableId; 

        /// <summary>
        /// The columns that comprise the table.
        /// </summary>
        Columns: seq<Column>;
        
        /// <summary>
        /// Names of the columns in the Primary Key.
        /// </summary> 
        PrimaryKeyColumnNames: seq<String> 
    }
    override this.ToString() = this.Id.ToString()



/// <summary>
/// Different options for the cardinality of a relationship between two tables.
/// It may not be possible to determine these exactly from just the foreign key
/// definitions alone, we may introduce heuristics later to provide our best guess.
/// </summary>
type Cardinality = 
    | Indeterminate = 0
    | OneToZeroOrOne = 1
    | OneToOne = 2
    | OneToZeroOrMany = 3
    | OneToMany = 4
    | ZeroOrOneToZeroOrOne = 5
    | ZeroOrOneToOne = 6
    | ZeroOrOneToZeroOrMany = 7
    | ZeroOrOneToMany = 8


/// <summary>
/// Details of a foreign key relationship from one table to another. 
/// </summary>
type ForeignKey = 
    { 
        /// <summary>
        /// Name of the foreign key constraint in the database.
        /// </summary>
        Name : string; 

        /// <summary>
        /// Id of the "parent" table which contains the primary key end
        /// of the relationship.
        /// </summary>
        PrimaryKeyTableId: TableId;
        
        /// <summary>
        /// Id of the "child" end of the relationship, which contains the
        /// foreign key end of the relationship.
        /// </summary> 
        ForeignKeyTableId: TableId; 

        /// <summary>
        /// Columns in the Foreign Key table that correspond to the 
        /// primary key columns on the parent table.
        /// </summary>
        ForeignKeyColumnNames: seq<String>
    }

/// <summary>
/// Module that provides functions that retrieve the schema information from a
/// database and convert it to the corresponding data types.
/// </summary>
module Model =
    /// <summary>
    /// Retrieves many rows from a stored procedure.
    /// </summary>
    /// <typeparam name="T">The type of the return values.</typeparam>
    /// <param name="connection">The connection to the sql server instance.</param>
    /// <param name="sproc">The name of the stored procedure to invoke.</param>
    /// <param name="parameters">Sequence of name/value pairs that will be used as the parameters to the sproc.</param>
    /// <param name="map">Function that extracts the data from the SqlDataReader and converts them
    /// to something more useful. If this returns None, the row is omitted from the output sequence.</param>
    /// <returns>A sequence of values of type T.</returns>
    let private retrieveMany (connection: SqlConnection) 
                             (sproc: string) 
                             (parameters: seq<string * #obj>) 
                             (map: SqlDataReader -> 'T option) = 
        
        seq {
            use cmd = connection.CreateCommand()
            cmd.CommandType <- CommandType.StoredProcedure
            cmd.CommandText <- sproc
            for p in parameters do cmd.Parameters.AddWithValue(fst p, snd p) |> ignore
            use reader = cmd.ExecuteReader()
            while reader.Read() do 
                let result = map reader
                if Option.isSome result then yield result.Value
        } 

    /// <summary>
    /// Retrieves zero or one rows by executing a stored procedure. 
    /// </summary>
    /// <typeparam name="T">The type of the return value.</typeparam>
    /// <param name="connection">The connection to the sql server instance.</param>
    /// <param name="sproc">The name of the stored procedure to invoke.</param>
    /// <param name="parameters">Sequence of name/value pairs that will be used as the parameters to the sproc.</param>
    /// <param name="map">Function that extracts the data from the SqlDataReader and converts it 
    /// to something more useful. </param>
    /// <returns>A optional value of type T.</returns>
    let private retrieveOne (connection: SqlConnection) 
                            (sproc: string) 
                            (parameters: seq<string * #obj>) 
                            (map: SqlDataReader -> 'T) = 
        
        use cmd = connection.CreateCommand()
        cmd.CommandType <- CommandType.StoredProcedure
        cmd.CommandText <- sproc
        for p in parameters do cmd.Parameters.AddWithValue(fst p, snd p) |> ignore
        use reader = cmd.ExecuteReader()
        if reader.Read() then Some (map reader) else None
        
    /// <summary>
    /// Retrieves the name of the primary key of the specified table.
    /// </summary>
    /// <param name="connection">Connection to the database being queried.
    /// </param>
    /// <param name="tableId">Id of the table being queried.</param>
    let private retrievePrimaryKey connection tableId = 
        retrieveMany connection
                     "sp_pkeys"
                     [ ("@table_qualifier", connection.Database);
                       ("@table_owner", tableId.Schema);
                       ("@table_name", tableId.Name)]
                     (fun r -> Some (r.GetString(3)))
        
    /// <summary>
    /// Retrieves details of the columns for the specified tables.
    /// </summary>
    /// <param name="connection">Connection to the database being queried.</param>
    /// <param name="tableId">Id of the table being queried.</param>
    let private retrieveColumns connection tableId =
        retrieveMany connection
                 "sp_columns"
                 [("@table_qualifier", connection.Database);
                  ("@table_owner", tableId.Schema);
                  ("@table_name", tableId.Name)]
                 (fun r ->
                     let dataType = fun dt (size: int) (prec: int16) -> 
                                     match dt with
                                     | "char" | "nchar" | "varchar" | "nvarchar" -> String.Concat(dt, "(", size, ")")
                                     | "decimal" -> String.Concat(dt, "(", size, ",", prec, ")")
                                     | _ -> dt 
                     Some {
                             Name = r.GetString(3); 
                             DataType = dataType (r.GetString(5)) (r.GetInt32(6)) (if r.IsDBNull(8) then 0s else r.GetInt16(8)); 
                             IsNullable = r.GetInt16(10) = 1s
                          })
    
    /// <summary>
    /// Retrieves a list of the databases stored in the specified instance.
    /// </summary>
    /// <param name="sqlInstance">String containing the server and instance name of the
    /// SQL Server in question.</param>
    let retrieveDatabases (sqlInstance:String) = 
        let connectionString = "Server=" + sqlInstance + ";Integrated Security=SSPI"
        use connection = new SqlConnection(connectionString)
        connection.Open() 
        retrieveMany connection
                     "sp_databases"
                     []
                     (fun r -> 
                        let db = r.GetString(0)
                        match db with
                        | "master" | "msdb" | "model" | "tempdb" -> None
                        | _ -> Some db) |> Seq.toArray

    /// <summary>
    /// Retrieves details of the foreign keys for which the specified table is
    /// the primary key table.
    /// </summary>
    /// <param name="connection">Connection to the database being queried.</param>
    /// <param name="tableId">Id of the table being queried.</param>
    let retrieveForeignKeys connection tableId = 
        retrieveMany connection
                     "sp_fkeys"
                     [("@pktable_qualifier", connection.Database);
                      ("@pktable_owner", tableId.Schema);
                      ("@pktable_name", tableId.Name)]
                     (fun r -> Some (r.GetString(11), { Schema = r.GetString(5); Name = r.GetString(6)},r.GetString(7)))
            |> Seq.groupBy (fun (name, fktableId, _) -> (name, fktableId) ) 
            |> Seq.map (fun (fkId, all) -> {
                                                Name = fst fkId; 
                                                PrimaryKeyTableId = tableId; 
                                                ForeignKeyTableId = snd fkId; 
                                                ForeignKeyColumnNames = seq {for (_, _, col) in all do yield col}
                                           })

   
    /// <summary>
    /// Retrieves details of the tables in a database.
    /// </summary>
    /// <param name="connection">Connection to the database being queried.</param>
    /// <param name="options">Object which specifies which tables should be included.</param>
    let retrieveTables (connection: SqlConnection) (options: GenerationOptions) = 
        let ignoreSchemas = ["INFORMATION_SCHEMA"; "sys"]
        retrieveMany connection 
                 "sp_tables" 
                 [("@table_qualifier", connection.Database)] 
                 (fun r ->
                        let schema = r.GetString(1)

                        if Seq.exists (fun s -> s.Equals(schema)) ignoreSchemas then 
                            None
                        else

                            let id = {Schema = schema; Name =  r.GetString(2) }
                            let processTable = Some({
                                                    Id = id
                                                    Columns = (retrieveColumns connection id) |> Seq.toArray;
                                                    PrimaryKeyColumnNames = (retrievePrimaryKey connection id) |> Seq.toArray
                                               })
                                
                            let isMatch p = 
                                let tname =String.Concat(id.Schema, ".", id.Name) 
                                System.Text.RegularExpressions.Regex.IsMatch(tname, p)

                            match (options.IncludePattern, options.ExcludePattern) with
                            | (Some inc, None) -> if (isMatch inc) then processTable else None
                            | (Some inc, Some excl) -> if (isMatch inc) && not (isMatch excl) then processTable else None
                            | (None, Some excl) -> if not (isMatch excl) then processTable else None
                            | (None, None) -> processTable)

                            
    
    