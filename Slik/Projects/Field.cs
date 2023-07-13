namespace Slik.Projects;

public class Field
{
    public Field(string @class, string name, string type, string offset)
    {
        Class = @class;
        Name = name;
        Type = type;
        Offset = offset;
    }

    public string Name { get; set; }
    public string Class { get; set; }
    public string Type { get; set; }
    public string Offset { get; set; }
}