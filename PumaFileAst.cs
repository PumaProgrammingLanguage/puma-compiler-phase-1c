using System.Collections.Generic;

class PumaFileAst
{
    public string TypeName { get; set; }
    public string BaseType { get; set; }
    public List<string> Enums { get; } = new();
    public List<string> Properties { get; } = new();
    // Add records, functions, etc.
}