namespace Sql2gv

/// <summary>
/// Options for controlling the generation of the Graphviz file from the database schema.
/// </summary>
type GenerationOptions = {

    /// <summary>
    /// Name of the database to process.
    /// </summary>
    Database: string;

    /// <summary>
    /// If provided, specifies a regular expression that table names must match in order
    /// to be included.
    /// </summary>
    IncludePattern: string option;

    /// <summary>
    /// If provided, specifies a regular expression that table names must not match in
    /// order to be included. 
    /// </summary>
    ExcludePattern: string option;

    /// <summary>
    /// If true, the nodes in the graphviz file will just contain the name of the tables,
    /// rather than containing details of the columns.
    /// </summary>
    EmitSimpleNodes: bool;
}