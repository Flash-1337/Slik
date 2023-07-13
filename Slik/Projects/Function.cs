namespace Slik;

internal class Function
{
    public Function(string @class, string name, string args, string returnType = "virtual void")
    {
        Class = @class;
        Name = name;
        Args = args;
        ReturnType = returnType;
    }

    public string Name { get; set; }
    public string Class { get; set; }
    public string Args { get; set; }
    public string ReturnType { get; set; }
    public bool Changed { get; set; }
}