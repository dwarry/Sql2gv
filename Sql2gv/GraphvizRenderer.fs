namespace Sql2gv

open System
open System.Collections.Generic
open System.Linq


module GraphvizRenderer =

    let private tableIdToNodeName tableId = 
            String.Concat(tableId.Schema, "__", tableId.Name)

    let private columnToDot table (col: Column) = 
        let pk = match Seq.tryFindIndex (fun c -> c.Equals(col.Name)) table.PrimaryKeyColumnNames with
                    | None -> " "
                    | Some x -> (x + 1).ToString()
        let fmt = "<tr><td align=\"left\" >{3}{0}{4}</td><td>{3}{1}{4}</td><td>{3}{2}{4}</td></tr>"
        let nullableStart = if col.IsNullable then "<font color=\"gray\">" else ""
        let nullableEnd = if col.IsNullable then "</font>" else ""
        String.Format(fmt, col.Name, col.DataType, pk, nullableStart, nullableEnd)

    let private tableToDot isSimpleMode table =
        if isSimpleMode then 
            String.Format("{0} [label=\"{1}.{2}\"]", tableIdToNodeName table.Id, table.Id.Schema, table.Id.Name)
        else
            let cols = String.Concat(seq [| for c in table.Columns do yield columnToDot table c |])
            String.Format("{0} [label=<<table><tr><td colspan=\"3\">{1}.{2}</td></tr>{3}</table>>,shape=none]",
                          tableIdToNodeName table.Id,
                          table.Id.Schema,
                          table.Id.Name,
                          cols)

    let private cardinality (fk:ForeignKey) (tables : Dictionary<TableId, Table>) = 
        let cols = seq { for c in tables.[fk.ForeignKeyTableId].Columns do if fk.ForeignKeyColumnNames.Contains(c.Name) then yield c}
        let optional = Seq.exists (fun c -> c.IsNullable) cols
        let sharedKey = Seq.forall2 (fun (pkc: String) (fkc: String) -> pkc.Equals(fkc) || (fkc.EndsWith(fk.PrimaryKeyTableId.Name + "_id")) )
                                     tables.[fk.PrimaryKeyTableId].PrimaryKeyColumnNames
                                     fk.ForeignKeyColumnNames
        match (optional, sharedKey) with
            |(true, true)   -> Cardinality.ZeroOrOneToOne
            |(true, false)  -> Cardinality.ZeroOrOneToZeroOrMany
            |(false, true)  -> Cardinality.OneToZeroOrOne
            |(false, false) -> Cardinality.OneToZeroOrMany
        

    let private edgeStyle c = 
        match c with
        | Cardinality.OneToMany -> "arrowtail=tee,arrowhead=crow"
        | Cardinality.OneToOne -> "arrowtail=tee,arrowhead=tee"
        | Cardinality.OneToZeroOrMany -> "arrowtail=tee,arrowhead=crowodot"
        | Cardinality.OneToZeroOrOne -> "arrowtail=tee,arrowhead=teeodot" 
        | Cardinality.ZeroOrOneToMany -> "arrowtail=teeodot,arrowhead=crow"
        | Cardinality.ZeroOrOneToOne -> "arrowtail=teeodot,arrowhead=tee"
        | Cardinality.ZeroOrOneToZeroOrMany -> "arrowtail=teeodot,arrowhead=crowodot"
        | Cardinality.ZeroOrOneToZeroOrOne -> "arrowtail=teeodot,arrowhead=teeodot"   

    let private fkToDot (tables: Dictionary<TableId, Table>) (fk: ForeignKey) = 
        String.Format("{0} -> {1} [{2},dir=both]", 
                      tableIdToNodeName fk.PrimaryKeyTableId,
                      tableIdToNodeName fk.ForeignKeyTableId,
                      edgeStyle (cardinality fk tables)) 


    let generateDotFile (isSimpleMode: bool) (tables: seq<Table>) (foreignKeys : seq<ForeignKey>) = 
        let tableDict = tables.ToDictionary(fun t -> t.Id) 
        let nodes = String.Join("\n", seq { for t in tables do yield t |> tableToDot isSimpleMode })
        let edges = String.Join("\n", seq { for fk in foreignKeys do yield fk |> fkToDot tableDict })
        String.Format("digraph Database {0}\n{1}\n{2}\n{3}", "{", nodes, edges, "}")