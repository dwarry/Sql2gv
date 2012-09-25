namespace Sql2gv

type GenerationOptions = {

    Database: string;

    IncludePattern: string option;

    ExcludePattern: string option;

    EmitSimpleNodes: bool;
}