﻿namespace CSharpTools.ReferenceFinder;

public class DefinitionResult
{
    public string File { get; set; }
    public Dictionary<string, Definition> Definitions { get; set; } = new();
}

