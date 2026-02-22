// See https://aka.ms/new-console-template for more information
using FastChess.Magic;

var text = MagicGenerator.GenerateAndLog();

System.IO.File.WriteAllText("MagicData.cs", text);
