// See https://aka.ms/new-console-template for more information
using FastChess.Core;

var text = MagicGenerator.GenerateAndLog();

System.IO.File.WriteAllText("MagicData.cs", text);
