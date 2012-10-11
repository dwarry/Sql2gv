#Sql2gv

Many years ago, I wrote a C# library that represented the structure of a 
SQL Server database, and used that to generate a [GraphViz](http://graphviz.org)
file that could be used to generate an ER diagram. This was borne out of 
frustration with the fact that the diagram tool in Sql Server Management
Studio had many of the bugs that plagued the version in Access 97, and
I found it so utterly frustrating to use!.  

I figured this would be a useful first project for having a play with F#. So here it is! 

##Projects

###Sql2gv.Common

This contains the metadata-model and the associated logic to build this from 
a connection to a SQL Server database. 

###Sql2gv

A command-line interface that allows you to generate graphviz files.

###Sql2gv.Ui

A WPF front-end to Sql2gv.Common. Uses CaliburnMicro as the MVVM application framework. 
It renders the diagram on the fly so you can see what's going on - it needs GraphViz
to be installed for this; the path needs to be set in the config file's `PathToGraphViz`
app setting. This should identify the bin folder in the graphviz directory. 
It also allows you to launch GvEdit to play with the generated graphviz file directly
if you need more control. 

##Acknowledgments

 *  Icons

    Icons are from [The Noun Project](http://thenounproject.com). "Waiting Room", "Table" and 
    "Clipboard" are public domain, but "Database", "Document" and "Pen" are by 
    [Dmitri Baranovskiy](http://thenounproject.com/DmitryBaranovskiy/) and "Network" is by
    [Stijn Janmaat](http://thenounproject.com/stijnjanmaat). The SVG icons were converted to
	XAML by [Inkscape](http://inkscape.org).

 *  [Caliburn.Micro](http://caliburnmicro.codeplex.com)
 
    Caliburn.Micro is a great Model-View-ViewModel application
    framework which greatly simplifies data binding and commanding through an extensible
	set of conventions. Licensed under the [MIT licence](http://caliburnmicro.codeplex.com/license).

 *  [Extended WPF Toolkit](http://wpftoolkit.codeplex.com/)

    Various controls from the Extended WPF Toolkit were used, 
	including the BusyIndicator, WatermarkTextBox and the Zoombox. 
	Licenced under the [MS Public License](http://wpftoolkit.codeplex.com/license).  

