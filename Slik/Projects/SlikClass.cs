using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Slik.Projects
{
    [Serializable]
    class FunctionTable
    {
        public List<Function> Functions = new();
        public SlikClass Class;

        public FunctionTable(SlikClass @class)
        {
            Class = @class;
        }
    }

    class FieldTable
    {
        public List<Field> Fields = new();
        public SlikClass Class;
        
        public FieldTable(SlikClass @class)
        {
            Class = @class;
        }
    }

    [Serializable]
    class SlikClass
    {
        public string Name;
        public string Description;
        public DateTime Created;

        public FunctionTable FunctionTable;
        public FieldTable FieldTable;


        public SlikClass(string name, string description = "")
        {
            Name = name;
            Description = description;
            Created = DateTime.Now;

            FunctionTable = new FunctionTable(this);
            FieldTable = new FieldTable(this);
        }


        // For serialization
        private SlikClass()
        {
        }
    }

    class SlikProject
    {
        public string Name, Description, Path;
        public DateTime Created, LastModified;
        
        public bool Saved = false;
        
        public List<SlikClass> Classes = new();
        
        public SlikProject(string name, string description = "")
        {
            Name = name;
            Description = description;
            Path = ""; // TODO: Add path
            Created = DateTime.Now;
            LastModified = DateTime.Now;
        }
        
        
    }
}
