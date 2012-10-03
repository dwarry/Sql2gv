namespace Sql2gv

open System
open System.Collections.Generic
open System.Data
open System.Data.SqlClient
open System.Linq

type Column = { Name: string; DataType: string; IsNullable: Boolean}

type TableId = {Schema : string; Name : String}

type Table = { Id: TableId; Columns: seq<Column>; PrimaryKeyColumnNames: seq<String> }

type Index = 
    | PrimaryKey of seq<Column>
    | Unique of seq<Column>
    | OrdinaryIndex of seq<Column>

type Cardinality = 
    | OneToZeroOrOne = 1
    | OneToOne = 2
    | OneToZeroOrMany = 3
    | OneToMany = 4
    | ZeroOrOneToZeroOrOne = 5
    | ZeroOrOneToOne = 6
    | ZeroOrOneToZeroOrMany = 7
    | ZeroOrOneToMany = 8



type ForeignKey = { Name : string; PrimaryKeyTableId: TableId; ForeignKeyTableId: TableId; ForeignKeyColumnNames: seq<String>}

module Model =
    let private retrieveMany (connection: SqlConnection) (sproc: string) (parameters: seq<string * #obj>) (map: SqlDataReader -> 'T option) = 
        
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

    let private retrieveOne (connection: SqlConnection) (sproc: string) (parameters: seq<string * #obj>) (map: SqlDataReader -> 'T) = 
        
        use cmd = connection.CreateCommand()
        cmd.CommandType <- CommandType.StoredProcedure
        cmd.CommandText <- sproc
        for p in parameters do cmd.Parameters.AddWithValue(fst p, snd p) |> ignore
        use reader = cmd.ExecuteReader()
        if reader.Read() then Some (map reader) else None
        

    let private retrievePrimaryKey connection databaseName tableId = 
        retrieveMany connection
                     "sp_pkeys"
                     [ ("@table_qualifier", databaseName);
                       ("@table_owner", tableId.Schema);
                       ("@table_name", tableId.Name)]
                     (fun r -> Some (r.GetString(3)))
        

    let private retrieveColumns connection databaseName tableId =
        retrieveMany connection
                 "sp_columns"
                 [("@table_qualifier", databaseName);
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

    let retrieveForeignKeys connection databaseName tableId = 
        retrieveMany connection
                     "sp_fkeys"
                     [("@pktable_qualifier", databaseName);
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

   

    let retrieveTables (connection: SqlConnection) (databaseName: string) (options: GenerationOptions) = 
        let ignoreSchemas = ["INFORMATION_SCHEMA"; "sys"]
        retrieveMany connection 
                 "sp_tables" 
                 [("@table_qualifier", databaseName)] 
                 (fun r ->
                        let schema = r.GetString(1)

                        if Seq.exists (fun s -> s.Equals(schema)) ignoreSchemas then 
                            None
                        else

                            let id = {Schema = schema; Name =  r.GetString(2) }
                            let processTable = Some({
                                                    Id = id
                                                    Columns = (retrieveColumns connection databaseName id) |> Seq.toArray;
                                                    PrimaryKeyColumnNames = (retrievePrimaryKey connection databaseName id) |> Seq.toArray
                                               })
                                
                            let isMatch p = 
                                let tname =String.Concat(id.Schema, ".", id.Name) 
                                System.Text.RegularExpressions.Regex.IsMatch(tname, p)

                            match (options.IncludePattern, options.ExcludePattern) with
                            | (Some inc, None) -> if isMatch inc then processTable else None
                            | (Some inc, Some excl) -> if isMatch inc && not (isMatch excl) then processTable else None
                            | (None, Some excl) -> if not (isMatch excl) then processTable else None
                            | (None, None) -> processTable)

                            
    
    