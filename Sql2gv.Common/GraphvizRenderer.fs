namespace Sql2gv

open System
open System.Collections.Generic
open System.Linq


/// Module that controls the creation of Graphviz files
/// from the schema information. 
module GraphvizRenderer =

    /// Converts a TableId to a node name. 
    let private tableIdToNodeName tableId = 
            String.Concat(tableId.Schema, "__", tableId.Name)


    /// <summary>
    /// Render a column as a TR in an html-label.
    /// </summary>
    /// <param name="table">The table being rendered.</param>
    /// <param name="col">The column being rendered.</param>
    /// <returns>
    /// A string containing the html for the row in the rendered table element.
    /// </returns>
    let private columnToDot table (col: Column) = 
        let pk = match Seq.tryFindIndex (fun c -> c.Equals(col.Name)) table.PrimaryKeyColumnNames with
                    | None -> " "
                    | Some x -> (x + 1).ToString()
        let fmt = "<tr><td align=\"left\" {3}>{0}</td><td {3}>{1}</td><td {3}>{2}</td></tr>"
        let nullable = if col.IsNullable then "bgcolor=\"gray\"" else ""
        String.Format(fmt, col.Name, col.DataType, pk, nullable)

    /// <summary>
    /// Convert a Table into a node in the graphviz file.
    /// </summary>
    /// <param name="isSimpleMode">If true, simple nodes just specifying the table name will be generated.
    /// Otherwise, the nodes will be HTML-labels containing tables describing the database table.</param>
    /// <returns>
    /// A string containing the node declaration.
    /// </returns>
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

    /// <summary>
    /// Derive the cardinality of a relationships given the ForeignKey and a 
    /// dictionary of the Tables in the schema.
    /// </summary>
    /// <remarks>
    /// It's not possible to strictly derive the full range of cardinalities 
    /// just given a standard foreign key. There's no way to tell whether the
    /// many-end is optional or not, for instance. We can have a reasonable 
    /// guess though, and can possibly extend this function with additional 
    /// heuristics, depending on the patterns and conventions of the database
    /// schemas that we actually need to process.
    /// </remarks>
    /// <param name="fk">The ForeignKey being processed.</param>
    /// <param name="tables">A dictionary of tables, keyed by their TableId values.</param>
    /// <returns>
    /// The Cardinality of the relationship.
    /// </returns>
    let private cardinality  (tables : Dictionary<TableId, Table>) (fk:ForeignKey)= 
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
        
    /// <summary>
    /// Converts a Cardinality value to the styles that can be applied to an edge
    /// in the graphviz file.
    /// </summary>
    /// <param name="c">The cardinality of the asssociation.</param>
    /// <returns>
    /// A string containing the arrowtypes of the ends of the edge. 
    /// </returns>
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
        | _ -> "arrowtail=none,arrowhead=none"
        
    /// <summary>
    /// Converts a Foreign Key into an edge declaration in a graphviz file.
    /// </summary>
    /// <param name="tables">Dictionary of all the tables being processed, keyed by their TableIds.</param>
    /// <param name="fk">The foreign key being converted.</param>
    /// <returns>
    /// A string containing the edge declaration.
    /// </returns>
    let private fkToDot (tables: Dictionary<TableId, Table>) (fk: ForeignKey) = 
        String.Format("{0} -> {1} [{2},dir=both]", 
                      tableIdToNodeName fk.PrimaryKeyTableId,
                      tableIdToNodeName fk.ForeignKeyTableId,
                      edgeStyle (cardinality tables fk)) 

    /// <summary>
    /// Generate the graphviz dot file for a collection of tables and associated foreign keys.
    /// </summary>
    /// <param name="isSimpleMode">Indicates whether simple boxes or full tables should be rendered.</param>
    /// <param name="tables">Sequence of Tables that should be included in the graphviz file.</param>
    /// <param name="foreignKeys">Sequence of the ForeignKeys associated with the Tables.</param>
    /// <returns>
    /// A string containing the dot file contents
    /// </returns>
    let generateDotFile (isSimpleMode: bool) (tables: seq<Table>) (foreignKeys : seq<ForeignKey>) = 
        let tableDict = tables.ToDictionary(fun t -> t.Id) 
        let relevantFks = Seq.filter (fun fk -> tableDict.ContainsKey(fk.ForeignKeyTableId)) foreignKeys
        let nodes = String.Join("\n", seq { for t in tables do yield t |> tableToDot isSimpleMode })
        let edges = String.Join("\n", seq { for fk in relevantFks do yield fk |> fkToDot tableDict })
        let graphOptions = "node [shape=box3d,fontname=Arial,fontsize=10]\n"
        String.Format("digraph Database {0}\n{1}\n{2}\n{3}\n{4}", "{", graphOptions, nodes, edges, "}")