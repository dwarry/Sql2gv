#Sql2gv

Many years ago, I wrote a C# library that represented the structure of a 
SQL Server database, and used that to generate a [GraphViz](http://graphviz.org)
file that could be used to generate an ER diagram. I figured this would be
a useful first project for having a play with F#. So here it is! 

##Projects

###Sql2gv.Common

This contains the metadata-model and the associated logic to build this from 
a connection to a SQL Server database. 

###Sql2gv

A command-line interface that allows you to generate graphviz files using Sql2gv.Common.

###Sql2gv.Ui

A WPF front-end to Sql2gv.Common. Uses CaliburnMicro as the MVVM application framework. 
It renders the diagram on the fly so you can see what's going on - it needs GraphViz
to be installed for this; the path needs to be set in the config file's PathToGraphViz
app setting. This should identify the bin folder in the graphviz directory. 
It also allows you to launch GvEdit to play with the generated graphviz file directly
if you need more control. 

##Acknowledgments

* Icons
Icons are from [The Noun Project](http://thenounproject.com). "Waiting Room", "Table" and 
"Clipboard" are public domain, but "Database", "Document" and "Pen" are by 
[Dmitri Baranovskiy](http://thenounproject.com/DmitryBaranovskiy/) and "Network" is by
[Stijn Janmaat](http://thenounproject.com/stijnjanmaat).

* Caliburn.Micro
[Caliburn.Micro](http://caliburnmicro.codeplex.com) is a great Model-View-ViewModel application
framework

* WPF Extension Toolkit